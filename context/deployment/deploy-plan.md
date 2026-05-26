---
title: Deploy Vehicle Tracker to ACA with CI/CD Quality Gates
description: Step-by-step plan for deploying the Vehicle Tracker skeleton to Azure Container Apps, wiring Aspire Azure publish configuration, and setting up a safer GitHub Actions pipeline.
author: Vehicle Tracker Team
ms.date: 2026-05-26
ms.topic: how-to
keywords:
  - azure container apps
  - aspire
  - azd
  - github actions
  - ci/cd
---

## Decisions

| Decision | Choice |
|---|---|
| Goal | Prove Azure provisioning + app deployment + CI/CD for the current skeleton app |
| Release label | Infrastructure-ready, **not** MVP-ready |
| Container build | `azd`/Aspire built-in container publishing |
| Database | Azure PostgreSQL provisioned now; schema/migrations tracked as a follow-up requirement |
| Auth | Cookie-based ASP.NET Core Identity remains a release blocker for MVP |
| CI/CD workflow | Single `ci-cd.yml` for quality + app deploy, separate manual infra mutation path |
| Quality gates | Backend and frontend run in parallel; deploy blocked until both pass |
| Region | `westeurope` |

## Scope Boundary

This plan deploys the current skeleton safely enough to validate the Azure path. It does **not** by itself make the product production-ready for real users.

Before calling the app MVP-ready, the following must also exist:

- EF Core migrations applied to Azure PostgreSQL
- ASP.NET Core Identity with cookie auth working end-to-end
- Data Protection persistence verified against a real login cookie
- A true E2E "week 1" flow passing against the deployed environment

## Azure and GitHub Prerequisites

### Identity model for this repo

This repo should use `GitHub Actions -> OIDC/federated credential -> Microsoft Entra app/service principal -> Azure`.

Do **not** plan around an Azure DevOps-style "service connection". That term does not apply to GitHub Actions.

### Required access before pipeline setup

- Azure subscription access to create and manage the target resource group and services
- Permission to run `azd provision` and `azd deploy` against the target subscription
- GitHub repository admin access to manage Actions secrets, variables, workflows, and environments
- Microsoft Entra permission to create or update:
  - an app registration / service principal
  - a federated credential trusting the GitHub repository

### Tenant policy caveat

`azd pipeline config --provider github` can configure OIDC automatically, but it may fail if the tenant blocks app registration or federated credential changes.

If the tenant is locked down, one of the following must be true:

- the signed-in user can register applications, or
- the signed-in user has an Entra role such as `Application Developer`, `Application Administrator`, or `Cloud Application Administrator`, or
- an Entra admin pre-creates the app registration and federated credential for the repository

### Expected GitHub-side configuration

- GitHub Actions enabled for the repository
- A protected GitHub environment named `dev`
- Environment or repository variables for:
  - `AZURE_ENV_NAME`
  - `AZURE_LOCATION`
- Azure authentication values created by `azd pipeline config`, or equivalent values if the identity is created manually

### Preferred authentication path

Preferred: `OIDC` / federated credentials with short-lived tokens.

Fallback only if OIDC cannot be approved in the tenant: `azd pipeline config --auth-type client-credentials`

Client credentials require storing a long-lived secret in GitHub and should be treated as a less secure fallback.

## Current State Gaps

- `Program.cs` has `UseHttpsRedirection()` and no forwarded-header handling
- Production has no API-prefixed health endpoint; current Aspire defaults map health only in development
- `Program.cs` has no Data Protection persistence strategy for ACA restarts/revisions
- `AppHost.cs` is missing ACA environment wiring, Azure Postgres publish config, Azure Storage for Data Protection, and replica configuration
- `VehicleTracker.AppHost.csproj` is missing Aspire Azure hosting packages
- `VehicleTracker.csproj` is missing Azure identity/Data Protection packages and warning enforcement
- Frontend quality tooling is not normalized yet: linting is absent, and the test runner/dependencies are not explicitly pinned for CI
- No `azure.yaml`
- No custom GitHub Actions workflow with concurrency/environment protection

---

## Phase 1 - Prerequisites

> Run once on the local machine before code changes.

- [x] Install `azd`: `winget install Microsoft.Azd`
- [x] Install the Container Apps Azure CLI extension: `az extension add --name containerapp --upgrade`
- [x] Log in: `azd auth login` then `az login`
- [x] Confirm Node.js is on PATH
- [ ] Confirm GitHub repository admin access for Actions variables, environments, and secrets

