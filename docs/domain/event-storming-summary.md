# Event Storming Summary — Location-Sharing & Geofence Alerting Platform

**Workshop level:** Business process (big-picture) Event Storming
**Methodology:** Alberto Brandolini's Event Storming
**Status:** Model complete through all eight phases; open questions captured as hot spots.

---

## 1. What the system does

A mobile application that lets people share their real-time location with each other under explicit permission, and lets a user define geofences ("fences") on people who have shared their location with them. When a watched person crosses a fence, the fence's creator is alerted.

The domain has a distinctive three-role structure that emerged during the workshop:

- **Watcher** — creates a fence on another person and receives the alert when it is crossed.
- **Watched** — the person whose location is tracked and who crosses fences.
- **Permission relationship** — the consent that authorizes a watcher to see a watched person's location and therefore to fence them.

The central business rule: *a user may only create a fence on someone who has granted them permission to see their location, and a fence crossing alerts the fence's creator (the watcher), not the person who crossed.*

---

## 2. Bounded contexts / aggregates

Five aggregates were identified, each guarding its own consistency boundary:

- **User** — account lifecycle: registration, email verification, login, password change, deletion.
- **Permission** — the consent relationship: request, grant, deny (terminal), revoke, expire.
- **Location** — a person's current position and the periodic reporting of it.
- **Fence** — geofence definition (create/modify/delete) and detection of crossings.
- **Alert** — delivery of notifications to watchers when a crossing is detected.

The **Fence** aggregate owns the crossing decision (`Fence Crossed`), because a fence knows its own boundary and its owner. The **Alert** aggregate owns delivery (`Alert Sent`). This keeps *detection* and *delivery* as separate responsibilities.

---

## 3. Key event flows

**Account lifecycle:** Prospect User → Register User → *User Registered* → Verify Email → *Email Verified* → User Log in → *User Logged In*. Later, optional: Password Change and Delete User.

**Permission lifecycle:** an asker issues Request Permission → *Permission Requested*. The requested user then either Grants (→ *Permission Granted*) or Denies (→ *Permission Denied*, terminal). A granted permission may later be Revoked (by the granting user) or Expired (by the system on a timer).

**Location → Fence → Alert spine (the reactive core):**
1. Time policy: *every X seconds → send Location* → *Location Sent* → *Location Updated*.
2. Reactive policy: *whenever Location Updated → evaluate this person's fences* (permission is re-checked at this point).
3. Fence aggregate detects *Fence Crossed*.
4. Reactive policy: *whenever Fence Crossed → send alert to the fence's creator* (rate-limited).
5. Alert aggregate delivers → *Alert Sent*.

**User deletion cascade:** *whenever User Deleted →* three parallel cleanup policies delete the user's Locations, Permissions, and Fences across the respective aggregates.

---

## 4. Policies (reactive rules)

- **Time-based:** report location every X seconds; expire a granted permission when it reaches its expiry time.
- **Reactive spine:** location update triggers fence evaluation; fence crossing triggers alert delivery.
- **Cascade:** user deletion fans out to purge locations, permissions, and fences.
- **Deliberately not implemented:** revocation/expiry does *not* delete the fences it authorized. Instead, permission is re-checked at evaluation time, so an orphaned fence is inert while permission is absent and reactivates if permission is re-granted (accepted behavior — see ADR-002).

---

## 5. External systems (domain edges)

- **Device Hardware / GPS** (upstream) — source of raw position feeding `send Location`. Origin of the phantom-crossing risk; the app depends on device accuracy it does not control.
- **OAuth2 identity provider** (downstream) — account registration and authentication.
- **OpenStreetMap** (downstream) — map rendering for location views and fence creation.
- **SES / email provider** (downstream) — alert delivery. Currently email only.

---

# Business Decision Records

The following BDRs capture the significant decisions made (or explicitly deferred) during the workshop. Decisions still open are marked **Proposed**; positions the group actively took are marked **Accepted**.

---

## BDR-001: Fence aggregate owns crossing detection; Alert aggregate owns delivery

**Status:** Accepted
**Date:** 2026-07-15
**Deciders:** Architecture / backend team

### Context
Detecting that a fence was crossed requires both a fence definition and a current location. Ownership of that decision could sit on the Fence aggregate, the Location aggregate, or a dedicated third aggregate. Separately, sending the resulting alert is a distinct concern (recipients, channels, delivery guarantees).

### Decision
The **Fence** aggregate owns crossing detection and emits `Fence Crossed`. A reactive policy then triggers the **Alert** aggregate, which owns delivery and emits `Alert Sent`. Detection and delivery are separate aggregates connected by a policy.

### Options Considered

#### Option A: Fence owns detection, Alert owns delivery (chosen)
| Dimension | Assessment |
|-----------|------------|
| Complexity | Medium |
| Cohesion | High — each aggregate owns one responsibility |
| Coupling | Fence reads location data via read model |

**Pros:** Clean separation of detection vs. delivery; a fence naturally knows its own bounds and owner; delivery concerns (channels, rate limiting) stay out of the geometry logic.
**Cons:** Fence must read Location data it does not own; two aggregates to coordinate via policy.

#### Option B: Location owns detection
| Dimension | Assessment |
|-----------|------------|
| Complexity | Medium |
| Cohesion | Lower — location logic bloated with fence knowledge |

**Pros:** The moving entity knows where it is.
**Cons:** Location must know about every watcher's fences; couples a high-frequency write path to fence configuration.

