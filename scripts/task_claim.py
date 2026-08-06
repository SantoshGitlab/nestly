#!/usr/bin/env python3
"""Cross-session task claiming for tasks.csv.

Several Claude/worker sessions can be pointed at this repo at the same time.
tasks.csv alone cannot express "someone is working on this right now" -- a row
is 'todo' both when it is free and when another session picked it up ten
minutes ago -- so this module adds the missing in-progress dimension as
out-of-band claim files and never invents a new CSV status value for it.

A claim is a file under .task-claims/<id>.claim written with O_EXCL, so exactly
one session can create it. It carries an owner label, a heartbeat timestamp and
optionally the owning session's pid. A claim is reclaimed when that pid is gone,
or -- since this script is invoked once per command and its own pid dies with
it -- when the heartbeat has not moved for --ttl minutes. Either way a session
killed mid-task never parks a task forever.

The claim files are local coordination state, not project history: they live
outside git and are safe to delete when no session is running.

Usage:
    task_claim.py next   [--owner LABEL] [--ttl 90]   # claim next free task
    task_claim.py claim  ID [--owner LABEL]           # claim one specific task
    task_claim.py beat   ID                           # refresh heartbeat
    task_claim.py done   ID [--note TEXT]             # status=done + release
    task_claim.py release ID [--note TEXT]            # give it back as todo
    task_claim.py status [--ttl 90]                   # what is open/claimed

Every subcommand prints one JSON object on stdout. Exit code 0 means the
command succeeded; `next` exits 3 when there is simply nothing left to claim.
"""

from __future__ import annotations

import argparse
import csv
import json
import os
import re
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from task_worker import FileLock, parse_deps  # noqa: E402  (reuse, do not fork)

PROJECT_DIR = Path(__file__).resolve().parent.parent
TASKS_CSV = PROJECT_DIR / "tasks.csv"
CLAIMS_DIR = PROJECT_DIR / ".task-claims"
CSV_LOCK = CLAIMS_DIR / "tasks-csv.lock"

# Rows whose id does not match this are summary/among-rows, never schedulable.
TASK_ID = re.compile(r"^[0-9]+[a-z0-9]*$")
OPEN_STATUSES = {"todo"}
DEFAULT_TTL_MIN = 90


def now() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds")


def read_rows() -> tuple[list[dict], list[str]]:
    """Rows plus the file's own header, so a rewrite cannot drop a column."""
    with TASKS_CSV.open(newline="") as f:
        reader = csv.DictReader(f)
        return list(reader), list(reader.fieldnames or [])


def write_rows(rows: list[dict], fields: list[str]) -> None:
    """Atomic, properly quoted, original columns preserved."""
    tmp = TASKS_CSV.with_suffix(".csv.tmp")
    with tmp.open("w", newline="") as f:
        w = csv.DictWriter(f, fieldnames=fields, extrasaction="ignore")
        w.writeheader()
        w.writerows(rows)
    tmp.replace(TASKS_CSV)


def status_of(row: dict) -> str:
    return (row.get("status") or "").strip().lower()


def schedulable(row: dict) -> bool:
    return bool(TASK_ID.match((row.get("id") or "").strip())) \
        and status_of(row) in OPEN_STATUSES


def claim_path(task_id: str) -> Path:
    return CLAIMS_DIR / f"{task_id}.claim"


def read_claim(path: Path) -> dict | None:
    try:
        return json.loads(path.read_text())
    except (OSError, ValueError):
        return None


def pid_alive(pid: int) -> bool:
    try:
        os.kill(pid, 0)
    except ProcessLookupError:
        return False
    except PermissionError:
        return True  # exists, owned by someone else
    return True


def claim_state(task_id: str, ttl_min: int) -> tuple[str, dict | None]:
    """-> ('free'|'held'|'stale', claim). 'stale' means safe to take over."""
    path = claim_path(task_id)
    if not path.exists():
        return "free", None
    claim = read_claim(path)
    if claim is None:
        return "stale", None  # unreadable/truncated: treat as abandoned
    pid = claim.get("pid")
    if isinstance(pid, int) and not pid_alive(pid):
        return "stale", claim
    age_min = (time.time() - claim.get("heartbeat_epoch", 0)) / 60
    if age_min > ttl_min:
        return "stale", claim
    return "held", claim