---

## Phase 2 - Frontend Quality Toolchain

> Can run in parallel with Phase 3 and Phase 4.

### Required change

Normalize the frontend toolchain before wiring CI. The repo currently mixes Angular unit-test configuration and Vitest references. Pick one runner and make it explicit.

### Recommended choice for this repo

Stay with Angular's built-in test path for now because `angular.json` already points at `karma.conf.js`.

- [ ] In `src/VehicleTracker.Frontend/`, add ESLint:

  ```bash
  ng add @angular-eslint/schematics
  ```

- [ ] Add direct dev dependencies for the current unit-test path so CI does not rely on transitive packages:

  ```bash
  npm install -D karma karma-chrome-launcher karma-jasmine karma-jasmine-html-reporter karma-coverage jasmine-core @types/jasmine
  ```

- [ ] Add explicit scripts in `package.json`:
  - `lint`: `ng lint`
  - `test:ci`: `ng test --watch=false --browsers=ChromeHeadless`

- [ ] Verify locally:

  ```bash
  npm run build
  npm run test:ci
  npm run lint
  ```

### Exit criteria

- `npm run lint` exits `0`
- `npm run test:ci` exits `0`
- CI can run frontend checks without guessing the runner

---

## Phase 3 - AppHost Azure Publish Wiring

> Can run in parallel with Phase 2 and Phase 4.

- [x] Add NuGet packages to `src/VehicleTracker.AppHost/VehicleTracker.AppHost.csproj`:
  - `Aspire.Hosting.Azure.AppContainers`
  - `Aspire.Hosting.Azure.PostgreSQL`
  - `Aspire.Hosting.Azure.Storage`

- [x] Update `AppHost.cs`:
  - Add `builder.AddAzureContainerAppEnvironment("aca-env")`
  - Change the existing Postgres resource to publish as Azure PostgreSQL Flexible Server
  - Add Azure Blob Storage for Data Protection keys
  - Add the storage reference to the API project
  - Publish the API as an Azure Container App with `MinReplicas = 1`, `MaxReplicas = 3`
  - Add a startup/readiness probe target for the API container if the generated ACA model allows it

- [x] Standardize the configuration contract for Data Protection:
  - Using Aspire connection string injection (`ConnectionStrings:dataprotectionblobs`) — consistent with Aspire resource reference pattern

### Exit criteria

- `azd` can infer all Azure resources from AppHost
- The API has Azure Storage available for persistent key storage
- Replica count is explicitly set instead of relying on defaults

---

## Phase 4 - API ACA Compatibility and Production-Safe Health Checks

> Can run in parallel with Phase 2 and Phase 3.

- [x] Add NuGet packages to `src/VehicleTracker/VehicleTracker.csproj`:
  - `Azure.Identity`
  - `Azure.Extensions.AspNetCore.DataProtection.Blobs`

- [x] Add `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` to the main property group

- [x] Update `Program.cs`:
  - Remove `app.UseHttpsRedirection();`
  - Configure forwarded headers before `builder.Build()`
  - Add `app.UseForwardedHeaders();` before controller/fallback routing
  - Add non-development Data Protection persistence backed by Azure Blob Storage
  - Add **real production-safe API health endpoints** under `/api/` so they work behind the SPA fallback and respect the repo routing convention

### Recommended shape

```csharp
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

if (!builder.Environment.IsDevelopment())
{
    var blobUri = builder.Configuration["DataProtection:BlobUri"]
        ?? throw new InvalidOperationException("Missing DataProtection:BlobUri");

    builder.Services.AddDataProtection()
        .PersistKeysToAzureBlobStorage(new Uri(blobUri), new DefaultAzureCredential());
}

var app = builder.Build();

app.UseForwardedHeaders();

app.MapHealthChecks("/api/health/live", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("live")
});

app.MapHealthChecks("/api/health/ready");
```

### Verification requirement

Do **not** use `/api/health` as a smoke test unless the app explicitly maps it in production. The current ServiceDefaults health endpoints are development-only.

### Exit criteria

- No ACA redirect loop
- Health checks work in production under `/api/...`
- Data Protection config is explicit and unambiguous

---

## Phase 5 - azd Initialization

