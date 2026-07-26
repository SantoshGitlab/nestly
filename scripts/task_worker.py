#!/usr/bin/env python3
"""
Project-agnostic autonomous task worker driving Hermes Agent + a local LLM.

Design principle (the whole point of this file):
    THE SCRIPT owns verification, git commits, and every tasks.csv write.
    THE MODEL only writes code.

The previous automation on this repo let the model self-report "done", with a
verification command (`npm run build`) that could never pass on a .NET repo.
Result: ~60 tasks marked done that weren't, plus a corrupted CSV. Here the
model's opinion is never consulted: a task is marked done if and only if the
build/test commands this script runs actually exit 0, and any task whose
verification fails is git-reverted so a broken attempt can't accumulate.

Usage:
    task_worker.py --project /path/to/repo            # run until no work left
    task_worker.py --project /path/to/repo --once     # single task, then exit
    task_worker.py --project /path/to/repo --dry-run  # show the plan, do nothing
    task_worker.py --status                           # status of all projects

Per-project config lives at <project>/.hermes-worker.json (see DEFAULT_CONFIG).
Global config at ~/.hermes/workers/config.json.
"""

import argparse
import csv
import json
import os
import re
import signal
import subprocess
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

WORKERS_DIR = Path.home() / ".hermes" / "workers"
LOCKS_DIR = WORKERS_DIR / "locks"
GLOBAL_CONFIG = WORKERS_DIR / "config.json"

DEFAULT_GLOBAL = {
    # How many hermes/LLM calls may run at once across ALL projects.
    # A 12B model is ~8GB resident; on a 16GB machine 1 is the safe default.
    # Project workers still run concurrently, they just queue on the model.
    "max_concurrent_models": 1,
    "min_free_ram_gb": 2.5,
    "hermes_bin": str(Path.home() / ".hermes/venvs/hermes-dev/bin/hermes"),
}

DEFAULT_CONFIG = {
    "name": None,
    "tasks_csv": "tasks.csv",
    "verify": [],
    "env": {},
    "path_prepend": [],
    "max_attempts": 3,
    "task_timeout_sec": 900,
    "branch": None,
    "context_files": ["AGENTS.md"],
}


def now():
    return datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M:%S")


def log(project_dir, msg):
    line = f"{now()} {msg}"
    print(line, flush=True)
    with (Path(project_dir) / ".hermes-worker.log").open("a") as f:
        f.write(line + "\n")


def load_json(path, default):
    p = Path(path)
    if not p.exists():
        return dict(default)
    merged = dict(default)
    merged.update(json.loads(p.read_text()))
    return merged


def free_ram_gb():
    """Free + inactive (reclaimable) memory on macOS, in GB."""
    try:
        out = subprocess.run(["vm_stat"], capture_output=True, text=True,
                             timeout=10).stdout
        page = 4096
        m = re.search(r"page size of (\d+) bytes", out)
        if m:
            page = int(m.group(1))
        stats = {}
        for line in out.splitlines():
            mm = re.match(r'"?([\w\s]+?)"?:\s+(\d+)\.', line.strip())
            if mm:
                stats[mm.group(1).strip()] = int(mm.group(2))
        pages = stats.get("Pages free", 0) + stats.get("Pages inactive", 0)
        return pages * page / (1024 ** 3)
    except Exception:
        return 999.0  # never block on an unreadable reading


