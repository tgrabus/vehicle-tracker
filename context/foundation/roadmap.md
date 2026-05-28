---
project: "Vehicle Tracker"
version: 1
status: draft
created: 2026-05-27
updated: 2026-05-28
prd_version: 1
main_goal: market-feedback
top_blocker: capacity
---

# Roadmap: Vehicle Tracker

> Derived from `context/foundation/prd.md` (v1) + auto-researched codebase baseline.
> Edit-in-place; archive when superseded.
> Slices below are listed in dependency order. The "At a glance" table is the index.

## Vision recap

A vehicle owner has no single place to see what needs attention in their car or motorcycle and when. Service history is scattered — receipts in the glove compartment, mechanic notes, memory. The product bridges the gap: it combines OEM service schedule knowledge with the owner's personal service history and current mileage into one actionable status view, so the user always knows where they stand before the mechanic has to tell them.

## North star

**S-04: Core service loop — user logs a history entry and sees the service item status flip OK → Overdue on mileage update.**

> The north star — the smallest end-to-end slice whose successful delivery proves the core product hypothesis, placed as early as its prerequisites allow because everything else only matters if this works — is the moment the product becomes real: a user logs a past service, updates their current mileage, and the dashboard shows exactly what's due.

This maps directly to Primary Success Criterion steps 4–7 and is the single flow that validates the core insight: "I know the schedule rule + I know where I stand right now = actionable status."

## At a glance

| ID | Change ID | Outcome (user can …) | Prerequisites | PRD refs | Status |
|---|---|---|---|---|---|
| F-01 | db-context-scaffold | (foundation) data persistence layer wired to the database; schema migration infrastructure ready; no domain entities yet | — | FR-001 | done |
| F-02 | frontend-shell-scaffold | (foundation) UI component library installed and themed; authenticated app shell with navigation and routing structure; no feature screens, no auth logic | — | FR-019 | ready |
| S-01 | auth-flows | register, log in, and log out | F-01, F-02 | FR-001, FR-002, FR-003 | proposed |
| S-02 | vehicle-management | add a vehicle, edit its details, soft-delete it (history archived), and own multiple vehicles under one account | S-01 | FR-004, FR-006, FR-007, FR-008 | proposed |
| S-03 | service-catalog-setup | accept, reject, or modify suggested service items when adding a vehicle; edit any item's service interval | S-02 | FR-009, FR-013, FR-014 | proposed |
| S-04 | core-service-loop | add a history entry (mileage pre-filled), see the alert dashboard sorted Overdue → Approaching → OK, update mileage inline, and see status recalculate instantly | S-03 | FR-005, FR-015, FR-016, FR-018, FR-019, FR-021, US-01 | proposed |

## Baseline

What's already in place in the codebase as of 2026-05-27 (auto-researched + user-confirmed).
Foundations below assume these are present and do NOT re-scaffold them.

- **Frontend:** Partial — Angular 21 + AppRoutingModule scaffolded; routes array empty, no feature components, no Angular Material dependency yet. (`src/VehicleTracker.Frontend/src/app/`)
- **Backend / API:** Partial — ASP.NET Core 10 bootstrapped with health checks + OpenAPI; only a placeholder WeatherForecastController, no domain controllers, no Identity. (`src/VehicleTracker/Program.cs`)
- **Data:** Absent — PostgreSQL wired in AppHost.cs but no ApplicationDbContext, no EF Core packages, no migrations.
- **Auth:** Absent — No ASP.NET Core Identity packages; Program.cs carries a TODO: "add PersistKeysToAzureBlobStorage once ASP.NET Core Identity is implemented".
- **Deploy / infra:** Present — GitHub Actions CI/CD with azd deploy; AppHost.cs declares Azure Container Registry, ACA environment, and PostgreSQL Flexible Server. (`.github/workflows/ci-cd.yml`, `src/VehicleTracker.AppHost/AppHost.cs`)
- **Observability:** Partial — Aspire ServiceDefaults wires OpenTelemetry baseline; `/api/health/live` and `/api/health/ready` registered; no app-level error tracking or custom metrics. (`src/VehicleTracker.ServiceDefaults/Extensions.cs`)

## Foundations

### F-01: Database scaffold

