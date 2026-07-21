# ADR-0003: Apache Kafka as the event backbone

**Status:** Accepted
**Date:** 2026-07-21
**Deciders:** Piotr Tomaszewski

## Context

The domain's reactive core — `Location Updated` → evaluate fences → `Fence Crossed` → `Alert Sent` — is fundamentally event-driven (see [event storming summary](../domain/event-storming-summary.md), section 3). The project's stated goal is to learn event-driven architecture and event sourcing in depth, which requires a transport that supports durable, replayable, ordered event logs, not just fire-and-forget messaging.

## Decision

Use **Apache Kafka** as the event backbone for domain events between services (`LocationUpdated`, `FenceCrossed`, `AlertSent`, etc.), and as the basis for exploring event-sourced aggregates (rebuilding state from a persisted event log rather than current-state storage).

## Options Considered

### Option A: Apache Kafka (chosen)
**Pros:** Durable, replayable log fits both event-driven messaging and event-sourcing experimentation; schema registry support for evolving event contracts; the most widely recognized EDA backbone on a CV.
**Cons:** Operationally heavier than a simple broker; needs a local dev story (e.g. `docker-compose`, or a Kafka-API-compatible alternative like Redpanda) — to be captured in a follow-up ADR once evaluated.

### Option B: RabbitMQ
**Pros:** Simpler to run and reason about for pure pub/sub.
**Cons:** No built-in long-term log retention/replay — a poor fit for event sourcing, which is an explicit learning goal here.

### Option C: Cloud-native (Azure Service Bus / Event Grid, or AWS EventBridge/SNS/SQS)
**Pros:** Less infrastructure to run ourselves; integrates natively with the chosen cloud's IaC.
**Cons:** Ties the architecture's core to one cloud provider; weaker replay/log semantics than Kafka for the event-sourcing goal.

## Consequences

- Easier: a single, consistent event transport across all services; natural fit if/when we build a dedicated event store on top of or alongside Kafka.
- Harder: added operational complexity (cluster or managed Kafka, schema registry) that must be justified purely by learning value, since this is not a production system with real load.
- Revisit when: local developer experience or Azure-hosting cost/complexity makes a managed alternative (e.g. Azure Event Hubs, which speaks the Kafka protocol) clearly preferable — see the open cloud/IaC decision.