class FileLock:
    """Cooperative lock via O_EXCL. Stale locks (dead PID) are reclaimed."""

    def __init__(self, path, label=""):
        self.path = Path(path)
        self.label = label
        self.fd = None

    def _stale(self):
        try:
            pid = int(self.path.read_text().split()[0])
        except Exception:
            return True
        try:
            os.kill(pid, 0)
            return False
        except (OSError, ProcessLookupError):
            return True

    def acquire(self, timeout=None, poll=5):
        start = time.time()
        while True:
            try:
                self.fd = os.open(str(self.path),
                                  os.O_CREAT | os.O_EXCL | os.O_WRONLY)
                os.write(self.fd, f"{os.getpid()} {self.label} {now()}".encode())
                return True
            except FileExistsError:
                if self.path.exists() and self._stale():
                    try:
                        self.path.unlink()
                    except FileNotFoundError:
                        pass
                    continue
                if timeout is not None and time.time() - start > timeout:
                    return False
                time.sleep(poll)

    def release(self):
        if self.fd is not None:
            try:
                os.close(self.fd)
            except OSError:
                pass
            self.fd = None
        try:
            self.path.unlink()
        except FileNotFoundError:
            pass


# ---------------------------------------------------------------- tasks.csv

FIELDS = ["id", "task", "status", "priority", "notes"]


def read_tasks(csv_path):
    with open(csv_path, newline="") as f:
        return list(csv.DictReader(f))


def write_tasks(csv_path, rows):
    """Atomic, properly-quoted write. Never hand-format CSV."""
    tmp = Path(str(csv_path) + ".tmp")
    with tmp.open("w", newline="") as f:
        w = csv.DictWriter(f, fieldnames=FIELDS, extrasaction="ignore")
        w.writeheader()
        w.writerows(rows)
    tmp.replace(csv_path)


def parse_deps(notes):
    """'depends on #4 #37 | reset: ...' -> ['4','37'] (stops at the | marker)."""
    if not notes:
        return []
    head = notes.split("|")[0]
    if "depends on" not in head:
        return []
    return re.findall(r"#([0-9]+[a-z]?)", head)


def attempts_of(project_dir, task_id):
    f = Path(project_dir) / ".hermes-worker-attempts.json"
    if not f.exists():
        return 0
    try:
        return json.loads(f.read_text()).get(task_id, 0)
    except Exception:
        return 0


def bump_attempt(project_dir, task_id):
    f = Path(project_dir) / ".hermes-worker-attempts.json"
    data = {}
    if f.exists():
        try:
            data = json.loads(f.read_text())
        except Exception:
            data = {}
    data[task_id] = data.get(task_id, 0) + 1
    f.write_text(json.dumps(data, indent=2))
    return data[task_id]


def record_failure(project_dir, task_id, phase, detail):
    """Failures go to their own log file, NOT into the CSV notes column.

    autopilot-local stuffed multi-line compiler errors into notes, which is
    what produced the unescaped-comma / unterminated-quote corruption.
    """
    with (Path(project_dir) / ".hermes-worker-failures.jsonl").open("a") as fh:
        fh.write(json.dumps({
            "time": now(), "task": task_id, "phase": phase,
            "detail": detail[-4000:],
        }) + "\n")


def pick_task(rows, project_dir, max_attempts):
    """First todo row whose deps are all done and which hasn't exhausted retries."""
    done = {r["id"] for r in rows if r["status"].strip().lower() == "done"}
    for r in rows:
        if r["status"].strip().lower() != "todo":
            continue
        if attempts_of(project_dir, r["id"]) >= max_attempts:
            continue
        if all(d in done for d in parse_deps(r.get("notes", ""))):
            return r
    return None


# ------------------------------------------------------------------ running

def run(cmd, cwd, env, timeout):
    try:
        p = subprocess.run(cmd, cwd=cwd, env=env, timeout=timeout,
                           shell=isinstance(cmd, str),
                           capture_output=True, text=True)
        return p.returncode, (p.stdout or "") + (p.stderr or "")
    except subprocess.TimeoutExpired:
        return 124, f"TIMEOUT after {timeout}s"


def build_env(cfg):
    env = os.environ.copy()
    for k, v in (cfg.get("env") or {}).items():
        env[k] = os.path.expanduser(str(v))
    pre = [os.path.expanduser(p) for p in (cfg.get("path_prepend") or [])]
    if pre:
        env["PATH"] = ":".join(pre) + ":" + env.get("PATH", "")
    return env


