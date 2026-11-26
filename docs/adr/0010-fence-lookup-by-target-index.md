# ADR-0010: Find fences by target through an index on `FenceCreated` events

**Status:** Proposed
**Date:** 2026-11-04 (revised 2026-11-25)
**Deciders:** Piotr Tomaszewski

## Context

`LocationUpdatedHandler` receives a `LocationUpdated` (`user_id`, `latitude`, `longitude`, `occurred_at`) and has to evaluate every active fence whose `TargetId` is that user. For each fence, `FenceEvaluateHandler` loads the aggregate and calls `Fence.Evaluate`. That method compares the new position with the fence's current `TargetPosition` (`Unknown`, `Inside`, `Outside`) and appends one of three events when the position changes:

- `FenceTargetPositionEstablished`: the first reading after the fence was created or modified. It records where the target is and raises no alert.
- `FenceEntered`: the target moved from `Outside` to `Inside`.
- `FenceExited`: the target moved from `Inside` to `Outside`.

The event store ([ADR-0004](0004-mongodb-hand-rolled-event-sourcing.md)) can only load a stream by its id (`IEventStore.GetAsync(streamId)`). Nothing can answer "which fences target user X?". [`docs/architecture/overview.md`](../architecture/overview.md) lists this as an open question: whether `fence-service` needs its own materialized read model of fence definitions, or should query on demand.

Forces at play:

- **The handler loads every candidate aggregate anyway.** `Evaluate` needs the aggregate's current `TargetPosition`, its `LastEvaluatedAt` and its stream version. These are needed for idempotency under at-least-once delivery ([ADR-0009](0009-at-least-once-consumer-offset-store.md)) and for optimistic concurrency. A lookup can only save the step of *finding* fence ids, not the step of *loading* them.
- **`TargetId` and `CreatorId` are immutable.** Both are set in `FenceCreated`, and no later event changes them (`FenceModified` only carries name, radius and location). So the creation event alone answers "which fences were ever placed on user X, and by whom?", and that answer never goes stale.
- **Small fan-out.** A person is the target of a handful of fences, not thousands.
- **Learning goal.** The project values exercising CQRS, but a read model that removes no work would be a pattern for its own sake. A real read-side need will come later, e.g. listing fences in the UI.

## Decision

`LocationUpdatedHandler` finds candidate fences **on demand, with an indexed query over `FenceCreated` events in the event store**, and then evaluates each one:

1. `IEventStore.GetFences(userId)` queries the events collection for `FenceCreated` events with `EventData.TargetId == userId`. It returns `FenceInfo(FenceId, CreatorId)` for each match. Including `CreatorId` lets the permission check run before any stream is loaded.
2. For each fence, the handler re-checks permission with `IPermissionService.CanWatch(creatorId, targetId)` and skips the fence if the check fails. The check runs at evaluation time, not only when the fence is created, so a revoked permission stops alerts straight away.
3. `FenceEvaluateHandler` loads the stream with `GetAsync` and rebuilds the `Fence`. `Fence.Load` returns a failure `Result` for an empty stream instead of throwing.
4. `Fence.Evaluate(location, occurredAt)` returns no event when any of these is true:
   - the fence is deleted;
   - the reading is older than `LastEvaluatedAt`;
   - the target's position hasn't changed.

   Otherwise it returns one of the three events above, which is appended at `lastVersion + 1`.

Supporting changes to the event store:

- **A short, stable event-type field** (e.g. `"FenceCreated"`) stored next to the existing assembly-qualified `EventType`. The query filters on it, so a namespace or assembly rename doesn't silently break the lookup. *Not done yet:* `GetFences` currently filters on the assembly-qualified name.
- **A partial index** on `EventData.TargetId`, with `partialFilterExpression` limited to `FenceCreated`, so other event types don't bloat it. The field path is `EventData.TargetId`, stored as a `Guid` by the registered value-object serializer. *Not done yet.*
- **A unique index on `(StreamId, Version)`**, with `SaveAsync` turning a duplicate-key error into a failure `Result`. [ADR-0004](0004-mongodb-hand-rolled-event-sourcing.md) promises optimistic concurrency via the version field, but today `SaveAsync` does a plain insert and always succeeds. Without the index, a gRPC `Modify` racing an evaluation can append two events with the same version. This index is needed whichever lookup is chosen. *Not done yet.*
- All indexes are created at application start.

### Idempotency and ordering

Kafka delivery is at-least-once ([ADR-0009](0009-at-least-once-consumer-offset-store.md)), so the same `LocationUpdated` can arrive twice. After a crash, a batch of older messages can also be redelivered after newer ones were already handled. Both cases are handled inside the aggregate, not with a separate dedup table:

