"""
End-to-end test: Register 2 users, create conversation, send messages
Uses raw JSON string building to preserve decimal precision for 18+ digit IDs
"""
import requests
import base64
import os
import json
import time
import re
from decimal import Decimal

BASE = "http://localhost:5005"

# ─── Precision-safe JSON handling ───

def precise_parse(text):
    """Parse JSON keeping numbers as Decimal."""
    return json.loads(text, parse_float=Decimal, parse_int=Decimal)

def precise_dumps(obj):
    """Serialize to JSON with Decimal values as raw numbers (no float conversion)."""
    text = json.dumps(obj, default=lambda o: f"__DECIMAL__{o}__" if isinstance(o, Decimal) else str(o))
    # Replace "__DECIMAL__123.456__" with raw number 123.456
    text = re.sub(r'"__DECIMAL__([0-9.]+)__"', r'\1', text)
    return text

def b64_dummy(size):
    return base64.b64encode(os.urandom(size)).decode()

def post(url, payload, headers=None):
    h = {**(headers or {}), "Content-Type": "application/json"}
    body = precise_dumps(payload)
    return requests.post(url, data=body, headers=h)

def get(url, params=None, headers=None):
    return requests.get(url, params=params, headers=headers)

# ─── API Helpers ───

def register_user(name, password="Password1234"):
    r = post(f"{BASE}/api/auth/register", {"displayName": name, "password": password})
    if r.status_code == 409:
        r = post(f"{BASE}/api/auth/login", {"displayName": name, "password": password})
    r.raise_for_status()
    data = precise_parse(r.text)
    print(f"  [OK] {name} userId={data['userId']}")
    return data

def register_device(token, device_name="TestBrowser"):
    headers = {"Authorization": f"Bearer {token}"}
    payload = {
        "deviceName": device_name,
        "identityPublicKeyClassical": b64_dummy(32),
        "identityPublicKeyPostQuantum": b64_dummy(1952),
        "signedPreKeyPublic": b64_dummy(32),
        "signedPreKeySignature": b64_dummy(64),
        "signedPreKeyId": 1,
        "kyberPreKeyPublic": b64_dummy(1184),
        "kyberPreKeySignature": b64_dummy(64),
        "oneTimePreKeys": [
            {"keyId": i, "publicKey": b64_dummy(32)} for i in range(1, 6)
        ]
    }
    r = post(f"{BASE}/api/devices", payload, headers)
    if r.status_code == 201:
        data = precise_parse(r.text)
        print(f"  [OK] Device deviceId={data['deviceId']}")
        return data["deviceId"]
    else:
        r2 = get(f"{BASE}/api/devices", headers=headers)
        if r2.ok:
            devices = precise_parse(r2.text)
            if devices:
                did = devices[0]["id"]
                print(f"  [OK] Existing device={did}")
                return did
        raise Exception(f"Device reg failed: {r.status_code} {r.text}")

def create_conversation(token, other_user_id):
    headers = {"Authorization": f"Bearer {token}"}
    r = post(f"{BASE}/api/conversations", {"participantUserId": other_user_id}, headers)
    if not r.ok:
        print(f"  [FAIL] Create conv ({r.status_code}): {r.text[:500]}")
        r.raise_for_status()
    data = precise_parse(r.text)
    print(f"  [OK] convId={data['conversationId']}, isNew={data.get('isNewConversation')}")
    return data["conversationId"]

def send_msg(token, conv_id, recipient_dev, text="Hello"):
    headers = {"Authorization": f"Bearer {token}"}
    payload = {
        "conversationId": conv_id,
        "recipientDeviceId": recipient_dev,
        "ciphertext": base64.b64encode(text.encode()).decode(),
        "messageType": Decimal(0),
        "contentType": Decimal(0)
    }
    r = post(f"{BASE}/api/messages", payload, headers)
    if r.ok:
        data = precise_parse(r.text)
        print(f"  [OK] \"{text}\" -> msgId={data.get('messageId')}, seq={data.get('sequenceNumber')}")
        return data
    else:
        print(f"  [FAIL] Send ({r.status_code}): {r.text[:500]}")
        return None

