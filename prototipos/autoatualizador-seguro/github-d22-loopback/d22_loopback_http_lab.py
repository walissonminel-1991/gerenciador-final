"""D2.2: real HTTP/1.1 sockets restricted to numeric IPv4 loopback.

This is a PUBLIC-TEST-KEY, inert-bytes-only lab. No GitHub request,
installer, production trust, or product modification is authorized.
"""
from __future__ import annotations
import http.client
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import os
from pathlib import Path
import shutil
import socket
import sys
import tempfile
import threading
import time

HERE = Path(__file__).resolve().parent
for name in ("github-d2-inert", "github-crossprocess-lab",
             "github-trust-lab", "github-auth-lab"):
    sys.path.insert(0, str(HERE.parent / name))
from d2_inert_lab import (acquire_and_stage, verify_d0_tuf, check_bytes,
                          DESTINATION, MAX_LAB_BYTES, ALLOWED_URL_PREFIX)
from d12_crossprocess_lab import release_fixture
from d11_persistent_lab import release_roles
from d1_release_manifest_lab import (ANCHOR, ASSET, PAYLOAD, D1Rejected,
    create_manifest, digest, encode_manifest, load_generator, require)

TAG = "v5.13.11.66"
EXPECTED = ALLOWED_URL_PREFIX + TAG + "/" + ASSET
LOCAL = "127.0.0.1"
PATH = "/inert-only"
TIMEOUT = 0.6

def headers():
    return [("Content-Type","application/octet-stream"),
            ("Content-Length",str(len(PAYLOAD)))]

class InertHandler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"
    def log_message(self, *_):
        pass
    def do_GET(self):
        self.close_connection = True
        require(self.client_address[0] == LOCAL, "NON_LOOPBACK_PEER")
        server = self.server
        server.count += 1
        case = server.case
        if self.path != PATH:
            self.send_response_only(404)
            self.send_header("Content-Length", "0")
            self.end_headers()
            return
        try:
            self.send_response_only(case.get("status", 200))
            for k,v in case.get("headers",headers()):
                self.send_header(k,v)
            self.end_headers()
            delay=case.get("delay",0)
            if delay: time.sleep(delay)
            body=case.get("body", PAYLOAD)
            end=case.get("cut")
            if end is not None: body=body[:end]
            fragment=case.get("fragment",4096)
            for idx in range(0,len(body),fragment):
                self.wfile.write(body[idx:idx+fragment])
                self.wfile.flush()
            if end is not None:
                try: self.connection.shutdown(socket.SHUT_RDWR)
                except OSError: pass
        except (BrokenPipeError,ConnectionResetError,ConnectionAbortedError,OSError):
            pass

class LoopbackServer:
    def __enter__(self):
        self.http = ThreadingHTTPServer((LOCAL,0),InertHandler)
        self.http.daemon_threads = True
        self.http.count = 0
        self.http.case = {}
        require(self.http.server_address[0] == LOCAL and
                0 < self.http.server_port < 65536, "BIND_NOT_LOOPBACK")
        self.thread = threading.Thread(target=self.http.serve_forever,daemon=True)
        self.thread.start()
        return self
    def __exit__(self,*_):
        self.http.shutdown()
        self.http.server_close()
        self.thread.join(timeout=3)
        require(not self.thread.is_alive(),"SERVER_STILL_RUNNING")
    def set_case(self,case):
        require(type(case) is dict,"UNTRUSTED_TEST_CASE")
        self.http.case = case
    @property
    def count(self): return self.http.count
    @property
    def port(self): return self.http.server_port

