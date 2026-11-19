"""Local Git journal tooling. Remote updates are outside this program."""
import argparse
import base64
from dataclasses import dataclass, field
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile

PHASES = ("A", "B0", "B")
OWNER = 344032217
SOURCE_REPO = 1423533003
PRIVATE_REPO = 1437181649
NORM = "04e4b082c2c71913a516ba9050c6a0f070e1c8b6fcf005af990324b79f776948"
N_END = "2026-11-26T02:59:00Z"
ENTRY = ".ux525/state/event.json"
MAX_EVENTS = 512
MAX_JSON = 32_768
DONE_SCOPE = "ux526-fixture-done"
DONE_RESULTS = {"A": "fixture-output-verified", "B0": "fixture-copy-verified",
                "B": "fixture-archive-local-only"}


class Denied(ValueError):
    pass


def require(ok, reason):
    if not ok:
        raise Denied(reason)


def fields(obj, names):
    require(type(obj) is dict and set(obj) == set(names.split()), "unexpected fields")


def hexid(value, length=64):
    require(type(value) is str and re.fullmatch(rf"[a-f0-9]{{{length}}}", value)
            and value != "0" * length, "invalid digest")
    return value


def positive(value):
    require(type(value) is int and 0 < value <= 2**53 - 1, "invalid integer")
    return value


def digest(data):
    return hashlib.sha256(data).hexdigest()


def unique_pairs(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, "duplicate JSON field")
        result[key] = value
    return result


def decode(data):
    require(len(data) <= MAX_JSON, "JSON too large")
    try:
        return json.loads(data, object_pairs_hook=unique_pairs,
                          parse_constant=lambda _: require(False, "nonfinite JSON"))
    except (UnicodeError, RecursionError, ValueError) as exc:
        raise Denied("invalid JSON") from exc


def canonical(obj):
    return (json.dumps(obj, sort_keys=True, separators=(",", ":"), ensure_ascii=True,
                       allow_nan=False) + "\n").encode("ascii")


def utc(value):
    require(type(value) is str and re.fullmatch(r"\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ", value),
            "invalid UTC timestamp")
    try:
        return datetime.fromisoformat(value[:-1] + "+00:00")
    except ValueError as exc:
        raise Denied("invalid UTC timestamp") from exc


def now_utc():
    return datetime.now(timezone.utc).replace(microsecond=0)


def receipt_key(value):
    require(type(value) is str and re.fullmatch(r"AAAAC3NzaC1lZDI1NTE5AAAAI[A-Za-z0-9+/]{43}", value),
            "invalid Ed25519 receipt key")
    return value


def receipt_tool(*args, data=None):
    try:
        env = {key: value for key, value in os.environ.items() if not key.upper().startswith("SSH_")}
        return subprocess.run(["ssh-keygen", *map(str, args)], input=data,
                              stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=8,
                              check=True, env=env).stdout
    except (OSError, subprocess.SubprocessError) as error:
        raise Denied("receipt signing or verification failed") from error


def new_receipt_key(path):
    require(not path.exists(), "receipt key already exists")
    receipt_tool("-q", "-t", "ed25519", "-N", "", "-C", DONE_SCOPE, "-f", path)
    scheme, key, comment = path.with_suffix(".pub").read_text(encoding="ascii").split()
    require(scheme == "ssh-ed25519" and comment == DONE_SCOPE, "wrong receipt key format")
    return receipt_key(key)


def sign_done(event, path):
    proof = receipt_tool("-Y", "sign", "-n", DONE_SCOPE, "-f", path, data=canonical(event))
    return {**event, "signature": base64.b64encode(proof).decode("ascii")}


def verify_done(event, key):
    body = dict(event)
    signature = body.pop("signature")
    require(type(signature) is str and re.fullmatch(r"[A-Za-z0-9+/=]{1,1024}", signature),
            "invalid receipt signature format")
    try:
        proof = base64.b64decode(signature, validate=True)
    except ValueError as error:
        raise Denied("invalid receipt signature encoding") from error
    require(base64.b64encode(proof).decode("ascii") == signature, "noncanonical receipt signature")
    with tempfile.TemporaryDirectory() as directory:
        signer = Path(directory) / "signers"
        signer.write_bytes(f"{DONE_SCOPE} namespaces=\"{DONE_SCOPE}\" ssh-ed25519 {receipt_key(key)}\n".encode("ascii"))
        signed = Path(directory) / "signature"
        signed.write_bytes(proof)
        receipt_tool("-Y", "verify", "-n", DONE_SCOPE, "-I", DONE_SCOPE, "-f", signer,
                     "-s", signed, data=canonical(body))


