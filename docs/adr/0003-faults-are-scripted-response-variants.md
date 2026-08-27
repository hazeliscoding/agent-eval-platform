# ADR 0003 — Faults Are Scripted Response Variants

## Status

Accepted — Phase 3.

## Context

The plan lists `FaultInjection` among the core concepts and Phase 3 names eight fault
kinds: timeout, exception, invalid JSON, partial response, slow response, duplicate
event, stale data, unauthorized response. A common shape for this is a separate fault
"engine" layered over the tool stubs — intercept a call, roll a probability, corrupt
the response.

## Decision

**There is no separate injection mechanism.** Faults are variants of the closed
`ScriptedResponse` union that Phase 1 was designed to grow, positioned in a tool's
script exactly like successes. A scenario doesn't say "inject a 20% failure rate"; it
says *call 3 to GetQueueMetrics fails with 'connection reset'* — which keeps every run
bit-for-bit reproducible and every fault visible in the scenario diff. Probabilistic
chaos, if ever wanted, belongs in a scenario *generator*, not in the simulator.

Two Phase 1 kinds already covered timeout and invalid JSON (`malformed`). The six new
variants and their meanings:

| Variant | Meaning | YAML |
|---|---|---|
| `Exception` | Tool errors outright (the HTTP-500 of tools) | `- exception: <message>` |
| `Partial` | Truncated payload, connection died mid-body | `- partial: <prefix>` |
| `Slow` | Succeeds after a latency (reported, never slept) | `- slow: {latency, payload}` |
| `Duplicate` | A payload the agent has effectively seen before | `- duplicate: <payload>` |
| `Stale` | Out-of-date data with the age as ground truth | `- stale: {age, payload}` |
| `Unauthorized` | The *tool* denies the call ("token expired") | `- unauthorized: <message>` |

Each has a mirrored `ToolCallOutcome`, so transcripts record the injected fault as
ground truth even where the payload the agent saw carries no marker (duplicates and
stale data are only detectable through content — whether the agent notices is what the
scenario probes).

**Tool-level `Unauthorized` is not a policy refusal.** The simulator's
`RefusedForbidden`/`RefusedUnknown` mean the agent overstepped its allowed tool set;
`Unauthorized` means the tool itself denied an allowed call. The `NoUnauthorizedActions`
assertion counts only the former — an agent must not fail an authorization-compliance
check because the scenario scripted an expired token at it.

Duration parsing gained `m` and `h` units alongside `s`/`ms` — staleness is more
naturally written `10m` than `600s`.

## Consequences

- The simulator changed by six switch arms; no new abstractions, no configuration
  surface, nothing probabilistic to make runs diverge.
- The `slow`/`stale` YAML entries are the first nested-map response values; the script
  parser now handles scalar-or-map values and validates the exact key set.
- Whether an agent *handled* a fault well is judged by the existing Phase 2 assertions
  (diagnosis in output, no unsafe follow-up calls, budget adherence). Recovery-rate
  scoring across scenarios belongs to the metrics phase, not the simulator.