class BoundedLoopbackHttpSource:
    def __init__(self,port):
        self.port=port
        self.calls=0
        self.connections=0
    def stream(self,expected_url):
        self.calls += 1
        require(self.calls==1 and expected_url==EXPECTED and
                type(self.port) is int and 0 < self.port < 65536,
                "SOURCE_IS_LOOPBACK_ONLY")
        # Literal numeric host and http.client, no proxy or redirect support.
        conn=http.client.HTTPConnection(LOCAL,self.port,timeout=TIMEOUT)
        try:
            conn.request("GET",PATH,headers={"Accept-Encoding":"identity",
                                            "Connection":"close"})
            self.connections += 1
            response=conn.getresponse()
            require(response.version == 11 and response.status == 200,
                    "STATUS_OR_REDIRECT_REJECTED")
            pairs=response.getheaders()
            require(1 < len(pairs) <= 8,"HTTP_HEADER_BUDGET")
            fields={}
            for k,v in pairs:
                require(type(k) is str and type(v) is str and k.isascii()
                        and k and all(ch.isalnum() or ch=="-" for ch in k)
                        and len(v) <= 256 and v==v.strip()
                        and not any(c in v for c in "\r\n\x00"),
                        "HTTP_HEADER_FORMAT")
                low=k.lower()
                require(low not in fields,"DUPLICATE_HEADER")
                fields[low]=v
            require(set(fields)=={"content-type","content-length"},
                    "UNEXPECTED_OR_MISSING_HEADERS")
            require(fields["content-type"]=="application/octet-stream",
                    "WRONG_MEDIA_TYPE")
            length=fields["content-length"]
            require(length.isascii() and length.isdecimal() and
                    length==str(int(length)) and
                    0 < int(length) <= MAX_LAB_BYTES and
                    int(length)==len(PAYLOAD),"UNSIGNED_HTTP_LENGTH")
            read=0
            while read < int(length):
                block=response.read(min(4096,int(length)-read))
                require(type(block) is bytes and len(block)>0,"INCOMPLETE_HTTP_BODY")
                read += len(block)
                require(read <= int(length),"HTTP_READ_OVERFLOW")
                yield block
            require(read == int(length),"HTTP_READ_INCOMPLETE")
        except (http.client.HTTPException,TimeoutError,socket.timeout) as exc:
            raise D1Rejected("HTTP_LOOPBACK_INTERRUPTED") from exc
        finally:
            conn.close()