- **Redelivery of the same message:** the first delivery already moved `TargetPosition`, so the second one finds no change and produces no event.
- **Stale messages:** `FenceTargetPositionEstablished`, `FenceEntered` and `FenceExited` carry the reading's `OccurredAt`, which sets `LastEvaluatedAt`. `Evaluate` ignores any reading older than that. This prevents a replayed batch from producing a duplicate `Entered`/`Exited` pair, which would mean duplicate alerts for the watcher.

## Options Considered

### Option A: Indexed query on `FenceCreated` events (chosen)
**Pros:** No new collection, so there's nothing to keep in sync and no dual write. The lookup can't drift, because `TargetId` and `CreatorId` never change after creation. Works on a standalone `mongod`, with no transactions or replica set. Very little code: one query, two indexes.
**Cons:** Deleted fences stay in the results forever, so every location update loads their streams too, only to discard them. The query reaches into `EventData`, which couples the lookup to how one event type is serialized. It doesn't exercise CQRS.

### Option B: Inline `fences_by_target` read model in the same MongoDB transaction
A collection (`FenceId`, `TargetId`, `CreatorId`, location, radius) that each fence command handler updates in the same multi-document transaction as the event append.
**Pros:** A textbook CQRS read model. Deleted fences disappear from the lookup. No drift, thanks to the transaction.
**Cons:** The handler still has to load every candidate aggregate, because `TargetPosition` and `LastEvaluatedAt` can't safely live in the read model. So the read model saves only the lookup, which the index in Option A already makes cheap. It needs a replica set for transactions, a session threaded through the event store and a second repository, and a rollback test. That's a lot of moving parts for no measurable gain today.

### Option C: Inline read model without a transaction
**Pros:** Works on a standalone `mongod`.
**Cons:** A crash between the two writes leaves a fence that exists but is never evaluated, or the reverse. Repairing that needs versioned idempotent upserts and a rebuild tool.

### Option D: Asynchronous projector (change streams or outbox)
**Pros:** Full write/read decoupling. The projection can be replayed from scratch.
**Cons:** Eventual consistency: a fence created just before a location update may be missed. Needs a replica set or an outbox. The most code, and the hardest to test.

### Option E: Redis cache in front of the lookup
**Pros:** Faster reads.
**Cons:** It speeds up something that isn't a bottleneck, and it adds a second store with invalidation on create/modify/delete. Caching `TargetPosition` would be unsafe: a stale value means double alerts or lost transitions. Redis fits more naturally in `alert-service` rate limiting.

## Consequences

- **Easier:**
  - `LocationUpdatedHandler` can be built now on the existing event store and the existing standalone MongoDB.
  - There is no projection to rebuild or keep consistent.
  - Idempotency comes from aggregate state, so no separate dedup store is needed.
  - Once the unique index exists, version conflicts surface as duplicate-key errors instead of silently producing two events with the same version.
- **Harder:**
  - The lookup depends on the serialized shape of `FenceCreated`, so renaming `TargetId`/`CreatorId` or changing their serializer must update the query and the index.
  - Every update also loads streams of deleted fences on that target, a cost that grows with fence churn per person.
  - The stale-message guard relies on the producer's `OccurredAt`, so a location producer with a skewed clock can cause readings to be dropped.
- **Revisit when:**
  - a read-side use case appears that needs fence definitions without loading aggregates (UI listing, another service). That's the natural point for Option B or D.
  - deleted fences pile up enough to make stream loading measurable on the hot path. Consider Option B, or excluding deleted fences through a `FenceDeleted` lookup.
  - a transactional outbox lands, which brings transactions and a replica set for other reasons and makes Option B cheaper.

## Follow-ups

Resolved since the first draft:

- **Crossing semantics:** settled as `FenceTargetPositionEstablished` / `FenceEntered` / `FenceExited`, replacing `FenceCrossed` / `FenceUncrossed` and the `IsCrossed` flag. The first reading after a create or modify only establishes the position and never alerts. Which of Entered/Exited raises an alert is up to `alert-service`.
- **`CrossFenceHandler`:** removed. `FenceEvaluateHandler` owns crossing detection.
- **Permission re-check placement:** `fence-service` re-checks with `IPermissionService.CanWatch` at evaluation time, as `CLAUDE.md` describes. [`overview.md`](../architecture/overview.md) now shows the same thing, without the separate Privacy Filter. How `fence-service` gets permission state (a gRPC call or a local projection) is still open and is listed there.
- **`FenceTargetPositionEstablished` timestamp:** it now carries the reading's timestamp and sets `LastEvaluatedAt`, so the stale-message guard applies from the first reading.

Still open:

- **Failure handling in `LocationUpdatedHandler`:** decide what a failed evaluation of one fence means for the Kafka message: skip and log, retry the whole message, or send it to a dead-letter topic. This should be decided together with [ADR-0009](0009-at-least-once-consumer-offset-store.md).
