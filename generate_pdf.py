"""
Generate ToledoMessage Academic Paper (PDF)
Professional paper-style layout with diagrams, tables, and algorithms
"""
import os
from reportlab.lib.pagesizes import A4
from reportlab.lib.units import inch, cm, mm
from reportlab.lib.colors import HexColor, black, white, gray
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.enums import TA_CENTER, TA_JUSTIFY, TA_LEFT, TA_RIGHT
from reportlab.platypus import (SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle,
                                 PageBreak, Image, KeepTogether, HRFlowable)
from reportlab.platypus.flowables import Flowable
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import matplotlib.patches as mpatches
from io import BytesIO

# ─── Page dimensions ───
PAGE_W, PAGE_H = A4  # 595.27, 841.89
MARGIN_L = 2*cm
MARGIN_R = 2*cm
CONTENT_W = PAGE_W - MARGIN_L - MARGIN_R   # ~17.27 cm usable

# ─── Colors ───
C_PRIMARY = HexColor('#1976D2')
C_DARK = HexColor('#0F111A')
C_NAVY = HexColor('#16213E')
C_BLUE_L = HexColor('#42A5F5')
C_TEAL = HexColor('#008069')
C_GREEN = HexColor('#25D366')
C_PURPLE = HexColor('#7C4DFF')
C_ORANGE = HexColor('#FF9800')
C_RED = HexColor('#E53935')
C_GRAY = HexColor('#757575')
C_GRAY_L = HexColor('#E0E0E0')
C_BG_LIGHT = HexColor('#F5F7FA')

# ─── Styles ───
styles = getSampleStyleSheet()

styles.add(ParagraphStyle(name='PaperTitle', fontName='Helvetica-Bold', fontSize=22,
    leading=28, alignment=TA_CENTER, spaceAfter=6, textColor=HexColor('#1A1A2E')))
styles.add(ParagraphStyle(name='PaperSubtitle', fontName='Helvetica', fontSize=12,
    leading=16, alignment=TA_CENTER, spaceAfter=4, textColor=C_GRAY))
styles.add(ParagraphStyle(name='PaperAuthors', fontName='Helvetica-Bold', fontSize=11,
    leading=15, alignment=TA_CENTER, spaceAfter=2, textColor=HexColor('#424242')))
styles.add(ParagraphStyle(name='PaperAffiliation', fontName='Helvetica', fontSize=10,
    leading=14, alignment=TA_CENTER, spaceAfter=12, textColor=C_GRAY))
styles.add(ParagraphStyle(name='AbstractTitle', fontName='Helvetica-Bold', fontSize=11,
    leading=14, alignment=TA_CENTER, spaceAfter=6, textColor=black))
styles.add(ParagraphStyle(name='Abstract', fontName='Helvetica', fontSize=9.5,
    leading=13, alignment=TA_JUSTIFY, spaceAfter=6, textColor=HexColor('#333333'),
    leftIndent=36, rightIndent=36))
styles.add(ParagraphStyle(name='SectionTitle', fontName='Helvetica-Bold', fontSize=13,
    leading=17, spaceBefore=18, spaceAfter=8, textColor=C_PRIMARY))
styles.add(ParagraphStyle(name='SubsectionTitle', fontName='Helvetica-Bold', fontSize=11,
    leading=14, spaceBefore=12, spaceAfter=6, textColor=HexColor('#1565C0')))
styles.add(ParagraphStyle(name='BodyText2', fontName='Helvetica', fontSize=9.5,
    leading=13.5, alignment=TA_JUSTIFY, spaceAfter=8, textColor=HexColor('#333333')))
styles.add(ParagraphStyle(name='CodeBlock', fontName='Courier', fontSize=7.5,
    leading=10.5, spaceAfter=8, spaceBefore=4, textColor=HexColor('#1A1A2E'),
    backColor=HexColor('#F0F4F8'), leftIndent=14, rightIndent=14,
    borderWidth=0.5, borderColor=HexColor('#D0D8E0'), borderPadding=8))
styles.add(ParagraphStyle(name='Caption', fontName='Helvetica-Oblique', fontSize=9,
    leading=12, alignment=TA_CENTER, spaceBefore=4, spaceAfter=14, textColor=C_GRAY))
styles.add(ParagraphStyle(name='BulletItem', fontName='Helvetica', fontSize=9.5,
    leading=13, alignment=TA_JUSTIFY, spaceAfter=4, textColor=HexColor('#333333'),
    leftIndent=24, bulletIndent=12))
styles.add(ParagraphStyle(name='AlgorithmTitle', fontName='Helvetica-Bold', fontSize=10,
    leading=13, spaceBefore=6, spaceAfter=4, textColor=HexColor('#1565C0')))
styles.add(ParagraphStyle(name='KeywordStyle', fontName='Helvetica-Bold', fontSize=9.5,
    leading=13, alignment=TA_LEFT, textColor=HexColor('#424242')))
# Cell-level paragraph styles for tables
styles.add(ParagraphStyle(name='CellCode', fontName='Courier', fontSize=7.5,
    leading=10, textColor=HexColor('#333333')))
styles.add(ParagraphStyle(name='CellText', fontName='Helvetica', fontSize=8.5,
    leading=11.5, textColor=HexColor('#333333')))

def make_figure(draw_func, w=6, h=4, dpi=150):
    buf = BytesIO()
    fig, ax = plt.subplots(figsize=(w, h), dpi=dpi)
    fig.patch.set_facecolor('white')
    ax.set_facecolor('white')
    draw_func(fig, ax)
    fig.savefig(buf, format='png', bbox_inches='tight', facecolor='white', edgecolor='none', pad_inches=0.15)
    plt.close(fig)
    buf.seek(0)
    return buf

# ─── Page number callback ───
def add_page_number(canvas, doc):
    canvas.saveState()
    canvas.setFont('Helvetica', 8)
    canvas.setFillColor(HexColor('#999999'))
    canvas.drawCentredString(PAGE_W / 2, 1.2*cm,
        f"ToledoMessage: Post-Quantum Secure Messaging  \u2014  Page {doc.page}")
    canvas.restoreState()