def run(exe):
    require(sys.platform=="win32" and os.environ.get("GITHUB_ACTIONS")=="true"
            and os.environ.get("RUNNER_TEMP"),"WINDOWS_GITHUB_RUNNER_ONLY")
    runner=Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
    exe=Path(exe).resolve(strict=True)
    require(exe.is_relative_to(runner) and exe.name=="GF.D11.D0Probe.exe",
            "D0_EXPERIMENTAL_ONLY")
    require(digest(ANCHOR.read_bytes())==
            "a76278c5a75b80c11f3ef7b5b559ac747fd0bc386b0d53e0940e57d33d6f676e",
            "TEST_ROOT_PIN")
    g=load_generator()
    manifest=encode_manifest(create_manifest(TAG))
    roles=release_roles(g,1,manifest)
    with tempfile.TemporaryDirectory(dir=runner,prefix="gf-d22-") as lab:
        work=Path(lab)
        initial=work/"auth";initial.mkdir()
        tag=verify_d0_tuf(exe,initial,manifest,roles,
                          release_fixture(TAG),"5.13.11.65")
        require(tag==TAG,"D0_TUF_RELEASE_BINDING")
        sentinel=work/"personal-data-preserve.fixture.txt"
        sentinel.write_bytes(b"NO_USER_DATA_CHANGES\n")
        positive=[]
        negative=[]
        with LoopbackServer() as server:
            def test(name,fixture,expect=False,hook=None):
                directory=Path(tempfile.mkdtemp(dir=runner,prefix="gf-d2-"))
                stage=directory/"stage";stage.mkdir()
                server.set_case(fixture)
                before=server.count
                source=BoundedLoopbackHttpSource(server.port)
                try:
                    try:
                        result=acquire_and_stage(manifest,source,stage,runner,
                                                 EXPECTED,after_write=hook)
                    except (D1Rejected,OSError,ValueError,http.client.HTTPException):
                        require(not expect,"VALID_HTTP_REJECTED_"+name)
                        require(not (stage/DESTINATION).exists(),
                                "FAILED_HTTP_WROTE_FINAL_"+name)
                        negative.append(name)
                    else:
                        require(expect,"INVALID_HTTP_ACCEPTED_"+name)
                        require(result["staged_inert_only"] is True and
                                result["bytes_verified"]==len(PAYLOAD) and
                                result["sha256"]==digest(PAYLOAD) and
                                not result["can_install"] and
                                not result["operational_ready"] and
                                not result["automatic_recovery_authorized"],
                                "D2_INERT_ONLY")
                        check_bytes(stage/DESTINATION,len(PAYLOAD),digest(PAYLOAD))
                        positive.append(name)
                    require(server.count==before+1 and source.calls==1 and
                            source.connections==1,"EXACTLY_ONE_LOCAL_CONNECTION_"+name)
                finally:
                    shutil.rmtree(directory)
            for n in (1,7,4096):
                test("positive-"+str(n),{"fragment":n},expect=True)
            h=headers()
            h0,h1=h
            for code in (301,302,307,308):
                test("redirect-"+str(code),{"status":code,
                     "headers":h+[("Location","https://evil.invalid/redirect")]})
            for code in (206,404,429,500):
                test("status-"+str(code),{"status":code})
            test("missing-length",{"headers":[h0]})
            test("missing-type",{"headers":[h1]})
            test("duplicate-length",{"headers":h+[h1]})
            test("duplicate-length-case",{"headers":h+[("content-length",h1[1])]})
            test("conflicting-length",{"headers":h+[("Content-Length","1")]})
            test("short-length",{"headers":[h0,("Content-Length",str(len(PAYLOAD)-1))]})
            test("long-length",{"headers":[h0,("Content-Length",str(len(PAYLOAD)+1))]})
            test("zero-length",{"headers":[h0,("Content-Length","0")]})
            test("negative-length",{"headers":[h0,("Content-Length","-1")]})
            test("garbled-length",{"headers":[h0,("Content-Length","unknown")]})
            test("leading-zero-length",{"headers":[h0,("Content-Length","0"+h1[1])]})
            test("oversized-length",{"headers":[h0,("Content-Length",str(MAX_LAB_BYTES+1))]})
            test("duplicate-type",{"headers":h+[h0]})
            test("html-type",{"headers":[("Content-Type","text/html"),h1]})
            test("compressed-type",{"headers":h+[("Content-Encoding","gzip")]})
            test("chunked-transfer",{"headers":h+[("Transfer-Encoding","chunked")]})
            test("location-on-200",{"headers":h+[("Location","https://evil.invalid/a")]})
            test("unexpected-header",{"headers":h+[("X-Untrusted","x")]})
            test("truncated-body",{"body":PAYLOAD[:-1]})
            test("tampered-body",{"body":b"X"+PAYLOAD[1:]})
            test("socket-cut",{"cut":9,"fragment":3})
            test("socket-timeout",{"delay":1.0})
            def mutate(path):path.write_bytes(b"X"*len(PAYLOAD))
            test("post-write-tamper",{},hook=mutate)
            require(len(positive)==3 and len(negative)==31
                    and len(set(negative))==31 and server.count==34,
                    "D22_ADVERSARIAL_COUNTS")
            require(sentinel.read_bytes()==b"NO_USER_DATA_CHANGES\n",
                    "OUTSIDE_STAGING_SENTINEL")
            output={
                "schema":"gf-github-d22-loopback-http/1",
                "status":"passed",
                "positive_inert_http_transfers":len(positive),
                "negative_http_cases_rejected":len(negative),
                "loopback_requests":server.count,
                "transport":"tcp-ipv4-loopback-only",
                "http_library":"python-stdlib-http.client",
                "http_real_loopback_exercised":True,
                "d0_d1_test_manifest_verified":True,
                "d2_writer_reused":True,
                "outside_stage_sentinel_preserved":True,
                "external_http_requests":0,
                "github_redirect_support_proven":False,
                "tls_or_github_real_tested":False,
                "production_trust_root":False,
                "same_user_toctou_resistance_proven":False,
                "full_history_rollback_resistance_proven":False,
                "real_executable_downloads":0,
                "installations":0,"product_modified":False,
                "operational_ready":False,
                "automatic_recovery_authorized":False
            }
            print("GF_D22_RESULT "+json.dumps(output,sort_keys=True))
    return 0

if __name__=="__main__":
    require(len(sys.argv)==2,"D22_ARGS")
    raise SystemExit(run(sys.argv[1]))
