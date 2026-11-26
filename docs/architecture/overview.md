# Architecture Overview

**Status:** living document — reflects the current plan, not a finished system. Updated as decisions land in [`docs/adr/`](../adr/README.md).

## Bounded contexts → services

The Event Storming session identified five aggregates, each a natural bounded context. Confirmed through the data-flow diagram below: one microservice per aggregate, with `location-service` further split internally (see Bounded-context notes).

| Aggregate | Service | Responsibility |
|-----------|---------|-----------------|
| User | `user-service` | Account lifecycle: registration, email verification, login, password change, deletion |
| Permission | `permission-service` | The consent relationship: request, grant, deny, revoke, expire |
| Location | `location-service` | Current position and periodic location reporting |
| Fence | `fence-service` | Geofence definition and crossing detection |
| Alert | `alert-service` | Delivery of notifications to watchers |

## Event flow (the reactive spine)

Settled through three rounds of whiteboarding the actual data flow (not just the aggregate map):

```mermaid
flowchart LR
    GPS[Device GPS / clients] --> Ingest[location-service: Ingestion]
    Ingest --> Kafka[(Kafka)]
    Kafka --> LocState[location-service: State]
    LocState <--> LocCache[(Redis)]
    LocState <--> LocDB[(DB)]
    LocState -->|LocationUpdated, post-dedup| Fence[fence-service: GeoFence]
    Fence -.->|CanWatch per fence, mechanism TBD| Perm[permission-service]
    Perm <--> PermCache[(Redis: Permissions Cache)]
    Perm <--> PermDB[(DB)]
    Fence -->|FenceEntered / FenceExited| Alert[alert-service]
    Alert --> SES[SES / email]
    Alert --> UI[UI]

    Gateway[api-gateway] <--> SignalR[(SignalR)]
    SignalR --> UI
    UI --> Gateway
    Gateway <--> Perm
    Gateway <--> Fence
    Gateway <--> User[user-service]
    User <--> UserDB[(DB)]

    IdP[OAuth2 identity provider] --> User
    OSM[OpenStreetMap] --> UI
```

The key property this design enforces (per [BDR-002](../domain/event-storming-summary.md#bdr-002-permission-enforced-at-evaluation-time-not-cascaded-on-revocation)): permission is re-checked on *every* location update, not just when serving reads or creating a fence. `location-service` rejects ~90% of raw pings that carry no positional change before anything downstream ever sees them.

The re-check happens **inside `fence-service`, once per fence**, during evaluation. For each fence on the updated user, `LocationUpdatedHandler` calls `IPermissionService.CanWatch(creatorId, targetId)` and skips the fence if the watcher no longer has permission. The fence stays in place, so it becomes active again if permission is re-granted. An earlier draft of this diagram put a Privacy Filter in `permission-service` in front of `fence-service`. It was dropped because permission is a property of a watcher–target pair, and a single location update fans out to several watchers' fences. Only fence evaluation knows which watchers are involved. Fence lookup and evaluation are described in [ADR-0010](../adr/0010-fence-lookup-by-target-index.md).

How `fence-service` gets the permission state is **not yet decided**. Today `CanWatch` is a stub that always allows. See "Cross-cutting concerns" below.

Full domain narrative, aggregates, and business decision records: [`docs/domain/event-storming-summary.md`](../domain/event-storming-summary.md).

## Bounded-context notes

- **`location-service`** is internally split into an `Ingestion` component (absorbs raw GPS pings, publishes to Kafka) and a `State` component (dedup/current-position cache + history `DB`) — one bounded context, two deployable pieces.
- **`permission-service`** owns its own `DB`, separate from `user-service`'s — User and Permission stayed two aggregates with two stores, per the original event-storming split, not merged.
- Database engines are deliberately left generic (`DB`) for `location-service`, `permission-service`, and `user-service` — not yet decided. `fence-service` and `alert-service` use MongoDB per [ADR-0004](../adr/0004-mongodb-hand-rolled-event-sourcing.md); for `alert-service` that store holds the per-attempt delivery event log (retries, channel outcomes) described in [BDR-005](../domain/event-storming-summary.md#bdr-005-alert-aggregate-models-delivery-as-a-retryable-multi-channel-history).
- **Event contracts on Kafka** are Protobuf schemas held in a Schema Registry, with `BACKWARD_TRANSITIVE` compatibility and a new `.vN` topic for breaking changes, per [ADR-0008](../adr/0008-schema-registry-for-event-contracts.md). While the first flow (`location-service` → `fence-service`) is being brought up, payloads are temporarily plain JSON, as that ADR allows. Topics carry one event type each, under the registry's default `TopicNameStrategy`. Where ordering across event types for the same entity matters (e.g. `PermissionGranted` / `PermissionRevoked` for one owner–watcher pair), the topic carries a single Protobuf envelope message with a `oneof` of the event types, keyed by that entity. It still has one schema, so `TopicNameStrategy` and ADR-0008 still apply. Introduce envelopes only when a concrete ordering need appears.

## Known simplifications in the diagram (revisit before/at implementation)

- The `User Deleted` cascade (BDR-004) isn't represented in this data-flow diagram; it's a lifecycle concern tracked separately, not dropped.

## Cross-cutting concerns — not yet decided

These are the advanced patterns the project intends to exercise; each will get its own ADR once evaluated rather than being decided upfront:

- **CQRS read models**: beyond the Permissions Cache already in the diagram, whether `fence-service` needs its own materialized read model of fence definitions vs. querying on demand. [ADR-0010](../adr/0010-fence-lookup-by-target-index.md) proposes no read model for now: fence evaluation finds fences through an indexed query on `FenceCreated` events. A read model comes back on the table when a read-side use case needs fence definitions without loading aggregates.
- **How `fence-service` reads permission state** for the per-fence `CanWatch` check. Candidates:
  - a synchronous gRPC call to `permission-service`, backed by its Permissions Cache. Always up to date, but it couples the hot path to that service being available.
  - a local projection in `fence-service`, built from `PermissionGranted` / `PermissionRevoked` / `PermissionExpired` events on Kafka. It keeps the path fully event-driven, but it is eventually consistent: a fence may still alert briefly after a revocation.

  Needs an ADR before `PermissionService` is implemented.
- **Saga / process manager** for the `User Deleted` fan-out cascade (must be reliable and idempotent across three services).
- **API gateway implementation** — the diagram fixes its existence and role, not the tool (candidate: YARP). Its external surface (UI-facing) is REST/JSON and its internal calls into services (`Gateway <--> Fence`, etc.) are gRPC, per [ADR-0006](../adr/0006-grpc-internal-rest-external.md).
- **Service-to-service auth** (likely mTLS or a service mesh, depending on the Kubernetes decision).
- **Observability** — distributed tracing across the Location → Fence (→ Permission) → Alert spine.
- **Cloud / IaC target** — Azure-leaning, Terraform vs. Kubernetes/Helm still under evaluation (SES stays on AWS regardless, per the event storming domain edges).

## Related docs

- Domain model & business decisions: [`docs/domain/event-storming-summary.md`](../domain/event-storming-summary.md)
- Decision log: [`docs/adr/README.md`](../adr/README.md)
