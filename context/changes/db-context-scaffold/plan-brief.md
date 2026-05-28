# Database Context Scaffold — Plan Brief

> Full plan: `context/changes/db-context-scaffold/plan.md`

## What & Why

Wire EF Core + Npgsql to the Aspire-managed PostgreSQL database so every subsequent slice (S-01 through S-04) has a place to add schema. This is pure infrastructure — no domain entities, no feature code. Without it, the auth layer (S-01) cannot land.

## Starting Point

`AppHost.cs` already declares a PostgreSQL Flexible Server and injects the `vehicletracker` connection string into the API via `WithReference(postgres)`. The API project has no EF Core packages, no `ApplicationDbContext`, and no migrations. The CI pipeline builds and vulnerability-scans the code but does not validate migrations.

## Desired End State

A `VehicleTracker.Data` class library owns `ApplicationDbContext` and the migration history. The API registers the context via the Aspire Npgsql integration. In local dev, the Aspire dashboard shows an `api-migrations` resource that finishes before the API starts. On deploy, `azd up` runs the migration as a Container App Job. CI rejects any PR with a broken or out-of-sync migration.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
|---|---|---|---|
| Migration execution strategy | Aspire `AddEFMigrations` (not auto-migrate on startup, not CI-only `dotnet ef database update`) | Ties migrations into the Aspire orchestrator for local dev and generates a Container App Job for deploy — no CI secrets for DB connection needed. | Plan |
| DbContext project | Separate `VehicleTracker.Data` class library | User preference; clean separation of persistence boundary; matches Aspire's recommended pattern for `WithMigrationsProject`. | Plan |
| Initial migration | Empty `InitialCreate` | Proves the toolchain end-to-end before any slice adds schema; establishes `__EFMigrationsHistory` baseline. | Plan |
| CI migration gate | `dotnet ef migrations script --idempotent` in `backend-quality` | Catches broken migration code on every PR without a live database. | Plan (user confirmed) |

## Scope

**In scope:**
- `VehicleTracker.Data` class library with `ApplicationDbContext` and `IDesignTimeDbContextFactory`
- Aspire Npgsql integration in the API (`AddNpgsqlDbContext`)
- `dotnet-ef` local tool manifest (`.config/dotnet-tools.json`)
- `Migrations/InitialCreate` (empty migration, proves toolchain)
- `AddEFMigrations` wiring in `AppHost.cs` with `RunDatabaseUpdateOnStart()` + Container App Job publish
- CI `backend-quality`: `dotnet ef migrations script --idempotent` step
- CI `deploy`: `dotnet tool restore` before `azd up`

**Out of scope:**
- Domain entities (`DbSet<>`) — added slice by slice starting with S-01
- Seed data
- Separate migration worker project (replaced by `AddEFMigrations`)
- Any feature code

## Architecture / Approach

Single `VehicleTracker.Data` library holds the DbContext and migrations. The API project references it and registers the context via `builder.AddNpgsqlDbContext<ApplicationDbContext>("vehicletracker")`. The AppHost references the Data project to resolve `Projects.VehicleTracker_Data` and calls `api.AddEFMigrations("api-migrations").WithMigrationsProject<Projects.VehicleTracker_Data>()`. In local dev, `RunDatabaseUpdateOnStart()` runs `dotnet ef database update` as an Aspire resource; in deploy, `PublishAsMigrationBundle(publishContainer: true) + PublishAsAzureContainerAppJob()` deploys it as a one-shot ACA Job. CI generates an idempotent SQL script from code — no DB required.

## Phases at a Glance

| Phase | What it delivers | Key risk |
|---|---|---|
| 1. Scaffold VehicleTracker.Data | Class library, DbContext, design-time factory, project references, Aspire Npgsql registration in API | Package version mismatch between `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` and EF Core 10.x |
| 2. Create InitialCreate migration | `.config/dotnet-tools.json`, `Migrations/InitialCreate.*` files committed | `IDesignTimeDbContextFactory` not discovered by tooling (resolved by placing it in the Data project) |
| 3. Wire AddEFMigrations + CI gate | AppHost orchestration, Container App Job publish, CI validation step | `Projects.VehicleTracker_Data` not resolving in AppHost (requires project reference added in Phase 1) |

**Prerequisites:** None — F-01 has no upstream dependencies per the roadmap.
**Estimated effort:** ~1 session across 3 phases.

## Open Risks & Assumptions

- `Aspire.Hosting.EntityFrameworkCore` version must match Aspire 13.3.5 — verify on NuGet before installing
- `dotnet ef migrations bundle` (called by `PublishAsMigrationBundle`) targets `linux-x64` by default; the ACA Job runs on Linux so this is correct, but verify if the build agent architecture differs

## Success Criteria (Summary)

- `dotnet build` passes on all three projects (Data, API, AppHost) with no warnings
- Aspire dashboard shows `api-migrations → Finished` before `api` goes green in local dev
- CI `backend-quality` fails fast when a PR has a broken migration snapshot; passes cleanly for this baseline