# ─── Build Document ───
out_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "ToledoMessage_Paper.pdf")
doc = SimpleDocTemplate(out_path, pagesize=A4,
    topMargin=1.8*cm, bottomMargin=2.2*cm, leftMargin=MARGIN_L, rightMargin=MARGIN_R)

story = []

# ═══════════════════════════════════════════════════════════
# TITLE PAGE
# ═══════════════════════════════════════════════════════════
story.append(Spacer(1, 1.5*cm))
story.append(HRFlowable(width="60%", thickness=2, color=C_PRIMARY, spaceAfter=12, spaceBefore=6))

story.append(Paragraph("ToledoMessage: A Post-Quantum Hybrid<br/>End-to-End Encrypted Messaging Platform",
    styles['PaperTitle']))

story.append(Paragraph("Combining Classical and Post-Quantum Cryptography for Secure Browser-Based Communication",
    styles['PaperSubtitle']))

story.append(HRFlowable(width="60%", thickness=2, color=C_PRIMARY, spaceAfter=18, spaceBefore=12))

story.append(Paragraph("Mohamed Khamis", styles['PaperAuthors']))
story.append(Paragraph("University of Toledo, Department of Computer Science<br/>Toledo, Ohio, United States",
    styles['PaperAffiliation']))
story.append(Paragraph("February 2026", styles['PaperAffiliation']))

story.append(Spacer(1, 0.8*cm))

# Abstract
story.append(Paragraph("ABSTRACT", styles['AbstractTitle']))
story.append(Paragraph(
    "This paper presents ToledoMessage, a novel secure messaging platform that implements a hybrid "
    "post-quantum end-to-end encryption (E2EE) system within a browser-based WebAssembly environment. "
    "The system combines the X3DH (Extended Triple Diffie-Hellman) key agreement protocol with the "
    "NIST-standardized ML-KEM-768 (formerly CRYSTALS-Kyber) key encapsulation mechanism, creating "
    "a hybrid classical/post-quantum key exchange that provides security against both current and "
    "future quantum computing threats. Message-level encryption employs the Double Ratchet protocol "
    "with AES-256-GCM authenticated encryption, providing forward secrecy and break-in recovery. "
    "Digital signatures use a hybrid Ed25519 + ML-DSA-65 (formerly Dilithium) scheme for authentication. "
    "The platform is implemented using Blazor WebAssembly for the client, ASP.NET Core with SignalR "
    "for real-time server communication, and BouncyCastle for cryptographic operations. The server "
    "operates under a zero-knowledge model, never accessing plaintext messages or private keys. "
    "We present the system architecture, cryptographic protocol details, security analysis, and "
    "a comparison with existing messaging platforms including Signal, WhatsApp, and Telegram.",
    styles['Abstract']))

story.append(Spacer(1, 0.3*cm))
story.append(Paragraph(
    "<b>Keywords:</b> Post-Quantum Cryptography, End-to-End Encryption, Signal Protocol, X3DH, "
    "Double Ratchet, ML-KEM-768, Blazor WebAssembly, Hybrid Cryptography, Secure Messaging",
    styles['KeywordStyle']))

story.append(Spacer(1, 0.5*cm))
story.append(HRFlowable(width="100%", thickness=0.5, color=C_GRAY_L))

# ═══════════════════════════════════════════════════════════
# 1. INTRODUCTION
# ═══════════════════════════════════════════════════════════
story.append(Paragraph("1. Introduction", styles['SectionTitle']))

story.append(Paragraph(
    "The proliferation of quantum computing poses an existential threat to the cryptographic "
    "foundations of modern secure communication. Current messaging applications such as Signal, "
    "WhatsApp, and iMessage rely on classical Diffie-Hellman key exchange and ECDSA signatures, "
    "which are vulnerable to Shor's algorithm on a sufficiently powerful quantum computer [1]. "
    "The National Institute of Standards and Technology (NIST) has recently standardized post-quantum "
    "cryptographic algorithms, including ML-KEM (Module-Lattice-Based Key Encapsulation Mechanism) "
    "and ML-DSA (Module-Lattice-Based Digital Signature Algorithm), marking a critical inflection "
    "point in the transition to quantum-resistant cryptography [2].",
    styles['BodyText2']))

story.append(Paragraph(
    "ToledoMessage addresses this challenge by implementing a hybrid cryptographic model that "
    "combines proven classical algorithms with NIST-standardized post-quantum primitives. The "
    "system's key contribution is the integration of ML-KEM-768 into the Signal Protocol's X3DH "
    "key agreement, creating a key exchange that requires an attacker to break both the classical "
    "X25519 Diffie-Hellman and the lattice-based ML-KEM to compromise a session. This hybrid "
    "approach provides defense-in-depth: if either primitive is found to be vulnerable, the "
    "other continues to protect communication security.",
    styles['BodyText2']))

story.append(Paragraph(
    "A second contribution is the deployment of this hybrid cryptographic stack within a browser "
    "environment using Blazor WebAssembly (WASM). All cryptographic operations\u2014key generation, "
    "session management, encryption, and decryption\u2014execute entirely in the client's browser "
    "sandbox, with the server operating under a strict zero-knowledge model. The server stores "
    "and routes only encrypted ciphertext, never accessing plaintext messages or private keys.",
    styles['BodyText2']))

# ═══════════════════════════════════════════════════════════
# 2. SYSTEM ARCHITECTURE
# ═══════════════════════════════════════════════════════════
story.append(Paragraph("2. System Architecture", styles['SectionTitle']))
story.append(Paragraph("2.1 High-Level Overview", styles['SubsectionTitle']))

story.append(Paragraph(
    "ToledoMessage follows a three-tier architecture consisting of: (1) a Blazor WebAssembly "
    "client running in the browser, (2) an ASP.NET Core server providing REST APIs and real-time "
    "SignalR communication, and (3) a SQL Server 2022 database for persistent storage. Figure 1 "
    "illustrates the overall system architecture.",
    styles['BodyText2']))

story.append(Spacer(1, 0.4*cm))