- **Outcome:** (foundation) data persistence layer connected to the database; schema migration infrastructure in place — no domain entities yet. Each slice adds its own schema incrementally. One standard migration application path is defined so slice-owned schema changes apply consistently across dev and deploy environments.
- **Change ID:** db-context-scaffold
- **PRD refs:** FR-001
- **Unlocks:** S-01 (auth layer needs a data store; establishing the base here prevents a refactor later), and indirectly S-02, S-03, S-04 (each adds its own schema to the same store)
- **Prerequisites:** —
- **Parallel with:** F-02 (purely frontend; no overlap with EF Core backend work)
- **Blockers:** —
- **Unknowns:**
	- How should schema migrations run in this project: applied automatically on startup, or as a separate deployment step? Owner: Tomasz. Block: no.
- **Risk:** Minimal — purely additive infrastructure with no domain decisions. The migration application path must be settled here so slice-owned schema changes are not applied inconsistently across dev and deploy environments.
- **Status:** done

### F-02: App shell scaffold
- **Outcome:** (foundation) UI component library installed with a configured theme (colour palette, typography, density); authenticated app shell layout created with responsive navigation and a feature-screen slot; routing structure established so each slice adds its own routes consistently; no feature screens, no public layout, no auth guard — those are added slice by slice.
- **Change ID:** frontend-shell-scaffold
- **PRD refs:** FR-019
- **Unlocks:** S-01 (auth screens land in public layout S-01 creates; authenticated shell is ready for post-login redirect), S-02 (vehicle screens land in authenticated shell), S-03 (catalog screens land in authenticated shell), S-04 (alert dashboard lands in authenticated shell)
- **Prerequisites:** —
- **Parallel with:** F-01 (purely frontend; no overlap with EF Core backend work)
- **Blockers:** —
- **Unknowns:** —
- **Risk:** Theme choices (colour palette, typography, density) set here are inherited by every component in every feature slice — a theme change after S-02 ships costs rework across all screens. Settle on the palette before this foundation is marked done.
- **Status:** ready

## Slices

### S-01: Auth flows

- **Outcome:** user can register with email and password, log in, and log out; session-based authentication enforced on all protected routes; unauthenticated requests redirected to login; all subsequent user data scoped to the owning account.
- **Change ID:** auth-flows
- **PRD refs:** FR-001, FR-002, FR-003
- **Prerequisites:** F-01, F-02
- **Parallel with:** —
- **Blockers:** —
- **Unknowns:** —
- **Risk:** Register/login screens and the public layout are created in this slice; post-login redirect enters the authenticated shell from F-02. Data-isolation correctness — scoping all domain data to the owning user — is established here and must be verified before S-02 adds the first domain entity. The authentication convention defined here must be reusable unchanged by S-02 through S-04.
- **Status:** proposed

### S-02: Vehicle management

- **Outcome:** user can add a vehicle (make, model, year, plate, current mileage), edit its details, soft-delete it (service history archived, not erased), and own multiple vehicles under one account; alert dashboard displays the vehicle list with an empty service item state.
- **Change ID:** vehicle-management
- **PRD refs:** FR-004, FR-006, FR-007, FR-008
- **Prerequisites:** S-01
- **Parallel with:** —
- **Blockers:** —
- **Unknowns:** —
- **Risk:** FR-005 (mileage update as a first-class dashboard action, not buried in settings) is implemented in S-04 but the vehicle card layout should reserve space for the inline mileage update control to avoid a layout refactor in the north-star slice. This is also the first domain entity after auth — the data ownership pattern set here is the template for all subsequent domain entities.
- **Status:** proposed

### S-03: Service catalog setup

- **Outcome:** user sees 10 suggested service items when adding a vehicle, can accept, reject, or modify the list before saving; user can edit the default interval (km threshold, time threshold, logic type) for any service item.
- **Change ID:** service-catalog-setup
- **PRD refs:** FR-009, FR-013, FR-014
- **Prerequisites:** S-02
- **Parallel with:** —
- **Blockers:** —
- **Unknowns:** —
- **Risk:** The interval logic type (km-only, time-only, km-or-time) must be explicit in the domain model — S-04 reads it to choose the alert classification branch. Default catalog items and intervals are seeded during this slice; verify they match PRD FR-013 defaults before marking done.
- **Status:** proposed

### S-04: Core service loop *(north star)*