> Depends on Phases 2, 3, and 4.

- [x] Run from the repo root:

  ```bash
  azd init
  ```

- [x] When prompted:
  - Select `src/VehicleTracker.AppHost/VehicleTracker.AppHost.csproj`
  - Set environment name: `dev`

- [x] Verify `azure.yaml` exists at the repo root and points at the AppHost project

---

## Phase 6 - Provision Azure Resources

> Depends on Phase 5.

- [x] Run:

  ```bash
  azd provision
  ```

- [x] Choose the Azure subscription and set location to `westeurope`

- [x] Verify the following resources exist:
  - Azure Container Apps environment
  - Azure Container Registry
  - Azure Database for PostgreSQL Flexible Server
  - Azure Storage Account for Data Protection

- [x] Set ACR lifecycle retention: **deferred** — no accumulated images at skeleton stage; revisit before real-user release

- [x] Record the blob/container/key path chosen for Data Protection: managed by Aspire via `ConnectionStrings:dataprotectionblobs` connection string injected into the API container

### Exit criteria

- Provision completes without manual portal-only steps
- ACR retention is enabled on day 1
- The region is fixed before any second environment is created

---

## Phase 7 - First Deploy and Verification

> Depends on Phase 6.

- [x] Run:

  ```bash
  azd deploy
  ```

- [x] Run `azd show` and capture the live HTTPS URL
- [x] Verify in a browser:
  - SPA root loads
  - A valid API route under `/api/...` returns JSON, not `index.html`
  - `/api/health/live` returns `200`
  - `/api/health/ready` returns `200`

- [ ] Stream logs:

  ```bash
  az containerapp logs show --name api --resource-group <rg> --follow
  ```

- [ ] Verify the Data Protection artifact exists in Azure Blob Storage

### Deferred but mandatory once Identity exists

- [ ] Sign in, capture a real auth session, restart or roll a revision, and confirm the cookie still works

---

## Phase 8 - CI/CD Pipeline

> Depends on Phase 7.

### Design corrections

- App deployment and infrastructure mutation are separate concerns
- Normal pushes to `main` should deploy the app, not reprovision everything by default
- Infra changes should require either manual dispatch or a clearly scoped job
- Deploys to the shared `dev` environment must be serialized

### GitHub setup

- [ ] Run:

  ```bash
  azd pipeline config --provider github
  ```

- [ ] Delete the generated `.github/workflows/azure-dev.yml`
- [ ] Create a GitHub Actions environment named `dev`
- [ ] Put the deploy job behind that environment
- [ ] Add repository variables:
  - `AZURE_ENV_NAME=dev`
  - `AZURE_LOCATION=westeurope`

### Required workflow behaviors

- `backend-quality` must target backend projects only
- `frontend-quality` must target frontend only
- `deploy-app` runs only on pushes to `main`
- `deploy-app` uses `concurrency` so two merges cannot deploy at once
- `provision-infra` is manual, or otherwise narrowly scoped to infra-changing commits

### Recommended `ci-cd.yml`

