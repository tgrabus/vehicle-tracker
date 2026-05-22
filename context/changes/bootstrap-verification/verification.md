---
bootstrapped_at: 2026-05-22T00:00:00Z
starter_id: dotnet
starter_name: .NET (ASP.NET Core webapi)
project_name: vehicle-tracker
language_family: dotnet
package_manager: dotnet
cwd_strategy: subdir-then-move
bootstrapper_confidence: verified
phase_3_status: ok
audit_command: "dotnet list package --vulnerable --include-transitive"
---

## Hand-off

```yaml
starter_id: dotnet
package_manager: dotnet
project_name: vehicle-tracker
hints:
  language_family: dotnet
  team_size: solo
  deployment_target: azure-app-service
  ci_provider: github-actions
  ci_default_flow: auto-deploy-on-merge
  bootstrapper_confidence: verified
  path_taken: standard
  quality_override: false
  self_check_answers: null
  has_auth: true
  has_payments: false
  has_realtime: false
  has_ai: false
  has_background_jobs: false
```

**Why this stack**

Vehicle Tracker is a 1-week solo MVP using a BFF (Backend for Frontend) pattern: .NET 10 (ASP.NET Core webapi) serves the compiled Angular 21 + Angular Material SPA directly from `wwwroot/`, deployed as a single Azure App Service. This eliminates CORS configuration, simplifies authentication to ASP.NET Core Identity with HttpOnly session cookies, and collapses CI/CD to a single pipeline that builds the Angular bundle, copies it into the publish output, and deploys one artifact. PostgreSQL via EF Core handles the relational data model (vehicles, service items, history entries, users) with strongly-typed migrations. Angular Material covers the alert dashboard, vehicle cards, and mileage-update flow with accessible components out of the box. The full-stack .NET + Angular combination is strongly typed end-to-end (C# on the server, TypeScript on the client), convention-based in both ecosystems, and well-represented in training data — all four agent-friendly gates pass. For a solo after-hours project constrained to one week, a single deployable unit with no CORS surface and cookie-based auth is the right call over a decoupled SPA + API architecture.

## Pre-scaffold verification

| Signal      | Value                                                           | Severity | Notes                                                    |
|-------------|------------------------------------------------------------------|----------|----------------------------------------------------------|
| npm package | not run                                                          | n/a      | not a JS starter; no npm package to check                |
| GitHub repo | not run                                                          | n/a      | docs_url is https://learn.microsoft.com/aspnet/core — not a GitHub URL |

No recency signal available for this starter. Proceeding without a staleness warning.

## Scaffold log

**Resolved invocation**: `dotnet new webapi -n vehicle-tracker -o .bootstrap-scaffold --no-restore`

> Adaptation note: cmd_template is `dotnet new webapi -n {name} --no-restore`. For `subdir-then-move`, `{name}` normally becomes `.bootstrap-scaffold`, but `.bootstrap-scaffold` is not a valid C# identifier. The invocation was adapted to `-n vehicle-tracker -o .bootstrap-scaffold` to produce a correctly named project while preserving the temp-directory strategy.

**Strategy**: subdir-then-move (scaffold into a temp directory then move files up)

**Exit code**: 0

**Files moved**: 6

- `appsettings.Development.json` — moved silently
- `appsettings.json` — moved silently
- `Program.cs` — moved silently
- `vehicle-tracker.csproj` — moved silently
- `vehicle-tracker.http` — moved silently
- `Properties\launchSettings.json` — moved silently

**Conflicts (.scaffold siblings)**: none

**.gitignore handling**: absent in scaffold

**.bootstrap-scaffold cleanup**: deleted

## Post-scaffold audit

**Tool**: `dotnet list package --vulnerable --include-transitive`

**Note on execution**: The machine's NuGet configuration includes a private Azure DevOps feed (`ukmail-dev.pkgs.visualstudio.com/_packaging/artifacts-dhlparcel-uk`) that returned 401 Unauthorized. The audit was run against the public NuGet.org feed only (`--source https://api.nuget.org/v3/index.json`). Results reflect public advisories only.

**Summary**: 0 CRITICAL, 0 HIGH, 0 MODERATE, 0 LOW

**Direct vs transitive**: not distinguished by `dotnet list package --vulnerable`

The given project `vehicle-tracker` has no vulnerable packages given the current sources.

#### CRITICAL findings

None.

#### HIGH findings

None.

#### MODERATE findings

None.

#### LOW / INFO findings

None.

## Hints recorded but not acted on

| Hint                    | Value               |
|-------------------------|---------------------|
| bootstrapper_confidence | verified            |
| quality_override        | false               |
| path_taken              | standard            |
| self_check_answers      | null                |
| team_size               | solo                |
| deployment_target       | azure-app-service   |
| ci_provider             | github-actions      |
| ci_default_flow         | auto-deploy-on-merge |
| has_auth                | true                |
| has_payments            | false               |
| has_realtime            | false               |
| has_ai                  | false               |
| has_background_jobs     | false               |

None of the above triggered automated action in v1. A future M1L4 skill ("Memory Architecture") will act on `deployment_target`, `ci_provider`, `ci_default_flow`, and the `has_*` feature flags to generate `AGENTS.md`, CI workflow files, and auth scaffolding.

## Next steps

Next: a future skill will set up agent context (CLAUDE.md, AGENTS.md). For now, your project is scaffolded and verified — happy hacking.

Useful manual steps in the meantime:

- `git init` (if you have not already) to start your own repo history.
- Review any `.scaffold` siblings the conflict policy created and decide which version of each file to keep.
- Address audit findings per your project's risk tolerance — the full breakdown is in this log.
- The webapi template was scaffolded with `--no-restore`. Run `dotnet restore` (or `dotnet build`) to pull NuGet packages before developing.
- The hand-off describes a BFF pattern with Angular 21 served from `wwwroot/`. The scaffold provides the .NET webapi baseline; the Angular project setup is a separate step not covered by this skill.
