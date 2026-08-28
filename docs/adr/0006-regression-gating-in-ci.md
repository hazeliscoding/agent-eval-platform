# ADR 0006 — Regression Gating in CI

## Status

Accepted — Phase 6.

## Context

The finale turns the platform into a guardrail: record a suite's score as a baseline,
then fail CI when a later run regresses. The plan names four gates — success rate drops
past a threshold, unsafe actions increase, cost rises unexpectedly, latency exceeds
budget. This is also the phase that finally needs a runnable entry point and somewhere to
persist a baseline.

## Decision

**Baselines are version-controlled JSON, not a database.** A baseline is a committed file
(`JsonFileBaselineStore`, indented and stably ordered) so a score change surfaces in a
pull-request diff and is reviewed like any other change. This is how eval baselines work
in practice, and it's honest about the repo: there is still no service to run, so a
Postgres/`docker compose` story would be theater. The store sits behind an
`IBaselineStore` port; the application stays IO-free.

**The gate is pure and strict; the CLI picks operational defaults.** `RegressionGate`
compares two `Score`s under `RegressionThresholds` and reports one typed check per gate —
no model, no IO, so a given baseline/score/thresholds always yields the same verdict, and
`RegressionReport.Passed` is what the exit code derives from. Its defaults are strict
(zero tolerance everywhere). The **CLI** then chooses defaults suited to driving a *live*
model — and that split earned its keep the moment the first live `check` ran.

**Deterministic metrics gate strictly; noisy metrics get a tolerance band.** Success rate
and unsafe-action count are deterministic given the simulated tools, so they default to
zero tolerance — a safety regression must always fail. Cost, by contrast, wobbles
run-to-run because a live model's token counts vary; a 0% cost gate flapped on the very
first real check (`$0.0062` vs a `$0.0061` baseline). The plan's wording — cost increases
*unexpectedly* — implies a band, not equality, so the CLI defaults cost to +20% and
leaves it tunable per suite. Latency is only gated when a budget is set, and it gates the
*simulated* latency (ADR 0005), never wall-clock.

**The CI core is testable; the CLI is a shell.** `RegressionRunner` (run → record, or
run → gate) holds all the logic and is tested end-to-end with the fake model, including a
record-then-check that fails on a regressed prompt. `Program.cs` only parses args, loads
scenarios from a directory, wires the live Anthropic model, and maps the report to an
exit code (non-zero on regression — the actual gate). A `check` with no baseline is an
error, never a silent pass.

## Consequences

- New `tools/AgentEvalPlatform.Cli` (`aep record` / `aep check`) plus a committed
  `samples/incident-suite/` so the run commands in the README point at something real.
  Verified live: `record` then `check` on Haiku 4.5 scored 2/2 and exited 0.
- The Angular **dashboard** in the plan is not built — no UI in this repo. It's a separate
  aspirational item, not one of the six phase deliverables, and would be a project of its
  own.
- An observation from the live smoke test, not a defect: Haiku 4.5 resisted even a
  deliberately reckless, injection-following prompt and stayed in-bounds. That's the model
  being well-aligned; the gate's failure path is proven deterministically, where behavior
  is controlled.
