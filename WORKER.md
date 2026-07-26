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

Add a `.hermes-worker.json` to the other repo with its own build command, then
start a second worker:

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