# ── Architecture diagram — FIXED: wider spacing, no overlapping text ──
def arch_fig(fig, ax):
    ax.set_xlim(-0.5, 13.5)
    ax.set_ylim(-0.3, 8.2)
    ax.axis('off')

    # Row 1: Client | Server | Database (wide boxes with padding)
    # Client box
    box1 = mpatches.FancyBboxPatch((0, 4.5), 3.8, 3.2, boxstyle="round,pad=0.25",
        facecolor='#E3F2FD', edgecolor='#1976D2', linewidth=2)
    ax.add_patch(box1)
    ax.text(1.9, 7.2, 'Blazor WASM Client', ha='center', fontsize=10, fontweight='bold', color='#1565C0')
    ax.text(1.9, 6.0, 'CryptoService\nSessionService\nKeyGeneration\nLocalStorage (IndexedDB)',
            ha='center', fontsize=8, color='#333333', linespacing=1.4)

    # Server box
    box2 = mpatches.FancyBboxPatch((5.0, 4.5), 3.8, 3.2, boxstyle="round,pad=0.25",
        facecolor='#F3E5F5', edgecolor='#7C4DFF', linewidth=2)
    ax.add_patch(box2)
    ax.text(6.9, 7.2, 'ASP.NET Core Server', ha='center', fontsize=10, fontweight='bold', color='#6A1B9A')
    ax.text(6.9, 6.0, 'ChatHub (SignalR)\nREST Controllers\nIdentity + JWT Auth\nEF Core 10',
            ha='center', fontsize=8, color='#333333', linespacing=1.4)

    # Database box
    box3 = mpatches.FancyBboxPatch((10.0, 4.5), 3.2, 3.2, boxstyle="round,pad=0.25",
        facecolor='#FFEBEE', edgecolor='#E53935', linewidth=2)
    ax.add_patch(box3)
    ax.text(11.6, 7.2, 'SQL Server 2022', ha='center', fontsize=10, fontweight='bold', color='#C62828')
    ax.text(11.6, 6.0, 'Users, Devices\nConversations\nMessages (encrypted)\nPreKey Bundles',
            ha='center', fontsize=8, color='#333333', linespacing=1.4)

    # Row 2: Browser IndexedDB
    box4 = mpatches.FancyBboxPatch((0, 0.2), 3.8, 3.0, boxstyle="round,pad=0.25",
        facecolor='#E8F5E9', edgecolor='#2E7D32', linewidth=2)
    ax.add_patch(box4)
    ax.text(1.9, 2.75, 'Browser IndexedDB', ha='center', fontsize=10, fontweight='bold', color='#2E7D32')
    ax.text(1.9, 1.55, 'Private Keys\nSession State\nRatchet Chains\nAuth Tokens',
            ha='center', fontsize=8, color='#333333', linespacing=1.4)

    # Arrows: Client <-> Server
    ax.annotate('', xy=(4.85, 6.6), xytext=(3.95, 6.6),
        arrowprops=dict(arrowstyle='<->', color='#25D366', lw=2.5))
    ax.text(4.4, 7.0, 'SignalR', ha='center', fontsize=8, color='#2E7D32', fontweight='bold')

    ax.annotate('', xy=(4.85, 5.5), xytext=(3.95, 5.5),
        arrowprops=dict(arrowstyle='<->', color='#FF9800', lw=2))
    ax.text(4.4, 5.1, 'HTTPS REST', ha='center', fontsize=8, color='#E65100', fontweight='bold')

    # Arrow: Server <-> Database
    ax.annotate('', xy=(9.85, 6.1), xytext=(8.95, 6.1),
        arrowprops=dict(arrowstyle='<->', color='#E53935', lw=2))
    ax.text(9.4, 6.5, 'EF Core', ha='center', fontsize=8, color='#C62828', fontweight='bold')

    # Arrow: Client <-> IndexedDB (vertical)
    ax.annotate('', xy=(1.9, 4.3), xytext=(1.9, 3.4),
        arrowprops=dict(arrowstyle='<->', color='#2E7D32', lw=2))
    ax.text(2.85, 3.85, 'JS Interop', ha='center', fontsize=8, color='#2E7D32', fontweight='bold')

buf = make_figure(arch_fig, w=8, h=5)
story.append(Image(buf, width=14.5*cm, height=8.5*cm))
story.append(Paragraph("Figure 1: ToledoMessage system architecture.", styles['Caption']))
story.append(Spacer(1, 0.3*cm))

story.append(Paragraph("2.2 Technology Stack", styles['SubsectionTitle']))

tech_data = [
    ['Component', 'Technology', 'Purpose'],
    ['Client Runtime', 'Blazor WebAssembly (.NET 10)', 'Browser-based UI + crypto execution'],
    ['Crypto Library', 'BouncyCastle 2.6.2', 'X25519, ML-KEM-768, Ed25519, ML-DSA-65, AES-GCM'],
    ['Server Framework', 'ASP.NET Core 10', 'REST API endpoints + middleware'],
    ['Real-Time', 'SignalR', 'WebSocket-based message delivery + events'],
    ['Authentication', 'ASP.NET Core Identity + JWT', 'User registration, login, token issuance'],
    ['ORM', 'Entity Framework Core 10', 'Code First migrations, SQL Server provider'],
    ['Database', 'SQL Server 2022', 'Persistent storage (encrypted data only)'],
    ['Client Storage', 'Browser IndexedDB', 'Private keys, session state, tokens'],
]

t = Table(tech_data, colWidths=[3.2*cm, 5*cm, 9*cm])
t.setStyle(TableStyle([
    ('BACKGROUND', (0,0), (-1,0), C_PRIMARY),
    ('TEXTCOLOR', (0,0), (-1,0), white),
    ('FONTNAME', (0,0), (-1,0), 'Helvetica-Bold'),
    ('FONTSIZE', (0,0), (-1,-1), 8.5),
    ('FONTSIZE', (0,0), (-1,0), 9),
    ('ALIGN', (0,0), (-1,0), 'CENTER'),
    ('ALIGN', (0,1), (-1,-1), 'LEFT'),
    ('VALIGN', (0,0), (-1,-1), 'MIDDLE'),
    ('GRID', (0,0), (-1,-1), 0.5, HexColor('#D0D8E0')),
    ('ROWBACKGROUNDS', (0,1), (-1,-1), [white, HexColor('#F5F7FA')]),
    ('TOPPADDING', (0,0), (-1,-1), 5),
    ('BOTTOMPADDING', (0,0), (-1,-1), 5),
    ('LEFTPADDING', (0,0), (-1,-1), 6),
    ('RIGHTPADDING', (0,0), (-1,-1), 6),
]))
story.append(KeepTogether([t,
    Paragraph("Table 1: Technology stack components and their roles.", styles['Caption'])]))