def validate_binding(binding):
    fields(binding, "schema mode nonce_commitment owner source_repo source_sha source_tree "
           "controller_sha controller_tree private_repo workflow_id workflow_sha ref run job "
           "attempt trigger_actor norma inputs_digest n_end receipt_key")
    require(binding["schema"] == 2 and type(binding["schema"]) is int, "wrong schema")
    receipt_key(binding["receipt_key"])
    modes = {"fixture": "refs/heads/ux525/exec/qualificacao", "product": "refs/heads/ux525/exec/produto"}
    require(type(binding["mode"]) is str and modes.get(binding["mode"]) == binding["ref"],
            "invalid mode/ref")
    for key in ("source_sha", "source_tree", "controller_sha", "controller_tree", "workflow_sha"):
        hexid(binding[key], 40)
    for key in ("nonce_commitment", "inputs_digest"):
        hexid(binding[key])
    for key in ("owner", "source_repo", "private_repo", "workflow_id", "run", "job",
                "attempt", "trigger_actor"):
        positive(binding[key])
    require((binding["owner"], binding["source_repo"], binding["private_repo"],
             binding["attempt"], binding["trigger_actor"]) == (OWNER, SOURCE_REPO, PRIVATE_REPO, 1, OWNER),
            "wrong owner, repository or attempt")
    require(binding["norma"] == NORM and binding["n_end"] == N_END, "wrong norm identity")


def owner_receipt(bundle):
    fields(bundle, "binding decisions")
    binding, decisions = bundle["binding"], bundle["decisions"]
    validate_binding(binding)
    require(type(decisions) is list and len(decisions) == 3, "three distinct decisions required")
    ids = set()
    expiries = []
    for phase, decision in zip(PHASES, decisions):
        fields(decision, "phase decision_id start end purpose destination max_uses")
        require(decision["phase"] == phase and hexid(decision["decision_id"]) not in ids,
                "wrong phase or reused decision id")
        ids.add(decision["decision_id"])
        positive(decision["max_uses"])
        require(decision["max_uses"] == 1, "one use required")
        start, end = utc(decision["start"]), utc(decision["end"])
        require(start < end <= utc(N_END), "invalid decision window")
        purposes = {"A": "one-publish-local", "B0": "sealed-copy-and-read-only-verification",
                    "B": "one-private-delivery"}
        if binding["mode"] == "fixture":
            purposes = {key: "synthetic-" + value for key, value in purposes.items()}
            purposes["B"] = "synthetic-local-archive-only"
        require(decision["purpose"] == purposes[phase], "wrong purpose")
        dest = decision["destination"]
        if phase == "B" and binding["mode"] == "product":
            fields(dest, "repo readers ttl_days max_bytes")
            require(type(dest["readers"]) is list and dest["readers"] == [OWNER]
                    and all(type(v) is int for v in dest["readers"]), "B allows owner only")
            require((positive(dest["repo"]), positive(dest["ttl_days"])) == (PRIVATE_REPO, 1)
                    and 0 < positive(dest["max_bytes"]) <= 268435456, "wrong B destination")
        else:
            require(dest == {"scope": "same-windows-job"}, "wrong local destination")
        expiries.append({"phase": phase, "id": decision["decision_id"],
                         "decision": digest(canonical(decision)), "start": decision["start"],
                         "end": decision["end"]})
    operation = digest(b"UX526/operation/v2\0" + canonical(binding))
    approval = digest(b"UX526/approval/v2\0" + canonical(bundle))
    attempt = {key: binding[key] for key in ("private_repo", "workflow_sha", "run", "job", "attempt")}
    return {"operation": operation, "approval": approval, "grants": expiries,
            "attempt": digest(b"UX526/attempt/v2\0" + canonical(attempt)),
            "receipt_key": binding["receipt_key"], "review_comment": "UX526 APPROVE " + approval}


def owner_envelope(bundle):
    return {"exact_decisions": bundle, "public": owner_receipt(bundle)}