def get_pending(token, device_id, label=""):
    headers = {"Authorization": f"Bearer {token}"}
    r = get(f"{BASE}/api/messages/pending", params={"deviceId": str(device_id)}, headers=headers)
    if r.ok:
        data = precise_parse(r.text)
        print(f"  [OK] {label} pending: {len(data)}")
        for msg in data:
            try:
                pt = base64.b64decode(msg.get("ciphertext", "")).decode()
            except:
                pt = "<enc>"
            print(f"       msgId={msg['messageId']}, text=\"{pt}\"")
        return data
    else:
        print(f"  [FAIL] Pending ({r.status_code}): {r.text[:300]}")
        return []

def ack(token, mid):
    headers = {"Authorization": f"Bearer {token}"}
    r = post(f"{BASE}/api/messages/{mid}/acknowledge", {}, headers)
    st = "OK" if r.ok else f"WARN {r.status_code}"
    print(f"  [{st}] Ack {mid}")

def search(token, q):
    headers = {"Authorization": f"Bearer {token}"}
    r = get(f"{BASE}/api/users/search", params={"q": q}, headers=headers)
    if r.ok:
        data = precise_parse(r.text)
        users = data.get("users", []) if isinstance(data, dict) else data
        print(f"  [OK] Search '{q}': {len(users)}")
        for u in users:
            print(f"       {u['displayName']} userId={u['userId']}")
    else:
        print(f"  [WARN] Search ({r.status_code})")


def main():
    ts = int(time.time())
    print("=" * 60)
    print("ToledoMessage End-to-End Test")
    print("=" * 60)

    print(f"\n--- 1. Register Users ---")
    alice = register_user(f"Alice{ts}")
    bob = register_user(f"Bob{ts}")

    print(f"\n--- 2. Register Devices ---")
    alice_dev = register_device(alice["token"], "Alice-Browser")
    bob_dev = register_device(bob["token"], "Bob-Browser")

    print(f"\n--- 3. Search ---")
    search(alice["token"], f"Bob{ts}")

    print(f"\n--- 4. Create Conversation ---")
    conv = create_conversation(alice["token"], bob["userId"])

    print(f"\n--- 5. Alice -> Bob ---")
    m1 = send_msg(alice["token"], conv, bob_dev, "Hello Bob!")
    m2 = send_msg(alice["token"], conv, bob_dev, "How are you?")

    print(f"\n--- 6. Bob Pending ---")
    bp = get_pending(bob["token"], bob_dev, "Bob")

    print(f"\n--- 7. Bob -> Alice ---")
    m3 = send_msg(bob["token"], conv, alice_dev, "Hey Alice! Great!")

    print(f"\n--- 8. Alice Pending ---")
    ap = get_pending(alice["token"], alice_dev, "Alice")

    print(f"\n--- 9. Acknowledge ---")
    for m in bp: ack(bob["token"], m["messageId"])
    for m in ap: ack(alice["token"], m["messageId"])

    print(f"\n--- 10. Verify Clean ---")
    br = get_pending(bob["token"], bob_dev, "Bob")
    ar = get_pending(alice["token"], alice_dev, "Alice")

    print("\n" + "=" * 60)
    ok = m1 and m2 and m3 and len(bp)==2 and len(ap)>=1 and len(br)==0 and len(ar)==0
    print("ALL TESTS PASSED!" if ok else "ISSUES:")
    if not ok:
        if not m1: print("  - m1 failed")
        if not m2: print("  - m2 failed")
        if not m3: print("  - m3 failed")
        if len(bp)!=2: print(f"  - Bob pending: {len(bp)} (expected 2)")
        if len(ap)<1: print(f"  - Alice pending: {len(ap)} (expected >=1)")
        if len(br)!=0: print(f"  - Bob remaining: {len(br)}")
        if len(ar)!=0: print(f"  - Alice remaining: {len(ar)}")
    print("=" * 60)

if __name__ == "__main__":
    main()
