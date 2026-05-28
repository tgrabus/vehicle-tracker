# Database Context Scaffold Implementation Plan

## Overview

Wire EF Core + Npgsql to the Aspire-managed PostgreSQL database in a dedicated `VehicleTracker.Data` class library. Create the empty `InitialCreate` migration that establishes the migration history baseline. Integrate `AddEFMigrations` in the AppHost so migrations run automatically via `dotnet ef database update` in local dev and deploy as an Azure Container App Job during `azd up`. Add `dotnet ef migrations script --idempotent` to the CI `backend-quality` job so every PR validates migration coherence before deploy.

## Current State Analysis

- `VehicleTracker.csproj` — no EF Core or Npgsql packages; no `ApplicationDbContext`
- `Program.cs` — no DbContext registration; carries a `TODO` for `PersistKeysToAzureBlobStorage`
- `AppHost.cs` — PostgreSQL Flexible Server declared; `AddDatabase("vehicletracker")` injects the `vehicletracker` connection string into the API via `WithReference(postgres)`; runs as a Docker container with a data volume in dev
- No `Migrations/` folder anywhere in the solution
- `.github/workflows/ci-cd.yml` — `backend-quality` job builds and vulnerability-scans but does not validate migrations; `deploy` job runs `azd up` directly
- `Aspire.Hosting.Azure.AppContainers` 13.3.5 already in AppHost — `PublishAsAzureContainerAppJob()` is available without extra packages

## Desired End State

A `VehicleTracker.Data` class library owns `ApplicationDbContext` and an `IDesignTimeDbContextFactory` for EF tooling. The API project registers the context via the Aspire Npgsql integration. A `Migrations/InitialCreate` migration establishes the baseline. `AddEFMigrations` in the AppHost runs `dotnet ef database update` before the API starts in local dev (visible in the Aspire dashboard) and deploys as a Container App Job before the API replica starts on Azure. CI rejects any PR where migrations are broken or the snapshot is out of sync.

### Key Discoveries

- `postgres` in `AppHost.cs` is the **database** resource (`IResourceBuilder<AzurePostgresFlexibleServerDatabaseResource>`), not the server — `.WithReference(postgres)` on the migration resource takes this type directly
- `Aspire.Hosting.EntityFrameworkCore` (new in Aspire 13.x) exposes `AddEFMigrations` with `RunDatabaseUpdateOnStart()` for local dev and `PublishAsMigrationBundle(publishContainer: true)` + `PublishAsAzureContainerAppJob()` for deploy — no separate migration worker project needed
- `IDesignTimeDbContextFactory<ApplicationDbContext>` must live in `VehicleTracker.Data` so `dotnet ef` can instantiate the context without the Aspire DI container (required for both local `migrations add` and CI `migrations script`)
- `dotnet ef migrations script --idempotent` does **not** need a live database — it generates SQL from migration code; no DB service container in CI required
- `PublishAsMigrationBundle(publishContainer: true)` wraps the bundle in a container image; `PublishAsAzureContainerAppJob()` deploys it as a one-shot ACA Job that runs before the API container starts — `Aspire.Hosting.Azure.AppContainers` is already in AppHost
- AppHost needs a **project reference** to `VehicleTracker.Data` so `Projects.VehicleTracker_Data` is generated and `WithMigrationsProject<Projects.VehicleTracker_Data>()` compiles

## What We're NOT Doing

- No domain entities — `ApplicationDbContext` has no `DbSet<>` properties; those are added slice by slice (S-01 onwards)
- No seed data — seeding belongs to S-01 (Identity) and later slices
- No separate migration worker project — `AddEFMigrations` replaces the manual worker approach shown in older Aspire docs
- No separate `DatabaseUrl` secret in CI — `dotnet ef migrations script` needs no live DB
- No changes to the Angular frontend or any feature code

## Implementation Approach

Create a `VehicleTracker.Data` class library that owns the persistence layer boundary. Register the Aspire Npgsql integration in the API project. Use Aspire's `AddEFMigrations` API to tie migration execution into the orchestrator for both local dev and deploy. Gate every PR with a CI migration script generation step that catches broken migration code before it reaches Azure.

## Critical Implementation Details

