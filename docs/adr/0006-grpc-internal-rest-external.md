# ADR-0006: gRPC for internal service calls, REST for the external-facing API

**Status:** Accepted
**Date:** 2026-08-19
**Deciders:** Piotr Tomaszewski

## Context

`docs/architecture/overview.md` fixes the existence of an `api-gateway` sitting between the UI and the backend services (`Gateway <--> Fence`, `Gateway <--> Perm`, `Gateway <--> User`), but leaves its concrete tooling and the transport it uses open ("API gateway implementation — the diagram fixes its existence and role, not the tool").

Some user actions — e.g. creating a fence — need a synchronous request/response: the user clicks "create" and expects an immediate success/failure, not eventual consistency via Kafka. That means, alongside the async event backbone (ADR-0003), there's also a synchronous call path: browser → `api-gateway` → the owning service's own `Api` project (e.g. `fence-service`'s `Api`), which invokes the aggregate and maps its `Result` (ADR-0005) to a response.

Two distinct hops exist on this path with different constraints:
- **External hop:** browser (or any third-party client) → `api-gateway`. Client is out of our control, needs broad tooling support, human-readable payloads help debugging.
- **Internal hop:** `api-gateway` → a backend service (e.g. `fence-service`), and potentially service-to-service beyond the gateway later. Both ends are ours, deployed together, and can share strongly-typed contracts.

Treating both hops as "the same kind of API call" and picking one transport for both misses that they have different constraints and different audiences.

## Decision

Use **gRPC for internal, service-to-service calls** (gateway → `fence-service`, and any future direct service-to-service synchronous calls), and **REST/JSON for the external-facing API** (browser/UI → `api-gateway`).

Each service that's called internally (starting with `fence-service`) exposes its command/query surface as a `.proto`-defined gRPC service. `api-gateway` is the only component that also exposes REST/JSON, translating external HTTP requests into internal gRPC calls.

The `.proto` contracts for gRPC are a distinct concern from the Kafka integration-event schemas already planned per service (see ADR-0005's mention of per-context contracts packages for versioned Kafka events) — one is a synchronous request/response schema, the other an async event schema. They should not be merged into the same package or conflated as "the same contract."

## Options Considered

### Option A: gRPC internal, REST external (chosen)
**Pros:** Each hop uses the transport suited to its actual constraints — REST/JSON stays the simple, universally-supported, human-debuggable option for arbitrary external clients; gRPC gives strongly-typed, codegen'd contracts and HTTP/2 for calls where we control both ends. Pairs naturally with the still-open service-to-service auth decision (mTLS is the leading candidate, and gRPC-over-mTLS is a well-worn combination).
**Cons:** Two transports to run and reason about instead of one; the gateway needs both a REST server and gRPC clients; proto contracts are a second schema-versioning surface alongside Kafka's.

### Option B: REST everywhere
**Pros:** One transport, one mental model; simplest to build and debug; reuses the same OpenAPI tooling end to end.
**Cons:** Gives up strongly-typed internal contracts and codegen'd clients for calls entirely within our control, where that tooling has the least cost and the most benefit.

### Option C: gRPC everywhere, including external
**Pros:** One transport internally and externally; strong contracts for all clients.
**Cons:** Poor fit for arbitrary browser/third-party clients (needs grpc-web or a translating proxy either way); loses the debuggability and universal tooling REST gives the public-facing surface, for no real benefit at that edge.

## Consequences

- Easier: internal calls get compiler-checked contracts and generated clients, catching schema drift at build time instead of at runtime; the external surface stays simple and approachable for any client, including the browser directly if ever needed.
- Harder: `api-gateway` must run both a REST endpoint and gRPC clients, and translate between the two payload shapes and error models (e.g. gRPC status codes ↔ HTTP status codes, echoing the `Result`-to-4xx mapping from ADR-0005); two contract-versioning disciplines to maintain (proto for internal, OpenAPI/JSON for external) instead of one.
- Revisit when: the internal call graph turns out to need async/streaming semantics that push toward Kafka instead of a synchronous call, or the two-transport overhead in the gateway outweighs the type-safety benefit at this project's scale.