def git(project_dir, *args):
    return run(["git", *args], project_dir, os.environ.copy(), 120)


def git_is_clean(project_dir):
    _, out = git(project_dir, "status", "--porcelain")
    return out.strip() == ""


def build_prompt(task, cfg, project_dir):
    ctx = [n for n in (cfg.get("context_files") or [])
           if (Path(project_dir) / n).exists()]
    ctx_line = (f"Project conventions are documented in: {', '.join(ctx)}. "
                f"Read them first.\n" if ctx else "")
    verify_line = "\n".join(f"  {c}" for c in cfg.get("verify", []))
    return f"""You are implementing ONE task in this repository. Work only in this repo.

TASK ID: {task['id']}
TASK: {task['task']}
NOTES: {task.get('notes', '')}

{ctx_line}
Rules:
- Implement this task properly, matching the existing code's conventions
  (namespaces, base types, file layout). Read neighbouring files first.
- Write real, compiling code. No placeholder comments like "// ... rest of
  the code", no stub bodies, no TODO-only files.
- DO NOT edit {cfg['tasks_csv']}. Task status is managed outside this session
  and your edits to it would be discarded.
- DO NOT run git commit / git push. Committing is handled outside this session.
- Your work will be verified by running:
{verify_line}
  If that does not pass, your changes are thrown away. So make it compile.

When you are done, briefly state which files you created or changed.
"""


def verify(project_dir, cfg, env):
    """Run every verify command. Returns (ok, combined_output)."""
    combined = []
    for cmd in cfg.get("verify", []):
        code, out = run(cmd, project_dir, env, cfg.get("task_timeout_sec", 900))
        combined.append(f"$ {cmd}\n{out}")
        if code != 0:
            return False, "\n".join(combined)
    return True, "\n".join(combined)


def write_status(project_dir, cfg, rows, current=None, state="running"):
    def count(s):
        return sum(1 for r in rows if r["status"].strip().lower() == s)
    status = {
        "project": cfg.get("name") or Path(project_dir).name,
        "path": str(project_dir), "state": state, "updated": now(),
        "current_task": current, "done": count("done"), "todo": count("todo"),
        "blocked": count("blocked"), "total": len(rows), "pid": os.getpid(),
    }
    (Path(project_dir) / ".hermes-worker-status.json").write_text(
        json.dumps(status, indent=2))
    return status


