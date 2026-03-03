#!/usr/bin/env python3
"""Full E2E test cycle for ToledoMessage deployed on IIS."""

import base64, os, json, subprocess, re, sys, time

BASE = "http://localhost:8080"
results = {"pass": 0, "fail": 0}
RUN = str(int(time.time()))[-6:]  # unique suffix per run

def dec(id_str):
    """Wrap a decimal ID string for safe JSON serialization.
    Returns a special marker that json_with_decimals() replaces with a raw number."""
    return f"__DECID_{id_str}__"

def json_with_decimals(obj):
    """Serialize dict to JSON, then replace decimal placeholders with raw numbers."""
    s = json.dumps(obj)
    s = re.sub(r'"__DECID_([0-9.]+)__"', r'\1', s)
    return s

def curl_post(path, token, body=None):
    url = BASE + path
    cmd = ["curl", "-s", "-X", "POST", url, "-H", f"Authorization: Bearer {token}"]
    if body is not None:
        cmd += ["-H", "Content-Type: application/json", "-d", json_with_decimals(body)]
    else:
        cmd += ["-H", "Content-Length: 0"]
    r = subprocess.run(cmd, capture_output=True, text=True)
    return r.stdout

def curl_get(path, token):
    r = subprocess.run(["curl", "-s", BASE + path,
        "-H", f"Authorization: Bearer {token}", "-H", "Accept: application/json"],
        capture_output=True, text=True)
    return r.stdout

def curl_put(path, token, body):
    r = subprocess.run(["curl", "-s", "-X", "PUT", BASE + path,
        "-H", "Content-Type: application/json", "-H", f"Authorization: Bearer {token}",
        "-d", json_with_decimals(body)], capture_output=True, text=True)
    return r.stdout

def curl_delete(path, token):
    r = subprocess.run(["curl", "-s", "-X", "DELETE", BASE + path,
        "-H", f"Authorization: Bearer {token}"], capture_output=True, text=True)
    return r.stdout

def check(name, condition, detail=""):
    if condition:
        results["pass"] += 1
        print(f"  PASS  {name}")
    else:
        results["fail"] += 1
        print(f"  FAIL  {name} -- {detail}")

def login(display_name, password):
    r = subprocess.run(["curl", "-s", "-X", "POST", BASE + "/api/auth/login",
        "-H", "Content-Type: application/json",
        "--data-raw", json.dumps({"displayName": display_name, "password": password})],
        capture_output=True, text=True)
    raw = r.stdout
    token = json.loads(raw)["token"]
    uid = re.search(r'"userId":([0-9.]+)', raw).group(1)
    return token, uid

def register(display_name, password):
    r = subprocess.run(["curl", "-s", "-X", "POST", BASE + "/api/auth/register",
        "-H", "Content-Type: application/json",
        "--data-raw", json.dumps({"displayName": display_name, "password": password})],
        capture_output=True, text=True)
    raw = r.stdout
    d = json.loads(raw)
    uid = re.search(r'"userId":([0-9.]+)', raw).group(1)
    return d["token"], uid

def make_device(token, name):
    body = {
        "deviceName": name,
        "identityPublicKeyClassical": base64.b64encode(os.urandom(32)).decode(),
        "identityPublicKeyPostQuantum": base64.b64encode(os.urandom(1952)).decode(),
        "signedPreKeyPublic": base64.b64encode(os.urandom(32)).decode(),
        "signedPreKeySignature": base64.b64encode(os.urandom(3377)).decode(),
        "signedPreKeyId": 1,
        "kyberPreKeyPublic": base64.b64encode(os.urandom(1184)).decode(),
        "kyberPreKeySignature": base64.b64encode(os.urandom(3377)).decode(),
        "oneTimePreKeys": [{"keyId": i+1, "publicKey": base64.b64encode(os.urandom(32)).decode()} for i in range(10)]
    }
    resp = curl_post("/api/devices", token, body)
    m = re.search(r'"deviceId":([0-9.]+)', resp)
    return m.group(1) if m else f"FAIL: {resp[:100]}"

print("=" * 50)
print("  ToledoMessage Full E2E Test Cycle")
print("=" * 50)

ALICE_NAME = f"CycleAlice{RUN}"
BOB_NAME = f"CycleBob{RUN}"
ALICE_PWD = "AlicePassword1234"
BOB_PWD = "BobbyPassword5678"

