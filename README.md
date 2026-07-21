# WhereUAt

<img width="1640" height="1133" alt="Event Storming board for WhereUAt" src="https://github.com/user-attachments/assets/7e29a2a7-0a86-4e95-bf36-5d64e71ecf6f" />

WhereUAt is a location-sharing and geofence-alerting platform: users share their real-time location under explicit permission, and can define geofences on the people who've shared with them — crossing a fence alerts the fence's creator, not the person who crossed it.

It's built as a hands-on portfolio project to demonstrate **event-driven microservices, domain-driven design, and event sourcing** end to end — not just a CRUD app with a few extra buzzwords, but a system where those patterns are load-bearing.

## The domain, in short

Three roles emerged from an [Event Storming](docs/domain/event-storming-summary.md) workshop:

- **Watcher** — creates a fence on another person and receives the alert when it's crossed.
- **Watched** — the person whose location is tracked.
- **Permission** — the consent that authorizes a watcher to see a watched person's location and fence them.

Five bounded contexts (User, Permission, Location, Fence, Alert) form the domain model; the reactive core is a location update triggering fence evaluation, which triggers alert delivery. Full write-up, event flows, and the business decisions behind them: [`docs/domain/event-storming-summary.md`](docs/domain/event-storming-summary.md).

## What this project is demonstrating

| Area | Applied as |
|------|------------|
| Domain-Driven Design | Bounded contexts and aggregates derived directly from Event Storming, not guessed after the fact |
| Event-driven architecture | Services communicate over domain events on Kafka, not synchronous chains of REST calls |
| Event sourcing | Aggregate state (at least for Fence/Alert) rebuilt from an event log rather than stored as current-state rows |
| Microservices | One service per bounded context, independently deployable |
| Infrastructure as Code | Cloud infrastructure and deployment topology defined and versioned as code |

See [`docs/architecture/overview.md`](docs/architecture/overview.md) for how these map to services today, and [`docs/adr/`](docs/adr/README.md) for the decision-by-decision reasoning.

## Tech stack

| Layer | Choice | Status |
|-------|--------|--------|
| Language / runtime | .NET / C# | Decided — [ADR-0002](docs/adr/0002-dotnet-for-microservices.md) |
| Event backbone | Apache Kafka | Decided — [ADR-0003](docs/adr/0003-kafka-as-event-backbone.md) |
| Cloud | Azure (SES stays on AWS for email, per the domain edges) | Leaning, not final |
| Infrastructure as Code | Terraform and/or Kubernetes + Helm | Under evaluation |

## Repository structure

```
.
├── docs/            # architecture, domain, and decision docs (see below)
├── services/        # microservices — not started yet
└── infra/           # IaC — not started yet
```

## Documentation

`/docs` is the living source of truth beyond this README:

- [`docs/domain/`](docs/domain/) — Event Storming output and business decision records.
- [`docs/architecture/`](docs/architecture/) — current bounded-context-to-service mapping and diagrams.
- [`docs/adr/`](docs/adr/) — Architecture Decision Records, the log of *why* each technical choice was made.

Docs are updated alongside the code and decisions that make them true, not as an afterthought — see [`docs/README.md`](docs/README.md) for the full convention.

## Status

Currently in the architecture and design phase: domain model and initial tech-stack decisions are settled; service implementation, event-sourcing infrastructure, and IaC haven't started yet. This section — and the structure above — will be updated as that changes.
