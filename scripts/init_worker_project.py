#!/usr/bin/env python3
"""
One-command setup for pointing task_worker.py at a new project.

    python3 init_worker_project.py /path/to/other-repo

Auto-detects the project type from files present (dotnet/node/python/rust/go),
proposes a sensible `verify` command, writes `.hermes-worker.json`, and
scaffolds a starter `tasks.csv` + `AGENTS.md` if the project doesn't have them
yet. Always prints what it detected and asks for confirmation before writing
anything, since the verify command it guesses is a starting point to check,
not something to trust blindly - a wrong verify command is exactly what
corrupted the Nestly project's original tasks.csv before.

After this, start the worker with:
    python3 task_worker.py --project /path/to/other-repo --once
"""

import csv
import json
import sys
from pathlib import Path


def detect(project_dir: Path):
    """Returns (kind, verify_cmds, notes) - a best-guess, not a guarantee."""
    sln = list(project_dir.glob("*.sln"))
    if sln:
        cmd = f"dotnet build {sln[0].name}"
        return "dotnet", [cmd], (
            f"found {sln[0].name}. If there are test projects, add "
            f"'dotnet test' as a second verify command once you confirm "
            f"'{cmd}' passes cleanly on its own first."
        )

    csproj = list(project_dir.glob("*.csproj")) or list(project_dir.rglob("*.csproj"))
    if csproj:
        return "dotnet", ["dotnet build"], (
            "found .csproj but no .sln - 'dotnet build' will build whatever "
            "project is in the current directory. Consider pointing at a "
            "specific .sln or .csproj if there are multiple projects."
        )

    pkg = project_dir / "package.json"
    if pkg.exists():
        try:
            scripts = json.loads(pkg.read_text()).get("scripts", {})
        except Exception:
            scripts = {}
        cmds = []
        if "build" in scripts:
            cmds.append("npm run build")
        if "test" in scripts:
            cmds.append("npm test -- --run" if "vitest" in json.dumps(scripts)
                        else "npm test")
        if not cmds:
            return "node", [], (
                "found package.json but no build/test script defined in it. "
                "You must add a verify command by hand - this is exactly the "
                "gap that broke the previous automation on Nestly (it assumed "
                "'npm run build' existed in a repo that had no root "
                "package.json at all)."
            )
        return "node", cmds, f"found package.json with scripts: {list(scripts)}"

    if (project_dir / "Cargo.toml").exists():
        return "rust", ["cargo build", "cargo test"], "found Cargo.toml"

    if (project_dir / "go.mod").exists():
        return "go", ["go build ./...", "go test ./..."], "found go.mod"

    if (project_dir / "pyproject.toml").exists() or (project_dir / "setup.py").exists():
        has_pytest = (project_dir / "pytest.ini").exists() or \
            any(project_dir.rglob("test_*.py"))
        cmds = ["python3 -m compileall ."]
        if has_pytest:
            cmds.append("python3 -m pytest")
        return "python", cmds, (
            "found a Python project. 'compileall' only catches syntax errors "
            "- if this project has tests, pytest is included above; if it "
            "doesn't yet, consider that your verify command is weak until "
            "some exist."
        )

    return None, [], "could not detect a project type - you must write verify by hand."


def main():
    if len(sys.argv) != 2:
        print(f"usage: {sys.argv[0]} /path/to/project", file=sys.stderr)
        return 1
    project_dir = Path(sys.argv[1]).expanduser().resolve()
    if not project_dir.is_dir():
        print(f"not a directory: {project_dir}", file=sys.stderr)
        return 1
    if not (project_dir / ".git").exists():
        print(f"warning: {project_dir} doesn't look like a git repo "
              f"(no .git) - task_worker commits its work, so this needs to "
              f"be one.", file=sys.stderr)

    cfg_path = project_dir / ".hermes-worker.json"
    if cfg_path.exists():
        print(f"{cfg_path} already exists - not overwriting. "
              f"Edit it by hand if you need to change the verify command.")
        return 0

    kind, verify, note = detect(project_dir)
    print(f"Project: {project_dir}")
    print(f"Detected type: {kind or 'unknown'}")
    print(f"Note: {note}")
    print(f"Proposed verify command(s): {verify or '(none - you must add these)'}")
    print()

    if not verify:
        print("Refusing to write a config with an empty verify list - "
              "an unverified worker is exactly what corrupted the Nestly "
              "project before. Edit the script's detect() function or write "
              f"{cfg_path} by hand with a real verify command, then re-run.")
        return 2

    ans = input("Write .hermes-worker.json with this verify command? [y/N] ").strip().lower()
    if ans != "y":
        print("Not writing anything.")
        return 0

    cfg = {
        "name": project_dir.name,
        "tasks_csv": "tasks.csv",
        "branch": None,
        "verify": verify,
        "env": {},
        "path_prepend": [],
        "max_attempts": 3,
        "task_timeout_sec": 900,
        "context_files": ["AGENTS.md"],
    }
    cfg_path.write_text(json.dumps(cfg, indent=2) + "\n")
    print(f"wrote {cfg_path}")

    tasks_path = project_dir / "tasks.csv"
    if not tasks_path.exists():
        with tasks_path.open("w", newline="") as f:
            w = csv.writer(f)
            w.writerow(["id", "task", "status", "priority", "notes"])
            w.writerow(["1", "Describe your first task here", "todo", "high", ""])
        print(f"wrote a starter {tasks_path} - edit it to add your real backlog")

    agents_path = project_dir / "AGENTS.md"
    if not agents_path.exists():
        agents_path.write_text(
            f"# {project_dir.name} — agent workspace instructions\n\n"
            f"## Task workflow\n\n"
            f"The backlog lives in `tasks.csv` (columns: "
            f"id,task,status,priority,notes).\n\n"
            f"For each task: implement it matching this repo's existing "
            f"conventions, then verify with:\n"
            + "".join(f"  {c}\n" for c in verify) +
            f"\nDo not edit tasks.csv or run git commit yourself - that is "
            f"handled outside your session by task_worker.py.\n"
        )
        print(f"wrote a starter {agents_path} - edit it with real project "
              f"conventions before running the worker at scale")

    print()
    print("Next steps:")
    print(f"  1. Edit {tasks_path} with your real backlog "
          f"(or point tasks_csv at an existing one in .hermes-worker.json)")
    print(f"  2. Test one task: python3 task_worker.py --project "
          f"{project_dir} --once")
    print(f"  3. Then run the full backlog: nohup python3 task_worker.py "
          f"--project {project_dir} > /dev/null 2>&1 &")
    return 0


if __name__ == "__main__":
    sys.exit(main())
