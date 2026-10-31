"""GF D2.1: simulated HTTP response streaming of authenticated INERT bytes.

No sockets, no real HTTP requests, no executable downloads, no production trust.
This strictly bounded fake transport feeds the existing D2 stage writer; it is
not a production-grade HTTP client nor a complete Windows TOCTOU boundary.
"""
from __future__ import annotations

from io import BytesIO
import os
from pathlib import Path
import shutil
import sys
import tempfile

from tuf.api import exceptions

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "github-d2-inert"))
sys.path.insert(0, str(HERE.parent / "github-crossprocess-lab"))
sys.path.insert(0, str(HERE.parent / "github-trust-lab"))
sys.path.insert(0, str(HERE.parent / "github-auth-lab"))

from d2_inert_lab import (
    acquire_and_stage, verify_d0_tuf, check_bytes, DESTINATION,
    ALLOWED_URL_PREFIX, MAX_LAB_BYTES,
)
from d12_crossprocess_lab import release_fixture
from d11_persistent_lab import release_roles
from d1_release_manifest_lab import (
    ANCHOR, ASSET, PAYLOAD, D1Rejected, create_manifest,
    digest, encode_manifest, load_generator, require,
)

INERT_URL = ALLOWED_URL_PREFIX + "v5.13.11.66/" + ASSET
MAX_HEADER_COUNT = 12
READ_BLOCK = 4096


class SimulatedResponse:
    """A response object explicitly backed only by process-local memory."""
    def __init__(self, *, status=200, url=INERT_URL, headers=None, body=PAYLOAD,
                 redirect_history=(), fragment=READ_BLOCK, cut_at=None,
                 fault_at=None):
        self.status = status
        self.url = url
        self.headers = headers if headers is not None else (
            ("Content-Type", "application/octet-stream"),
            ("Content-Length", str(len(PAYLOAD))),
        )
        self.redirect_history = redirect_history
        self.body = body
        self.fragment = fragment
        self.cut_at = cut_at
        self.fault_at = fault_at
        self.stream = BytesIO(body) if type(body) is bytes else None
        self.closed = False
        self.bytes_read = 0

    def read(self, max_len):
        require(not self.closed and type(max_len) is int and
                0 < max_len <= READ_BLOCK, "SIMULATED_READ_REQUEST")
        if self.fault_at is not None and self.bytes_read >= self.fault_at:
            raise OSError("SIMULATED_TRANSPORT_DISCONNECT")
        if self.cut_at is not None and self.bytes_read >= self.cut_at:
            return b""
        n = min(max_len, self.fragment)
        if self.cut_at is not None:
            n = min(n, self.cut_at - self.bytes_read)
        value = self.stream.read(n)
        self.bytes_read += len(value)
        return value

    def close(self):
        self.closed = True
        if self.stream is not None:
            self.stream.close()


class StrictSimulatedHttpSource:
    """One GET-shaped response, with zero network capability.

    Only an already-authenticated expected URL can be supplied by the caller.
    The data stream is passed to D2 without buffering it as another whole file.
    """
    def __init__(self, response):
        self.response = response
        self.fetches = 0

    def stream(self, expected_url):
        self.fetches += 1
        r = self.response
        require(self.fetches == 1 and
                type(expected_url) is str and expected_url == INERT_URL and
                expected_url.startswith(ALLOWED_URL_PREFIX), "HTTP_SOURCE_FIXED")
        require(isinstance(r, SimulatedResponse), "HTTP_FAKE_ONLY")
        try:
            require(type(r.status) is int and r.status == 200,
                    "HTTP_NO_PARTIAL_OR_ERRORS")
            require(type(r.url) is str and r.url == expected_url,
                    "HTTP_NO_ORIGIN_SWAP")
            require(type(r.redirect_history) is tuple and
                    len(r.redirect_history) == 0, "HTTP_NO_REDIRECT")
            require(type(r.headers) in (list, tuple) and
                    0 < len(r.headers) <= MAX_HEADER_COUNT,
                    "HTTP_HEADER_COUNT")
            fields = {}
            for item in r.headers:
                require(type(item) is tuple and len(item) == 2 and
                        type(item[0]) is str and type(item[1]) is str,
                        "HTTP_HEADER_TYPES")
                k, v = item[0], item[1]
                require(k.isascii() and k == k.strip() and
                        k and all(c.isalnum() or c == "-" for c in k) and
                        not any(c in v for c in "\r\n\x00") and v == v.strip(),
                        "HTTP_HEADER_INVALID")
                k = k.lower()
                require(k not in fields, "HTTP_DUPLICATE_HEADER")
                fields[k] = v
            require(set(fields) == {"content-type", "content-length"},
                    "HTTP_UNEXPECTED_OR_MISSING_HEADER")
            require(fields["content-type"] == "application/octet-stream",
                    "HTTP_CONTENT_TYPE")
            declared = fields["content-length"]
            require(declared.isascii() and declared.isdecimal() and
                    declared == str(int(declared)) and
                    0 < int(declared) <= MAX_LAB_BYTES and
                    int(declared) == len(PAYLOAD),
                    "HTTP_CONTENT_LENGTH")
            require(type(r.fragment) is int and
                    0 < r.fragment <= READ_BLOCK and
                    type(r.body) is bytes, "HTTP_FAKE_BODY_AND_FRAGMENT")
            received = 0
            while True:
                part = r.read(READ_BLOCK)
                require(type(part) is bytes, "HTTP_BAD_FRAME")
                if not part:
                    break
                received += len(part)
                require(received <= int(declared) and
                        received <= MAX_LAB_BYTES, "HTTP_BODY_OVERFLOW")
                yield part
            require(received == int(declared), "HTTP_BODY_TRUNCATED")
        finally:
            r.close()


