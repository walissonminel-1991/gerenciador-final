"""Only the disposable GF OLD HTTP interruption test. Never product code."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import socket
from http.server import BaseHTTPRequestHandler, HTTPServer

PINNED_SHA = "3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e"
PINNED_LENGTH = 57695630
CUT_AFTER = 1048576


def atomic_json(path: Path, value: dict) -> None:
    tmp = path.with_suffix(path.suffix + ".tmp")
    with tmp.open("x", encoding="utf-8") as fd:
        json.dump(value, fd, sort_keys=True)
        fd.flush()
        os.fsync(fd.fileno())
    os.replace(tmp, path)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--ready", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    args = parser.parse_args()
    assert os.environ.get("GITHUB_ACTIONS") == "true"
    assert os.environ.get("GITHUB_REPOSITORY") == "walissonminel-1991/gerenciador-final"
    runner = Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
    source = args.source.resolve(strict=True)
    assert source.parent.parent == runner
    assert source.name == "gf-e2e-new-win-x64.zip"
    assert source.stat().st_size == PINNED_LENGTH
    with source.open("rb") as stream:
        assert hashlib.file_digest(stream, "sha256").hexdigest() == PINNED_SHA
    assert args.ready.parent.resolve(strict=True) == runner
    assert args.report.parent.resolve(strict=True) == runner
    assert not args.ready.exists() and not args.report.exists()

    class CutHandler(BaseHTTPRequestHandler):
        def do_GET(self):
            assert self.path == "/gf-e2e-new-win-x64.zip", "ONLY_PINNED_ZIP_PATH"
            assert "Authorization" not in self.headers
            assert "X-GitHub-Api-Version" not in self.headers
            self.send_response(200)
            self.send_header("Content-Type", "application/octet-stream")
            self.send_header("Content-Length", str(PINNED_LENGTH))
            self.send_header("Connection", "close")
            self.end_headers()
            sent = 0
            with source.open("rb") as reader:
                while sent < CUT_AFTER:
                    buf = reader.read(min(65536, CUT_AFTER - sent))
                    assert buf
                    self.wfile.write(buf)
                    self.wfile.flush()
                    sent += len(buf)
            # Real TCP disconnect after successful HTTP headers and
            # partial transport of the AUTHENTIC Draft ZIP, not a
            # synthetically truncated file delivered as HTTP success.
            try:
                self.connection.shutdown(socket.SHUT_RDWR)
            except OSError:
                pass
            self.connection.close()
            atomic_json(args.report, {
                "schema": "gf-e2e-http-disconnect/1",
                "http_status_sent": 200,
                "content_length_declared": PINNED_LENGTH,
                "authentic_archive_sha256": PINNED_SHA,
                "bytes_sent_before_tcp_disconnect": sent,
                "socket_shutdown_in_body": True,
                "loopback_only": True,
                "bearer_not_received": True,
                "test_only": True,
                "operational_ready": False,
            })
        def log_message(self, fmt, *values):
            # Never log signed URLs or headers.
            return

    with HTTPServer(("127.0.0.1", 0), CutHandler) as server:
        server.timeout = 70
        atomic_json(args.ready, {
            "schema": "gf-e2e-http-disconnect-ready/1",
            "port": server.server_port,
            "loopback_only": True,
            "test_only": True,
        })
        server.handle_request()
    print("GF_E2E_LOCAL_HTTP_STREAM_CUT server_exited=true", flush=True)


if __name__ == "__main__":
    main()
