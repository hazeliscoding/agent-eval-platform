# ADR 0005 — The Runner and Model Comparison

## Status

Accepted — Phase 5.

## Context

Phases 1–4 built the judgeable world (scenarios, simulator, assertions, injections) but
produced `AgentRun`s only by hand in tests. Phase 5 is the inflection point: to compare
models it must *drive a real model* against a scenario's simulated tools to produce those
runs, then diff runs across configurations. This is the first phase touching the model
provider and the live API — and it risks importing non-determinism into a platform whose
whole value is reproducibility.

## Decision

**The determinism boundary is the model, and only the model.** Tools stay deterministic
(the Phase 1 simulator), scoring stays deterministic (Phase 2 assertions). The one
variable piece is the agent's behavior — which is precisely what a comparison measures.
So `IAgentModel` is the single non-deterministic seam: `ScenarioRunner` offers the
scenario's allowed tools, routes every model tool call through a
`DeterministicToolSimulator`, feeds outcomes back, and assembles the `AgentRun`. Given a
fixed sequence of model turns, the run is fully determined — which is what lets a
`ScriptedAgentModel` test the runner and comparison end-to-end without a network call.

**The agent contract is copied from incident-control-plane verbatim.** `IAgentModel` +
the `AgentConversation` types (request/turn/tool-call/message) are the flagship's, so the
portfolio shares one agent abstraction. The only addition is per-turn token usage on
`AgentTurn`, carried through to `AgentRun` so cost can be scored.

**A configuration is the unit of comparison.** `RunConfiguration { Label, Model,
SystemPrompt, MaxTurns }` collapses the plan's three axes — model version, prompt
version, agent version — into one labelled thing. A comparison is a suite run under
several configurations; the first is the baseline, and regressions/improvements are
measured against it. That is the plan's "run the same suite across versions and diff the
reports," and it keeps the Phase 6 baseline-vs-stored-baseline distinction clean:
Phase 5 diffs configs against *each other*, Phase 6 will diff against a *stored* baseline.

**The Anthropic adapter leaves thinking unset.** A comparison tool drives an arbitrary
model matrix, and adaptive thinking 400s on models that don't support it (Haiku 4.5,
older). Since evals judge tool behavior rather than reasoning depth, the adapter omits
the thinking parameter — valid on every model — rather than hardcoding a per-model
capability table. (Discovered the hard way: the first live run 400'd on Haiku with
"adaptive thinking is not supported on this model.")

**"Duration" is simulated, not wall-clock.** `MaximumExecutionTime` needs a reproducible
number; real wall-clock is dominated by non-deterministic model latency. So a run's
duration is the sum of the latencies the *tools* reported (timeouts + slow responses) —
deterministic, and the thing an eval budget actually cares about.

## Consequences

- First infrastructure project in the repo (`AgentEvalPlatform.Infrastructure`, Anthropic
  SDK 12.42.0 — the flagship's version). The domain and application stay SDK-free.
- The live path is covered by one key-gated integration test (no-op without
  `ANTHROPIC_API_KEY`), mirroring the flagship's gating. Verified passing against a real
  Haiku 4.5 run.
- No console tool and no Docker yet: Phase 5 ships the comparison *library* plus the
  markdown report. A runnable CLI and any persistence belong with Phase 6 (regression
  baselines + CI gating), where they have a real home.
- `AgentRun` gained an input/output token split for cost; `ScenarioRunner` produces empty
  `ReachedStates` (this platform has no workflow state machine — `WorkflowReachedState`
  is for when an external workflow supplies its states).
