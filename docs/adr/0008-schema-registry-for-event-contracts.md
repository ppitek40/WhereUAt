# ADR-0008: Schema Registry for Kafka event contracts

**Status:** Accepted
**Date:** 2026-10-21
**Deciders:** Piotr Tomaszewski

## Context

`fence-service` now consumes `location.location-updated.v1`, so for the first time two services must agree on the bytes of an event. Nothing defines that agreement yet. The first consumer used `Confluent.SchemaRegistry.Serdes.JsonDeserializer<T>` while messages were produced as plain JSON, and it failed with `Invalid magic byte: 123`. Schema-aware serdes expect the Confluent wire format (`0x00` magic byte, 4-byte schema ID, payload), and plain JSON starts with `{`. The mismatch is minor, but it shows the real question: **what the wire format of an event is, and what enforces it.**

Forces at play:

- **Events outlive the code that wrote them.** Kafka retains them, and new consumers or projections replay topics from offset 0 (the reason [ADR-0003](0003-kafka-as-event-backbone.md) chose Kafka over RabbitMQ). A consumer deployed today has to read events produced months ago.
- **Contracts cross bounded-context boundaries.** Location, Permission, Fence and Alert are deployed independently. If a producer changes an event's shape without anyone noticing, it breaks a different team's service, and that service is the one that shows the error.
- **Contracts are already planned as a versioned package.** The current plan is to ship shared event types as a versioned NuGet package (not yet an ADR). That gives compile-time types to .NET services, but nothing checks the bytes actually on a topic, or messages written by non-.NET tools (Kafka UI, CLI producers, Kafka Connect).
- **Learning goal.** Schema evolution and contract compatibility are core event-driven topics. Doing them properly is worth more here than the simplest setup that works.
- **The project already uses Protobuf.** Internal service calls use gRPC ([ADR-0006](0006-grpc-internal-rest-external.md)), so `.proto` files, `Grpc.Tools` code generation and Protobuf evolution rules are already part of the toolchain.

## Decision

Integration events on Kafka are **serialized against schemas held in a Schema Registry**, using the Confluent wire format and the `Confluent.SchemaRegistry.Serdes.*` serializers and deserializers on both producer and consumer.

Conventions:

- **Format: Protobuf.** Event schemas are `.proto` files that live in the shared contracts package, and C# types are generated from them. The schema is the source of truth, and the C# classes are derived from it, not the other way round.
- **Subject naming: `TopicNameStrategy`** (subject `<topic>-value`). That matches the current topology of one event type per topic (`location.location-updated.v1`).
- **Compatibility mode: `BACKWARD_TRANSITIVE`** for every event subject. A new schema version must be able to read data written with *every* earlier version, not just the previous one, because replay from offset 0 meets all of them. In practice: add fields freely, never reuse or renumber field numbers, and remove fields only by marking them `reserved`.
- **Breaking changes create a new topic, not a new schema version.** If a change cannot pass the compatibility check, it is published to `<name>.v2` alongside `.v1` until consumers migrate. The `.vN` suffix in topic names is the semantic version, and the registry governs evolution *within* one.
- **Schemas are registered deliberately, not at runtime.** Producers run with `AutoRegisterSchemas = false`. Schemas are registered (and checked against the compatibility rule) by an explicit step, which mirrors ADR-0007's rule that topics are never auto-created. A producer that tries to send an unregistered shape fails at startup, not in production traffic.
- **Local development:** a Schema Registry container joins the shared `infra/` compose stack from ADR-0007, and Kafka UI is pointed at it so messages stay readable.

### Transitional phase: plain JSON while the pipeline is brought up

Protobuf is the target, but the first producer/consumer pairs are being built while the plumbing itself (topics, consumer loop, handlers) is still being debugged, and human-readable payloads make that much easier. Until the first end-to-end flow (`location-service` → `fence-service`) works reliably, events **may be sent as plain JSON with no registry** (Option A below), using simple custom `System.Text.Json` serializers and deserializers.

Constraints on the transitional phase:

- It is a bring-up aid, not a supported format. Nothing is built on the assumption that payloads are JSON (no JSON-specific consumers, no JSON parsing in Kafka UI-based workflows that would need to survive the switch).
- Both sides of a topic switch together. Because plain JSON and the Confluent wire format can't be mixed on one topic (that is exactly the `Invalid magic byte: 123` failure), the cut-over for a topic happens on producer and consumer at once, against an empty or recreated local topic.
- It ends once the first flow works end to end. From then on, new topics start on Protobuf and the registry directly.