**Design-time factory connection string** — the `IDesignTimeDbContextFactory` hardcodes a localhost Postgres connection string used only by `dotnet ef` tooling; it is never executed at runtime. The Aspire integration overrides the connection string at runtime via the injected `ConnectionStrings__vehicletracker` environment variable.

**`api.WaitForCompletion(apiMigrations)` must be called after the variable is defined** — `api` is defined first (fluent chain), then `apiMigrations = api.AddEFMigrations(...)`, then `api.WaitForCompletion(apiMigrations)` as a separate statement. This ordering is required because `apiMigrations` is defined from `api`.

**`dotnet-ef` tool required at `aspire publish` time** — `PublishAsMigrationBundle` runs `dotnet ef migrations bundle` during `azd up`; the deploy CI job must call `dotnet tool restore` before `azd up`.

---

## Phase 1: Scaffold VehicleTracker.Data and Wire Aspire DB Registration

### Overview

Create the `VehicleTracker.Data` class library with `ApplicationDbContext` and the design-time factory. Add project references to connect it to the API and AppHost. Register the Aspire Npgsql integration in `Program.cs` so the API receives the connection string at runtime.

### Changes Required

#### 1. New project: `VehicleTracker.Data`

**File**: `src/VehicleTracker.Data/VehicleTracker.Data.csproj`

**Intent**: Create a `Microsoft.NET.Sdk` class library targeting `net10.0` that owns the EF Core persistence layer. This project will accumulate `DbSet<>` properties and `OnModelCreating` configuration as each slice adds its schema.

**Contract**: Package references required — `Microsoft.EntityFrameworkCore` (10.x), `Npgsql.EntityFrameworkCore.PostgreSQL` (10.x), `Microsoft.EntityFrameworkCore.Design` (10.x, `PrivateAssets="All"`). `Nullable` and `ImplicitUsings` enabled. `TreatWarningsAsErrors` enabled (matches the API project convention).

#### 2. Add project to solution

**File**: `src/VehicleTracker.slnx`

**Intent**: Register `VehicleTracker.Data` in the solution so `dotnet build` and `dotnet restore` at the solution level include it.

**Contract**: Add a `<Project Path="VehicleTracker.Data/VehicleTracker.Data.csproj" />` entry.

#### 3. `ApplicationDbContext`

**File**: `src/VehicleTracker.Data/ApplicationDbContext.cs`

**Intent**: Define the root EF Core DbContext for the application. Empty at this stage — no `DbSet<>` properties. Each slice adds its own entity configuration here.

**Contract**: `public sealed class ApplicationDbContext : DbContext` with a constructor accepting `DbContextOptions<ApplicationDbContext>`. Namespace `VehicleTracker.Data`.

#### 4. `ApplicationDbContextFactory` (design-time factory)

**File**: `src/VehicleTracker.Data/ApplicationDbContextFactory.cs`

**Intent**: Allow `dotnet ef` tooling to instantiate `ApplicationDbContext` without the Aspire DI container. Used only when running `dotnet ef migrations add` or `dotnet ef migrations script` locally and in CI.

**Contract**: Implements `IDesignTimeDbContextFactory<ApplicationDbContext>`. Reads the connection string from the `ConnectionStrings__vehicletracker` environment variable; falls back to a localhost Postgres default so the factory works on a clean dev machine without env setup. The environment-variable name must match what Aspire injects at runtime (`vehicletracker` database name → `ConnectionStrings__vehicletracker`).

```csharp
public ApplicationDbContext CreateDbContext(string[] args)
{
    var connectionString =
        Environment.GetEnvironmentVariable("ConnectionStrings__vehicletracker")
        ?? "Host=localhost;Port=5432;Database=vehicletracker;Username=postgres;Password=postgres";

    var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
    optionsBuilder.UseNpgsql(connectionString);
    return new ApplicationDbContext(optionsBuilder.Options);
}
```

#### 5. Project reference: `VehicleTracker` → `VehicleTracker.Data`

**File**: `src/VehicleTracker/VehicleTracker.csproj`

**Intent**: Make `ApplicationDbContext` available to the API project for DI registration.