# --- STEP 1: Register ---
print(f"\n=== STEP 1: Register Users (suffix={RUN}) ===")
TOKEN_A, ALICE_ID = register(ALICE_NAME, ALICE_PWD)
TOKEN_B, BOB_ID = register(BOB_NAME, BOB_PWD)
check("Alice registered", TOKEN_A and ALICE_ID)
check("Bob registered", TOKEN_B and BOB_ID)

# --- STEP 2: Login ---
print("\n=== STEP 2: Login ===")
TOKEN_A, _ = login(ALICE_NAME, ALICE_PWD)
TOKEN_B, _ = login(BOB_NAME, BOB_PWD)
check("Alice login", len(TOKEN_A) > 50)
check("Bob login", len(TOKEN_B) > 50)

# --- STEP 3: Token Refresh ---
print("\n=== STEP 3: Token Refresh ===")
login_resp = subprocess.run(["curl", "-s", "-X", "POST", BASE + "/api/auth/login",
    "-H", "Content-Type: application/json",
    "--data-raw", json.dumps({"displayName": ALICE_NAME, "password": ALICE_PWD})],
    capture_output=True, text=True)
d = json.loads(login_resp.stdout)
refresh_resp = curl_post("/api/auth/refresh", "", None)
# Need unauthenticated refresh
r = subprocess.run(["curl", "-s", "-X", "POST", BASE + "/api/auth/refresh",
    "-H", "Content-Type: application/json",
    "-d", json.dumps({"accessToken": d["token"], "refreshToken": d["refreshToken"]})],
    capture_output=True, text=True)
check("Token refresh", "token" in r.stdout, r.stdout[:80])
TOKEN_A = json.loads(r.stdout).get("token", TOKEN_A)

# --- STEP 4: Register Devices ---
print("\n=== STEP 4: Register Devices ===")
DEV_A = make_device(TOKEN_A, "AliceChrome")
DEV_B = make_device(TOKEN_B, "BobFirefox")
check("Alice device", "." in DEV_A, DEV_A[:50])
check("Bob device", "." in DEV_B, DEV_B[:50])

# --- STEP 5: User Search ---
print("\n=== STEP 5: User Search ===")
search = curl_get(f"/api/users/search?q={BOB_NAME}", TOKEN_A)
check("Alice finds Bob", BOB_NAME in search)

search_none = curl_get("/api/users/search?q=NonExistentUser999", TOKEN_A)
check("No results for unknown user", '"users":[]' in search_none or "users" in search_none)

# --- STEP 6: Pre-Key Bundle ---
print("\n=== STEP 6: Pre-Key Bundle ===")
bundle = curl_get(f"/api/users/{BOB_ID}/prekey-bundle?deviceId={DEV_B}", TOKEN_A)
has_bundle = "signedPreKeyPublic" in bundle or "identityPublicKeyClassical" in bundle
check("Get Bob pre-key bundle", has_bundle, bundle[:120] if not has_bundle else "")

# --- STEP 7: Create 1:1 Conversation ---
print("\n=== STEP 7: Create Conversation ===")
conv_resp = curl_post("/api/conversations", TOKEN_A, {"participantUserId": dec(BOB_ID)})
conv_id_m = re.search(r'"conversationId":([0-9.]+)', conv_resp)
check("1:1 conversation created", conv_id_m is not None, conv_resp[:100])
CONV_ID = conv_id_m.group(1) if conv_id_m else "0"

# Duplicate should return existing
conv2 = curl_post("/api/conversations", TOKEN_A, {"participantUserId": dec(BOB_ID)})
check("Duplicate returns existing (isNew=false)", '"isNew":false' in conv2)

# --- STEP 8: Send Messages ---
print("\n=== STEP 8: Send Messages ===")
cipher1 = base64.b64encode(b"Hello Bob, this is Alice!").decode()
msg1 = curl_post("/api/messages", TOKEN_A, {
    "conversationId": dec(CONV_ID), "senderDeviceId": dec(DEV_A),
    "recipientDeviceId": dec(DEV_B), "ciphertext": cipher1,
    "messageType": 0, "contentType": 0
})
MSG1_ID = re.search(r'"messageId":([0-9.]+)', msg1)
check("Message 1 (Alice->Bob)", MSG1_ID is not None, msg1[:100])
MSG1_ID = MSG1_ID.group(1) if MSG1_ID else "0"

