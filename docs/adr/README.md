# Architecture Decision Records

An ADR captures a single significant, hard-to-reverse technical decision: what we decided, what alternatives we considered, and why. They exist so the reasoning behind early choices survives past the moment they were made — for future-us, and for explaining the project in interviews.

## Conventions

- One decision per ADR, numbered sequentially (`0001-title.md`).
- Copy [`template.md`](template.md) to start a new one.
- Status starts at **Proposed**; move to **Accepted** once settled.
- Never edit an Accepted ADR's decision after the fact. If it changes, write a new ADR and mark the old one `Superseded by ADR-XXXX`.

## Log

| # | Title | Status | Date |
|---|-------|--------|------|
| [0001](0001-record-architecture-decisions.md) | Record architecture decisions | Accepted | 2026-07-21 |
| [0002](0002-dotnet-for-microservices.md) | .NET / C# as the primary microservice language | Accepted | 2026-07-21 |
| [0003](0003-kafka-as-event-backbone.md) | Apache Kafka as the event backbone | Accepted | 2026-07-21 |