**Contract**: Add `<ProjectReference Include="..\VehicleTracker.Data\VehicleTracker.Data.csproj" />`.

#### 6. Aspire Npgsql integration package in `VehicleTracker`

**File**: `src/VehicleTracker/VehicleTracker.csproj`

**Intent**: Add the Aspire-integrated Npgsql EF Core package that registers the DbContext with OpenTelemetry, health checks, and retry pipelines wired to Aspire service defaults.

**Contract**: `<PackageReference Include="Aspire.Npgsql.EntityFrameworkCore.PostgreSQL" Version="..." />` — use the version consistent with Aspire 13.3.5 (current in AppHost). This package exposes the `AddNpgsqlDbContext<TContext>` extension used in step 7.

#### 7. Register DbContext in `Program.cs`

**File**: `src/VehicleTracker/Program.cs`

**Intent**: Register `ApplicationDbContext` in the DI container using the Aspire integration so it picks up the `vehicletracker` connection string, health checks, and OpenTelemetry automatically.

**Contract**: Add `builder.AddNpgsqlDbContext<ApplicationDbContext>("vehicletracker");` after `builder.AddServiceDefaults()` and before `var app = builder.Build()`. The string `"vehicletracker"` must match the database name in `AppHost.cs` (`AddDatabase("vehicletracker")`).

#### 8. Project reference: `VehicleTracker.AppHost` → `VehicleTracker.Data`

**File**: `src/VehicleTracker.AppHost/VehicleTracker.AppHost.csproj`

**Intent**: Make `Projects.VehicleTracker_Data` available to AppHost so `WithMigrationsProject<Projects.VehicleTracker_Data>()` compiles in Phase 3.

**Contract**: Add `<ProjectReference Include="..\VehicleTracker.Data\VehicleTracker.Data.csproj" />`.

### Success Criteria

#### Automated Verification

- `dotnet build src/VehicleTracker.Data/VehicleTracker.Data.csproj --configuration Release` succeeds with no warnings
- `dotnet build src/VehicleTracker/VehicleTracker.csproj --configuration Release` succeeds with no warnings
- `dotnet build src/VehicleTracker.AppHost/VehicleTracker.AppHost.csproj --configuration Release` succeeds with no warnings

#### Manual Verification

- Aspire dashboard shows a `vehicletracker` database health check entry when running the AppHost locally (confirms Aspire Npgsql integration is active)

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to Phase 2. Phase blocks use plain bullets — the corresponding `- [ ]` checkboxes for these items live in the `## Progress` section at the bottom of the plan.

---

## Phase 2: Create InitialCreate Migration

### Overview

Install the `dotnet-ef` CLI tool via a local tool manifest and run `dotnet ef migrations add InitialCreate` to create the baseline migration. The migration will be empty (no tables) because the DbContext has no `DbSet<>` properties yet — its purpose is to prove the migration toolchain works end-to-end before any slice adds schema.

### Changes Required

#### 1. Local tool manifest

**File**: `.config/dotnet-tools.json`

**Intent**: Pin the `dotnet-ef` CLI tool version to the repo so every developer and CI agent restores the same tool version via `dotnet tool restore`. This is the standard .NET local tool pattern.

**Contract**: Create `.config/dotnet-tools.json` using `dotnet new tool-manifest` then install the tool with `dotnet tool install dotnet-ef`. Use the version of `dotnet-ef` matching the EF Core 10.x package version chosen in Phase 1. The manifest is committed to source control.

#### 2. Run `dotnet ef migrations add InitialCreate`

**File**: `src/VehicleTracker.Data/Migrations/` (generated)

**Intent**: Generate the `InitialCreate` migration and snapshot. The migration will be empty — no `Up` or `Down` body beyond the scaffolded methods — because `ApplicationDbContext` currently has no entity types. This establishes the migrations folder, registers the migration in the `__EFMigrationsHistory` table on first run, and proves the `IDesignTimeDbContextFactory` works.

**Contract**: Run from the repo root:
```
dotnet tool restore
dotnet ef migrations add InitialCreate \
  --project src/VehicleTracker.Data/VehicleTracker.Data.csproj \
  --startup-project src/VehicleTracker/VehicleTracker.csproj
```
The generated files — `<timestamp>_InitialCreate.cs`, `<timestamp>_InitialCreate.Designer.cs`, and `ApplicationDbContextModelSnapshot.cs` — all land in `src/VehicleTracker.Data/Migrations/` and are committed to source control.

