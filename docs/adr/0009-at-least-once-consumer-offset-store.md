# ADR-0009: At-least-once Kafka consumers via manual offset store

**Status:** Proposed
**Date:** 2026-11-04
**Deciders:** Piotr Tomaszewski

## Context

`fence-service` consumes `location.location-updated.v1` through a generic `EventConsumerService<T>` (`FenceService/Api/Services/EventConsumerService.cs`). That base class will be reused for every Kafka consumer in the service, so the delivery guarantee it gives becomes the default for the whole project.

A Kafka consumer has two offsets per partition:

- **Position**, kept in memory: the offset the next `Consume()` returns. Every `Consume()` moves it forward, and Kafka never moves it back on its own.
- **Committed offset**, stored in Kafka: where the consumer group resumes after a restart or rebalance. It is a single number per partition, not a per-message acknowledgement.

With the Confluent .NET client defaults (`EnableAutoCommit = true`, `EnableAutoOffsetStore = true`), librdkafka marks a message's offset for commit as soon as `Consume()` returns it, before the handler runs, and the background auto-commit (every 5 s) sends it to Kafka. If the process dies while the handler is running (crash, OOM, `kill -9`, container restart, deploy), the offset may already be committed and the message is never handled. That is **at-most-once** delivery.

Forces at play:

- **A lost `Location Updated` is a lost alert.** The reactive core is `Location Updated` → fence evaluation → `Fence Crossed` → `Alert Sent`. Dropping a location update can mean a watcher is never told that someone crossed their fence, which is the product's main promise.
- **A duplicate is cheaper than a loss.** Re-evaluating the same location against the same fences should produce the same outcome, so duplicates can be absorbed by idempotent handling. Losses can't be recovered at all.
- **Handlers write to MongoDB, not back to Kafka.** Kafka's transactional exactly-once only covers read-process-write *within Kafka*. It does not extend to side effects in Mongo.
- **Learning goal.** Delivery semantics and offset management are core event-driven topics, and the project should choose them deliberately rather than inherit the client defaults.

## Decision

Kafka consumers use **at-least-once delivery, by storing offsets manually after the handler finishes and letting auto-commit send them**:

- `EnableAutoOffsetStore = false`: `Consume()` no longer marks the offset for commit.
- `EnableAutoCommit = true` (the default): librdkafka commits the most recently *stored* offset in the background, every `AutoCommitIntervalMs` (5 s by default).
- `consumer.StoreOffset(consumeResult)` is called **after** `Handle` returns a successful `Result`. The `ConsumeResult` overload stores `offset + 1`, which is the next offset to read and the value Kafka expects.
- `consumer.Close()` runs in a `finally` when the consume loop exits, which commits the last stored offset and leaves the group cleanly.

What this protects against, compared with the default:

```
Default (auto-store): process dies during Handle
  Consume() → msg 10   (offset 11 stored immediately)
  Handle(10) ───── 💥       auto-commit sends 11
  restart    → resumes at 11      msg 10 LOST

This decision: process dies during Handle
  Consume() → msg 10
  Handle(10) ───── 💥       StoreOffset never runs, committed stays 10
  restart    → resumes at 10      msg 10 handled again (possible duplicate)
```

Consequences that are part of the decision:

- **Handlers must be idempotent.** After a crash, every message handled since the last commit (up to ~5 s worth) is delivered again. Handling the same `LocationUpdated` twice must not emit a second `Fence Crossed`.
- **This does not cover handler failures.** If `Handle` throws or returns a failed `Result` and the loop continues, the offset for that message isn't stored. But the next message that succeeds stores a higher offset, and the committed offset moves past the failed message, which is then skipped for good. The same applies to a message that fails deserialization in `Consume()`. A manual offset store protects against **process failure**, not against **handler failure**. What to do with a message that can't be processed (retry, dead-letter topic, stop the consumer) is a separate, still-open decision (see Follow-ups).

Scope: every Kafka consumer built on `EventConsumerService<T>`. Producers and the Fence/Alert MongoDB event stores ([ADR-0004](0004-mongodb-hand-rolled-event-sourcing.md)) are not covered.

## Options Considered

### Option A: Client defaults: auto-store + auto-commit (at-most-once)
**Pros:** No code. Highest throughput. A message is never processed twice.
**Cons:** Any crash during handling can silently drop the message, and nothing records that it happened. That's unacceptable for location updates that drive alerts.

