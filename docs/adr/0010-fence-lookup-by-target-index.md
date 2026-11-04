# ADR-0010: Find fences by target through an index on `FenceCreated` events

**Status:** Proposed
**Date:** 2026-11-04
**Deciders:** Piotr Tomaszewski

## Context

`LocationUpdatedHandler` receives a `LocationUpdated` (`user_id`, `latitude`, `longitude`) and has to evaluate every active fence whose `TargetId` is that user. For each fence it decides whether the new position moves the target inside or outside the boundary, and appends `FenceCrossed` or `FenceUncrossed` to the fence's stream when the state changes.

The event store ([ADR-0004](0004-mongodb-hand-rolled-event-sourcing.md)) can only load a stream by its id (`IEventStore.GetAsync(streamId)`). Nothing can answer "which fences target user X?". [`docs/architecture/overview.md`](../architecture/overview.md) lists this as an open question: whether `fence-service` needs its own materialized read model of fence definitions, or should query on demand.

Forces at play:

- **The handler loads every candidate aggregate anyway.** Deciding between `Cross()` and `Uncross()` needs the aggregate's current `IsCrossed` and version, for idempotency ([ADR-0009](0009-at-least-once-consumer-offset-store.md)) and optimistic concurrency. A lookup can only save the step of *finding* fence ids, not the step of *loading* them.
- **`TargetId` is immutable.** It is set in `FenceCreated`, and no later event changes it (`FenceModified` only carries name, radius and location). So the creation event alone answers "which fences were ever placed on user X?", and that answer never goes stale.
- **Small fan-out.** A person is the target of a handful of fences, not thousands.
- **Learning goal.** The project values exercising CQRS, but a read model that removes no work would be a pattern for its own sake. A real read-side need will come later, e.g. listing fences in the UI.

## Decision

`LocationUpdatedHandler` finds candidate fences **on demand, with an indexed query over `FenceCreated` events in the event store**, then loads each stream:

1. Query the events collection for `FenceCreated` events with `EventData.TargetId == userId` and project only `StreamId`.
2. Load each stream with `GetAsync` and rebuild the `Fence`. Skip fences where `IsDeleted` is true.
3. Evaluate the geometry against the aggregate's current `Location` and `RadiusInMeters`, then call `Cross()` / `Uncross()` only when the state actually changes.

Supporting changes to the event store:

- **A short, stable event-type field** (e.g. `"FenceCreated"`) stored next to the existing assembly-qualified `EventType`. The query filters on it, so a namespace or assembly rename doesn't silently break the lookup.
- **A partial index** on `EventData.TargetId`, with `partialFilterExpression` limited to `FenceCreated`, so other event types don't bloat it. The exact field path depends on how `BsonValueObjectConverter` serializes `TargetId` and must be checked against a real document.
- **A unique index on `(StreamId, Version)`.** [ADR-0004](0004-mongodb-hand-rolled-event-sourcing.md) promises optimistic concurrency via the version field, but the current code doesn't enforce it: there is no such index, and `EventStore.SaveAsync` does a plain insert. Without it, two concurrent handlers can append two `FenceCrossed` events with the same version. This index is needed whichever lookup is chosen.
- Both indexes are created at application start.

## Options Considered

### Option A: Indexed query on `FenceCreated` events (chosen)
**Pros:** No new collection, so there's nothing to keep in sync and no dual write. The lookup can't drift, because `TargetId` never changes after creation. Works on a standalone `mongod`, with no transactions or replica set. Very little code: one query, two indexes.
**Cons:** Deleted fences stay in the results forever, so every location update loads their streams too, only to discard them. The query reaches into `EventData`, which couples the lookup to how one event type is serialized. It doesn't exercise CQRS.

### Option B: Inline `fences_by_target` read model in the same MongoDB transaction
A collection (`FenceId`, `TargetId`, location, radius) that each fence command handler updates in the same multi-document transaction as the event append.
**Pros:** A textbook CQRS read model. Deleted fences disappear from the lookup. No drift, thanks to the transaction.
**Cons:** The handler still has to load every candidate aggregate, because `IsCrossed` can't safely live in the read model. So the read model saves only the lookup, which the index in Option A already makes cheap. It needs a replica set for transactions, a session threaded through the event store and a second repository, and a rollback test. That's a lot of moving parts for no measurable gain today.

### Option C: Inline read model without a transaction
**Pros:** Works on a standalone `mongod`.
**Cons:** A crash between the two writes leaves a fence that exists but is never evaluated, or the reverse. Repairing that needs versioned idempotent upserts and a rebuild tool.

### Option D: Asynchronous projector (change streams or outbox)
**Pros:** Full write/read decoupling. The projection can be replayed from scratch.
**Cons:** Eventual consistency: a fence created just before a location update may be missed. Needs a replica set or an outbox. The most code, and the hardest to test.

### Option E: Redis cache in front of the lookup
**Pros:** Faster reads.
**Cons:** It speeds up something that isn't a bottleneck, and it adds a second store with invalidation on create/modify/delete. Caching `IsCrossed` would be unsafe: a stale bit means double alerts or lost transitions. Redis fits more naturally in `alert-service` rate limiting.

## Consequences

- Easier: `LocationUpdatedHandler` can be built now on the existing event store and the existing standalone MongoDB. There is no projection to rebuild or keep consistent. Version conflicts surface as duplicate-key errors instead of silently producing two events with the same version.
- Harder: the lookup depends on the serialized shape of `FenceCreated`, so renaming `TargetId` or changing its serializer must update the query and the index. Every update also loads streams of deleted fences on that target, a cost that grows with fence churn per person.
- Revisit when: a read-side use case appears that needs fence definitions without loading aggregates (UI listing, another service), which is the natural point for Option B or D; or deleted fences pile up enough to make stream loading measurable on the hot path (consider Option B, or excluding deleted fences through a `FenceDeleted` lookup); or a transactional outbox lands, which brings transactions and a replica set for other reasons and makes Option B cheaper.

## Follow-ups

- **Crossing semantics:** whether entering the zone is `Cross` (and alerts) and leaving is `Uncross`, or the reverse. This is a domain decision for the event-storming BDRs.
- **`CrossFenceHandler`** calls `fence.Cross()` but never persists the event. Once `LocationUpdatedHandler` owns crossing detection, decide whether to remove it or fix it.
- **Permission re-check placement:** [`overview.md`](../architecture/overview.md) puts the per-update permission check in `permission-service`'s Privacy Filter, *before* `fence-service`, while `CLAUDE.md` describes `fence-service` re-checking at evaluation time. Settle where `IPermissionService.CanWatch` runs before implementing it in the handler.