### Success Criteria

#### Automated Verification

- `src/VehicleTracker.Data/Migrations/` folder exists with three generated files
- `dotnet tool restore` completes without error
- `dotnet ef migrations script --idempotent --project src/VehicleTracker.Data/VehicleTracker.Data.csproj --startup-project src/VehicleTracker/VehicleTracker.csproj` exits 0 and emits valid SQL (confirms snapshot is coherent and migration compiles)

#### Manual Verification

- The generated migration's `Up` method body is empty (no `CreateTable` calls) — expected because `ApplicationDbContext` has no entities yet

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to Phase 3.

---

## Phase 3: Wire AddEFMigrations in AppHost and Add CI Validation

### Overview

Add `Aspire.Hosting.EntityFrameworkCore` to the AppHost project and wire `AddEFMigrations` so migrations run automatically before the API starts in local dev and deploy as a Container App Job in Azure. Add `dotnet tool restore` + `dotnet ef migrations script --idempotent` to the CI `backend-quality` job so every PR validates migration coherence before any deployment.

### Changes Required

#### 1. `Aspire.Hosting.EntityFrameworkCore` package in AppHost

**File**: `src/VehicleTracker.AppHost/VehicleTracker.AppHost.csproj`

**Intent**: Pull in the `AddEFMigrations` extension method for the AppHost orchestrator.

**Contract**: `<PackageReference Include="Aspire.Hosting.EntityFrameworkCore" Version="..." />` — use version consistent with Aspire 13.3.5.

#### 2. Wire `AddEFMigrations` in AppHost

**File**: `src/VehicleTracker.AppHost/AppHost.cs`

**Intent**: Register a migration resource derived from the API project that runs `dotnet ef database update` before the API starts in local dev, and generates a Container App Job migration bundle during `aspire publish` / `azd up`. The API must wait for migrations to complete before accepting traffic.

**Contract**: After the `api` variable is defined, add two statements:

```csharp
var apiMigrations = api.AddEFMigrations("api-migrations")
    .WithMigrationsProject<Projects.VehicleTracker_Data>()
    .WithReference(postgres)
    .WaitFor(postgres)
    .RunDatabaseUpdateOnStart()
    .PublishAsMigrationBundle(publishContainer: true)
    .PublishAsAzureContainerAppJob();

api.WaitForCompletion(apiMigrations);
```

`WithMigrationsProject<Projects.VehicleTracker_Data>()` tells Aspire the migrations live in the Data project, not in the API project. `RunDatabaseUpdateOnStart()` only affects local run — it has no effect during `aspire publish`. `PublishAsAzureContainerAppJob()` requires `Aspire.Hosting.Azure.AppContainers` which is already in the AppHost.

#### 3. Add migration validation to CI `backend-quality` job

**File**: `.github/workflows/ci-cd.yml`

**Intent**: Reject any PR where the migration model snapshot is out of sync with the migration history, or where migration C# fails to compile. This step runs on every push and PR, not only on deploy, so broken migrations never reach `azd up`.

**Contract**: In the `backend-quality` job, after the existing `dotnet build` steps and before the vulnerability scan, add:

```yaml
- name: Restore dotnet tools
  run: dotnet tool restore

- name: Validate migrations (idempotent SQL script)
  run: >
    dotnet ef migrations script --idempotent
    --project src/VehicleTracker.Data/VehicleTracker.Data.csproj
    --startup-project src/VehicleTracker/VehicleTracker.csproj
    --output /tmp/migrations.sql
```

No live database is required — EF generates SQL from code.

#### 4. Add `dotnet tool restore` to CI `deploy` job

**File**: `.github/workflows/ci-cd.yml`

**Intent**: `PublishAsMigrationBundle` runs `dotnet ef migrations bundle` during `azd up`; the `dotnet-ef` tool must be available in the deploy job.

**Contract**: In the `deploy` job, add `run: dotnet tool restore` after `actions/setup-dotnet` and before `azd up`.