def validate_owner_envelope(envelope, expected_approval):
    fields(envelope, "exact_decisions public")
    expected = owner_envelope(envelope["exact_decisions"])
    require(expected["public"]["approval"] == hexid(expected_approval)
            and canonical(envelope["public"]) == canonical(expected["public"]), "owner envelope differs")
    return expected


@dataclass
class Operation:
    approval: str
    grants: dict
    review: tuple
    attempt: str
    receipt_key: str
    used: dict = field(default_factory=dict)
    done: dict = field(default_factory=dict)
    terminal: str | None = None


class State:
    def __init__(self):
        self.operations = {}
        self.ids = set()
        self.reviews = set()
        self.last = None

    def apply(self, event):
        require(type(event) is dict and type(event.get("type")) is str, "bad event")
        kind = event["type"]
        extra = {"REGISTER": "approval review grants attempt receipt_key", "USE": "phase id manifest",
                 "DONE": "phase id manifest attempt result evidence signature", "REVOKE": "", "CANCEL": "", "HOLD": ""}
        require(kind in extra, "unknown transition")
        fields(event, "type at operation " + extra[kind])
        when = utc(event["at"])
        require(when <= utc(N_END) and (self.last is None or self.last <= when),
                "time went backward or outside N")
        op_id = hexid(event["operation"])
        if kind == "REGISTER":
            self.register(event, op_id, when)
        else:
            require(op_id in self.operations, "unknown operation")
            op = self.operations[op_id]
            require(op.terminal is None, "operation is terminal")
            if kind in ("REVOKE", "CANCEL", "HOLD"):
                op.terminal = kind
            else:
                self.phase(event, op, when)
        self.last = when

    def register(self, event, op_id, when):
        require(op_id not in self.operations, "operation reuse")
        approval = hexid(event["approval"])
        review = event["review"]
        fields(review, "run environment workflow head")
        review_id = (positive(review["run"]), positive(review["environment"]),
                     positive(review["workflow"]), hexid(review["head"], 40))
        require(all(past[0] != review_id[0] for past in self.reviews), "native review reuse")
        entries = event["grants"]
        require(type(entries) is list and len(entries) == 3, "missing grants")
        local_ids = set()
        for phase, grant in zip(PHASES, entries):
            fields(grant, "phase id decision start end")
            ident = hexid(grant["id"])
            require(grant["phase"] == phase and ident not in self.ids | local_ids, "reused decision")
            hexid(grant["decision"])
            require(utc(grant["start"]) < utc(grant["end"]) <= utc(N_END)
                    and when < utc(grant["end"]), "expired registration")
            local_ids.add(ident)
        self.operations[op_id] = Operation(approval, dict(zip(PHASES, entries)), review_id,
                                          hexid(event["attempt"]), receipt_key(event["receipt_key"]))
        self.ids.update(local_ids)
        self.reviews.add(review_id)

    def phase(self, event, op, when):
        phase = event["phase"]
        require(type(phase) is str and phase in PHASES, "invalid phase")
        grant = op.grants[phase]
        require(grant["id"] == event["id"] and utc(grant["start"]) <= when < utc(grant["end"]),
                "wrong or expired grant")
        if event["type"] == "USE":
            require(phase not in op.used, "phase already consumed")
            preceding = PHASES[:PHASES.index(phase)]
            require(all(item in op.done for item in preceding), "previous phase incomplete")
            expected = op.done["A"]["manifest"] if preceding else None
            require(event["manifest"] == expected, "manifest differs")
            op.used[phase] = event.copy()
        else:
            require(phase in op.used and phase not in op.done, "completion without single use")
            require(event["attempt"] == op.attempt and event["result"] == DONE_RESULTS[phase],
                    "wrong completion attempt or result")
            hexid(event["evidence"])
            hexid(event["manifest"])
            require(phase == "A" or event["manifest"] == op.done["A"]["manifest"], "manifest differs")
            verify_done(event, op.receipt_key)
            op.done[phase] = event.copy()

    def bound_operation(self, bundle, when):
        require(bundle["binding"]["mode"] == "fixture", "only the synthetic qualification can be admitted")
        receipt = owner_receipt(bundle)
        op = self.operations.get(receipt["operation"])
        require(op is not None and op.approval == receipt["approval"]
                and list(op.grants.values()) == receipt["grants"] and op.attempt == receipt["attempt"]
                and op.receipt_key == receipt["receipt_key"], "approval, grants or receipt identity differ")
        require(self.last is not None and self.last <= when and when < utc(N_END), "invalid live clock")
        require(op.terminal is None, "operation blocked")
        return op

    def ready(self, bundle, phase, expected_manifest, when):
        op = self.bound_operation(bundle, when)
        require(phase in op.used and phase not in op.done,
                "phase not admitted or operation blocked")
        grant = op.grants[phase]
        require(utc(grant["start"]) <= when < utc(grant["end"]), "phase expired")
        require(op.used[phase]["manifest"] == expected_manifest, "admission digest differs")
        return op