```yaml
name: CI/CD

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
  workflow_dispatch:

concurrency:
  group: vehicle-tracker-${{ github.ref }}
  cancel-in-progress: false

jobs:
  backend-quality:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.x'
      - run: dotnet restore src/VehicleTracker/VehicleTracker.csproj
      - run: dotnet restore src/VehicleTracker.AppHost/VehicleTracker.AppHost.csproj
      - run: dotnet restore src/VehicleTracker.ServiceDefaults/VehicleTracker.ServiceDefaults.csproj
      - run: dotnet build src/VehicleTracker/VehicleTracker.csproj --no-restore --configuration Release
      - run: dotnet build src/VehicleTracker.AppHost/VehicleTracker.AppHost.csproj --no-restore --configuration Release
      - run: dotnet build src/VehicleTracker.ServiceDefaults/VehicleTracker.ServiceDefaults.csproj --no-restore --configuration Release
      - name: NuGet vulnerability scan
        run: dotnet list src/VehicleTracker/VehicleTracker.csproj package --vulnerable --include-transitive

  frontend-quality:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '22'
          cache: 'npm'
          cache-dependency-path: src/VehicleTracker.Frontend/package-lock.json
      - run: npm ci
        working-directory: src/VehicleTracker.Frontend
      - run: npm run build
        working-directory: src/VehicleTracker.Frontend
      - run: npm run test:ci
        working-directory: src/VehicleTracker.Frontend
      - run: npm run lint
        working-directory: src/VehicleTracker.Frontend
      - name: npm vulnerability scan
        run: npm audit --audit-level=high
        working-directory: src/VehicleTracker.Frontend

  provision:
    needs: [backend-quality, frontend-quality]
    if: github.ref == 'refs/heads/main' && github.event_name == 'push'
    runs-on: ubuntu-latest
    environment: dev
    permissions:
      id-token: write
      contents: read
    env:
      AZURE_CLIENT_ID: ${{ secrets.AZURE_CLIENT_ID }}
      AZURE_TENANT_ID: ${{ secrets.AZURE_TENANT_ID }}
      AZURE_SUBSCRIPTION_ID: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
      AZURE_ENV_NAME: ${{ vars.AZURE_ENV_NAME }}
      AZURE_LOCATION: ${{ vars.AZURE_LOCATION }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.x'
      - uses: Azure/setup-azd@v2
      - run: azd auth login --client-id $AZURE_CLIENT_ID --federated-credential-provider github --tenant-id $AZURE_TENANT_ID
      - run: azd provision --no-prompt

  deploy:
    needs: [provision]
    runs-on: ubuntu-latest
    environment: dev
    permissions:
      id-token: write
      contents: read
    env:
      AZURE_CLIENT_ID: ${{ secrets.AZURE_CLIENT_ID }}
      AZURE_TENANT_ID: ${{ secrets.AZURE_TENANT_ID }}
      AZURE_SUBSCRIPTION_ID: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
      AZURE_ENV_NAME: ${{ vars.AZURE_ENV_NAME }}
      AZURE_LOCATION: ${{ vars.AZURE_LOCATION }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.x'
      - uses: actions/setup-node@v4
        with:
          node-version: '22'
      - uses: Azure/setup-azd@v2
      - run: azd auth login --client-id $AZURE_CLIENT_ID --federated-credential-provider github --tenant-id $AZURE_TENANT_ID
      - run: azd deploy --no-prompt
```

### Exit criteria

- PRs run quality checks only
- Main branch provisions infra then deploys app as separate serialized jobs
- No manual provisioning steps required

---

## Phase 9 - MVP Readiness Follow-Up

> Not part of the skeleton deploy, but required before real-user release.

- [ ] Add EF Core migrations for the production schema
- [ ] Decide where migrations run:
  - manual step during initial rollout, or
  - controlled release step before `azd deploy`
- [ ] Add ASP.NET Core Identity with HttpOnly cookie auth
- [ ] Add a deployment smoke test for the first real user journey:
  - register
  - sign in
  - create vehicle
  - add service history entry
  - verify dashboard state
- [ ] Verify Data Protection survives a new ACA revision with a real auth cookie

---

## Files Changed by This Plan

| File | Change |
|---|---|
| `src/VehicleTracker.AppHost/VehicleTracker.AppHost.csproj` | Add Azure Aspire hosting packages |
| `src/VehicleTracker.AppHost/AppHost.cs` | ACA env, Azure Postgres, Azure Storage, replica settings, publish wiring |
| `src/VehicleTracker/VehicleTracker.csproj` | Add Azure packages and warning enforcement |
| `src/VehicleTracker/Program.cs` | Remove HTTPS redirection, add forwarded headers, persistent Data Protection, production health endpoints |
| `src/VehicleTracker.Frontend/package.json` | Add explicit `lint` and `test:ci` scripts and direct test dependencies |
| `src/VehicleTracker.Frontend/angular.json` | Add/confirm `lint` target and keep test runner configuration consistent |
| `azure.yaml` | Generated by `azd init` |
| `.github/workflows/ci-cd.yml` | Custom workflow with isolated quality jobs, serialized deploys, and separate infra mutation path |
| `.github/workflows/azure-dev.yml` | Delete generated workflow after replacing it |

## Out of Scope

- Multi-region production architecture
- Front Door/CDN
- Full observability beyond default Aspire/OpenTelemetry wiring
- Dedicated ACA plan
- Terraform

## Known Limitations After This Plan

- The deployed app may still be a skeleton with no real persisted domain workflow
- The deployment is not a substitute for schema/auth rollout
- "Successful deploy" is not the same as "MVP released"
