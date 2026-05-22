---
project: "Vehicle Tracker"
context_type: greenfield
created: 2026-05-21
updated: 2026-05-22
checkpoint:
  current_phase: 8
  phases_completed: [1, 2, 3, 4, 5, 6, 7]
  gray_areas_resolved:
    - topic: "pain_category"
      decision: "Data trapped somewhere — history, dates, km — no single actionable view"
    - topic: "persona_scope"
      decision: "Single named pilot user (Tomasz); generalizes later to private vehicle owners"
    - topic: "insight"
      decision: "Bridges OEM schedule knowledge + personal service history + current mileage into one status view"
    - topic: "auth_model"
      decision: "Email + password, flat user model, no roles, no vehicle sharing in MVP"
    - topic: "mvp_scope"
      decision: "Week 1 = core flow only: auth + 1 vehicle (5 fields) + service catalog (10 base items, no conditional) + intervals + history + alert dashboard (3 logic types)"
    - topic: "success_secondary"
      decision: "Service history entry pre-fills current mileage; user can override before saving"
    - topic: "oem_interval"
      decision: "Single seeded-default editable interval per item; no OEM/custom distinction in MVP"
    - topic: "vehicle_delete"
      decision: "Soft-delete — history archived, not permanently removed"
    - topic: "catalog_model"
      decision: "System suggests base items on vehicle creation; user accepts/rejects; items not imposed"
    - topic: "non_goals_scope"
      decision: "Insurance (FR-022-024), tires (FR-025-031), technical inspection (FR-032-034), conditional catalog (FR-010-012), and measurement type (FR-017) deferred to sprint 2"
  frs_drafted: 17
  quality_check_status: accepted
---

## Vision & Problem Statement

A vehicle owner has no single place to see what needs attention in their car or motorcycle and when. Service history lives in their head, in receipts in the glove compartment, or at the mechanic. Inspection dates pass unnoticed. Insurance expires a week early. Tires age past seven years because nobody checked the DOT date.

The insight: OEM service schedules are public knowledge, and every owner has a history of past work somewhere — but no tool combines these with the owner's current mileage into a personal, actionable status view. The gap between "I know the rule" and "I know where I stand right now" is the product.

## User & Persona

**Primary persona:** Tomasz — owner of 1–2 vehicles (car + motorcycle). Regularly services but doesn't remember what or when. Not a mechanic. Wants a simple tool that tells him what to do before it's too late. Reaches for the product when the mechanic mentions something he wasn't tracking, or when an insurance renewal reminder doesn't arrive.

**Persona scope:** Single named pilot user for the MVP. The persona generalizes to private vehicle owners who self-track service — not fleet managers, not professional mechanics.

## Access Control

Email and password registration and login. Flat user model — one account per user, no roles, no vehicle sharing in MVP. Each user sees only their own vehicles and service data. An unauthenticated user hitting a gated route is redirected to login.

## Success Criteria

### Primary

The E2E flow works:

1. Register
2. Add vehicle (make, model, year, plate, mileage, drivetrain, gearbox type, fuel type)
3. Add service item with interval
4. Add service history entry (date, mileage at service, what was done)
5. Dashboard shows service item status as OK
6. Update current vehicle mileage +15,000 km
7. Dashboard shows same service item status as Overdue

### Secondary

- In-app alert notifies the user 30 and 7 days before insurance policy expiry (sprint 2).
- In-app alert notifies the user 30 and 7 days before technical inspection deadline (sprint 2).

### Guardrails

