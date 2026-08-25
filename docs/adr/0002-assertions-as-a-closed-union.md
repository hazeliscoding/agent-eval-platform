# ADR 0002 — Assertions as a Closed Union over an AgentRun

## Status

Accepted — Phase 2.

## Context

Phase 2 makes scenarios judgeable. The plan's assertion list spans three kinds of
evidence: the tool transcript (`ToolCalled`, `ToolNotCalled`, `ToolCallCount`,
`NoUnauthorizedActions`), the agent's final output (`OutputContains`,
`OutputMatchesSchema`), and run metrics (`WorkflowReachedState`, `MaximumTokenUsage`,
`MaximumExecutionTime`). No runner exists yet, so the evidence needs a shape of its
own that runner phases can later produce.

## Decision

**`AgentRun` is the judgeable artifact.** It bundles the Phase 1 transcript with the
final output, the ordered workflow states, token usage, and duration. Assertions never
reach into a live agent or simulator — they read a completed, immutable run, which
keeps evaluation replayable and keeps the future runner's contract small: produce an
`AgentRun`, nothing more.

**Assertions are a closed union, mirroring `ScriptedResponse`.** Each plan assertion
is a record variant; the evaluator switches exhaustively, so a new assertion kind is
one variant plus one branch — never a stringly-typed rule engine. Every
`AssertionResult` message states observed facts (counts, reached-state paths, budget
vs. actual) so a failing report line is diagnosable without re-running anything. Two
deliberate strictness choices: `ToolCallCount` is exact, and `OutputContains` is
ordinal case-sensitive — precision beats forgiveness in evals, and looser variants can
be added as new union members if a scenario ever needs them.

**JSON Schema validation goes behind a port.** `OutputMatchesSchema` needs real JSON
Schema semantics, which are not worth hand-rolling. The domain defines
`ISchemaValidator` and stays dependency-free; the application implements it with
**JsonSchema.Net** (the json-everything library, the de-facto .NET implementation).
Both failure modes are first-class results, attributed to the right party: a broken
schema blames the scenario, non-JSON output blames the agent. Neither throws.

**The YAML `assertions` key becomes typed.** The loader parses snake_case `type`
discriminators exactly as the plan's example writes them (`tool_called`,
`tool_not_called`, …), requires exactly the fields each type needs, and rejects
unexpected keys — a typo like `tools:` must fail loudly rather than silently weaken a
scenario. Errors stay path-addressed (`assertions[2]`), all reported in one pass.

## Consequences

- Phase 1's "refusals are recorded as data" decision pays off here:
  `NoUnauthorizedActions` is a one-line read of `Transcript.Refusals`.
- The evaluator takes its schema validator by constructor; tests script it, production
  wires `JsonSchemaValidator`. No DI container needed yet.
- `AgentRun` metrics (tokens, duration, states) are supplied by whoever builds the
  run. When the runner phase arrives, it owns measuring them; assertions won't change.