#### Option C: Dedicated "Monitoring" aggregate
**Pros:** Neither Fence nor Location carries the duty; single home for crossing logic.
**Cons:** A new aggregate to justify and maintain; the "one owner → many fences" relationship makes Fence a natural enough home to not need it yet.

### Trade-off Analysis
Given that a fence has exactly one owner and one boundary, the fence is the most cohesive home for "am I crossed?" The cost — Fence reading location via a read model — is acceptable and localized. Introducing a separate monitoring aggregate adds structure without clear benefit at current scope.

### Consequences
- Easier: fence geometry and alert delivery evolve independently.
- Harder: the crossing evaluation depends on a location read model being fresh (ties into the phantom-crossing hot spot).
- Revisit: if performance at scale (many fences per watched user) forces a rethink, a dedicated monitoring aggregate may be reconsidered.

---

## BDR-002: Permission enforced at evaluation time, not cascaded on revocation

**Status:** Accepted
**Date:** 2026-07-15
**Deciders:** Architecture / backend team

### Context
A watcher may only fence a person who granted them permission. When that permission is revoked or expires, the fences it authorized could be (a) deleted immediately, or (b) left in place but gated by a permission check performed each time a location is evaluated.

### Decision
Permission is **re-checked at evaluation time** on every location update. Revocation/expiry does **not** delete the authorized fences. An unauthorized fence is inert (no alerts fire) while permission is absent, and reactivates automatically if permission is re-granted. This behavior is accepted intentionally.

### Options Considered

#### Option A: Enforce at read/evaluation time (chosen)
**Pros:** Simple write path (revocation just flips permission state); no cascade logic; single enforcement point.
**Cons:** Orphaned fences accumulate in storage; the entire privacy guarantee rests on one policy always running correctly; re-granting silently reactivates old fences.

#### Option B: Cascade-delete fences on revocation/expiry
**Pros:** No stale fences; privacy guarantee not concentrated in one runtime check.
**Cons:** More write-time coordination; re-granting requires re-creating fences (may or may not be desired UX).

### Trade-off Analysis
The team accepts the read-time model for its simplicity and because "re-grant reactivates prior fences" was judged acceptable UX. The residual risk is that the privacy guarantee is concentrated in the evaluation-time permission check — that check becomes safety-critical.

### Consequences
- Easier: revocation and expiry are trivial state changes.
- Harder: the evaluation-time permission check is now safety-critical and must never be skipped or bypassed; stale fences accumulate.
- Revisit: if orphaned-fence accumulation becomes a storage or audit concern, or if "silent reactivation" proves surprising to users, reconsider cascade-deletion.

---

## BDR-003: Permission scope model

**Status:** Accepted  
**Date:** 2026-07-15  
**Deciders:** Product + legal/privacy + backend team

### Context

The model currently treats a permission as a single, flat, undifferentiated grant. The grant/deny decision is the most privacy-sensitive moment in the application, and what it actually authorizes was never resolved during the workshop. Real consent decisions typically depend on scope.

### Decision

Adopt **Option A: flat boolean permission** for the initial Permission aggregate.

A permission will represent a single grant-or-deny decision. More granular scope dimensions—such as data type, precision, duration, and purpose—are explicitly out of scope for this release.

### Options Considered

#### Option A: Flat boolean permission

| Dimension | Assessment |
|-----------|------------|
| Complexity | Low |
| Privacy fitness | Low |
| Legal fitness | Low |

**Pros:** Simplest to build and explain in the initial grant/deny flow.  
**Cons:** Cannot express meaningful consent; may be inadequate for future privacy or legal requirements; could be expensive to retrofit once fences and alerts depend on it.

#### Option B: Scoped permission

Scope dimensions considered: data type (live position vs. history), precision (exact vs. approximate/city-level), duration (indefinite vs. time-boxed), and purpose (viewing vs. fencing/alerting).

**Pros:** Supports more informed consent; better aligns with data-protection expectations; enables a more meaningful grant/deny UI.  
**Cons:** Requires a more complex aggregate and UI; scope must be enforced everywhere location is read.

### Trade-off Analysis

We chose the simpler flat permission model to reduce initial delivery complexity. This accepts the risk that consent may need to become more granular later. The Permission aggregate and dependent location-read paths should therefore remain straightforward to extend without assuming that a boolean grant will be permanent.

### Consequences

- Easier: initial Permission aggregate, grant/deny UI, and authorization checks.
- Harder: supporting granular consent or changing privacy requirements later.
- Risk: the model may require a costly migration if scoped permissions become mandatory.
- Revisit: after legal review or before introducing location history, approximate location, time-limited sharing, or purpose-specific location access.

---

## BDR-004: User deletion cascade

**Status:** Accepted
**Date:** 2026-07-15
**Deciders:** Backend team

### Context
A deleted user may own permissions (granted and received), fences, and accumulated location data. Without coordinated cleanup, deletion leaves orphaned data and potentially fences firing on a deleted person.

### Decision
`User Deleted` triggers three parallel cleanup policies that delete the user's Locations, Permissions, and Fences in their respective aggregates.

### Consequences
- Easier: account deletion produces a consistent, data-protection-friendly cleanup.
- Harder: the cascade must be reliable and ideally idempotent; partial failure needs handling.
- Revisit: alongside the data-retention decision, since routine retention is a separate concern from deletion.