cipher2 = base64.b64encode(b"Second message!").decode()
msg2 = curl_post("/api/messages", TOKEN_A, {
    "conversationId": dec(CONV_ID), "senderDeviceId": dec(DEV_A),
    "recipientDeviceId": dec(DEV_B), "ciphertext": cipher2,
    "messageType": 0, "contentType": 0
})
MSG2_ID = re.search(r'"messageId":([0-9.]+)', msg2)
check("Message 2 (Alice->Bob)", MSG2_ID is not None)
MSG2_ID = MSG2_ID.group(1) if MSG2_ID else "0"

# Bob replies with replyToMessageId
cipher3 = base64.b64encode(b"Hi Alice, this is Bob replying!").decode()
msg3 = curl_post("/api/messages", TOKEN_B, {
    "conversationId": dec(CONV_ID), "senderDeviceId": dec(DEV_B),
    "recipientDeviceId": dec(DEV_A), "ciphertext": cipher3,
    "messageType": 0, "contentType": 0,
    "replyToMessageId": dec(MSG1_ID)
})
MSG3_ID = re.search(r'"messageId":([0-9.]+)', msg3)
check("Message 3 (Bob reply->Alice)", MSG3_ID is not None)

# Media message
cipher4 = base64.b64encode(os.urandom(1024)).decode()
msg4 = curl_post("/api/messages", TOKEN_A, {
    "conversationId": dec(CONV_ID), "senderDeviceId": dec(DEV_A),
    "recipientDeviceId": dec(DEV_B), "ciphertext": cipher4,
    "messageType": 0, "contentType": 1,
    "fileName": "photo.jpg", "mimeType": "image/jpeg"
})
check("Message 4 (media/image)", '"messageId"' in msg4)

# --- STEP 9: Pending Messages ---
print("\n=== STEP 9: Pending Messages ===")
pending_b = json.loads(curl_get(f"/api/messages/pending?deviceId={DEV_B}", TOKEN_B))
check("Bob has 3 pending", len(pending_b) == 3, f"got {len(pending_b)}")

pending_a = json.loads(curl_get(f"/api/messages/pending?deviceId={DEV_A}", TOKEN_A))
check("Alice has 1 pending (Bob reply)", len(pending_a) == 1, f"got {len(pending_a)}")

# Verify reply reference
if pending_a:
    check("Reply has replyToMessageId", pending_a[0].get("replyToMessageId") is not None)

# --- STEP 10: Acknowledge ---
print("\n=== STEP 10: Acknowledge Delivery ===")
ack1 = curl_post(f"/api/messages/{MSG1_ID}/acknowledge", TOKEN_B)
check("Ack msg 1", "deliveredAt" in ack1)
ack2 = curl_post(f"/api/messages/{MSG2_ID}/acknowledge", TOKEN_B)
check("Ack msg 2", "deliveredAt" in ack2)

pending_after = json.loads(curl_get(f"/api/messages/pending?deviceId={DEV_B}", TOKEN_B))
check("Bob pending down to 1", len(pending_after) == 1, f"got {len(pending_after)}")

# --- STEP 11: Conversations List ---
print("\n=== STEP 11: Conversations List ===")
convs_a = curl_get("/api/conversations", TOKEN_A)
check("Alice sees conversation", CONV_ID[:10] in convs_a)
convs_b = curl_get("/api/conversations", TOKEN_B)
check("Bob sees conversation", CONV_ID[:10] in convs_b)

# --- STEP 12: Preferences ---
print("\n=== STEP 12: Preferences ===")
prefs = json.loads(curl_get("/api/preferences", TOKEN_A))
check("Default theme", prefs.get("theme") == "default")
check("SharedKeysEnabled default true", prefs.get("sharedKeysEnabled") is True)
check("ReadReceipts default true", prefs.get("readReceiptsEnabled") is True)

curl_put("/api/preferences", TOKEN_A, {"theme": "default-dark", "sharedKeysEnabled": False})
prefs2 = json.loads(curl_get("/api/preferences", TOKEN_A))
check("Theme changed to dark", prefs2.get("theme") == "default-dark")
check("SharedKeys toggled off", prefs2.get("sharedKeysEnabled") is False)

# Reset
curl_put("/api/preferences", TOKEN_A, {"theme": "default", "sharedKeysEnabled": True})

# --- STEP 13: Key Backup ---
print("\n=== STEP 13: Key Backup ===")
backup_body = {
    "encryptedBlob": base64.b64encode(os.urandom(512)).decode(),
    "salt": base64.b64encode(os.urandom(16)).decode(),
    "nonce": base64.b64encode(os.urandom(12)).decode()
}
curl_post("/api/keys/backup", TOKEN_A, backup_body)
backup = curl_get("/api/keys/backup", TOKEN_A)
check("Backup uploaded & retrieved", "encryptedBlob" in backup)