def do_one_task(project_dir, cfg, glob_cfg, env, dry_run=False):
    """Returns 'done' | 'failed' | 'empty'."""
    csv_path = Path(project_dir) / cfg["tasks_csv"]
    rows = read_tasks(csv_path)
    task = pick_task(rows, project_dir, cfg["max_attempts"])
    if not task:
        write_status(project_dir, cfg, rows, state="idle")
        return "empty"

    tid = task["id"]
    write_status(project_dir, cfg, rows, current=tid)
    log(project_dir, f"[{tid}] START  {task['task'][:110]}")

    if dry_run:
        log(project_dir,
            f"[{tid}] DRY-RUN prompt:\n{build_prompt(task, cfg, project_dir)}")
        return "empty"

    # Never start on a dirty tree - a leftover diff would get miscredited to
    # this task and committed.
    if not git_is_clean(project_dir):
        log(project_dir, f"[{tid}] tree dirty at start; resetting")
        git(project_dir, "reset", "--hard")
        git(project_dir, "clean", "-fd")

    _, base_sha = git(project_dir, "rev-parse", "HEAD")
    base_sha = base_sha.strip()

    # tasks.csv is restored verbatim after the model runs, so even if the model
    # ignores instructions and edits it, the change cannot survive.
    csv_backup = csv_path.read_bytes()

    waited = 0
    while free_ram_gb() < glob_cfg["min_free_ram_gb"]:
        if waited == 0:
            log(project_dir,
                f"[{tid}] waiting for RAM (<{glob_cfg['min_free_ram_gb']}GB free)")
        time.sleep(15)
        waited += 15
        if waited > 600:
            log(project_dir, f"[{tid}] RAM never freed; skipping this cycle")
            return "failed"

    sem = FileLock(LOCKS_DIR / "model-slot-0.lock",
                   label=f"{Path(project_dir).name}:{tid}")
    log(project_dir, f"[{tid}] waiting for model slot")
    sem.acquire()
    try:
        log(project_dir, f"[{tid}] running hermes")
        code, out = run(
            [glob_cfg["hermes_bin"], "--no-restore-cwd", "--yolo", "-z",
             build_prompt(task, cfg, project_dir)],
            project_dir, env, cfg["task_timeout_sec"])
    finally:
        sem.release()

    csv_path.write_bytes(csv_backup)  # model must not own task status

    if code == 124:
        n = bump_attempt(project_dir, tid)
        record_failure(project_dir, tid, "timeout", out)
        git(project_dir, "reset", "--hard", base_sha)
        git(project_dir, "clean", "-fd")
        log(project_dir,
            f"[{tid}] TIMEOUT (attempt {n}/{cfg['max_attempts']}) - reverted")
        return "failed"

    _, diff = git(project_dir, "status", "--porcelain")
    if not diff.strip():
        n = bump_attempt(project_dir, tid)
        record_failure(project_dir, tid, "no-changes", out)
        log(project_dir,
            f"[{tid}] NO CHANGES made (attempt {n}/{cfg['max_attempts']})")
        return "failed"

    log(project_dir, f"[{tid}] verifying")
    ok, vout = verify(project_dir, cfg, env)
    if not ok:
        n = bump_attempt(project_dir, tid)
        record_failure(project_dir, tid, "verify", vout)
        git(project_dir, "reset", "--hard", base_sha)
        git(project_dir, "clean", "-fd")
        log(project_dir,
            f"[{tid}] VERIFY FAILED (attempt {n}/{cfg['max_attempts']}) - reverted")
        return "failed"

    git(project_dir, "add", "-A")
    git(project_dir, "commit", "-m",
        f"task {tid}: {task['task'][:72]}\n\n"
        f"Implemented by Hermes Agent (local model) under task_worker.\n"
        f"Verified by: {'; '.join(cfg.get('verify', []))}\n")

    rows = read_tasks(csv_path)
    for r in rows:
        if r["id"] == tid:
            r["status"] = "done"
            base = (r.get("notes") or "").split("|")[0].strip()
            r["notes"] = (base + " | " if base else "") + \
                f"verified {now()} by task_worker"
    write_tasks(csv_path, rows)
    git(project_dir, "add", cfg["tasks_csv"])
    git(project_dir, "commit", "-m", f"task {tid}: mark done (verified)")

    log(project_dir, f"[{tid}] DONE + committed")
    write_status(project_dir, cfg, rows, current=None)
    return "done"


def mark_blocked(project_dir, cfg):
    """Tasks that burned all retries become 'blocked' so the picker moves on."""
    csv_path = Path(project_dir) / cfg["tasks_csv"]
    rows = read_tasks(csv_path)
    changed = 0
    for r in rows:
        if r["status"].strip().lower() == "todo" and \
                attempts_of(project_dir, r["id"]) >= cfg["max_attempts"]:
            r["status"] = "blocked"
            base = (r.get("notes") or "").split("|")[0].strip()
            r["notes"] = (base + " | " if base else "") + \
                f"blocked after {cfg['max_attempts']} attempts; " \
                f"see .hermes-worker-failures.jsonl"
            changed += 1
    if changed:
        write_tasks(csv_path, rows)
        git(project_dir, "add", cfg["tasks_csv"])
        git(project_dir, "commit", "-m",
            f"worker: mark {changed} task(s) blocked after max attempts")
    return changed


