# ADR-0007: Single-node Apache Kafka in KRaft mode for local development

**Status:** Proposed
**Date:** 2026-10-14
**Deciders:** Piotr Tomaszewski

## Context

[ADR-0003](0003-kafka-as-event-backbone.md) chose Apache Kafka as the event backbone, but explicitly deferred the local developer story ("e.g. `docker-compose`, or a Kafka-API-compatible alternative like Redpanda — to be captured in a follow-up ADR once evaluated"). `fence-service` is about to get its first Kafka consumer, so a broker now has to exist locally.

Forces at play:

- **Learning goal.** The project exists to build real depth in event-driven architecture. Running *actual* Kafka — and seeing its partitions, consumer groups, offsets, and lag directly — is worth more here than the lightest possible setup.
- **Kafka's coordination model has changed.** Kafka historically depended on a separate Apache ZooKeeper cluster for cluster metadata and controller election. KRaft mode (Kafka's built-in Raft-based metadata quorum, storing cluster metadata as an internal event log) replaced it, and ZooKeeper support was removed entirely in Kafka 4.0.
- **Two kinds of client.** Services running inside the compose network and processes running on the host (IDE runs, integration tests) both need to connect. Kafka clients bootstrap and then reconnect to whatever address the broker *advertises*, so a single advertised address can't serve both.
- **Kafka is shared infrastructure.** Every service on the reactive spine (`location-service`, `permission-service`, `fence-service`, `alert-service`) talks to the same broker, unlike MongoDB, which is owned per service.

## Decision

Run a **single-node Apache Kafka cluster in KRaft combined mode** (one process acting as both broker and controller) using the official `apache/kafka` Docker image, pinned to an explicit 4.x tag rather than `latest`.

Configuration conventions:

- **Two client listeners plus the controller listener:** an `INTERNAL` listener advertised as `kafka:29092` for containers on the compose network, an `EXTERNAL` listener advertised as `localhost:9092` for host processes, and a `CONTROLLER` listener for the KRaft quorum. Services read their bootstrap address from configuration, overridden per environment (as with the MongoDB connection string).
- **Replication factors of 1** for internal topics (`__consumer_offsets`, transaction state), since there is only one node.
- **Topic auto-creation disabled** (`auto.create.topics.enable=false`). Topics are created deliberately, with an explicit partition count, by an init step, never implicitly by the first producer or consumer to name them.
- **A Kafka UI** (the maintained `kafbat/kafka-ui` image) alongside the broker, for inspecting topics, messages, partitions, and consumer-group lag.
- **Location:** the broker lives in a shared, root-level infrastructure compose file (under `infra/`, per the README's planned layout), not inside any single service's `compose.yaml`. Service compose files attach to its network.

This ADR covers **local development only**. The hosted environment (self-run Kafka vs. a managed Kafka-protocol service such as Azure Event Hubs) stays open and is tied to the still-open cloud/IaC decision.

## Options Considered

### Option A: Apache Kafka, KRaft mode, single node (chosen)
**Pros:** It is the real Kafka that ADR-0003 chose, so behaviour, configuration, and tooling (`kafka-topics.sh`, `kafka-consumer-groups.sh`) carry over directly to any Kafka deployment. One container, no separate coordination cluster. KRaft is the current and only supported mode going forward. Its metadata log is itself a small example of the event-sourcing ideas this project is about.
**Cons:** JVM-based, so heavier on memory and slower to start than Redpanda. The listener configuration is fiddly, and once any `KAFKA_*` environment variable is set the image stops applying its defaults, so the full set must be specified. A schema registry, if adopted later, is a separate container.

### Option B: Redpanda
**Pros:** A single C++ binary that speaks the Kafka wire protocol, so `Confluent.Kafka` works unchanged. Fast startup, small footprint, and a built-in schema registry, HTTP proxy, and console UI.
**Cons:** It is a Kafka-compatible substitute rather than Kafka, and some broker configuration and edge-case behaviour differ. Experience gained is partly Redpanda-specific, which undercuts the learning goal behind ADR-0003. It is source-available under the Business Source License rather than Apache-licensed.

### Option C: Apache Kafka with ZooKeeper
**Pros:** Matches many older tutorials and existing `docker-compose` examples.
**Cons:** Removed in Kafka 4.0. It would mean two systems to run and learning a deprecated architecture. Not a viable option.

### Option D: Managed Kafka-protocol service (e.g. Azure Event Hubs) for development too
**Pros:** Nothing to run locally, and it would be the same endpoint type as a likely hosted target.
**Cons:** Needs a cloud subscription and network access for every dev and test run, has a cost, can't run under Testcontainers, and hides the broker internals the project wants to learn from. It also pre-empts the open cloud decision.

## Consequences

- Easier: one `docker compose up` gives every service the same broker. Integration tests can run the same image via Testcontainers. Topics, partitions, offsets, and consumer-group lag are inspectable from the CLI and the UI. Disabling auto-creation means a misspelled topic name fails loudly instead of silently creating an empty topic.
- Harder: topics need an explicit creation step (an init container or startup check), and partition counts have to be chosen up front. Contributors have to know which bootstrap address applies where (`kafka:29092` inside containers, `localhost:9092` on the host). A single node can't demonstrate replication, ISR behaviour, or broker failover.
- Revisit when: replication or failover behaviour becomes something worth exploring (move to a three-node local cluster); a schema registry is adopted and Redpanda's built-in one starts to look more attractive than an extra container; or the cloud/IaC decision lands on a managed Kafka-protocol service and local/hosted parity becomes a concern.
