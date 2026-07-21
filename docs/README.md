# Documentation

The living source of truth for WhereUAt's domain, architecture, and decisions — everything that doesn't fit in the top-level [`README.md`](../README.md).

## Structure

- [`domain/`](domain/) — outputs of domain discovery: Event Storming workshop notes, business decision records (BDRs).
- [`architecture/`](architecture/) — current architecture: bounded-context/service mapping, diagrams, open cross-cutting concerns.
- [`adr/`](adr/) — Architecture Decision Records: the numbered, dated log of significant technical decisions and why they were made.

## Conventions

- Docs are updated in the same change as whatever makes them true — no separate "doc debt" pass.
- ADRs are never edited after being Accepted; a changed decision gets a new ADR that supersedes the old one.
- Anything marked **Proposed** or listed under "not yet decided" is genuinely open — flag it if you disagree.