- **Outcome:** user can add a service history entry (date, mileage pre-filled from current vehicle mileage, cost and workshop optional), view the alert dashboard per vehicle with items sorted Overdue first then Approaching then OK, update current vehicle mileage inline on the dashboard, and see item statuses recalculate instantly without a full page reload.
- **Change ID:** core-service-loop
- **PRD refs:** FR-005, FR-015, FR-016, FR-018, FR-019, FR-021, US-01
- **Prerequisites:** S-03
- **Parallel with:** —
- **Blockers:** —
- **Unknowns:** —
- **Risk:** The correctness NFR ("alert status must never silently show OK when a service item is Overdue") is most tested by the km-or-time logic type, where both thresholds are evaluated independently and the more-exceeded one wins. A false-negative (missed Overdue) is a product failure per PRD; exhaustive test coverage of all three logic types is required before this slice is considered done.
- **Status:** proposed

## Backlog Handoff

| Roadmap ID | Change ID | Suggested issue title | Ready for `/10x-plan` | Notes |
|---|---|---|---|---|
| F-01 | db-context-scaffold | Wire data persistence layer to the database; set up schema migration infrastructure | yes | Run `/10x-plan db-context-scaffold` — parallel with F-02. |
| F-02 | frontend-shell-scaffold | Install UI component library and theme; build authenticated app shell with navigation and routing structure | yes | Run `/10x-plan frontend-shell-scaffold` — parallel with F-01. |
| S-01 | auth-flows | Implement registration, login, and logout; enforce session auth on protected routes; establish user-scoped data isolation | no | Awaiting F-01, F-02 |
| S-02 | vehicle-management | Add Vehicle entity + migration; build vehicle CRUD, soft-delete, multi-vehicle, empty dashboard | no | Awaiting S-01 |
| S-03 | service-catalog-setup | Build service catalog suggestion flow and interval editing | no | Awaiting S-02 |
| S-04 | core-service-loop | Build history entry, alert dashboard, and inline mileage-triggered recalculation | no | Awaiting S-03; north star |

## Open Roadmap Questions

1. **AI vs. OEM interval database** — For model-specific service schedules beyond the generic seeded defaults (FR-013), should the product use (a) a deterministic OEM interval database per make/model/engine, or (b) AI-assisted interval suggestion? Owner: Tomasz. Block: no (explicitly deferred to sprint 2; MVP ships with generic defaults; risk noted in PRD — AI-suggested intervals in a safety-relevant domain require "AI suggestion, not verified fact" labeling).
## Parked

- **Conditional catalog activation by fuel type, gearbox, and drivetrain (FR-010–012)** — Why parked: PRD §Non-Goals; adds conditional logic to v1 vehicle form; deferred to sprint 2.
- **Measurement-type service items for brake pad thickness (FR-017)** — Why parked: PRD §Non-Goals; keeps alert logic to 3 types in MVP.
- **90-day upcoming view (FR-020)** — Why parked: Priority: nice-to-have; not required by the primary success criterion.
- **Insurance module (FR-022–024)** — Why parked: PRD §Non-Goals; deferred to sprint 2.
- **Tires module (FR-025–031)** — Why parked: PRD §Non-Goals; largest FR-count module; deferred to sprint 2.
- **Technical inspection module (FR-032–034)** — Why parked: PRD §Non-Goals; analogous to insurance; deferred to sprint 2.
- **OEM API / VIN decode** — Why parked: PRD §Non-Goals; no free API for European cars; deferred to sprint 2.
- **File attachments (photos, PDF invoices)** — Why parked: PRD §Non-Goals; sprint 2.
- **Email notifications for insurance/inspection deadlines** — Why parked: PRD §Non-Goals; sprint 2.
- **PWA / push notifications** — Why parked: PRD §Non-Goals; sprint 2.
- **PDF report for vehicle sale** — Why parked: PRD §Non-Goals; sprint 3.
- **Fuel consumption tracking** — Why parked: PRD §Non-Goals; covered by Drivvo; not this product's core value.
- **Native mobile app** — Why parked: PRD §Non-Goals; Angular Material on mobile browser is sufficient for launch.
- **GPS / route tracking, OBD2 / diagnostics, workshop booking, fleet management** — Why parked: PRD §Non-Goals; different application domain, hardware requirement, or different customer segment.

## Done

(Empty on first generation. `/10x-archive` appends an entry here — and flips that item's `Status` to `done` — when a change whose `Change ID` matches the item is archived.)