Scope: this covers **integration events on Kafka**. How Fence and Alert serialize events in their own MongoDB event stores ([ADR-0004](0004-mongodb-hand-rolled-event-sourcing.md)) is a private concern of each service and is not covered here. Those events face the same evolution problem, though, and may warrant their own ADR.

## Options Considered

### Option A: Plain JSON, no registry
**Pros:** Simplest setup, with no extra container. Messages are human-readable everywhere and can be produced by hand with `kafka-console-producer`. A ~10-line custom `IDeserializer<T>` is enough.
**Cons:** Nothing enforces the contract. Shape mismatches show up as runtime failures or silent `null`s in the consumer, not in the producer. Evolution safety depends entirely on discipline and code review. Weakest fit for the learning goal.

### Option B: Schema Registry + JSON Schema
**Pros:** Messages stay human-readable JSON. Enforcement and compatibility checks come from the registry. It is the smallest step from the current code (the same `JsonDeserializer<T>`, plus a matching `JsonSerializer<T>` on the producer).
**Cons:** In .NET, the schema is usually *generated from C# classes* (via NJsonSchema), so the schema follows the code instead of being the contract. JSON Schema compatibility checking is harder to reason about (open vs. closed content models make "adding a field" ambiguous). Payloads are the largest of the three formats.

### Option C: Schema Registry + Protobuf (chosen)
**Pros:** Schema-first: `.proto` is the contract, and code is generated from it. Evolution rules are simple and well understood (field numbers, `reserved`). It reuses the toolchain and mental model already adopted for gRPC. Payloads are compact. .NET support in `Confluent.SchemaRegistry.Serdes.Protobuf` is first-class.
**Cons:** Binary on the wire, so payloads are unreadable without registry-aware tooling (Kafka UI with the registry configured, `kafka-protobuf-console-consumer`). Protobuf has no "required" fields, so every field is optional and domain invariants must be validated in code. Generated classes are not domain types, so services map them at the boundary (arguably a good thing: it keeps contracts out of the domain model).

### Option D: Schema Registry + Avro
**Pros:** The most common choice in the Kafka ecosystem, with the most mature compatibility checking. Writer/reader schema resolution is built into the format.
**Cons:** The weakest .NET tooling of the three (`Apache.Avro` plus `avrogen` code generation are less polished than Protobuf's). It would be a second IDL next to the `.proto` files already used for gRPC, with nothing gained over Protobuf for this project.

### Registry implementation (sub-decision)
- **Confluent Schema Registry** (`confluentinc/cp-schema-registry`): the reference implementation that the .NET serdes target. Licensed under the Confluent Community License: free to use, but not OSI open source.
- **Apicurio Registry**: Apache 2.0, and exposes a Confluent-compatible API so the same serdes work. Less commonly seen in tutorials and job descriptions.
- **Redpanda's built-in registry**: only relevant if ADR-0007 is revisited in favour of Redpanda.

Leaning: Confluent Schema Registry for local development, because it is the reference implementation and needs no compatibility layer. The hosted choice stays tied to the open cloud decision (Azure Event Hubs has its own schema registry with a different API and serdes).

## Consequences

- Easier: contract violations fail at the producer, or at schema registration, instead of in a downstream service. Incompatible schema changes are rejected by the registry, not caught (or missed) in review. Any consumer, including new projections replaying from offset 0, can decode any historical event via its embedded schema ID. The registry is a browsable catalogue of every event contract across bounded contexts.
- Harder: one more container and one more runtime dependency. Serializers cache schemas, so a registry outage blocks only new registrations and first lookups, but it is still something to run. Hand-producing test messages needs registry-aware tools instead of the plain console producer. Schema registration needs an explicit pipeline or init step. Services map between generated contract types and domain types. Integration tests need a registry (via Testcontainers) as well as a broker.
- Revisit when: the cloud decision lands on Azure Event Hubs (its schema registry is not Confluent-compatible, so serdes and tooling would change); Protobuf's lack of human readability proves a real drag on debugging; or event-store serialization in Fence/Alert gets its own ADR and it makes sense to align the two.

## Follow-ups

Settled with this ADR: Protobuf over JSON Schema. Still to decide, during implementation or in follow-up ADRs:

- Where schema registration runs (a compose init container locally, a CI step for hosted) and who owns it: the producing service's repo or the contracts package.
- Whether the contracts package ships only `.proto` files (each service generates its own code) or prebuilt generated C# types.
- Consumer behaviour on a deserialization failure (skip, retry, dead-letter topic). This is related but probably deserves its own decision.
