#!/usr/bin/env python3
"""
Bridges docs/tasks.csv (rich schema: ID,Phase,Module,Task,Description,Type,
Priority,Dependencies,Status) with the root tasks.csv that autopilot-local
consumes (id,task,status,priority,notes).

Numeric id N corresponds to docs row "T{N:03d}" -- IDs in docs/tasks.csv are
contiguous T001..T151 in row order, so no separate mapping file is needed.

push: docs/tasks.csv -> tasks.csv (regenerate the worker's view; run once
      before starting the worker, or again if docs/tasks.csv is hand-edited)
pull: tasks.csv -> docs/tasks.csv (write back what the worker completed;
      run after the worker has made progress)
"""
import csv
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DOCS_CSV = ROOT / "docs" / "tasks.csv"
ROOT_CSV = ROOT / "tasks.csv"

DOCS_TO_ROOT_STATUS = {"done": "done", "inprogress": "todo", "todo": "todo"}
ROOT_TO_DOCS_STATUS = {"done": "Done", "todo": "Todo", "blocked": "Blocked"}
PRIORITY_MAP = {"P0": "high", "P1": "high", "P2": "medium", "P3": "low"}


def clean(text):
    return re.sub(r"\s+", " ", text.replace(",", ";")).strip()


def numeric_id(task_id):
    return str(int(task_id.lstrip("T")))


def push():
    with DOCS_CSV.open(newline="") as f:
        rows = list(csv.DictReader(f))

    out_rows = []
    for row in rows:
        nid = numeric_id(row["ID"])
        task = clean(f"{row['Module']} - {row['Task']}: {row['Description']}")
        status = DOCS_TO_ROOT_STATUS[row["Status"].strip().lower()]
        priority = PRIORITY_MAP.get(row["Priority"].strip(), "medium")
        deps = [numeric_id(d.strip()) for d in row["Dependencies"].split(",") if d.strip()]
        notes = ("depends on " + " ".join(f"#{d}" for d in deps)) if deps else ""
        out_rows.append([nid, task, status, priority, notes])

    with ROOT_CSV.open("w", newline="") as f:
        w = csv.writer(f)
        w.writerow(["id", "task", "status", "priority", "notes"])
        w.writerows(out_rows)
    print(f"push: wrote {len(out_rows)} rows to {ROOT_CSV}")


def pull():
    with ROOT_CSV.open(newline="") as f:
        root_status = {row["id"]: row["status"].strip().lower() for row in csv.DictReader(f)}

    with DOCS_CSV.open(newline="") as f:
        rows = list(csv.DictReader(f))
        fieldnames = f.seek(0) or csv.DictReader(f).fieldnames

    changed = 0
    for row in rows:
        nid = numeric_id(row["ID"])
        new_status = ROOT_TO_DOCS_STATUS.get(root_status.get(nid, ""), None)
        if new_status and new_status != row["Status"]:
            row["Status"] = new_status
            changed += 1

    with DOCS_CSV.open("w", newline="") as f:
        w = csv.DictWriter(f, fieldnames=rows[0].keys())
        w.writeheader()
        w.writerows(rows)
    print(f"pull: updated {changed} row(s) in {DOCS_CSV}")


if __name__ == "__main__":
    if len(sys.argv) != 2 or sys.argv[1] not in ("push", "pull"):
        sys.exit(f"Usage: {sys.argv[0]} push|pull")
    {"push": push, "pull": pull}[sys.argv[1]]()