# ═══════════════════════════════════════════════════════════
# 3. CRYPTOGRAPHIC MODEL
# ═══════════════════════════════════════════════════════════
story.append(Paragraph("3. Cryptographic Model", styles['SectionTitle']))
story.append(Paragraph("3.1 Hybrid X3DH Key Agreement", styles['SubsectionTitle']))

story.append(Paragraph(
    "The Extended Triple Diffie-Hellman (X3DH) protocol, originally designed by Marlinspike and "
    "Perrin for the Signal Protocol [3], establishes a shared secret between two parties who may "
    "not be simultaneously online. ToledoMessage extends X3DH by incorporating ML-KEM-768 key "
    "encapsulation alongside the standard X25519 Diffie-Hellman exchanges.",
    styles['BodyText2']))

story.append(Paragraph(
    "Each device registers the following pre-key bundle with the server:",
    styles['BodyText2']))

prekey_items = [
    "<bullet>&bull;</bullet> <b>Identity Key:</b> Long-term Ed25519 public key (32 bytes) + ML-DSA-65 public key (1,952 bytes)",
    "<bullet>&bull;</bullet> <b>Signed Pre-Key:</b> Medium-term X25519 public key (32 bytes), signed with hybrid signature",
    "<bullet>&bull;</bullet> <b>Kyber Pre-Key:</b> ML-KEM-768 encapsulation key (1,184 bytes), signed with hybrid signature",
    "<bullet>&bull;</bullet> <b>One-Time Pre-Keys:</b> Batch of 10 ephemeral X25519 public keys (32 bytes each)",
]
for item in prekey_items:
    story.append(Paragraph(item, styles['BulletItem']))

story.append(Spacer(1, 0.5*cm))

story.append(Paragraph("<b>Algorithm 1: Hybrid X3DH Initiator (Alice)</b>", styles['AlgorithmTitle']))
story.append(Spacer(1, 0.15*cm))

x3dh_code = (
    "function X3DH_Initiate(BobBundle):<br/>"
    "  1. Verify(BobBundle.signedPreKey, BobBundle.identityKey)<br/>"
    "  2. Verify(BobBundle.kyberPreKey, BobBundle.identityKey)<br/>"
    "  3. (eph_pub, eph_priv) = X25519.GenerateKeyPair()<br/>"
    "  4. DH1 = X25519(eph_priv, BobBundle.signedPreKey)     // 32 bytes<br/>"
    "  5. DH2 = X25519(eph_priv, BobBundle.oneTimePreKey)    // 32 bytes<br/>"
    "  6. (kem_ct, kem_ss) = ML-KEM-768.Encapsulate(BobBundle.kyberPreKey)<br/>"
    "  7. ikm = DH1 || DH2 || kem_ss<br/>"
    "  8. SK = HKDF-SHA256(ikm, info=\"ToledoMessage-X3DH-v1\", len=64)<br/>"
    "  9. rootKey = SK[0:32], chainKey = SK[32:64]<br/>"
    "  return (rootKey, chainKey, eph_pub, kem_ct)"
)
story.append(Paragraph(x3dh_code, styles['CodeBlock']))
story.append(Spacer(1, 0.3*cm))

story.append(Paragraph(
    "The critical security property of this hybrid scheme is that the shared secret SK depends "
    "on material from <i>both</i> the classical X25519 exchanges and the post-quantum ML-KEM-768 "
    "encapsulation. An attacker must break both X25519 (solving the elliptic curve discrete "
    "logarithm problem) and ML-KEM-768 (solving the Module-LWE problem) to recover the session keys.",
    styles['BodyText2']))

story.append(Paragraph("3.2 Double Ratchet Protocol", styles['SubsectionTitle']))

story.append(Paragraph(
    "After session establishment via X3DH, ToledoMessage employs the Double Ratchet algorithm [4] "
    "to derive unique encryption keys for each message. The protocol maintains three key chains:",
    styles['BodyText2']))

chains = [
    "<bullet>&bull;</bullet> <b>Root Chain:</b> Updated via DH ratchet steps, producing new chain keys. Uses HKDF-SHA256 with the current root key as salt and a new DH shared secret as input key material.",
    "<bullet>&bull;</bullet> <b>Sending Chain:</b> Derives per-message keys for outgoing messages. Each step uses HMAC-SHA256 with the chain key to produce a message key and the next chain key.",
    "<bullet>&bull;</bullet> <b>Receiving Chain:</b> Symmetric to the sending chain, used for decrypting incoming messages. Maintains separate counters for out-of-order message handling.",
]
for chain in chains:
    story.append(Paragraph(chain, styles['BulletItem']))

story.append(Spacer(1, 0.4*cm))

story.append(Paragraph("<b>Algorithm 2: Double Ratchet Message Encryption</b>", styles['AlgorithmTitle']))
story.append(Spacer(1, 0.15*cm))

ratchet_code = (
    "function RatchetEncrypt(session, plaintext):<br/>"
    "  1. (session.sendChainKey, messageKey) = KDF_CK(session.sendChainKey)<br/>"
    "  2. session.sendMessageNumber += 1<br/>"
    "  3. nonce = SecureRandom(12 bytes)<br/>"
    "  4. ciphertext = AES-256-GCM.Encrypt(messageKey, nonce, plaintext)<br/>"
    "  5. SecureErase(messageKey)  // Immediately delete after use<br/>"
    "  return (nonce || ciphertext || authTag)"
)
story.append(Paragraph(ratchet_code, styles['CodeBlock']))
story.append(Spacer(1, 0.3*cm))

