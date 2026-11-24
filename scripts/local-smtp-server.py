import asyncio
import datetime
import ipaddress
import os
import ssl
import tempfile
from cryptography import x509
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.x509.oid import NameOID

CERT_DIR = os.path.join(tempfile.gettempdir(), "local_smtp_tls")
os.makedirs(CERT_DIR, exist_ok=True)
CERT_FILE = os.path.join(CERT_DIR, "cert.pem")
KEY_FILE = os.path.join(CERT_DIR, "key.pem")

def ensure_self_signed_cert():
    if os.path.exists(CERT_FILE) and os.path.exists(KEY_FILE):
        return
    key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
    subject = issuer = x509.Name([
        x509.NameAttribute(NameOID.COMMON_NAME, "host.docker.internal"),
    ])
    san = x509.SubjectAlternativeName([
        x509.DNSName("host.docker.internal"),
        x509.DNSName("localhost"),
        x509.IPAddress(ipaddress.IPv4Address("127.0.0.1")),
    ])
    now = datetime.datetime.now(datetime.timezone.utc)
    cert = (
        x509.CertificateBuilder()
        .subject_name(subject)
        .issuer_name(issuer)
        .public_key(key.public_key())
        .serial_number(x509.random_serial_number())
        .not_valid_before(now - datetime.timedelta(days=1))
        .not_valid_after(now + datetime.timedelta(days=14))
        .add_extension(san, critical=False)
        .sign(key, hashes.SHA256())
    )
    with open(KEY_FILE, "wb") as f:
        f.write(
            key.private_bytes(
                encoding=serialization.Encoding.PEM,
                format=serialization.PrivateFormat.TraditionalOpenSSL,
                encryption_algorithm=serialization.NoEncryption(),
            )
        )
    with open(CERT_FILE, "wb") as f:
        f.write(cert.public_bytes(serialization.Encoding.PEM))

ensure_self_signed_cert()

received_count = 0
lock = asyncio.Lock()

class SmtpSession:
    def __init__(self, reader: asyncio.StreamReader, writer: asyncio.StreamWriter, tls_ctx: ssl.SSLContext):
        self.reader = reader
        self.writer = writer
        self.tls_ctx = tls_ctx
        self.tls_active = False
        self.in_data = False
        self.data_lines: list[str] = []

    async def send(self, line: str):
        self.writer.write((line + "\r\n").encode("utf-8"))
        await self.writer.drain()

    async def run(self):
        global received_count
        await self.send("220 localhost Local Realistic SMTP Ready")
        try:
            while not self.reader.at_eof():
                raw = await self.reader.readline()
                if not raw:
                    break
                line = raw.decode("utf-8", errors="replace").rstrip("\r\n")

                if self.in_data:
                    if line == ".":
                        self.in_data = False
                        async with lock:
                            received_count += 1
                            cnt = received_count
                        print(f"[{datetime.datetime.now().isoformat()}] Delivered email #{cnt} (lines={len(self.data_lines)})", flush=True)
                        self.data_lines.clear()
                        await self.send("250 2.0.0 OK: queued")
                    else:
                        self.data_lines.append(line)
                    continue

                upper = line.upper().strip()
                if upper.startswith("EHLO") or upper.startswith("HELO"):
                    if not self.tls_active:
                        await self.send("250-localhost")
                        await self.send("250-STARTTLS")
                        await self.send("250 AUTH PLAIN LOGIN")
                    else:
                        await self.send("250-localhost")
                        await self.send("250 AUTH PLAIN LOGIN")
                elif upper == "STARTTLS" and not self.tls_active:
                    await self.send("220 2.0.0 Ready to start TLS")
                    transport = self.writer.transport
                    loop = asyncio.get_running_loop()
                    new_transport = await loop.start_tls(
                        transport,
                        self.writer._protocol,
                        self.tls_ctx,
                        server_side=True,
                    )
                    self.writer._transport = new_transport
                    self.reader._transport = new_transport
                    self.tls_active = True
                elif upper.startswith("AUTH PLAIN"):
                    parts = line.split(" ", 2)
                    if len(parts) == 3 and parts[2].strip():
                        await self.send("235 2.7.0 Authentication successful")
                    else:
                        await self.send("334 ")
                        resp = (await self.reader.readline()).decode("utf-8", errors="replace").strip()
                        if resp:
                            await self.send("235 2.7.0 Authentication successful")
                        else:
                            await self.send("535 5.7.8 Auth failed")
                elif upper.startswith("AUTH LOGIN"):
                    await self.send("334 VXNlcm5hbWU6")
                    await self.reader.readline()
                    await self.send("334 UGFzc3dvcmQ6")
                    await self.reader.readline()
                    await self.send("235 2.7.0 Authentication successful")
                elif upper.startswith("MAIL FROM:") or upper.startswith("RCPT TO:"):
                    await self.send("250 2.1.0 OK")
                elif upper == "DATA":
                    self.in_data = True
                    self.data_lines.clear()
                    await self.send("354 End data with <CR><LF>.<CR><LF>")
                elif upper == "RSET" or upper == "NOOP":
                    await self.send("250 2.0.0 OK")
                elif upper == "QUIT":
                    await self.send("221 2.0.0 Bye")
                    break
                else:
                    await self.send("250 2.0.0 OK")
        except Exception as e:
            print(f"Session error: {e}", flush=True)
        finally:
            try:
                self.writer.close()
                await self.writer.wait_closed()
            except Exception:
                pass

async def main():
    ctx = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
    ctx.load_cert_chain(CERT_FILE, KEY_FILE)
    server = await asyncio.start_server(
        lambda r, w: asyncio.create_task(SmtpSession(r, w, ctx).run()),
        host="0.0.0.0",
        port=2525,
    )
    print("Listening on 0.0.0.0:2525 (TLS enabled=True)", flush=True)
    async with server:
        await server.serve_forever()

if __name__ == "__main__":
    asyncio.run(main())
