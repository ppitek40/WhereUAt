# ADR-0002: .NET / C# as the primary microservice language

**Status:** Accepted
**Date:** 2026-07-21
**Deciders:** Piotr Tomaszewski

## Context

WhereUAt's explicit goal is to demonstrate depth in DDD, event sourcing, and event-driven microservices. The language/runtime choice needs to support expressive domain modelling (aggregates, value objects, invariants) and have a mature ecosystem for CQRS and event sourcing, while also being a strong, recognizable signal on a CV.

## Decision

Build the microservices in **.NET / C#**.

## Options Considered

### Option A: .NET / C# (chosen)
**Pros:** Mature DDD/CQRS/event-sourcing ecosystem (MediatR, MassTransit, Marten, EventStoreDB clients); records, pattern matching, and nullable reference types support expressive, well-typed domain events and value objects; strong enterprise recognition on a CV; first-class Kubernetes/container support.
**Cons:** Single-language monorepo doesn't showcase polyglot-microservices skills.

### Option B: Java / Kotlin (Spring Boot + Axon)
**Pros:** Axon Framework gives CQRS/event-sourcing almost out of the box.
**Cons:** More opinionated framework magic can obscure the underlying patterns — less useful for learning them from first principles.

### Option C: Node.js / TypeScript
**Pros:** Fast to iterate, lightweight services.
**Cons:** Weaker typing discipline for aggregates/value objects; thinner native event-sourcing ecosystem.

### Option D: Go
**Pros:** Excellent for high-throughput services (e.g. location ingestion).
**Cons:** No idiomatic DDD/ES libraries; most patterns would be hand-rolled, adding friction to the learning goal rather than removing it.

## Consequences

- Easier: consistent tooling, testing, and CI across all services; deep rather than broad demonstration of the chosen ecosystem.
- Harder: doesn't demonstrate polyglot interop out of the box — if that's wanted later, it can be added deliberately as one extra service communicating over Kafka contracts, without disrupting the rest.
- Revisit when: a specific service has requirements (e.g. raw ingestion throughput) that make a different runtime clearly better.