story.append(Paragraph(
    "This construction provides two critical security properties: (1) <b>Forward Secrecy</b> \u2014 "
    "compromise of the current session state does not reveal past message keys, as each chain key "
    "is immediately replaced after deriving a message key; (2) <b>Break-in Recovery</b> \u2014 "
    "if an attacker obtains the current session state, a new DH ratchet step (triggered by receiving "
    "a message with a new DH public key) establishes a fresh shared secret, healing the session.",
    styles['BodyText2']))

story.append(Paragraph("3.3 AES-256-GCM Authenticated Encryption", styles['SubsectionTitle']))

story.append(Paragraph(
    "Each message is encrypted using AES-256-GCM (Galois/Counter Mode), an AEAD (Authenticated "
    "Encryption with Associated Data) cipher. The implementation in the <i>AeadCipher</i> class "
    "uses a 256-bit key derived from the Double Ratchet, a 96-bit random nonce generated per "
    "message, and produces a 128-bit authentication tag. The wire format is:",
    styles['BodyText2']))

wire_code = (
    "output = nonce(12 bytes) || ciphertext(variable) || authTag(16 bytes)<br/>"
    "transport = Base64Encode(output)"
)
story.append(Paragraph(wire_code, styles['CodeBlock']))

story.append(Paragraph("3.4 Hybrid Digital Signatures", styles['SubsectionTitle']))

story.append(Paragraph(
    "ToledoMessage employs a hybrid signature scheme combining Ed25519 (classical, 64-byte signatures) "
    "with ML-DSA-65 (post-quantum, 3,309-byte signatures). The <i>HybridSigner</i> class produces "
    "signatures by concatenating both: signature = Ed25519.Sign(m) || ML-DSA-65.Sign(m). "
    "Verification requires both components to pass independently, ensuring that the compromise of "
    "one scheme does not invalidate the authentication guarantee.",
    styles['BodyText2']))

sig_data = [
    ['Property', 'Ed25519', 'ML-DSA-65', 'Hybrid (Combined)'],
    ['Public Key Size', '32 bytes', '1,952 bytes', '1,984 bytes'],
    ['Signature Size', '64 bytes', '3,309 bytes', '3,373 bytes'],
    ['Security Basis', 'ECDLP', 'Module-LWE', 'Both (defense-in-depth)'],
    ['Quantum Resistant', 'No', 'Yes (NIST Level 3)', 'Yes'],
    ['Standard', 'RFC 8032', 'FIPS 204', 'Custom hybrid'],
]

t2 = Table(sig_data, colWidths=[3.5*cm, 3.5*cm, 4*cm, 6.2*cm])
t2.setStyle(TableStyle([
    ('BACKGROUND', (0,0), (-1,0), C_PURPLE),
    ('TEXTCOLOR', (0,0), (-1,0), white),
    ('FONTNAME', (0,0), (-1,0), 'Helvetica-Bold'),
    ('FONTSIZE', (0,0), (-1,-1), 8.5),
    ('FONTSIZE', (0,0), (-1,0), 9),
    ('ALIGN', (0,0), (-1,-1), 'CENTER'),
    ('VALIGN', (0,0), (-1,-1), 'MIDDLE'),
    ('GRID', (0,0), (-1,-1), 0.5, HexColor('#D0D8E0')),
    ('ROWBACKGROUNDS', (0,1), (-1,-1), [white, HexColor('#F5F7FA')]),
    ('TOPPADDING', (0,0), (-1,-1), 5),
    ('BOTTOMPADDING', (0,0), (-1,-1), 5),
]))
story.append(KeepTogether([t2,
    Paragraph("Table 2: Comparison of signature scheme properties.", styles['Caption'])]))

# ═══════════════════════════════════════════════════════════
# 4. POST-QUANTUM INTEGRATION
# ═══════════════════════════════════════════════════════════
story.append(Paragraph("4. Post-Quantum Cryptographic Integration", styles['SectionTitle']))
story.append(Paragraph("4.1 ML-KEM-768 (CRYSTALS-Kyber)", styles['SubsectionTitle']))

story.append(Paragraph(
    "ML-KEM-768 is a lattice-based Key Encapsulation Mechanism standardized by NIST in FIPS 203 [5]. "
    "It provides IND-CCA2 security based on the Module Learning With Errors (MLWE) problem. "
    "ToledoMessage integrates ML-KEM-768 at the X3DH key agreement stage via the <i>MlKemKeyExchange</i> "
    "class, which wraps BouncyCastle's ML-KEM implementation.",
    styles['BodyText2']))

kem_data = [
    ['Parameter', 'Value'],
    ['Algorithm', 'ML-KEM-768 (FIPS 203)'],
    ['Security Level', 'NIST Level 3 (~192-bit classical equivalent)'],
    ['Public Key Size', '1,184 bytes'],
    ['Ciphertext Size', '1,088 bytes'],
    ['Shared Secret Size', '32 bytes'],
    ['Underlying Problem', 'Module-LWE (lattice-based)'],
    ['Integration Point', 'X3DH step 6: KEM encapsulation against Kyber pre-key'],
]

t3 = Table(kem_data, colWidths=[4.5*cm, 12.7*cm])
t3.setStyle(TableStyle([
    ('BACKGROUND', (0,0), (-1,0), C_TEAL),
    ('TEXTCOLOR', (0,0), (-1,0), white),
    ('FONTNAME', (0,0), (-1,0), 'Helvetica-Bold'),
    ('FONTSIZE', (0,0), (-1,-1), 8.5),
    ('ALIGN', (0,0), (0,-1), 'LEFT'),
    ('ALIGN', (1,0), (1,-1), 'LEFT'),
    ('VALIGN', (0,0), (-1,-1), 'MIDDLE'),
    ('GRID', (0,0), (-1,-1), 0.5, HexColor('#D0D8E0')),
    ('ROWBACKGROUNDS', (0,1), (-1,-1), [white, HexColor('#F5F7FA')]),
    ('TOPPADDING', (0,0), (-1,-1), 5),
    ('BOTTOMPADDING', (0,0), (-1,-1), 5),
    ('LEFTPADDING', (0,0), (-1,-1), 8),
    ('RIGHTPADDING', (0,0), (-1,-1), 8),
]))
story.append(KeepTogether([t3,
    Paragraph("Table 3: ML-KEM-768 parameters in ToledoMessage.", styles['Caption'])]))