def run_project(project_dir, once=False, dry_run=False, max_tasks=None):
    project_dir = str(Path(project_dir).resolve())
    cfg = load_json(Path(project_dir) / ".hermes-worker.json", DEFAULT_CONFIG)
    glob_cfg = load_json(GLOBAL_CONFIG, DEFAULT_GLOBAL)
    if not cfg.get("verify"):
        print(f"ERROR: no 'verify' commands configured in "
              f"{project_dir}/.hermes-worker.json - refusing to run.\n"
              f"Unverified automation is what corrupted this repo before.",
              file=sys.stderr)
        return 2
    env = build_env(cfg)

    lock = FileLock(LOCKS_DIR / f"project-{Path(project_dir).name}.lock",
                    label=project_dir)
    if not lock.acquire(timeout=0):
        print(f"Another worker already owns {project_dir}; exiting.",
              file=sys.stderr)
        return 3

    stop = {"flag": False}

    def _sig(_s, _f):
        stop["flag"] = True
        log(project_dir, "signal received; finishing current task then stopping")

    signal.signal(signal.SIGINT, _sig)
    signal.signal(signal.SIGTERM, _sig)

    counts = {"done": 0, "failed": 0}
    try:
        if cfg.get("branch"):
            _, cur = git(project_dir, "branch", "--show-current")
            if cur.strip() != cfg["branch"]:
                log(project_dir, f"WARNING: on branch {cur.strip()}, "
                                 f"config expects {cfg['branch']}")
        while True:
            res = do_one_task(project_dir, cfg, glob_cfg, env, dry_run=dry_run)
            if res == "empty":
                if mark_blocked(project_dir, cfg):
                    continue
                log(project_dir, "no eligible tasks remain")
                break
            counts[res if res in counts else "failed"] += 1
            if once or stop["flag"]:
                break
            if max_tasks and sum(counts.values()) >= max_tasks:
                log(project_dir, f"reached --max-tasks {max_tasks}")
                break
        log(project_dir, f"worker stopping: {counts['done']} done, "
                         f"{counts['failed']} failed this run")
    finally:
        try:
            write_status(project_dir, cfg,
                         read_tasks(Path(project_dir) / cfg["tasks_csv"]),
                         state="stopped")
        except Exception:
            pass
        lock.release()
    return 0


def show_status():
    roots = []
    for lk in sorted(LOCKS_DIR.glob("project-*.lock")):
        try:
            roots.append(Path(lk.read_text().split()[1]))
        except Exception:
            pass
    projects_root = Path.home() / "Projects"
    if projects_root.exists():
        roots += [projects_root / d for d in os.listdir(projects_root)]
    seen = set()
    print(f"{'PROJECT':<20} {'STATE':<10} {'DONE':>5} {'TODO':>5} "
          f"{'BLOCKED':>8} {'CURRENT':<10}")
    print("-" * 64)
    any_row = False
    for r in roots:
        sf = r / ".hermes-worker-status.json"
        if not sf.exists() or str(r) in seen:
            continue
        seen.add(str(r))
        try:
            s = json.loads(sf.read_text())
        except Exception:
            continue
        any_row = True
        print(f"{s.get('project', r.name):<20} {s.get('state', '?'):<10} "
              f"{s.get('done', 0):>5} {s.get('todo', 0):>5} "
              f"{s.get('blocked', 0):>8} {str(s.get('current_task') or '-'):<10}")
    if not any_row:
        print("(no projects have been run yet)")


def main():
    LOCKS_DIR.mkdir(parents=True, exist_ok=True)
    ap = argparse.ArgumentParser(
        description="Hermes-driven autonomous task worker")
    ap.add_argument("--project")
    ap.add_argument("--once", action="store_true")
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--max-tasks", type=int)
    ap.add_argument("--status", action="store_true")
    a = ap.parse_args()
    if a.status:
        show_status()
        return 0
    if not a.project:
        ap.error("--project is required (or use --status)")
    return run_project(a.project, once=a.once, dry_run=a.dry_run,
                       max_tasks=a.max_tasks)


if __name__ == "__main__":
    sys.exit(main())
