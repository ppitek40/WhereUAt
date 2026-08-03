# ADR-0005: Result pattern for expected domain errors, kept in a shared kernel

**Status:** Proposed
**Date:** 2026-08-03
**Deciders:** Piotr Tomaszewski

## Context

Aggregates currently signal rule violations by throwing (`Fence.Create` throws `ArgumentException` when `creatorId == targetId`; `Delete`/`Cross`/`Uncross` throw `InvalidOperationException` on invalid state transitions). Vogen value objects (`FenceName`, `RadiusInMeters`, `Latitude`, `Longitude`) already validate at construction and expose both a throwing (`From`) and non-throwing (`TryFrom`, returning a `Validation`) path, but only the throwing path is used today.

Not every throw site is the same kind of failure:

- **Expected, client-triggerable business-rule violations** — self-fencing, out-of-range name/radius/coordinates — are things a caller can legitimately submit, and an API needs to map them to a 4xx response. Using exceptions for these conflates ordinary control flow with exceptional flow and leaves the failure path off the method signature, so callers can forget to handle it.
- **State-corruption cases** — `Delete()` on an already-deleted fence, `Cross()`/`Uncross()` on a fence already in that state — are more likely symptoms of a duplicate/replayed command or a concurrency race than a "business rule" a user can trigger through normal use. Folding these into the same Result plumbing would quietly paper over an idempotency or concurrency bug instead of surfacing it.

Separately, the domain is split across five services (User, Permission, Location, Fence, Alert), each its own bounded context with its own `.sln`. Whatever error-handling primitive is chosen needs a home, and that choice interacts with the already-decided per-service contracts package (see the "Solution structure leaning" note in project memory) which exists for versioned Kafka integration-event schemas — not for generic coding-convention types.

## Decision

Introduce a `Result` / `Result<T>` / `Error` primitive and use it at aggregate method boundaries **only for expected, client-triggerable business-rule violations**. State-corruption cases (operating on an aggregate already in the target state, replayed commands) keep throwing — that failure mode indicates a bug in the caller or a concurrency/idempotency issue upstream of the aggregate, not a business rule to report back to a client.

House the `Result`/`Error` types in a new **shared kernel** package (e.g. `WhereUAt.SharedKernel`), separate from the per-context contracts packages. It carries no business meaning and is never serialized across a service boundary, so it doesn't belong in a contracts package — but it is intentionally shared code crossing bounded-context lines, which is exactly what "shared kernel" names as a deliberate, narrow exception to context independence.

Scope of the shared kernel is deliberately narrow: `Result`, `Result<T>`, `Error`/`ErrorType` (e.g. `Validation`, `Conflict`, `NotFound`, for later mapping to HTTP status), and nothing with domain semantics. No `FenceCrossed`-shaped types, no base `AggregateRoot`/`Entity` classes unless a second aggregate proves they're identical, no per-context concepts — those stay in each service's own Domain project or in that service's contracts package.

## Options Considered

### Option A: Result only where it's genuinely a business rule (chosen)
**Pros:** Keeps the signature honest — a method that can fail on bad input says so; state-corruption bugs still surface loudly instead of being silently normalized into an expected `Result.Failure`.
**Cons:** Two error-handling idioms live side by side in the same aggregate; requires a real judgment call per throw site rather than a single mechanical rule, and that call can be revisited as new cases show up.

### Option B: Result everywhere, no exceptions in the domain
**Pros:** One consistent idiom; nothing to argue about per call site.
**Cons:** Makes "fence already crossed" look like a normal, expected outcome of calling `Cross()`, hiding what's actually a replay/concurrency problem; pushes every caller to pattern-match on failures that should instead never happen if the caller is correct.

### Option C: Keep exceptions everywhere, no Result
**Pros:** No new primitive, no new package, least change.
**Cons:** Client-facing validation failures (self-fencing, out-of-range values) end up handled via try/catch in the Application layer or an ASP.NET exception-filter, which is a well-known but weaker way to make "this can fail" visible at the call site than a return type.

### Option D: Put Result/Error in the existing per-context contracts package
**Pros:** No new package to create or reference.
**Cons:** Conflates two different kinds of "shared": contracts packages exist for versioned wire schemas other services deserialize; `Result<T>` is an internal coding convention that never crosses a wire. Mixing them muddies what the contracts package is actually for.

## Consequences

- Easier: command handlers get a uniform, compiler-enforced way to turn expected domain failures into 4xx responses; genuine bugs (double-cross, double-delete) still fail loudly via exceptions instead of being masked as ordinary `Result` failures.
- Harder: every new throw site needs the same judgment call (expected business rule vs. state corruption) rather than a single mechanical rule; the shared kernel is a second place (besides each service's own Domain) that changes when the error-handling convention evolves, and needs discipline to keep from accumulating anything with actual business meaning.
- Revisit when: a second aggregate (Alert, or later User/Permission/Location) shows the expected/corruption split doesn't hold cleanly, or the shared kernel starts accumulating types that aren't purely generic.
