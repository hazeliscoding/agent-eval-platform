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

.NET 10 · ASP.NET Core · PostgreSQL · EF Core · Angular · OpenTelemetry · xUnit + Testcontainers

## Deterministic tool simulator

The Phase 1 core: scenarios are YAML — initial state, allowed/forbidden tools, expected
diagnosis, and a script per tool (`success` / `timeout` / `malformed`, in call order).
The simulator replays scripts deterministically, refuses forbidden and unknown tools *as
data* (the attempt is recorded, the run continues — observing bad behavior is the point),
and captures every call in an append-only transcript that later assertion phases read.
Timeouts are reported, never slept, so whole suites run in milliseconds. A script that
runs dry fails loudly rather than repeating itself. See
[ADR 0001](docs/adr/0001-scenarios-as-data-and-deterministic-simulator.md).

```bash
dotnet test   # domain invariants, replay semantics, YAML loader fixtures
```

## Status 🚧

In progress — see [docs/PLAN.md](docs/PLAN.md) for the phased build plan.

- [x] Phase 1 — Deterministic tool simulator (YAML scenarios, scripted responses, refusal-recording transcript)
- [ ] Phase 2 — Assertions
- [ ] Phase 3 — Fault injection
- [ ] Phase 4 — Prompt injection testing
- [ ] Phase 5 — Model comparison
- [ ] Phase 6 — Regression testing in CI

## Running Locally

```bash
dotnet test
```

Phase 1 is a library plus its tests — nothing to compose yet. `docker compose up`
arrives with the first phase that adds persistence or a host.
