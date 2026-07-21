# ADR-0001: Record architecture decisions

**Status:** Accepted
**Date:** 2026-07-21
**Deciders:** Piotr Tomaszewski

## Context

WhereUAt is being built as a hands-on showcase of microservices, DDD, event-driven architecture, and event sourcing. A number of significant technical decisions (language, messaging backbone, cloud/IaC target, event store) will be made early and revisited as the system grows. Without a record, the reasoning behind those choices — which matters both for future-me and for explaining the project in interviews — gets lost.

## Decision

Adopt lightweight Architecture Decision Records (ADRs), stored in `docs/adr/`, using Michael Nygard's format (Context / Decision / Consequences). Each ADR is numbered sequentially (`0001-...`), immutable once **Accepted**. A changed decision is captured as a new ADR that supersedes the old one — the old one is kept for history and marked `Superseded by ADR-XXXX`.

## Options Considered

### Option A: Lightweight ADRs in-repo (chosen)
**Pros:** Versioned alongside the code that implements them; zero tooling required; easy to reference from PRs.
**Cons:** Manual discipline required to keep them current.

### Option B: No formal decision log
**Pros:** Less overhead.
**Cons:** Rationale for early, foundational choices (e.g. why .NET, why Kafka) would be lost or only live in memory — a real cost for a project whose purpose is partly to *demonstrate* decision-making.

## Consequences

- Easier: revisiting or challenging a past decision — the trade-offs are already written down.
- Harder: one more artifact to keep in sync with reality; ADRs must actually get written at decision time, not retrofitted.
- Revisit when: never — this is the meta-decision that governs how the others are recorded.
