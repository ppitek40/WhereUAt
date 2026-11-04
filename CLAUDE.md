# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project status

One service exists so far: **FenceService** (.NET 10). It exposes a gRPC API for creating, modifying and deleting fences, stores Fence as an event-sourced aggregate in MongoDB, and consumes `LocationUpdated` events from Kafka. The other four contexts (User, Permission, Location, Alert) have no code yet.

Repository layout:

- `FenceService/`: solution `FenceService.slnx` with `Api` (gRPC host + Kafka consumer), `Application`, `Domain`, `Infrastructure`, `Sdk`, and `tests/` (`ApplicationTests`, `ArchitectureTests`, `IntegrationTests`).
- `SharedKernel/`: `WhereUAt.SharedKernel` (`Result`/`Error`, see ADR-0005), published as a NuGet package to GitHub Packages and consumed from there, not as a project reference.
- `kafka.yaml`: local Kafka (KRaft), Schema Registry, Kafka UI, and the `kafka-init` / `schema-init` containers that create topics and register schemas.
- `docker-compose.yaml`: the full local stack. It includes `kafka.yaml` plus `FenceService/compose.yaml` and `FenceService/compose.full.yaml`.

### Commands

Restoring needs a `GITHUB_TOKEN` environment variable with `read:packages`, because `FenceService/nuget.config` pulls `WhereUAt.SharedKernel` from GitHub Packages. The Docker build passes it in as a build secret.

```sh
dotnet build FenceService/FenceService.slnx
dotnet test FenceService/FenceService.slnx                       # IntegrationTests need Docker (Testcontainers MongoDB)
dotnet test FenceService/FenceService.slnx --filter "FullyQualifiedName~<TestName>"

docker compose up --build                                        # full stack from the repo root
docker compose -f kafka.yaml up -d                               # Kafka + Schema Registry + Kafka UI only, e.g. to run the API from the IDE
```

Local ports: Kafka `9092` (host listener; containers use `kafka:29092`), Schema Registry `8081`, Kafka UI `8080`, MongoDB `27017`.

There is no CI pipeline and no lint step yet.

## What this project is

WhereUAt is a location-sharing and geofence-alerting platform, built as a portfolio project whose explicit purpose is to demonstrate depth in **event-driven microservices, domain-driven design, and event sourcing** — not just to ship features. When making a technical choice here, prefer the option that best exercises those patterns over the option that's fastest to build.

Core rule of the domain: a user may only create a fence on someone who has granted them permission to see their location, and a fence crossing alerts the fence's *creator* (the watcher), not the person who crossed it.

## Domain model

Five bounded contexts, one aggregate each: **User**, **Permission**, **Location**, **Fence**, **Alert**. The reactive core is a policy chain: `Location Updated` → fence evaluation (re-checking permission at evaluation time, not on write) → `Fence Crossed` → `Alert Sent`. Full narrative and the business decisions behind these boundaries: `docs/domain/event-storming-summary.md`. Proposed service mapping and diagrams: `docs/architecture/overview.md`.

## Decisions made so far

Tracked as ADRs in `docs/adr/` (see `docs/adr/README.md` for the index) — read the relevant ADR before revisiting a settled decision:

- Services are built in .NET / C#.
- Apache Kafka is the event backbone for cross-service messaging.
- Fence and Alert use hand-rolled event sourcing on MongoDB.
- Kafka event contracts are Protobuf schemas in a Schema Registry (`BACKWARD_TRANSITIVE`; breaking changes go to a new `.vN` topic). Plain JSON is temporarily allowed while the first producer/consumer flow is debugged.
- Cloud (Azure-leaning) and IaC tooling (Terraform vs. Kubernetes/Helm) are still open — don't treat either as decided.

## Do not write the implementation

This is a personal learning project — the point is for the human to write everything but the docs themselves. **Do not write or edit code, config, scripts, or any other non-documentation file, even if asked to "just fix this," "add this feature," or "just add a config for X."** This applies once services exist, not just now while the repo is docs-only.

The only files you may create or edit are documentation: Markdown (`.md`) files, anywhere in the repo, and anything under `docs/`. Everything else — source code, `.csproj`/`.sln`/`.slnx` files, Dockerfiles, `docker-compose.yml`, CI/CD pipeline files, IaC/Terraform/Helm, `.editorconfig`, scripts, and any other config — is off-limits to edit or create, no matter how small or "just tooling" it seems.

What's in bounds instead:
- Explaining concepts, patterns, and trade-offs (DDD, event sourcing, CQRS, Kafka, etc.) in the context of this codebase.
- Reviewing code/config the human wrote and pointing out issues verbally, without patching it.
- Small, isolated sample/reference snippets shown in chat to illustrate an explanation — not written to any project file.
- Reading code to answer questions ("why does X do Y", "where is Z handled").

Still in bounds as before: documentation (`README.md`, `docs/`, ADRs, `CLAUDE.md` itself) — those are collaborative by design and not part of "the implementation."

If a request is ambiguous about which side of this line it's on, ask rather than assume.

## Working conventions in this repo

- `docs/` is a living source of truth: update it in the same change as whatever makes it true, not as a follow-up pass.
- Any significant, hard-to-reverse technical decision (new service, storage choice, messaging pattern, deployment topology) gets an ADR — copy `docs/adr/template.md`, don't skip straight to implementation.
- ADRs are never edited after being marked Accepted; a changed decision is a new ADR that supersedes the old one.
- Content marked "Proposed" or "not yet decided" in the docs is genuinely open — don't build against it as if it were settled without flagging that it's still a choice.
- No roadmap file is kept in the repo (deliberately removed) — don't recreate one.