def run(exe):
    require(sys.platform == "win32" and
            os.environ.get("GITHUB_ACTIONS") == "true" and
            os.environ.get("RUNNER_TEMP"), "D21_WINDOWS_ONLY")
    runner = Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
    exe = Path(exe).resolve(strict=True)
    require(exe.is_relative_to(runner) and exe.name == "GF.D11.D0Probe.exe",
            "D21_D0_SCOPE")
    require(digest(ANCHOR.read_bytes()) ==
            "a76278c5a75b80c11f3ef7b5b559ac747fd0bc386b0d53e0940e57d33d6f676e",
            "D21_TEST_ROOT_PIN")
    signer = load_generator()
    manifest = encode_manifest(create_manifest("v5.13.11.66"))
    roles = release_roles(signer, 1, manifest)
    with tempfile.TemporaryDirectory(dir=runner,prefix="gf-d21-") as work_path:
        work = Path(work_path)
        check = work / "authenticated"
        check.mkdir()
        tag = verify_d0_tuf(exe, check, manifest, roles,
                            release_fixture("v5.13.11.66"), "5.13.11.65")
        require(tag == "v5.13.11.66", "D21_SIGNED_RELEASE_BINDING")
        outside = work/"outside-stage-sentinel.txt"
        outside.write_bytes(b"D21-OUTSIDE-UNCHANGED\n")

        def stage_directory():
            p = Path(tempfile.mkdtemp(dir=runner, prefix="gf-d2-"))
            s = p / "stage"
            s.mkdir()
            return p, s

        successes = []
        def perform(response, *, corrupt_after_write=None):
            parent, stage = stage_directory()
            try:
                source = StrictSimulatedHttpSource(response)
                out = acquire_and_stage(manifest, source, stage, runner, INERT_URL,
                                        after_write=corrupt_after_write)
                require(source.fetches == 1 and
                        out["bytes_verified"] == len(PAYLOAD) and
                        out["sha256"] == digest(PAYLOAD) and
                        out["staged_inert_only"] is True and
                        out["can_install"] is False and
                        out["operational_ready"] is False and
                        out["automatic_recovery_authorized"] is False,
                        "D21_STAGING_CONTRACT")
                check_bytes(stage / DESTINATION, len(PAYLOAD), digest(PAYLOAD))
                return out
            finally:
                shutil.rmtree(parent)

        for fragment in (1, 7, READ_BLOCK):
            perform(SimulatedResponse(fragment=fragment))
            successes.append(fragment)

        failed = []
        def deny(name, response, *, hook=None):
            try:
                perform(response, corrupt_after_write=hook)
            except (D1Rejected, OSError, ValueError):
                failed.append(name)
                return
            raise AssertionError("D21_FALSE_ACCEPT_" + name)

        H = (("Content-Type", "application/octet-stream"),
             ("Content-Length", str(len(PAYLOAD))))
        deny("redirect-301", SimulatedResponse(status=301))
        deny("redirect-302", SimulatedResponse(status=302))
        deny("redirect-307", SimulatedResponse(status=307))
        deny("redirect-308", SimulatedResponse(status=308))
        deny("status-206-partial", SimulatedResponse(status=206))
        deny("status-404", SimulatedResponse(status=404))
        deny("status-429", SimulatedResponse(status=429))
        deny("status-500", SimulatedResponse(status=500))
        deny("https-host-spoof", SimulatedResponse(url="https://evil.invalid/a"))
        deny("insecure-url", SimulatedResponse(url=INERT_URL.replace("https:","http:")))
        deny("wrong-tag-url", SimulatedResponse(url=INERT_URL.replace("v5.13.11.66","v5.13.11.67")))
        deny("redirect-history", SimulatedResponse(redirect_history=("https://evil.invalid/a",)))
        deny("missing-length", SimulatedResponse(headers=(H[0],)))
        deny("missing-type", SimulatedResponse(headers=(H[1],)))
        deny("duplicate-length", SimulatedResponse(headers=H+(H[1],)))
        deny("duplicate-length-case", SimulatedResponse(headers=H+(("content-length",str(len(PAYLOAD))),)))
        deny("conflicting-length", SimulatedResponse(headers=H+(("Content-Length","1"),)))
        deny("non-numeric-length", SimulatedResponse(headers=(H[0],("Content-Length","unknown"))))
        deny("negative-length", SimulatedResponse(headers=(H[0],("Content-Length","-1"))))
        deny("leading-zero-length", SimulatedResponse(headers=(H[0],("Content-Length","0"+str(len(PAYLOAD))))))
        deny("zero-length", SimulatedResponse(headers=(H[0],("Content-Length","0"))))
        deny("length-too-large", SimulatedResponse(headers=(H[0],("Content-Length",str(MAX_LAB_BYTES+1)))))
        deny("length-truncated", SimulatedResponse(headers=(H[0],("Content-Length",str(len(PAYLOAD)-1)))))
        deny("length-inflated", SimulatedResponse(headers=(H[0],("Content-Length",str(len(PAYLOAD)+1)))))
        deny("html-payload-type", SimulatedResponse(headers=(("Content-Type","text/html"),H[1])))
        deny("content-compressed", SimulatedResponse(headers=H+(("Content-Encoding","gzip"),)))
        deny("chunked-transfer", SimulatedResponse(headers=H+(("Transfer-Encoding","chunked"),)))
        deny("header-injection", SimulatedResponse(headers=(("Content-Type","application/octet-stream\r\nX-Evil: yes"),H[1])))
        deny("header-overbudget", SimulatedResponse(headers=H+tuple(("X-A"+str(i),"v") for i in range(MAX_HEADER_COUNT))))
        deny("stream-early-eof", SimulatedResponse(cut_at=len(PAYLOAD)-2))
        deny("stream-read-error", SimulatedResponse(fault_at=7, fragment=7))
        deny("body-overrun", SimulatedResponse(body=PAYLOAD+b"X"))
        deny("body-underflow", SimulatedResponse(body=PAYLOAD[:-1]))
        deny("same-size-body-tamper", SimulatedResponse(body=b"X"+PAYLOAD[1:]))
        deny("invalid-fragment-size", SimulatedResponse(fragment=READ_BLOCK+1))
        deny("body-not-bytes", SimulatedResponse(body="not-bytes"))
        def corrupt(path):
            path.write_bytes(b"X"*len(PAYLOAD))
        deny("post-write-file-tamper", SimulatedResponse(), hook=corrupt)

        require(len(successes) == 3 and
                len(failed) == 37 and len(set(failed)) == 37,
                "D21_MATRIX_COUNTS")
        require(outside.read_bytes() == b"D21-OUTSIDE-UNCHANGED\n",
                "D21_OUTSIDE_STAGE_CHANGED")
        result = {
            "schema": "gf-github-d21-inert-http-simulation/1",
            "status": "passed",
            "positive_inert_streams":len(successes),
            "negative_streams_rejected":len(failed),
            "d0_csharp_reused":True,
            "d1_tuf_test_manifest_verified":True,
            "d2_stage_writer_reused":True,
            "memory_only_http_simulation":True,
            "streaming_length_sha_checked":True,
            "outside_stage_sentinel_preserved":True,
            "actual_http_network_tested":False,
            "github_redirect_flow_operational":False,
            "production_trust_root":False,
            "same_user_toctou_resistance_proven":False,
            "full_history_rollback_resistance_proven":False,
            "real_network_requests":0,
            "real_executable_downloads":0,
            "installations":0,
            "product_modified":False,
            "operational_ready":False,
            "automatic_recovery_authorized":False
        }
        print("GF_D21_RESULT "+__import__("json").dumps(result,sort_keys=True))
    return 0

if __name__ == "__main__":
    require(len(sys.argv)==2,"D21_CLI_ARGS")
    raise SystemExit(run(sys.argv[1]))