def write_claim(task_id: str, owner: str, task: str, pid: int | None) -> dict:
    """Create the claim with O_EXCL. Raises FileExistsError if someone won.

    `pid` is the *owning session's* pid, not this process's -- this script
    exits between commands, so recording its own pid would make every claim
    look dead the moment it was written.
    """
    CLAIMS_DIR.mkdir(parents=True, exist_ok=True)
    claim = {
        "id": task_id,
        "task": task,
        "owner": owner,
        "pid": pid,
        "host": os.uname().nodename,
        "claimed_at": now(),
        "heartbeat": now(),
        "heartbeat_epoch": time.time(),
    }
    fd = os.open(str(claim_path(task_id)),
                 os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o644)
    with os.fdopen(fd, "w") as f:
        json.dump(claim, f, indent=2)
    return claim


def deps_open(row: dict, by_id: dict[str, dict]) -> list[str]:
    """Dependency ids from notes that are not done yet."""
    return [d for d in parse_deps(row.get("notes") or "")
            if status_of(by_id.get(d, {})) not in ("done", "decomposed")]


def do_claim(task_id: str | None, owner: str, ttl_min: int,
             pid: int | None) -> int:
    """Claim a named task, or the first free one. CSV read fresh every try."""
    lock = FileLock(CSV_LOCK, label=owner)
    CLAIMS_DIR.mkdir(parents=True, exist_ok=True)
    if not lock.acquire(timeout=120, poll=2):
        print(json.dumps({"ok": False, "reason": "csv-lock-busy"}))
        return 4
    try:
        rows, _ = read_rows()
        by_id = {(r.get("id") or "").strip(): r for r in rows}

        if task_id is not None:
            row = by_id.get(task_id)
            if row is None:
                print(json.dumps({"ok": False, "reason": "no-such-task",
                                  "id": task_id}))
                return 3
            if not schedulable(row):
                # Already finished, or picked up and completed since we looked.
                print(json.dumps({"ok": False, "reason": "not-open",
                                  "id": task_id, "status": status_of(row)}))
                return 3
            candidates = [row]
        else:
            candidates = [r for r in rows if schedulable(r)]

        for row in candidates:
            rid = (row.get("id") or "").strip()
            blocked = deps_open(row, by_id)
            if blocked:
                continue
            state, held_by = claim_state(rid, ttl_min)
            if state == "held":
                if task_id is not None:
                    # Asked for this one by name: say who has it, don't just
                    # report the list as empty.
                    print(json.dumps({"ok": False, "reason": "held",
                                      "id": rid, "held_by": held_by}))
                    return 3
                continue
            if state == "stale":
                claim_path(rid).unlink(missing_ok=True)
            try:
                claim = write_claim(rid, owner, row.get("task") or "", pid)
            except FileExistsError:
                continue  # lost the race to a session not using the csv lock
            print(json.dumps({"ok": True, "claimed": claim,
                              "phase": row.get("phase", ""),
                              "notes": row.get("notes", ""),
                              "reclaimed_from": held_by}))
            return 0

        print(json.dumps({"ok": False, "reason": "nothing-claimable",
                          "open_tasks": len(candidates)}))
        return 3
    finally:
        lock.release()


def do_beat(task_id: str, owner: str | None) -> int:
    path = claim_path(task_id)
    claim = read_claim(path)
    if claim is None:
        print(json.dumps({"ok": False, "reason": "no-claim", "id": task_id}))
        return 3
    # Ownership is checked by label, not pid: the pid in the claim belongs to
    # the owning session, never to this short-lived process.
    if owner is not None and claim.get("owner") != owner:
        print(json.dumps({"ok": False, "reason": "not-owner", "id": task_id,
                          "owner": claim.get("owner"), "pid": claim.get("pid")}))
        return 5
    claim["heartbeat"] = now()
    claim["heartbeat_epoch"] = time.time()
    path.write_text(json.dumps(claim, indent=2))
    print(json.dumps({"ok": True, "id": task_id, "heartbeat": claim["heartbeat"]}))
    return 0


