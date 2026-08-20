# PHASE-26-HANDOFF.md

Working state for **Phase 26 - Production Readiness** (`tasks.csv` rows
375-391), written 2026-08-21 for a session that starts cold.

Findings and evidence live in
[PRODUCTION-READINESS.md](PRODUCTION-READINESS.md). This file is only the
**execution plan**: which branches exist, what order they merge in, and the
rules that keep the merges clean. Read it before touching anything.

## 1. WHERE THINGS STAND

`main` is at `3faf0e2` (the Phase 26 filing commit), pushed to `origin/main`.

Five of the seventeen rows were started, each by its own agent in its own
worktree under `.claude/worktrees/`, each on its own branch:

| Row | Branch | Scope |
|---|---|---|
| 385 | `feature/385-e2e-in-ci` | Run the Playwright suites in CI; write provider-web's missing suite |
| 388 | `feature/388-security-scanning` | Dependabot, dependency/SAST/image scanning |
| 389 | `feature/389-bootstrap-readiness` | Bootstrap data + a loud signal for an unbookable database |
| 387 | `feature/387-load-harness` | Real load harness + recorded baseline |
| 386 | *not yet started* | Split `Catalog.Tests`; re-verify `CustomerManagement.Tests` |

**386 is deliberately last.** It rewrites `backend/tests/**` and `Nestly.sln`,
and 389 adds tests into that same tree — running them concurrently would
conflict on exactly the files 386 is moving. Start 386 only after 389 has
merged, so it splits the final state rather than a stale one.

## 2. MERGE ORDER

**388 -> 385 -> 387 -> 389**, then start 386.

The order is not arbitrary. It runs from least-entangled to most: 388 touches
only new files, 385 owns `ci.yml`, 387 owns a new top-level directory, 389
reaches into `backend/shared/**`. Merging in this order means each conflict, if
one appears, is against a smaller accumulated diff.

## 3. THE RULE THAT KEEPS MERGES CLEAN

Each agent was given a **strict file-ownership boundary**, and any new agent
must be given one too:

- 385: `.github/workflows/ci.yml`, `frontend/provider-web/**`
- 388: `.github/dependabot.yml` and **new** workflow files only — explicitly
  forbidden from editing `ci.yml`, because 385 owns it
- 389: `backend/shared/**`, `backend/*/Program.cs`, `database/seed/**`,
  `database/scripts/**`, tests under `backend/tests/**`
- 387: one new top-level directory (`load/` or similar) — explicitly forbidden
  from editing `Nestly.sln`, because 386 will restructure it
- 386: `backend/tests/**`, `Nestly.sln`

**No agent may edit `tasks.csv`.** Git conflicts that file as a whole blob
(very long lines) and it is this repo's known merge hazard — see the memory
note and `docs/LAUNCH-READINESS-AUDIT.md` §3.6 for what corruption there costs.
Rows are closed by the coordinating session in **one commit after the merges**,
not by the agents.

## 4. CLOSING A ROW — THE PROJECT'S OLDEST TRAP

`AGENTS.md` is blunt about this and it is worth repeating: **a task's `status`
column is not evidence.** ~24 rows were once falsely marked `done` by
automation that never ran a real build. Before setting any of 385-389 to
`done`:

1. `dotnet build Nestly.sln` — 0 errors, 0 warnings.
2. `dotnet test Nestly.sln` — **the baseline is 2073 passing, 0 failing** as of
   `7f07b4a`. Do not regress it. (386 will change how these are distributed
   across projects; the total must not drop.)
3. Read the agent's own report for what it says it did *not* verify, and carry
   that caveat into the row's `notes` verbatim rather than smoothing it over.

**Do not merge a branch whose agent reported honest non-verification, and do
not merge a branch that is still mid-flight.** Leave it and say so. A branch
left unmerged with a clear reason is a good outcome; a branch merged on
optimism is how this backlog broke the first time.

When editing `tasks.csv`: a `csv.DictReader` -> `DictWriter` round-trip is
byte-identical **only with `lineterminator="\n"`** — the csv module defaults to
`\r\n` and will otherwise rewrite all 718 lines. Take the CSV lock
(`.task-claims/tasks-csv.lock`, via `FileLock` in `scripts/task_worker.py`)
before writing. Dependencies are read **only** from the first `|`-delimited
segment of `notes` (`parse_deps`), so a "depends on #N" written anywhere else
is invisible to `scripts/task_claim.py`.

Also `git fetch` and check `origin/main`'s `tasks.csv` before appending any new
row — concurrent sessions publish rows, and ids collide here for real.

## 5. WHAT REMAINS AFTER THESE FIVE

Twelve rows, none started: 375, 376, 377, 378, 379, 380, 381, 382, 383, 384,
390, 391. Eight are gated behind 376 or 378 — `scripts/task_claim.py status`
reports the live picture.

The two that gate a launch are **375** (the payment gateway is a fake
registered in every environment) and **376** (SMS/email is a no-op, so OTP
login silently fails in production). Neither is blocked by code — both are
blocked by vendor onboarding and, for SMS, DLT template registration. Those
have multi-week lead times and should be started by a human regardless of when
the code is written.