### Option B: Manual offset store after `Handle` + auto-commit (chosen)
**Pros:** At-least-once with one config flag and one call. Commits stay batched in the background, so there's no broker round-trip per message. It's the pattern the Confluent .NET client documents for at-least-once.
**Cons:** Duplicates after a crash or rebalance, up to one auto-commit interval's worth, so handlers must be idempotent. It's easy to misread as "failed messages are retried", which they aren't (see above).

### Option C: Synchronous `Commit(consumeResult)` after every message
**Pros:** The narrowest replay window: at most the in-flight message is redelivered. Easy to reason about.
**Cons:** A blocking broker round-trip per message, which caps throughput at roughly 1 / commit latency per partition. It still needs idempotent handlers, since a crash between `Handle` and `Commit` replays the message anyway. So the extra cost only buys a smaller replay window, not a stronger guarantee.

### Option D: Exactly-once
**Pros:** No duplicates.
**Cons:** Kafka transactions (`SendOffsetsToTransaction`) only make Kafka-to-Kafka pipelines exactly-once. The side effects here land in MongoDB, so true exactly-once would need the offset and the Mongo write in one atomic unit, e.g. an inbox collection or storing offsets in Mongo next to the aggregate. That's real complexity for a guarantee that idempotent handlers already give us in practice. It could come back as an idempotency mechanism (an inbox of processed event IDs), but not as the delivery model.

## Consequences

- Easier: a crash, deploy or container restart no longer loses messages that were mid-handling. The same guarantee applies to every consumer that derives from `EventConsumerService<T>`. Graceful shutdown commits progress explicitly through `Close()`.
- Harder: every handler has to be idempotent, and that has to be designed and tested per handler. Integration tests should cover redelivery, not just the happy path. The skip-on-failure behaviour described above is less visible than before and must not be mistaken for retry.
- Revisit when: the failure-handling decision lands (it may add retries or a dead-letter topic around the store); throughput or the replay window becomes a real concern (consider tuning `AutoCommitIntervalMs` or Option C); or a consumer ends up writing back to Kafka only, where transactional exactly-once becomes viable.

## Follow-ups

- **Failure handling for unprocessable messages** (skip, retry, dead-letter topic, stop the consumer): a separate ADR, already listed as open in [ADR-0008](0008-schema-registry-for-event-contracts.md). Until then, failed messages are logged and skipped.
- **`Result.Failure` and a thrown exception are currently handled the same way**: the failure is logged and the offset is not stored, so the message is skipped as soon as a later message succeeds. The failure-handling ADR should decide per `ErrorType` ([ADR-0005](0005-result-pattern-for-domain-errors.md)), not treat every failure the same way. The current leaning:

  | `ErrorType` | Meaning in a consumer | Handling |
  |---|---|---|
  | `Validation` | The event carries bad data (e.g. out-of-range coordinates). Retrying gives the same result. | Permanent: dead-letter topic. Until then, log a warning with the error code and topic/partition/offset, then `StoreOffset`. |
  | `NotFound` | The user has no fences, or nobody watches them. | Not a failure: "nothing to do". The handler should return `Success`. |
  | `Conflict` | Optimistic-concurrency race on the event-store append. | Transient: a few immediate retries (reload the aggregate, re-evaluate), then dead-letter. |

  Exceptions stay the signal for infrastructure failures (e.g. MongoDB unavailable) and go through retry with backoff. A `Result.Failure` should not be converted into an exception to share that path, because a permanent failure would then be retried as if it were transient. No failure should block the partition indefinitely.

- **Idempotency of `LocationUpdatedHandler`**: decide how a re-delivered update avoids a second `Fence Crossed` (for example, comparing against the last evaluated position per user and fence, or deduplicating by event ID).
- **Collision with [ADR-0005](0005-result-pattern-for-domain-errors.md).** ADR-0005 keeps `Cross()`/`Uncross()` on a fence already in that state as a thrown exception, because it signals "a duplicate or replayed command". Under this ADR duplicates are expected. A re-delivered `LocationUpdated` after a restart would call `Cross()` on an already-crossed fence, throw, and be treated by the consumer as an infrastructure failure and retried, which can never succeed. The handler must check whether the new position actually changes a fence's state before calling `Cross()`/`Uncross()`, and return `Success` when it doesn't. That keeps the aggregate's exception meaning what ADR-0005 intended: a real bug, not a routine redelivery.