def do_finish(task_id: str, new_status: str, note: str | None) -> int:
    """done -> status=done; release -> leave it todo. Both drop the claim."""
    lock = FileLock(CSV_LOCK, label=f"finish-{task_id}")
    if not lock.acquire(timeout=120, poll=2):
        print(json.dumps({"ok": False, "reason": "csv-lock-busy"}))
        return 4
    try:
        rows, fields = read_rows()
        row = next((r for r in rows
                    if (r.get("id") or "").strip() == task_id), None)
        if row is None:
            print(json.dumps({"ok": False, "reason": "no-such-task",
                              "id": task_id}))
            return 3
        before = status_of(row)
        if new_status:
            row["status"] = new_status
        if note:
            existing = (row.get("notes") or "").strip()
            row["notes"] = f"{existing} | {note}" if existing else note
        write_rows(rows, fields)
        claim_path(task_id).unlink(missing_ok=True)
        print(json.dumps({"ok": True, "id": task_id, "was": before,
                          "now": status_of(row)}))
        return 0
    finally:
        lock.release()


def do_status(ttl_min: int) -> int:
    rows, _ = read_rows()
    by_id = {(r.get("id") or "").strip(): r for r in rows}
    open_rows = [r for r in rows if schedulable(r)]
    free, held, blocked = [], [], []
    for row in open_rows:
        rid = (row.get("id") or "").strip()
        if deps_open(row, by_id):
            blocked.append({"id": rid, "waiting_on": deps_open(row, by_id)})
            continue
        state, claim = claim_state(rid, ttl_min)
        entry = {"id": rid, "task": (row.get("task") or "")[:70],
                 "phase": row.get("phase", "")}
        if state == "held":
            entry["held_by"] = {k: claim.get(k)
                                for k in ("owner", "pid", "heartbeat")}
            held.append(entry)
        else:
            free.append(entry)
    orphans = []
    if CLAIMS_DIR.exists():
        for path in sorted(CLAIMS_DIR.glob("*.claim")):
            rid = path.stem
            if rid not in by_id or status_of(by_id[rid]) not in OPEN_STATUSES:
                orphans.append(rid)
    print(json.dumps({"ok": True, "free": free, "in_progress": held,
                      "blocked": blocked, "stale_claim_files": orphans,
                      "ttl_minutes": ttl_min}, indent=2))
    return 0


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)

    for name, help_text in (("next", "claim the next free task"),
                            ("claim", "claim one specific task id")):
        p = sub.add_parser(name, help=help_text)
        if name == "claim":
            p.add_argument("id")
        p.add_argument("--owner", default=f"anon-{os.getpid()}",
                       help="stable label for the claiming session")
        p.add_argument("--pid", type=int,
                       help="pid of the long-lived owning session, if any; "
                            "a dead pid makes the claim reclaimable early")
        p.add_argument("--ttl", type=int, default=DEFAULT_TTL_MIN,
                       help="minutes without a heartbeat before a claim is stale")

    p_beat = sub.add_parser("beat", help="refresh the heartbeat on a claim")
    p_beat.add_argument("id")
    p_beat.add_argument("--owner", help="refuse if the claim is not this owner")

    p_done = sub.add_parser("done", help="mark done and release")
    p_done.add_argument("id")
    p_done.add_argument("--note")

    p_rel = sub.add_parser("release", help="release without completing")
    p_rel.add_argument("id")
    p_rel.add_argument("--note")

    p_st = sub.add_parser("status", help="show free/in-progress/blocked")
    p_st.add_argument("--ttl", type=int, default=DEFAULT_TTL_MIN)

    a = ap.parse_args()
    if a.cmd == "next":
        return do_claim(None, a.owner, a.ttl, a.pid)
    if a.cmd == "claim":
        return do_claim(a.id, a.owner, a.ttl, a.pid)
    if a.cmd == "beat":
        return do_beat(a.id, a.owner)
    if a.cmd == "done":
        return do_finish(a.id, "done", a.note)
    if a.cmd == "release":
        return do_finish(a.id, "", a.note)
    return do_status(a.ttl)


if __name__ == "__main__":
    sys.exit(main())
