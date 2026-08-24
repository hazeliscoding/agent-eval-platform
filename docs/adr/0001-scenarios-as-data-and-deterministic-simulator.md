# ADR 0001 — Scenarios as Data, Deterministic Tool Simulator

## Status

Accepted — Phase 1.

## Context

The platform's premise is that agent reliability must be measured by repeatedly running
the same situations. That requires two things: scenario definitions that non-engineers
can read and diff (which tools were allowed, what the world looked like, what the agent
should have concluded), and tool behavior that is bit-for-bit repeatable across runs —
an eval that flakes is worse than no eval.

## Decision

**Scenarios are YAML data, validated into a typed domain model.** The parser
(`ScenarioLoader`, YamlDotNet) owns structural validation and reports every problem in
one pass with the offending YAML path; domain invariants (a tool cannot be both allowed
and forbidden, scripts only for allowed tools) live in the `EvalScenario` constructor so
they hold no matter how a scenario is built. YamlDotNet is the de-facto standard .NET
YAML parser and the only new dependency this phase.

**Tool behavior is a replayed script, not a stub.** `DeterministicToolSimulator` gives
the Nth call to a tool the Nth scripted response (`success` / `timeout` / `malformed` in
Phase 1; Phase 3 extends the closed `ScriptedResponse` union). Three deliberate rules:

- **Timeouts are reported, never slept.** A suite of hundreds of scenarios must run in
  milliseconds; the agent under test sees a timeout result, the wall clock doesn't move.
- **Forbidden and unknown tools get refusals as data, not exceptions.** Observing an
  agent attempt an unauthorized action is the point of the eval — the attempt is
  recorded in the transcript and the agent gets a refusal, exactly as a real gateway
  would deny it.
- **Running past the end of a script fails loudly.** Repeating the last response would
  silently mask both under-scripted scenarios and agents that loop on a tool. The
  overflow call is recorded (`ScriptExhausted`), then `ScriptExhaustedException` aborts
  the run with the tool name and counts.

Every call — refused or served — lands in an append-only `ToolCallTranscript` with
sequence numbers and injected-clock timestamps. The transcript is the substrate Phase 2
assertions (`tool_called`, `tool_not_called`, call counts, unauthorized actions) will
evaluate; the YAML schema already tolerates an `assertions` key so scenarios can be
written forward.

## Consequences

- Scenario authoring needs no code changes or recompilation; CI can validate scenario
  files standalone.
- The simulator is synchronous and in-process. If a future phase evaluates agents over
  a wire protocol (MCP), an adapter wraps this simulator rather than replacing it.
- Scripts are per-tool sequences, not global interleavings. If a scenario ever needs
  "tool B answers differently after tool A was called", that's a new scripting concept
  and a new ADR — not a flag on this one.