story.append(Paragraph("4.2 Hybrid Security Argument", styles['SubsectionTitle']))

story.append(Paragraph(
    "The hybrid key agreement provides a \"best of both worlds\" security guarantee. Let SK = "
    "HKDF(DH1 || DH2 || kem_ss). An adversary attempting to recover SK must either: (a) solve "
    "the Computational Diffie-Hellman problem on Curve25519 to recover DH1 and DH2, <i>and</i> "
    "(b) solve the Module-LWE problem to recover kem_ss from the ML-KEM ciphertext. The HKDF "
    "construction ensures that even partial knowledge of the input key material does not reveal "
    "the derived key, provided at least one component remains secret [6].",
    styles['BodyText2']))

# ═══════════════════════════════════════════════════════════
# 5. MESSAGE FLOW
# ═══════════════════════════════════════════════════════════
story.append(Paragraph("5. End-to-End Message Flow", styles['SectionTitle']))

story.append(Paragraph(
    "The complete lifecycle of a message in ToledoMessage involves the following steps:",
    styles['BodyText2']))

flow_steps = [
    "<bullet>&bull;</bullet> <b>Step 1 - Session Establishment:</b> If no active Double Ratchet session exists with the recipient, the sender's <i>CryptoService</i> initiates X3DH by fetching the recipient's pre-key bundle from the server.",
    "<bullet>&bull;</bullet> <b>Step 2 - Key Derivation:</b> The Double Ratchet derives a unique message key (MK) from the sending chain. The chain key is advanced, ensuring MK is never reused.",
    "<bullet>&bull;</bullet> <b>Step 3 - Encryption:</b> <i>AeadCipher.Encrypt(MK, plaintext)</i> produces the AES-256-GCM ciphertext with random nonce and authentication tag.",
    "<bullet>&bull;</bullet> <b>Step 4 - Multi-Device Fan-Out:</b> <i>MessageEncryptionService</i> repeats steps 2\u20133 for each of the recipient's registered devices, producing independent ciphertexts.",
    "<bullet>&bull;</bullet> <b>Step 5 - Transport:</b> Encrypted envelopes are sent via SignalR's <i>ChatHub.SendMessage()</i>. The server stores the ciphertext and routes it to connected devices.",
    "<bullet>&bull;</bullet> <b>Step 6 - Delivery:</b> The recipient's device receives the envelope via SignalR push, decrypts using its own Double Ratchet session, and displays the plaintext.",
    "<bullet>&bull;</bullet> <b>Step 7 - Acknowledgment:</b> Delivery and read receipts flow back through SignalR, updating message status (Sending \u2192 Sent \u2192 Delivered \u2192 Read).",
]
for step in flow_steps:
    story.append(Paragraph(step, styles['BulletItem']))

# ═══════════════════════════════════════════════════════════
# 6. SECURITY ANALYSIS
# ═══════════════════════════════════════════════════════════
story.append(Paragraph("6. Security Analysis", styles['SectionTitle']))
story.append(Paragraph("6.1 Threat Model", styles['SubsectionTitle']))

story.append(Paragraph(
    "We consider an adversary with the following capabilities: (a) full control of the network "
    "(active man-in-the-middle), (b) ability to compromise the server and access all stored data, "
    "(c) access to a cryptographically relevant quantum computer (CRQC). Table 4 summarizes the "
    "security guarantees provided by ToledoMessage against each threat.",
    styles['BodyText2']))

threat_data = [
    ['Threat', 'Mitigation', 'Status'],
    ['Man-in-the-Middle', 'X3DH mutual auth + safety number verification', 'Mitigated'],
    ['Server Compromise', 'E2EE: server sees only ciphertext', 'Mitigated'],
    ['Past Key Compromise', 'Forward secrecy via Double Ratchet', 'Mitigated'],
    ['Future Key Compromise', 'Break-in recovery via DH ratchet', 'Mitigated'],
    ['Quantum Attack (keys)', 'ML-KEM-768 in X3DH key agreement', 'Mitigated'],
    ['Quantum Attack (sigs)', 'ML-DSA-65 in hybrid signatures', 'Mitigated'],
    ['Replay Attack', 'Random nonce + message counters', 'Mitigated'],
    ['Device Theft', 'Keys in browser IndexedDB (session-bound)', 'Partial'],
    ['Metadata Analysis', 'Server sees routing data (not content)', 'Acknowledged'],
]

t4 = Table(threat_data, colWidths=[4*cm, 9.7*cm, 3.5*cm])
t4.setStyle(TableStyle([
    ('BACKGROUND', (0,0), (-1,0), C_RED),
    ('TEXTCOLOR', (0,0), (-1,0), white),
    ('FONTNAME', (0,0), (-1,0), 'Helvetica-Bold'),
    ('FONTSIZE', (0,0), (-1,-1), 8.5),
    ('FONTSIZE', (0,0), (-1,0), 9),
    ('ALIGN', (0,0), (-1,0), 'CENTER'),
    ('ALIGN', (0,1), (0,-1), 'LEFT'),
    ('ALIGN', (1,1), (1,-1), 'LEFT'),
    ('ALIGN', (2,1), (2,-1), 'CENTER'),
    ('VALIGN', (0,0), (-1,-1), 'MIDDLE'),
    ('GRID', (0,0), (-1,-1), 0.5, HexColor('#D0D8E0')),
    ('ROWBACKGROUNDS', (0,1), (-1,-1), [white, HexColor('#F5F7FA')]),
    ('TOPPADDING', (0,0), (-1,-1), 5),
    ('BOTTOMPADDING', (0,0), (-1,-1), 5),
    ('LEFTPADDING', (0,0), (-1,-1), 6),
    ('RIGHTPADDING', (0,0), (-1,-1), 6),
]))
story.append(KeepTogether([t4,
    Paragraph("Table 4: Threat model and mitigations.", styles['Caption'])]))

# ═══════════════════════════════════════════════════════════
# 7. COMPARISON
# ═══════════════════════════════════════════════════════════
story.append(Paragraph("7. Comparison with Existing Systems", styles['SectionTitle']))

