# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project status

No service code exists yet — the repository currently contains only documentation (`README.md`, `docs/`). There are no build, lint, or test commands to run. Update this file with real commands as soon as the first service is scaffolded (see `docs/adr/0002-dotnet-for-microservices.md` for the chosen stack).

## What this project is

WhereUAt is a location-sharing and geofence-alerting platform, built as a portfolio project whose explicit purpose is to demonstrate depth in **event-driven microservices, domain-driven design, and event sourcing** — not just to ship features. When making a technical choice here, prefer the option that best exercises those patterns over the option that's fastest to build.

Core rule of the domain: a user may only create a fence on someone who has granted them permission to see their location, and a fence crossing alerts the fence's *creator* (the watcher), not the person who crossed it.

## Domain model

Five bounded contexts, one aggregate each: **User**, **Permission**, **Location**, **Fence**, **Alert**. The reactive core is a policy chain: `Location Updated` → fence evaluation (re-checking permission at evaluation time, not on write) → `Fence Crossed` → `Alert Sent`. Full narrative and the business decisions behind these boundaries: `docs/domain/event-storming-summary.md`. Proposed service mapping and diagrams: `docs/architecture/overview.md`.

## Decisions made so far

Tracked as ADRs in `docs/adr/` (see `docs/adr/README.md` for the index) — read the relevant ADR before revisiting a settled decision:

- Services are built in .NET / C#.
- Apache Kafka is the event backbone for cross-service messaging.
- Fence and Alert use hand-rolled event sourcing on MongoDB.
- Cloud (Azure-leaning) and IaC tooling (Terraform vs. Kubernetes/Helm) are still open — don't treat either as decided.

## Do not write the implementation

This is a personal learning project — the point is for the human to write the service code themselves. **Do not write or edit application/service source code, even if asked to "just fix this" or "add this feature."** This applies once services exist, not just now while the repo is docs-only.

What's in bounds instead:
- Explaining concepts, patterns, and trade-offs (DDD, event sourcing, CQRS, Kafka, etc.) in the context of this codebase.
- Reviewing code the human wrote and pointing out issues verbally, without patching it.
- Small, isolated sample/reference snippets to illustrate an explanation — not wired into the actual project files.
- Reading code to answer questions ("why does X do Y", "where is Z handled").

Still in bounds as before: documentation (`README.md`, `docs/`, ADRs, `CLAUDE.md` itself) — those are collaborative by design and not part of "the implementation."

If a request is ambiguous about which side of this line it's on, ask rather than assume.

## Working conventions in this repo

- `docs/` is a living source of truth: update it in the same change as whatever makes it true, not as a follow-up pass.
- Any significant, hard-to-reverse technical decision (new service, storage choice, messaging pattern, deployment topology) gets an ADR — copy `docs/adr/template.md`, don't skip straight to implementation.
- ADRs are never edited after being marked Accepted; a changed decision is a new ADR that supersedes the old one.
- Content marked "Proposed" or "not yet decided" in the docs is genuinely open — don't build against it as if it were settled without flagging that it's still a choice.
- No roadmap file is kept in the repo (deliberately removed) — don't recreate one.
