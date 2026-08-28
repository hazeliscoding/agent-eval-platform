# ADR 0007 — Static Dashboard SPA

## Status

Accepted — post-Phase-6 (the plan's Dashboard section).

## Context

The plan calls for a dashboard: pass/fail rate, scenario failures, tool usage, cost,
latency, security failures, historical trends. The six numbered phases shipped a platform
whose output is Markdown reports, JSON baselines, and structured comparison data — none of
which demo as well as a screen. For a portfolio, a visual artifact matters.

## Decision

**A static Angular SPA that renders JSON the platform already produces — no API, no
database.** The dashboard fetches a single `dataset.json` from `public/data/` and renders
it; there is no server. This honors the architecture the rest of the repo committed to
("no service to host") and keeps the dashboard trivially hostable (any static file server,
or GitHub Pages).

**The data contract is a C# DTO, emitted by the platform.**
`DashboardDatasetBuilder` projects a `ComparisonResult` (plus run history and a
caller-supplied timestamp) into a flat `DashboardDataset` — presentation-oriented, no
domain types leaking in, so the JSON is stable across internal refactors. Two producers
write it: `aep compare` for real (live-model) runs, and a deterministic generator for the
committed demo data. A round-trip unit test pins the schema so the two can't drift from
what the SPA reads.

**Committed demo data is deterministic, not live.** `tools/AgentEvalPlatform.SampleData`
(mirroring incident-control-plane's `DemoSeeder`) uses the scripted fake model to produce
the committed `dataset.json`. This is free, reproducible, and — crucially — can *show* the
interesting states a live run won't: the sample encodes a prompt regression where an
"aggressive" system prompt makes the agent obey an injected instruction, so the dashboard
demonstrates injection compliance and the regression side-by-side. (The live Haiku smoke
test in Phase 6 resisted every adversarial prompt — good for the model, boring for a demo.)

**No component-framework dependency.** The sibling console uses Angular Material for its
form-heavy UI; this dashboard is read-only, so it's built with hand-crafted SCSS over a
small set of CSS custom-property tokens (light and dark), and the one chart is inline SVG.
That keeps the dependency list to Angular itself — honoring "justify any new library" —
and gives exact control over the look. A deliberate, noted deviation from the plan's
"Angular + Material".

## Consequences

- New `src/aep-dashboard` (kebab-case, the idiomatic Angular project name) and
  `tools/AgentEvalPlatform.SampleData`. The dataset DTOs live in Application alongside the
  other reporting writers.
- "Historical trends" is the only view needing stored history; the demo synthesizes a
  short curve, and `aep compare` emits one point per configuration per run. Accumulating a
  real multi-run history file is a natural follow-up, left out to keep scope bounded.
- The dashboard is regenerated and re-screenshotted by running the generator then
  `npm run screenshots`; the committed `dataset.json` keeps it working with no key.