# Use Paragraph cells so long text wraps properly
def hdr(t): return Paragraph(f'<b>{t}</b>', ParagraphStyle('h', fontName='Helvetica-Bold', fontSize=8, leading=10, textColor=white, alignment=TA_CENTER))
def cel(t): return Paragraph(t, ParagraphStyle('c', fontName='Helvetica', fontSize=8, leading=10.5, textColor=HexColor('#333333'), alignment=TA_CENTER))
def cel_l(t): return Paragraph(t, ParagraphStyle('cl', fontName='Helvetica', fontSize=8, leading=10.5, textColor=HexColor('#333333'), alignment=TA_LEFT))

comp_data = [
    [hdr('Feature'), hdr('Toledo-<br/>Message'), hdr('Signal'), hdr('WhatsApp'), hdr('Telegram'), hdr('iMessage')],
    [cel_l('E2EE by Default'), cel('Yes'), cel('Yes'), cel('Yes'), cel('No*'), cel('Yes')],
    [cel_l('Post-Quantum KEM'), cel('ML-KEM-768'), cel('PQXDH**'), cel('No'), cel('No'), cel('PQ3**')],
    [cel_l('PQ Signatures'), cel('ML-DSA-65'), cel('No'), cel('No'), cel('No'), cel('No')],
    [cel_l('Forward Secrecy'), cel('Yes'), cel('Yes'), cel('Yes'), cel('Yes*'), cel('Yes')],
    [cel_l('Open Source Crypto'), cel('Yes'), cel('Yes'), cel('No'), cel('No'), cel('No')],
    [cel_l('Safety Numbers'), cel('Yes'), cel('Yes'), cel('Yes'), cel('No'), cel('No')],
    [cel_l('Multi-Device E2EE'), cel('Yes'), cel('Partial'), cel('Partial'), cel('No'), cel('Yes')],
    [cel_l('Disappearing Msgs'), cel('Yes'), cel('Yes'), cel('Yes'), cel('Yes'), cel('No')],
    [cel_l('Browser Client'), cel('Yes (WASM)'), cel('No'), cel('No'), cel('Yes'), cel('No')],
    [cel_l('Key Agreement'), cel('Hybrid X3DH'), cel('X3DH'), cel('X3DH'), cel('DH'), cel('ECIES')],
    [cel_l('Symmetric Cipher'), cel('AES-256-GCM'), cel('AES-256-CBC'), cel('AES-256-GCM'), cel('AES-256-IGE'), cel('AES-CTR')],
]

comp_col_w = [3.5*cm, 2.6*cm, 2.3*cm, 2.3*cm, 2.3*cm, 2.3*cm]
t5 = Table(comp_data, colWidths=comp_col_w)
t5.setStyle(TableStyle([
    ('BACKGROUND', (0,0), (-1,0), C_PRIMARY),
    ('VALIGN', (0,0), (-1,-1), 'MIDDLE'),
    ('GRID', (0,0), (-1,-1), 0.5, HexColor('#D0D8E0')),
    ('ROWBACKGROUNDS', (0,1), (-1,-1), [white, HexColor('#F5F7FA')]),
    ('TOPPADDING', (0,0), (-1,-1), 4),
    ('BOTTOMPADDING', (0,0), (-1,-1), 4),
    ('LEFTPADDING', (0,0), (-1,-1), 4),
    ('RIGHTPADDING', (0,0), (-1,-1), 4),
]))
story.append(KeepTogether([t5,
    Paragraph("Table 5: Feature comparison. *Telegram requires manual opt-in for secret chats. "
              "**Signal's PQXDH and Apple's PQ3 are recent additions (2023\u20132024) using different PQ schemes.",
              styles['Caption'])]))

# Comparison chart
def comp_chart(fig, ax):
    apps = ['ToledoMessage', 'Signal', 'WhatsApp', 'Telegram\n(Secret)', 'iMessage']
    scores = [7, 5.5, 4.5, 2.5, 4]
    colors = ['#3A76F0', '#2196F3', '#25D366', '#2AABEE', '#007AFF']

    bars = ax.barh(apps, scores, color=colors, edgecolor='white', linewidth=0.8, height=0.55)
    ax.set_xlim(0, 8)
    ax.set_xlabel('Security Feature Score (out of 7)', fontsize=9, color='#333333')
    ax.tick_params(colors='#333333', labelsize=9)
    ax.spines['top'].set_visible(False)
    ax.spines['right'].set_visible(False)
    for bar, score in zip(bars, scores):
        ax.text(bar.get_width() + 0.12, bar.get_y() + bar.get_height()/2,
                f'{score}/7', ha='left', va='center', fontsize=9, fontweight='bold', color='#333333')

buf = make_figure(comp_chart, w=6.5, h=2.8)
story.append(Image(buf, width=14*cm, height=6*cm))
story.append(Paragraph("Figure 2: Aggregate security feature comparison.", styles['Caption']))

# ═══════════════════════════════════════════════════════════
# 8. IMPLEMENTATION DETAILS
# ═══════════════════════════════════════════════════════════
story.append(Paragraph("8. Implementation Details", styles['SectionTitle']))
story.append(Paragraph("8.1 Client-Side Services", styles['SubsectionTitle']))

story.append(Paragraph(
    "The Blazor WebAssembly client implements the following key services, all running entirely "
    "within the browser's WASM sandbox:",
    styles['BodyText2']))

