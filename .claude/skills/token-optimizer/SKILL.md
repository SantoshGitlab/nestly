---
name: token-optimizer
description: Context-usage discipline for this unattended worker -- delegating exploration, reading only what's needed, and skipping worker bookkeeping files. Use at the start of every task and whenever the session's context is growing large.
disable-model-invocation: false
---

Each task here runs in its own fresh, unattended session with no human to
notice if context balloons, so treat every read as a cost:

- Delegate broad "where/how" searches to the `explorer` subagent (via the
  Task tool) instead of chains of raw Read/Grep/Glob in this session --
  only its summary comes back, not everything it read.
- Before reading a file, check whether you already read it this session
  and whether it's changed since -- don't re-read it just to be safe.
- For files over ~300 lines, read the section you need with offset/limit
  rather than the whole file, unless you genuinely need all of it.
- Skip this project's own worker bookkeeping unless a task is specifically
  about the worker itself: `.task-completions/`, `.claude-auto-worker.log`,
  `.claude-token-usage`, `.claude-cost-total`, `.claude-session-id`,
  `.claude-task-attempts`, `dashboard/`. These are for the human/dashboard,
  not code context.
- Read `tasks.csv` for the current task row; you don't need to hold the
  full history of already-`done` rows in context to complete the next one.
- If a verification command (build/test/lint) produces long output, pipe it
  through `tail`/`grep` for the failure instead of letting the full log
  land in context.
