"""Owned 127.0.0.1 source for functional-first disposable Windows test only.

Serves exactly a generated .66 ZIP and its computed SHA256. No GitHub secrets,
arbitrary path serving, public listener, upload, telemetry, or production use.
"""
import hashlib
import http.server
import json
import sys
from pathlib import Path

root = Path(sys.argv[1]).resolve(strict=True)
archive = root / "gf-private-e2e-new.zip"
assert archive.is_file() and archive.stat().st_size < 250_000_000
checksum = hashlib.file_digest(archive.open("rb"), "sha256").hexdigest()
feed = json.dumps({
    "schema": "gf-private-functional-feed/1",
    "version": "5.13.11.66",
    "archive": "gf-new.zip",
    "sha256": checksum,
}, sort_keys=True).encode("utf-8")


class Handler(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
        if self.path == "/feed.json":
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(feed)))
            self.end_headers()
            self.wfile.write(feed)
        elif self.path == "/gf-new.zip":
            self.send_response(200)
            self.send_header("Content-Type", "application/octet-stream")
            self.send_header("Content-Length", str(archive.stat().st_size))
            self.end_headers()
            with archive.open("rb") as source:
                while data := source.read(64 * 1024):
                    self.wfile.write(data)
        else:
            self.send_error(404)

    def log_message(self, _format, *_args):
        pass


with http.server.ThreadingHTTPServer(("127.0.0.1", 0), Handler) as server:
    (root / "gf-private-e2e-port.txt").write_text(
        str(server.server_address[1]), encoding="ascii")
    server.serve_forever(poll_interval=0.2)