### Success Criteria

#### Automated Verification

- `dotnet build src/VehicleTracker.AppHost/VehicleTracker.AppHost.csproj --configuration Release` succeeds after the AppHost changes
- CI `backend-quality` job passes end-to-end including the new migration script step
- CI `deploy` job succeeds with `dotnet tool restore` present before `azd up`

#### Manual Verification

- Aspire dashboard shows an `api-migrations` resource that transitions through `Pending → Running → Finished` before the `api` resource starts
- `azd up` completes and a migration Container App Job appears in the Azure portal (one-shot job, runs once, shows Succeeded)
- After `azd up`, the `__EFMigrationsHistory` table exists in the `vehicletracker` database with one row for `InitialCreate`

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful.

---

## Testing Strategy

### Automated

- `dotnet build` across all three projects (Data, API, AppHost) — no warnings or errors
- `dotnet ef migrations script --idempotent` exits 0 and produces valid SQL — run in CI and verifiable locally

### Manual Testing Steps

1. Run the AppHost (`aspire run` or F5 in VS Code); confirm Aspire dashboard shows `api-migrations` resource reaching `Finished` state before `api` turns green
2. Connect to the local Postgres container (e.g., via pgAdmin or `psql`); confirm `__EFMigrationsHistory` table exists with one row: `MigrationId` = `<timestamp>_InitialCreate`
3. Open a PR that modifies the `ApplicationDbContext` without adding a migration — confirm the `backend-quality` CI job fails at the `Validate migrations` step (snapshot out of sync)

## Migration Notes

The `InitialCreate` migration is empty — its sole purpose is establishing the `__EFMigrationsHistory` table and baseline. Each subsequent slice (S-01 onwards) adds its own `dotnet ef migrations add` command targeting the same project; the migration is reviewed as part of the slice's PR and validated by the CI gate added in Phase 3.

## References

- Roadmap F-01: `context/foundation/roadmap.md`
- Aspire EF Core migrations guide: https://aspire.dev/integrations/databases/efcore/migrations/
- AppHost entry point: `src/VehicleTracker.AppHost/AppHost.cs`
- CI/CD pipeline: `.github/workflows/ci-cd.yml`

---

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles.

### Phase 1: Scaffold VehicleTracker.Data and Wire Aspire DB Registration

#### Automated

- [x] 1.1 `dotnet build src/VehicleTracker.Data/VehicleTracker.Data.csproj --configuration Release` succeeds with no warnings
- [x] 1.2 `dotnet build src/VehicleTracker/VehicleTracker.csproj --configuration Release` succeeds with no warnings
- [x] 1.3 `dotnet build src/VehicleTracker.AppHost/VehicleTracker.AppHost.csproj --configuration Release` succeeds with no warnings

#### Manual

- [ ] 1.4 Aspire dashboard shows a `vehicletracker` database health check entry when running locally

### Phase 2: Create InitialCreate Migration

#### Automated

- [ ] 2.1 `src/VehicleTracker.Data/Migrations/` folder exists with three generated files
- [ ] 2.2 `dotnet tool restore` completes without error
- [ ] 2.3 `dotnet ef migrations script --idempotent` exits 0 and emits valid SQL

#### Manual

- [ ] 2.4 The generated migration's `Up` method body is empty (no `CreateTable` calls)

### Phase 3: Wire AddEFMigrations in AppHost and Add CI Validation

#### Automated

- [ ] 3.1 `dotnet build src/VehicleTracker.AppHost/VehicleTracker.AppHost.csproj --configuration Release` succeeds after AppHost changes
- [ ] 3.2 CI `backend-quality` job passes including the new migration script step
- [ ] 3.3 CI `deploy` job succeeds with `dotnet tool restore` present

#### Manual

- [ ] 3.4 Aspire dashboard shows `api-migrations` transitioning `Pending → Running → Finished` before `api` starts
- [ ] 3.5 `azd up` completes; migration Container App Job appears in Azure portal with Succeeded status
- [ ] 3.6 After `azd up`, `__EFMigrationsHistory` table exists with one row for `InitialCreate`
- [ ] 3.6 `__EFMigrationsHistory` table exists with one row for `InitialCreate` after deploy