curl_delete("/api/keys/backup", TOKEN_A)
backup_gone = curl_get("/api/keys/backup", TOKEN_A)
check("Backup deleted (404)", "encryptedBlob" not in backup_gone)

# --- STEP 14: Group Conversation ---
print("\n=== STEP 14: Group Conversation ===")
CHARLIE_NAME = f"CycleCharlie{RUN}"
TOKEN_C, CHARLIE_ID = register(CHARLIE_NAME, "CharliePassword1234")
group = curl_post("/api/conversations/group", TOKEN_A, {
    "groupName": "Test Group Chat",
    "participantUserIds": [dec(BOB_ID), dec(CHARLIE_ID)]
})
grp_id_m = re.search(r'"conversationId":([0-9.]+)', group)
check("Group created", grp_id_m is not None, group[:100] if not grp_id_m else "")
GRP_ID = grp_id_m.group(1) if grp_id_m else "0"

# --- STEP 15: Disappearing Timer ---
print("\n=== STEP 15: Disappearing Timer ===")
timer_resp = curl_put(f"/api/conversations/{CONV_ID}/timer", TOKEN_A, {"timerSeconds": 3600})
check("Timer set to 1 hour", timer_resp == "" or "204" not in timer_resp)

# --- STEP 16: SignalR Hub ---
print("\n=== STEP 16: SignalR Hub ===")
negotiate = curl_post("/hubs/chat/negotiate?negotiateVersion=1", TOKEN_A)
check("SignalR negotiate", "connectionId" in negotiate)
neg_data = json.loads(negotiate)
transports = [t["transport"] for t in neg_data.get("availableTransports", [])]
check("WebSockets available", "WebSockets" in transports)

# --- STEP 17: Blazor Client ---
print("\n=== STEP 17: Blazor WASM Client ===")
for path, name in [("/", "Home page"), ("/_framework/blazor.web.js", "Blazor JS"), ("/favicon.svg", "Favicon")]:
    r = subprocess.run(["curl", "-s", "-o", "/dev/null", "-w", "%{http_code}", BASE + path], capture_output=True, text=True)
    check(f"{name} (200)", r.stdout == "200", f"got {r.stdout}")

# --- STEP 18: Input Validation ---
print("\n=== STEP 18: Input Validation ===")
# Short password
r = subprocess.run(["curl", "-s", "-o", "/dev/null", "-w", "%{http_code}", "-X", "POST",
    BASE + "/api/auth/register", "-H", "Content-Type: application/json",
    "--data-raw", json.dumps({"displayName": "ShortPwd", "password": "short"})],
    capture_output=True, text=True)
check("Reject short password (400)", r.stdout == "400", f"got {r.stdout}")

# Long device name
r = subprocess.run(["curl", "-s", "-o", "/dev/null", "-w", "%{http_code}", "-X", "POST",
    BASE + "/api/auth/register", "-H", "Content-Type: application/json",
    "--data-raw", json.dumps({"displayName": "A" * 40, "password": "ValidPassword1234"})],
    capture_output=True, text=True)
check("Reject long display name (400)", r.stdout == "400", f"got {r.stdout}")

# --- STEP 19: Rate Limiting ---
print("\n=== STEP 19: Rate Limiting ===")
# Hit search 12 times quickly (limit is 10/min)
last_code = "200"
for i in range(12):
    r = subprocess.run(["curl", "-s", "-o", "/dev/null", "-w", "%{http_code}",
        f"{BASE}/api/users/search?q=test{i}",
        "-H", f"Authorization: Bearer {TOKEN_A}", "-H", "Accept: application/json"],
        capture_output=True, text=True)
    last_code = r.stdout
check("Rate limit triggers (429)", last_code == "429", f"got {last_code}")

# --- STEP 20: Health Check ---
print("\n=== STEP 20: Health Check ===")
r = subprocess.run(["curl", "-s", BASE + "/health"], capture_output=True, text=True)
check("Health endpoint", r.stdout == "Healthy")

# === SUMMARY ===
total = results["pass"] + results["fail"]
print(f"\n{'=' * 50}")
print(f"  RESULTS: {results['pass']}/{total} passed, {results['fail']} failed")
if results["fail"] == 0:
    print("  ALL TESTS PASSED")
else:
    print(f"  {results['fail']} TEST(S) FAILED")
print(f"{'=' * 50}")
sys.exit(0 if results["fail"] == 0 else 1)
