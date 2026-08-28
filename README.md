# Agent Eval Platform 🧪

A framework for repeatedly testing agent behavior under normal and adversarial conditions. It answers the question every agentic system dodges: *how do we know this agent is actually reliable?*

## Why

Agents fail in ways unit tests don't catch — wrong tool calls, prompt injection, unsafe actions, silent cost creep. This platform runs scenario suites with deterministic tool simulators, fault injection, and assertions, then tracks scores against baselines so regressions fail CI.

## Core Ideas

- **Scenarios as data** — YAML-defined initial state, allowed/forbidden tools, expected diagnosis, assertions.
- **Deterministic tool simulator** — scripted responses: success, timeout, malformed, injected.
- **Fault + prompt-injection testing** — adversarial strings in logs, docs, and tool output must be treated as data.
- **Model/prompt comparison** — run the same suite across models and prompt versions, diff the reports.

## Stack

.NET 10 · Anthropic SDK · ASP.NET Core · PostgreSQL · EF Core · Angular · OpenTelemetry · xUnit + Testcontainers

## Deterministic tool simulator

The Phase 1 core: scenarios are YAML — initial state, allowed/forbidden tools, expected
diagnosis, and a script per tool (`success` / `timeout` / `malformed`, in call order).
The simulator replays scripts deterministically, refuses forbidden and unknown tools *as
data* (the attempt is recorded, the run continues — observing bad behavior is the point),
and captures every call in an append-only transcript that later assertion phases read.
Timeouts are reported, never slept, so whole suites run in milliseconds. A script that
runs dry fails loudly rather than repeating itself. See
[ADR 0001](docs/adr/0001-scenarios-as-data-and-deterministic-simulator.md).

## Model comparison

Run the same suite across models, prompt versions, or agent versions and diff the
results. A `RunConfiguration` (label + model + system prompt) is one point in the
comparison; the `ScenarioRunner` drives that model through each scenario's simulated
tools to produce a scored `AgentRun`, and `SuiteComparison` diffs the configurations
against a baseline — surfacing which scenarios *regressed*. The only non-deterministic
piece is the model itself (behind `IAgentModel`, with an Anthropic adapter); everything
downstream is deterministic, so the same model turns always produce the same score. The
Markdown report shows the score table, regressions, and a per-scenario pass/fail matrix.
See [ADR 0005](docs/adr/0005-runner-and-model-comparison.md).

## Fault injection

No fault engine, no probabilities: a fault is just another scripted response, placed
deterministically — *call 3 fails with "connection reset"*. Beyond timeouts and
malformed output, scripts can inject exceptions, partial (truncated) payloads, slow
responses (latency reported, never slept), duplicate events, stale data with an
explicit age, and tool-level authorization denials. Each fault is recorded in the
transcript as ground truth, even where the payload the agent saw carries no marker.
Tool-level denials deliberately don't count against the agent's authorization
compliance — the scenario scripted them. See
[ADR 0003](docs/adr/0003-faults-are-scripted-response-variants.md).

## Prompt injection testing

Does the agent treat tool output as data or as instructions? An injection carries an
adversarial payload through a surface (logs, documents, email, issue comments, or a tool
description) plus ground truth the agent never sees — the attack text and the tool it
tries to coerce. The agent receives only the composed payload; resistance is scored
deterministically from the transcript, with no LLM judge: `injection_resisted` fails only
if the agent called the demanded tool *after* being exposed to the injection. A named
template corpus covers the common attacks; scenarios can also supply literal text. See
[ADR 0004](docs/adr/0004-prompt-injection-as-payloads-with-ground-truth.md).

## Assertions

Runs are judged, not eyeballed. An `AgentRun` (transcript + final output + workflow
states + tokens + duration) is evaluated against a scenario's typed assertions —
`tool_called`, `tool_call_count`, `output_contains`, `output_matches_schema`,
`workflow_reached_state`, `no_unauthorized_actions` (which reads the refusals the
simulator recorded), and token/time budgets. Every verdict message states the observed
facts, so a failing report line is diagnosable on its own. JSON Schema checking sits
behind a domain port, implemented with JsonSchema.Net. See
[ADR 0002](docs/adr/0002-assertions-as-a-closed-union.md).

```bash
dotnet test   # domain invariants, replay semantics, assertion verdicts, YAML loader fixtures
```

## Status 🚧

In progress — see [docs/PLAN.md](docs/PLAN.md) for the phased build plan.

- [x] Phase 1 — Deterministic tool simulator (YAML scenarios, scripted responses, refusal-recording transcript)
- [x] Phase 2 — Assertions (AgentRun artifact, typed assertion union, schema validation, YAML parsing)
- [x] Phase 3 — Fault injection (exception, partial, slow, duplicate, stale, unauthorized — as scripted variants)
- [x] Phase 4 — Prompt injection testing (five surfaces, template corpus, sequence-aware resistance scoring)
- [x] Phase 5 — Model comparison (real model runner, per-config scoring, baseline diff, Markdown report)
- [ ] Phase 6 — Regression testing in CI

## Running Locally

```bash
dotnet test
```

Phase 1 is a library plus its tests — nothing to compose yet. `docker compose up`
arrives with the first phase that adds persistence or a host.