class GitLedger:
    def __init__(self, path, genesis):
        self.path = path
        self.genesis = hexid(genesis, 40)

    def git(self, *args, data=None, env=None):
        try:
            return subprocess.run(["git", "--no-replace-objects", "-C", self.path, *args],
                                  input=data, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                  timeout=15, check=True, env=env).stdout
        except (OSError, subprocess.SubprocessError) as exc:
            raise Denied("Git operation failed") from exc

    def load(self, head):
        hexid(head, 40)
        require(self.git("ls-tree", "-z", self.genesis) == b"", "genesis must be empty")
        state = State()
        current = head
        events = []
        while current != self.genesis:
            require(len(events) < MAX_EVENTS, "journal exceeds reviewed limit")
            meta = self.git("rev-list", "--parents", "-n", "1", current).decode("ascii").split()
            require(len(meta) == 2 and meta[0] == current, "merge or unexpected ancestry")
            tree = current
            for mode, segment in zip((b"040000 tree", b"040000 tree", b"100644 blob"),
                                     ENTRY.encode().split(b"/"), strict=True):
                listing = self.git("ls-tree", "-z", tree)
                match = re.fullmatch(mode + rb" ([a-f0-9]{40})\t" + re.escape(segment) + rb"\x00", listing)
                require(match is not None, "unexpected journal tree")
                tree = match.group(1).decode("ascii")
            size = int(self.git("cat-file", "-s", tree))
            require(size <= MAX_JSON, "journal event too large")
            raw = self.git("cat-file", "blob", tree)
            event = decode(raw)
            require(raw == canonical(event), "noncanonical journal event")
            events.append(event)
            current = hexid(meta[1], 40)
        for event in reversed(events):
            state.apply(event)
        return state

    def candidate(self, head, event):
        self.load(head).apply(event)
        blob = self.git("hash-object", "-w", "--stdin", data=canonical(event)).strip()
        tree = self.git("mktree", data=b"100644 blob " + blob + b"\tevent.json\n").strip()
        for dirname in (b"state", b".ux525"):
            tree = self.git("mktree", data=b"040000 tree " + tree + b"\t" + dirname + b"\n").strip()
        result = self.git("-c", "user.name=UX525 Journal", "-c", "user.email=ux525@local.invalid",
                          "commit-tree", tree.decode(), "-p", head, "-m", "UX525 journal event")
        return hexid(result.decode().strip(), 40)


def main():
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest="action", required=True)
    approval = sub.add_parser("owner-view")
    approval.add_argument("envelope")
    approval.add_argument("--expect-approval", required=True)
    for name in ("validate", "candidate"):
        command = sub.add_parser(name)
        command.add_argument("repo")
        command.add_argument("genesis")
        command.add_argument("head")
        if name == "candidate":
            command.add_argument("event")
    args = parser.parse_args()
    if args.action == "owner-view":
        with open(args.envelope, "rb") as handle:
            envelope = decode(handle.read(MAX_JSON + 1))
        print(json.dumps(validate_owner_envelope(envelope, args.expect_approval), indent=2))
        return
    ledger = GitLedger(args.repo, args.genesis)
    if args.action == "candidate":
        with open(args.event, "rb") as handle:
            print(ledger.candidate(args.head, decode(handle.read(MAX_JSON + 1))))
    else:
        state = ledger.load(args.head)
        print(json.dumps({"valid": True, "operations": len(state.operations), "head": args.head}))


if __name__ == "__main__":
    try:
        main()
    except (Denied, OSError) as error:
        sys.exit(str(error))
