# Autonomous task worker (`scripts/task_worker.py`)

Runs the backlog in `tasks.csv` unattended: picks the next eligible task, hands
it to [Hermes Agent](https://github.com/NousResearch/hermes-agent) backed by a
local Ollama model, verifies the result by actually building the project, and
commits only if the build passes.

It replaces the previous `autopilot-local` worker, which corrupted this repo.

## Why this one is different

The old worker asked the model whether it had finished, and its verification
command was `npm run build` — in a .NET repo with no root `package.json`. That
command could never pass, so verification silently degraded to "the model says
it's done." Result: ~60 tasks marked done that were never implemented, a
`tasks.csv` with unterminated quotes and misaligned columns, and ~17 junk
directories created from LLM markdown that got parsed as file paths.

The structural fix here is a separation of authority:

| Concern | Owned by |
| --- | --- |
| Writing code | the model |
| Deciding a task is done | **the script** (only if the build exits 0) |
| Writing `tasks.csv` | **the script** (via the `csv` module) |
| Committing to git | **the script** |

The model's opinion about its own success is never consulted. `tasks.csv` is
snapshotted before each model run and restored byte-for-byte afterwards, so
even if the model ignores its instructions and edits the file, the edit cannot
survive. Failing attempts are `git reset --hard`ed away, so a broken attempt
can never accumulate into the next one.

## Running it

Single task, in the foreground — do this first:

```bash
python3 scripts/task_worker.py --project /Users/mukesh/Projects/Nestly --once
```

The whole backlog, in the background:

```bash
nohup python3 scripts/task_worker.py --project /Users/mukesh/Projects/Nestly > /dev/null 2>&1 &
```

Watch it:

```bash
tail -f /Users/mukesh/Projects/Nestly/.hermes-worker.log
```

Status of every project:

```bash
python3 scripts/task_worker.py --status
```

Stop gracefully — it finishes the current task, then exits:

```bash
pkill -f "task_worker.py --project /Users/mukesh/Projects/Nestly"
```

Other useful flags: `--dry-run` (print the prompt for the next task and exit,
touching nothing) and `--max-tasks N` (stop after N attempts).

## Configuration

Per project, `.hermes-worker.json` in the repo root:

```json
{
  "name": "nestly",
  "tasks_csv": "tasks.csv",
  "branch": "autopilot-work",
  "verify": ["dotnet build Nestly.sln"],
  "env": { "DOTNET_ROOT": "~/.dotnet" },
  "path_prepend": ["~/.dotnet"],
  "max_attempts": 3,
  "task_timeout_sec": 900,
  "context_files": ["AGENTS.md"]
}
```

`verify` is the important one — it is the definition of "done". Every command
must exit 0 or the task is reverted. **The worker refuses to start if `verify`
is empty**, because unverified automation is exactly what broke this repo.

Global settings live in `~/.hermes/workers/config.json`:

```json
{
  "max_concurrent_models": 1,
  "min_free_ram_gb": 2.5,
  "hermes_bin": "/Users/mukesh/.hermes/venvs/hermes-dev/bin/hermes"
}
```

## Running several projects at once

One command sets up a new project:

```bash
python3 /Users/mukesh/Projects/Nestly/scripts/init_worker_project.py /path/to/other-repo
```

It detects the project type (dotnet/node/rust/go/python) from the files
present, proposes a `verify` command, shows you what it found, and asks for
confirmation before writing `.hermes-worker.json` (plus a starter
`tasks.csv`/`AGENTS.md` if the repo doesn't have them). It refuses to write a
config with an empty `verify` list rather than guess — an unverified worker
is exactly what corrupted this repo before.

**The proposed verify command is a starting point to check, not something to
trust blindly.** Run it by hand once before trusting the worker with it:

```bash
cd /path/to/other-repo && <the verify command it proposed>
```

Then start the worker:

```bash
cd /path/to/other-repo && python3 /Users/mukesh/Projects/Nestly/scripts/task_worker.py --project /path/to/other-repo --once
```

And once that single task looks right, the full backlog:

```bash
nohup python3 /Users/mukesh/Projects/Nestly/scripts/task_worker.py --project /path/to/other-repo > /dev/null 2>&1 &
```

Two locking layers make this safe:

- **Per-project lock** (`~/.hermes/workers/locks/project-<name>.lock`) — one
  worker per repo. A second worker on the same repo exits immediately rather
  than racing on the same git tree.
- **Global model slot** (`~/.hermes/workers/locks/model-slot-0.lock`) — only
  one model call runs at a time across *all* projects.

So project workers run concurrently, but they queue for the model. That is
deliberate: a 9-12B model is 6-8GB resident, and on a 16GB machine two
concurrent model calls would swap and thrash, making everything slower than
running them serially. While one project is inside the model, the others are
free to be building/verifying/committing — the parallelism is real, it just
isn't parallel *inference*.

Locks are reclaimed automatically if a worker is killed (the holder's PID is
checked for liveness), so a crash won't wedge the queue.

A RAM guard also pauses before each model call if free memory is below
`min_free_ram_gb`, and gives up on that cycle after 10 minutes rather than
hanging forever.

## Automatic decomposition

Complex tasks get slower and less reliable in a single agentic attempt — the
model has to explore several files, run real toolchain commands, maybe fix
compile errors, all while its own context keeps growing (and generation gets
slower as it grows). The worker detects this and splits a task into
sequential subtasks rather than pushing through a single, longer attempt.

Two detection paths, "detect first, decompose only if required":

- **Proactive** — before a task's *first* attempt, a cheap heuristic flags
  ones that read like bundled deliverables (3+ semicolon-separated clauses,
  or a description ≥22 words). Many rows in this backlog are written exactly
  that way ("Slot windows; day-of-week rules; holidays/blackouts; cutoffs;
  advance-days; capacity").
- **Reactive** — a task that's used up all but its last attempt gets one
  decomposition attempt as a last resort before it would otherwise be marked
  `blocked`, regardless of what the heuristic said. Repeated failure is
  itself evidence the task was too large for one shot.

The decomposition request itself is a single **text-only** model call — no
file/terminal tools, no implementation, capped at `decompose_timeout_sec`
(default 300s). Far cheaper than a full agentic attempt. If the response
doesn't parse into 2-`max_subtasks` clean lines, decomposition is simply
skipped and the task runs as originally written — a malformed response never
triggers a guessed decomposition.

When it does apply, the parent row (e.g. `9`) is replaced with lettered
subtask rows (`9a`, `9b`, `9c`, ...), the same convention this backlog already
uses for its hand-decomposed groups (`40` → `40a`...`40e`). Subtasks form a
dependency chain — `9b` depends on `9a`, `9c` on `9b` — the first inherits
the parent's original dependencies, and any other row that depended on `9`
gets rewritten to depend on `9c` instead (which transitively requires the
whole chain). This restructuring is committed on its own, separately from any
code change.

Declines safely (falls through to a normal attempt) on an id collision, or if
the parent id already carries a letter suffix — decomposing an
already-decomposed task would need a different id scheme, so it just declines
rather than guessing one. A task is only ever offered one decomposition
attempt (tracked in `.hermes-worker-decompose-tried.json`), so a task that
still struggles after being split doesn't get split again on every
subsequent failure.

Config knobs, in `.hermes-worker.json`:

```json
{
  "auto_decompose": true,
  "max_subtasks": 5,
  "decompose_timeout_sec": 300
}
```

## Task selection and retries

A task is eligible when its status is `todo`, every id in its `depends on #…`
notes is `done`, and it hasn't exhausted `max_attempts`. Rows are scanned in
file order, so the backlog runs roughly top to bottom.

Each retry tells the model what went wrong last time — the actual compiler
errors for a build failure, or "you described the work instead of writing
files" if it made no changes. Without this, retries re-send an identical
prompt and tend to reproduce an identical failure.

After `max_attempts` the task is marked `blocked` and the worker moves on.
Blocked tasks need a human or a stronger model.

## Files it writes

Committed: `tasks.csv` (status updates), plus whatever the model changed.

Gitignored runtime state:

| File | Contents |
| --- | --- |
| `.hermes-worker.log` | human-readable run log |
| `.hermes-worker-status.json` | current state, counts (drives `--status`) |
| `.hermes-worker-attempts.json` | attempt counter per task |
| `.hermes-worker-failures.jsonl` | full failure detail per attempt |

Failure detail goes to its own JSONL file and **never** into the CSV notes
column — the old worker stuffed multi-line compiler errors in there, which is
precisely what produced the unescaped commas and unterminated quotes.

## Expectations

Local models in this size class handle straightforward tasks (entities, value
objects, EF configurations) far better than complex ones (payment webhook
verification, booking orchestration with concurrency). Expect a real failure
rate and a growing `blocked` count on the harder half of the backlog.

That's a bounded, visible cost by design: every failure is reverted and
logged, nothing broken gets committed, and `--status` shows you exactly where
it stalled. Check in after a few hours rather than assuming a clean sweep.
