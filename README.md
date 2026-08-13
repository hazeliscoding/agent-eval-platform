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

## Status 🚧

Planning — see [docs/PLAN.md](docs/PLAN.md) for the phased build plan.

- [ ] Phase 1 — Deterministic tool simulator
- [ ] Phase 2 — Assertions
- [ ] Phase 3 — Fault injection
- [ ] Phase 4 — Prompt injection testing
- [ ] Phase 5 — Model comparison
- [ ] Phase 6 — Regression testing in CI

## Running Locally

```bash
docker compose up
```

(Coming with Phase 1.)