# ── Table 6 — FIXED: Use Paragraph cells so long service names wrap properly ──
svc_data = [
    [Paragraph('<b>Service</b>', ParagraphStyle('sh', fontName='Helvetica-Bold', fontSize=9, leading=11, textColor=white, alignment=TA_CENTER)),
     Paragraph('<b>Responsibility</b>', ParagraphStyle('sh2', fontName='Helvetica-Bold', fontSize=9, leading=11, textColor=white, alignment=TA_CENTER))],
    [Paragraph('CryptoService', styles['CellCode']),
     Paragraph('Orchestrates X3DH initiation, session establishment, encrypt/decrypt operations', styles['CellText'])],
    [Paragraph('SessionService', styles['CellCode']),
     Paragraph('Manages Double Ratchet state per peer device (root key, chain keys, counters)', styles['CellText'])],
    [Paragraph('KeyGenerationService', styles['CellCode']),
     Paragraph('Generates identity keys (Ed25519+ML-DSA), signed pre-keys, Kyber pre-keys, OTP keys', styles['CellText'])],
    [Paragraph('MessageEncryption-<br/>Service', styles['CellCode']),
     Paragraph("Multi-device fan-out: encrypts for all of recipient's registered devices", styles['CellText'])],
    [Paragraph('LocalStorageService', styles['CellCode']),
     Paragraph('Encrypted IndexedDB storage via JS Interop for keys and session state', styles['CellText'])],
    [Paragraph('FingerprintService', styles['CellCode']),
     Paragraph('Computes SHA-256 safety numbers from identity keys for verification', styles['CellText'])],
    [Paragraph('PreKeyReplenishment-<br/>Service', styles['CellCode']),
     Paragraph('Monitors and auto-replenishes one-time pre-keys when count drops low', styles['CellText'])],
    [Paragraph('SignalRService', styles['CellCode']),
     Paragraph('WebSocket connection management, event subscription, message transport', styles['CellText'])],
    [Paragraph('ThemeService', styles['CellCode']),
     Paragraph('Persists and applies user-selected UI theme via localStorage', styles['CellText'])],
]

t6 = Table(svc_data, colWidths=[4.5*cm, 12.7*cm])
t6.setStyle(TableStyle([
    ('BACKGROUND', (0,0), (-1,0), HexColor('#1565C0')),
    ('VALIGN', (0,0), (-1,-1), 'MIDDLE'),
    ('GRID', (0,0), (-1,-1), 0.5, HexColor('#D0D8E0')),
    ('ROWBACKGROUNDS', (0,1), (-1,-1), [white, HexColor('#F5F7FA')]),
    ('TOPPADDING', (0,0), (-1,-1), 5),
    ('BOTTOMPADDING', (0,0), (-1,-1), 5),
    ('LEFTPADDING', (0,0), (-1,-1), 6),
    ('RIGHTPADDING', (0,0), (-1,-1), 6),
]))
story.append(KeepTogether([t6,
    Paragraph("Table 6: Client-side service architecture.", styles['Caption'])]))

story.append(Paragraph("8.2 Database Schema", styles['SubsectionTitle']))

story.append(Paragraph(
    "The SQL Server database stores user accounts, device registrations (with public keys only), "
    "conversations, participants, and encrypted messages. The schema uses decimal primary keys "
    "with precision 20 and scale 0, UTC timestamps, and cascading deletes for referential integrity. "
    "Critically, the database never contains private keys or plaintext message content\u2014only "
    "ciphertext blobs and public cryptographic material.",
    styles['BodyText2']))

story.append(Paragraph("8.3 Test Coverage", styles['SubsectionTitle']))

story.append(Paragraph(
    "The cryptographic layer is validated by 65 unit tests covering X25519 key exchange, ML-KEM-768 "
    "encapsulation/decapsulation, Ed25519 and ML-DSA-65 signing/verification, hybrid signatures, "
    "X3DH protocol flow, Double Ratchet session management, and AES-256-GCM encryption/decryption. "
    "All tests pass consistently across the test suite.",
    styles['BodyText2']))

# ═══════════════════════════════════════════════════════════
# 9. CONCLUSION
# ═══════════════════════════════════════════════════════════
story.append(Paragraph("9. Conclusion", styles['SectionTitle']))

story.append(Paragraph(
    "ToledoMessage demonstrates that hybrid post-quantum end-to-end encryption is feasible within "
    "a browser-based environment. By integrating ML-KEM-768 into the X3DH key agreement and "
    "ML-DSA-65 into the signature scheme, the system provides quantum-resistant security without "
    "sacrificing the proven guarantees of classical cryptography. The Double Ratchet protocol "
    "ensures forward secrecy and break-in recovery at the per-message level, while the zero-knowledge "
    "server architecture ensures that even a fully compromised server cannot access message content.",
    styles['BodyText2']))

story.append(Paragraph(
    "Future work includes: (1) formal security proofs using tools such as ProVerif or Tamarin, "
    "(2) mobile native applications using .NET MAUI, (3) encrypted file and media transfer, "
    "(4) voice and video calling with SRTP encryption, (5) multi-device synchronization using "
    "the Sesame protocol, and (6) decentralized federation following the Matrix protocol model.",
    styles['BodyText2']))

# ═══════════════════════════════════════════════════════════
# REFERENCES
# ═══════════════════════════════════════════════════════════
story.append(Paragraph("References", styles['SectionTitle']))

refStyle = ParagraphStyle('RefItem', parent=styles['BodyText2'],
    fontSize=8.5, leading=12, spaceAfter=4, leftIndent=24, firstLineIndent=-24)

refs = [
    "[1] P. Shor, \"Algorithms for quantum computation: discrete logarithms and factoring,\" <i>Proc. 35th FOCS</i>, IEEE, 1994.",
    "[2] NIST, \"Post-Quantum Cryptography Standardization,\" FIPS 203, FIPS 204, FIPS 205, 2024.",
    "[3] M. Marlinspike and T. Perrin, \"The X3DH Key Agreement Protocol,\" Signal Foundation, 2016.",
    "[4] T. Perrin and M. Marlinspike, \"The Double Ratchet Algorithm,\" Signal Foundation, 2016.",
    "[5] NIST, \"Module-Lattice-Based Key-Encapsulation Mechanism Standard,\" FIPS 203, August 2024.",
    "[6] H. Krawczyk, \"Cryptographic Extraction and Key Derivation: The HKDF Scheme,\" <i>Proc. CRYPTO 2010</i>, Springer.",
    "[7] D. J. Bernstein et al., \"Post-Quantum Cryptography,\" <i>Nature</i>, vol. 549, 2017.",
    "[8] Signal Foundation, \"Signal Protocol Technical Documentation,\" signal.org/docs, 2023.",
]

for ref in refs:
    story.append(Paragraph(ref, refStyle))

# ─── Build PDF with page numbers ───
doc.build(story, onFirstPage=add_page_number, onLaterPages=add_page_number)
print(f"Saved: {out_path}")
