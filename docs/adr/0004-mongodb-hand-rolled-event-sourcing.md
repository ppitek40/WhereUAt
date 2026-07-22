# ADR-0004: Hand-rolled event sourcing on MongoDB for Fence and Alert

**Status:** Accepted
**Date:** 2026-07-22
**Deciders:** Piotr Tomaszewski

## Context

ADR-0003 committed to Kafka for event-driven messaging between services, but that's a separate question from what durably backs *event-sourced aggregate state* — the log an aggregate's current state is rebuilt from, for `fence-service` (crossing history) and `alert-service` (delivery history). `docs/roadmap.md` listed this as an open choice between Marten on Postgres, MongoDB, and EventStoreDB.

## Decision

Use **MongoDB**, with **hand-rolled** event sourcing: an append-only events collection per aggregate stream, optimistic concurrency via a version field, and projections built by hand rather than via a framework.

## Options Considered

### Option A: Marten on Postgres
**Pros:** Streams, snapshots, and projections come largely for free; less code to write and maintain.
**Cons:** Outsources exactly the mechanics this project exists to learn — using Marten well teaches its API surface, not how event sourcing actually works underneath.

### Option B: MongoDB, hand-rolled (chosen)
**Pros:** A document per event is a natural fit; writing the plumbing ourselves (versioning, optimistic concurrency, replay, snapshotting) is squarely the point of the "event sourcing" line on the CV; flexible schema suits varied event payloads across aggregates.
**Cons:** Every correctness concern (races on concurrent writes, non-idempotent replay) is ours to catch, not a library's; more upfront work before Phase 2 delivers a working feature.

### Option C: EventStoreDB
**Pros:** Purpose-built for event sourcing; strongest "used the real tool for the job" signal.
**Cons:** One more piece of infra to run and its own idiomatic API to learn — same problem as Option A (outsourcing the mechanics), just with a different sales pitch.

## Consequences

- Easier: full control over event schema and versioning, tailored exactly to `fence-service`/`alert-service`; every mechanic can be explained in depth because it was written by hand, not called from a library.
- Harder: optimistic concurrency, snapshotting, and replay all need to be designed and tested ourselves; the system now runs two database engines (MongoDB here, whatever `user-service`/`permission-service` end up using) rather than one — each service still owns exactly one store, so this doesn't create shared-database coupling, but it does add operational surface.
- Revisit when: the hand-rolled implementation's maintenance cost outweighs its learning value.