- Data isolation per user: no cross-user data leakage (User A never sees User B's vehicles).
- Alert calculation correctness: no false-OK state — showing OK when an item is Overdue is worse than showing Overdue when it isn't.
- Mileage update recalculates dashboard without a full page reload — user sees the status change instantly.
- App is usable on a mobile browser (Chrome/Safari on phone) without a native app install.

## User Stories

### US-01: Core E2E — service item flips from OK to Overdue

- **Given** a registered user with a vehicle added and a km-or-time service item configured with a 15,000 km / 12-month interval
- **When** they add a service history entry (date, mileage at service, what was done)
- **Then** the service item status on the dashboard shows OK

- **Given** the service item shows OK and the user updates current vehicle mileage by +15,000 km
- **When** the dashboard refreshes
- **Then** the same service item status shows Overdue

#### Acceptance Criteria

- Status transitions happen without a full page reload after mileage update.
- An item that is Overdue by km must show Overdue even if the time threshold has not been reached.
- An item that is Overdue by time must show Overdue even if the km threshold has not been reached.

## Functional Requirements

### Authentication

- FR-001: User can register with email and password. Priority: must-have
- FR-002: User can log in with email and password. Priority: must-have
- FR-003: User can log out. Priority: must-have

### Vehicles

- FR-004: User can add a vehicle with make, model, year, license plate, and current mileage. Priority: must-have
- FR-005: User can update current mileage for a vehicle from the dashboard (mileage update is a first-class dashboard action, not buried in settings). Priority: must-have
  > Socrates: Counter-argument considered: "if mileage update requires navigation to a settings page, users stop updating it and all alert logic breaks." Resolution: kept as must-have; UX placement is a hard constraint — must be reachable from the main dashboard view without navigating away.
- FR-006: User can edit vehicle details. Priority: must-have
- FR-007: User can remove a vehicle; the vehicle's history is preserved as an archived record and remains accessible, rather than being permanently erased. Priority: must-have
  > Socrates: Counter-argument considered: "permanent deletion would destroy service history with no recovery path." Resolution: FR revised — removal archives the vehicle's data; the history record is retained.
- FR-008: User can own multiple vehicles under one account. Priority: must-have

### Service Catalog

- FR-009: System suggests the standard 10 base service items when a new vehicle is added; user accepts, rejects, or modifies the list before it is saved (items are not imposed). Priority: must-have
  > Socrates: Counter-argument considered: "users don't want a pre-imposed catalog they must clean up." Resolution: FR revised to a suggestion model — system proposes, user confirms. No item activates without user acceptance.

### Intervals

- FR-013: System pre-seeds one editable interval per service item using the catalog default values (as defined in the product brief: e.g. engine oil 15,000 km / 12 months; brake fluid 24 months). No OEM/custom distinction in MVP — a single editable interval per item. Priority: must-have
  > Socrates: Counter-argument considered: "sourcing actual OEM intervals per make/model is research work that blocks launch." Resolution: FR revised — system uses one seeded default interval from the brief's catalog; no OEM/custom split in MVP.
- FR-014: User can edit the interval for any service item. Priority: must-have

### Service History

- FR-015: User can add a service history entry for an interval-type service item (date, mileage at service, cost, workshop, notes — cost and workshop are optional). Priority: must-have
- FR-016: When adding a service history entry, system pre-fills the mileage field from the current vehicle mileage; user can override the pre-filled value. Priority: must-have
  > Socrates: Counter-argument considered: "prompting for mileage on every entry is annoying if the user just updated mileage." Resolution: FR revised to pre-fill from current mileage rather than prompt from scratch; override is always available.

### Alert Calculation

- FR-018: System classifies each service item status as Overdue / Approaching / OK using the correct alert logic type: km-only (distance since last entry vs. interval km), time-only (time since last entry vs. interval months), km-or-time (whichever threshold is reached first). Approaching zone: less than 1,000 km remaining OR less than 30 days remaining. Priority: must-have
- FR-019: User can view an alert dashboard per vehicle with service items sorted Overdue first, then Approaching, then OK. Priority: must-have
- FR-020: User can view a 90-day upcoming view showing items whose due date or mileage falls within the next 90 days. Priority: nice-to-have
- FR-021: User can view the history entries for a service item (list of past entries with date, mileage, cost, workshop, notes). Priority: must-have

## Non-Functional Requirements

- User-perceived dashboard load and mileage recalculation response time is less than 2 seconds at p95.
- Each user's vehicle and service data is inaccessible to other users — no cross-user data leakage across account boundaries.
- Alert status must never silently show OK when a service item is Overdue. A false-negative (missed Overdue) is a product failure; a false-positive (unnecessary Approaching) is tolerable.
- App is usable on a mobile browser (Chrome/Safari on a smartphone) without a native app installation.
- No third-party analytics or tracking scripts are loaded in MVP.
- All traffic is HTTPS only. Plain HTTP is not served.

## Business Logic

For each service item, the system applies the correct alert logic type: compares the last history entry against current mileage and current date, and classifies status as Overdue, Approaching, or OK.

Three logic types govern the classification:

1. **km-only** — the system computes kilometres driven since the last service entry and compares against the item's interval. Distance at or beyond the interval is Overdue; within 1,000 km of the interval is Approaching.
2. **time-only** — the system computes months elapsed since the last service entry and compares against the item's interval. Time at or beyond the interval is Overdue; within 30 days of the interval is Approaching.
3. **km-or-time** — the system runs both km and time comparisons; whichever threshold is reached first determines the status. A service item where km are fine but time has elapsed is still Overdue.

The user encounters the rule through the alert dashboard, where every service item carries a status indicator, and through the mileage update flow, where updating the vehicle's current mileage triggers immediate recalculation of all km-dependent items.

## Non-Goals

- Conditional service catalog activation by fuel type, gearbox type, and drivetrain (FR-010–012) — deferred to sprint 2; simplifies vehicle form to 5 fields and removes conditional logic from v1.
- Measurement-type service items for brake pad thickness in mm (FR-017) — deferred to sprint 2; keeps alert logic to 3 types.
- Insurance module (FR-022–024) — deferred to sprint 2; no overlap with US-01 core flow.
- Tires module (FR-025–031) — deferred to sprint 2; largest module by FR count.
- Technical inspection module (FR-032–034) — deferred to sprint 2; analogous to insurance.
- OEM API or VIN decode integration — no free API for European cars; deferred to sprint 2.
- File attachments (photos, PDF invoices) — sprint 2.
- Service cost estimation — sprint 2.
- Email notifications for insurance and inspection deadlines — deferred to sprint 2.
- PWA or push notifications — sprint 2.
- PDF report for vehicle sale — sprint 3.
- Fuel consumption tracking — Drivvo covers this; not the core value of this product.
- GPS or route tracking — different application.
- OBD2 or diagnostics integration — requires hardware.
- Workshop appointment booking — too complex for MVP.
- Native mobile app — PWA is sufficient for launch.
- Fleet management for business — different segment, different product.

## Open Questions

- **AI vs. OEM interval database:** The seeded catalog uses generic default intervals (FR-013). For model-specific service schedules (e.g., "Audi A4 45 TFSI at 60,000 km requires gearbox oil + spark plugs"), two paths exist: (a) deterministic OEM interval database per make/model/engine (the conditional catalog, FR-010–012, already deferred to sprint 2), or (b) AI-assisted interval suggestion — LLM answers "what's due at 60k km for this vehicle?" instead of maintaining a manual database. Decision deferred: evaluate after MVP ships and after stack selection. Risk to flag: AI-suggested intervals in a safety-relevant domain require explicit "AI suggestion, not verified fact" labeling; hallucinated intervals could mean a missed critical service.
