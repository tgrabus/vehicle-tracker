---
project: vehicle-tracker
researched_at: 2026-05-26
recommended_platform: Azure Container Apps (Consumption) + Azure Container Registry + Azure Database for PostgreSQL Flexible Server
runner_up: Azure App Service (Linux, container mode)
context_type: mvp
tech_stack:
  language: C#
  framework: ASP.NET Core 10 + Angular 21 (BFF pattern, single deployable unit)
  runtime: .NET 10 (Linux container)
  database: PostgreSQL (via EF Core + Npgsql)
deployment_note: Container deployment confirmed — multi-stage Dockerfile (Node.js for Angular build → .NET SDK → ASP.NET Core runtime)
provisioning_tool: azd (Azure Developer CLI) + Aspire-generated Bicep (GA, May 2026)
terraform_verdict: NOT recommended — Terraform support in azd is beta (May 2026); no Aspire-native IaC generation; remote state setup required before CI/CD; overkill for solo MVP
---

## Recommendation

**Deploy on Azure Container Apps (Consumption plan) with Azure Container Registry and Azure Database for PostgreSQL Flexible Server, provisioned via `azd` (Azure Developer CLI) with Aspire-generated Bicep.**

The stack is a containerised BFF (.NET 10 API serving an Angular 21 bundle from `wwwroot/`). The project already includes a `.NET Aspire AppHost` (`VehicleTracker.AppHost`) — this is the key asset. `azd` reads the Aspire manifest and automatically generates Bicep for the entire ACA environment (managed identity, ACR, Log Analytics workspace, ACA environment, and AcrPull role assignment) with a single call to `AddAzureContainerAppEnvironment`. This eliminates the 10+ manual `az` commands that raw-CLI provisioning requires. `azd pipeline config` generates GitHub Actions with OIDC in one command — no manual service principal setup. ACA is ~$40/month cheaper than App Service at this scale and provides revision-based rollback without a tier upgrade.

**Provisioning tool decision (Terraform vs azd):** Terraform support in `azd` is currently **beta** (May 2026). More importantly, Aspire has no Terraform code generation — only Bicep. Choosing Terraform would require hand-writing all `.tf` files and setting up remote state before CI/CD can run. For a solo MVP with an Aspire AppHost already in the project, `azd` + Aspire Bicep is the only sensible choice.

**Confirmed deployment constraints (from developer interview and session context):**
- Containers confirmed (multi-stage Dockerfile required: Node.js → .NET SDK → ASP.NET Core runtime)
- Single region (`westeurope` or equivalent)
- DX prioritised — CI/CD automation via GitHub Actions
- Cost-sensitive — always-on at idle vs scale-to-zero trade-off evaluated

## Platform Comparison

### Scoring Matrix

| Platform | CLI-first | Managed / Serverless | Agent-readable docs | Stable deploy API | MCP / Integration | Total |
|---|---|---|---|---|---|---|
| **Azure Container Apps** | Pass | Pass | Pass | Pass | Partial | 4.5/5 |
| Azure App Service (container) | Pass | Partial | Pass | Pass | Partial | 4/5 |
| Render | Pass | Pass | Pass | Pass | Pass | 5/5 |
| Railway | Partial | Pass | Pass | Partial | Partial | 3/5 |
| Cloudflare Workers | — | — | — | — | — | Dropped (no .NET 10 runtime) |
| Vercel | — | — | — | — | — | Dropped (no .NET 10 runtime) |
| Netlify | — | — | — | — | — | Dropped (no .NET 10 runtime) |

**Hard filter applied:** Cloudflare Workers, Vercel, and Netlify are edge/serverless JS-only runtimes. A full ASP.NET Core 10 persistent process cannot run on them. Dropped before scoring.

**Soft weights applied:**
- Q2 (prioritise DX) → Favoured platforms with native GitHub Actions + container workflows
- Q3 (Azure familiar) → Azure ecosystem breaks ties against Render
- Q4 (single region) → No edge-native premium applied
- Q5 (co-location preferred) → All shortlisted platforms provide managed Postgres on the same vendor

---

### Shortlisted Platforms

#### 1. Azure Container Apps (Consumption) — Recommended

**CLI:** Full `az containerapp` coverage via the `containerapp` extension. Every routine operation — environment create, app create, image update, revision rollback, log stream, secret set — has a documented `az containerapp` command. `az containerapp up` can scaffold the entire setup from a Dockerfile in one command for initial provisioning.

**Managed:** Higher abstraction level than App Service. No plan tier to manage, no OS patching surface, auto-scaling built in. ACA handles load balancing, HTTPS termination, and container orchestration transparently.

**Docs:** learn.microsoft.com/azure/container-apps is comprehensive, GitHub-hosted source, well-maintained. The GitHub Actions `azure/container-apps-deploy-action@v1` is officially maintained by Microsoft and supports build-from-Dockerfile in a single action step.

**Deploy API:** Each deployment creates an immutable revision. `az containerapp update --image` triggers a new revision automatically. Revision traffic is controllable (`az containerapp ingress traffic set`). Rollback = activate a previous revision (`az containerapp revision activate`). No tier upgrade required.

**MCP:** No dedicated ACA MCP server. The Azure MCP server (`azure/azure-mcp`) covers resource management broadly. Partial — `az containerapp` CLI is sufficient for all MVP operations.

**Pricing (MVP estimate, May 2026):**
- ACA Consumption, `min-replicas=1`, 0.5 vCPU + 1 GB RAM (idle billing applies when no requests):
  - Idle vCPU: 0.5 × 2,592,000 s/month = 1,296,000 s; free grant 180,000 s; billable 1,116,000 × $0.000003 = **$3.35**
  - Idle Memory: 1 GB × 2,592,000 s; free grant 360,000 s; billable 2,232,000 × $0.000003 = **$6.70**
  - Active charges while handling requests (negligible for solo, low-traffic MVP)
  - **ACA compute: ~$10/month**
- ACR Basic (10 GB storage): **$5/month** — see Risk Register for quota risk; upgrade to Standard ($20/month) after ~3 months of daily pushes
- Azure Database for PostgreSQL Flexible Server, Burstable B1ms: **$12.41/month**
- **Total: ~$27/month (ACR Basic) or ~$42/month (ACR Standard after quota risk materialises)**

**Why it wins over App Service (container):** Same Azure ecosystem, same `az` CLI, same OIDC GitHub Actions setup — but ~$40/month cheaper at this scale. Revision-based rollback without a tier upgrade. Scales to zero when idle. Container-native model matches the chosen deployment strategy.

---

#### 2. Azure App Service (Linux, container mode) — Runner-Up

All the same Azure CLI/IAM/GitHub Actions setup as ACA, but on the App Service Plan model. Running a container on App Service requires exactly the same Dockerfile and ACR push pipeline as ACA, but the App Service plan is billed at $54.75/month (B1) regardless of whether the container is handling requests. Deployment slots for staging require Standard S1 (~$73/month). No revision model — rollback requires re-deploying a prior image tag manually.

**Why it scores second:** Legitimate fallback if the ACA-specific gotchas (Data Protection key persistence, ingress HTTP redirect loop) prove time-consuming to fix. No new Azure concepts to learn — App Service is more familiar from previous sessions. Total cost: ~$72/month (B1 + ACR Basic + Postgres).

---

#### 3. Render

**CLI:** `render` CLI available. Auto-deploys from Git push. Official REST API. The only platform in this research with a **published MCP server** — highest integration score.

**Pricing:** Docker web service (Starter $7 + Standard $25) + Postgres Basic 1 GB ($19) = $26–44/month. Competitive with ACA.

**Why it scores third:** Raw scores are excellent (5/5 criteria). MCP server is a genuine advantage. Falls behind Azure because the user has no Render familiarity (Q3) and switching vendors adds a new IAM model, a new CLI, and a new secret management pattern. The Azure familiarity weight is decisive.

---

## Anti-Bias Cross-Check: Azure Container Apps

### Devil's Advocate — Weaknesses

1. **ASP.NET Core Data Protection keys are ephemeral by default.** When ACA scales a container to zero and creates a new replica, the in-memory key ring is gone. All existing `.AspNetCore.Cookies` authentication tokens become invalid — users are silently logged out with no error. Without `AddDataProtection().PersistKeysToAzureBlobStorage(...)`, this is a production incident triggered by the first scale-to-zero event. Requires an Azure Storage Account (free tier available for low usage) and explicit configuration in `Program.cs`.

2. **Azure Container Registry adds a third managed service with a 10 GB storage limit on Basic tier.** The pipeline is GitHub Actions → build image → push to ACR → ACA pulls from ACR. ACR Basic = $5/month but has a 10 GB hard quota. A multi-stage BFF image is typically 100–300 MB. After 30–100 daily CI pushes (with no lifecycle policy purging old tags), the quota is exceeded and the next deployment fails at 11 PM. ACR Standard = $20/month with 100 GB. An ACR lifecycle policy (`az acr config retention set`) mitigates this on Basic, but must be configured proactively.

3. **`UseHttpsRedirection()` in ASP.NET Core causes an infinite redirect loop on ACA.** ACA's ingress terminates TLS externally. The container receives plain HTTP internally. `UseHttpsRedirection()` sees HTTP and issues a 301 redirect to HTTPS — which ACA then forwards as HTTP again, creating an infinite loop. The fix is either to remove `UseHttpsRedirection()` and rely on ACA's forced HTTPS ingress setting, or to add `UseForwardedHeaders()` with the correct `KnownNetworks` covering the ACA internal network. This is not mentioned in the ACA .NET quickstart guides.

4. **Cold start of 8–15 seconds when `min-replicas=0`.** ASP.NET Core startup (DI container build, EF Core model initialisation, Identity middleware) adds 3–6 seconds on top of container pull latency. The first POST to `/api/account/login` after an idle period arrives during startup and returns 502 — visible as a broken login flow. The `startupProbe` configuration in ACA mitigates this by delaying traffic until the container is ready, but it must be set explicitly. `min-replicas=1` avoids the problem at the cost of ~$10/month idle billing.

5. **No SLA for single-replica Consumption plan.** The ACA 99.95% SLA applies only when 2+ replicas are deployed. For a solo MVP at `min-replicas=1`, there is no contractual uptime guarantee. This is the same limitation as App Service B1 Basic, but worth noting before declaring the app "production ready."

---

### Pre-Mortem — How This Could Fail

The team deployed the .NET 10 + Angular BFF as a container on Azure Container Apps, Consumption plan, `min-replicas=0` for cost savings. Three months in, users reported mysterious random logouts. The root cause: no Data Protection key persistence. Every scale-to-zero event created a new replica with a fresh in-memory key ring, silently invalidating all live session cookies. The developer had read about Data Protection in the context of multi-instance load balancing but dismissed it as irrelevant for a single-replica app — forgetting that scale-to-zero is equivalent to a crash and restart. The `Program.cs` change required a new dependency (`Azure.Extensions.AspNetCore.DataProtection.Blobs` NuGet package), a new Storage Account (`az storage account create`), and a new Blob container — none of which were in the original deploy plan. Fixing it required a second deployment at 2 AM after a user escalation.

The second failure arrived at week 10: the CI pipeline failed with a cryptic `unauthorized: authentication required` error from ACR. Investigation revealed the ACR Basic storage quota had been exceeded — 67 image pushes at ~150 MB each totalled 10 GB. The fix required upgrading to ACR Standard ($20/month) and manually purging old image tags. Neither the upgrade cost nor the purge procedure had been documented.

The third failure was the `UseHttpsRedirection()` infinite redirect loop discovered only in production (local development uses `dotnet run` which does not terminate TLS the same way ACA does). The symptom was a browser `ERR_TOO_MANY_REDIRECTS` error on the login page. Diagnosing it required reading ACA ingress documentation that was not in the original research.

---

### Unknown Unknowns

- **ASP.NET Core Data Protection keys must be persisted to Azure Blob Storage.** ACA creates new replicas from scratch on every scale-to-zero event. The default in-memory key ring is lost. Every user who had an active session loses it silently. Fix: `builder.Services.AddDataProtection().PersistKeysToAzureBlobStorage(blobClient, "dataprotection-keys.xml")`. Requires `Azure.Extensions.AspNetCore.DataProtection.Blobs` and an Azure Storage Account (Blob Storage LRS — cheapest tier is essentially free at this scale).

- **`UseHttpsRedirection()` must be removed or guarded.** ACA terminates TLS externally; the container receives HTTP internally on the configured target port. `UseHttpsRedirection()` causes an infinite redirect loop. Either remove the middleware and rely on ACA's forced HTTPS ingress setting, or configure `app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedProto })` and check `X-Forwarded-Proto` before redirecting.

- **`ASPNETCORE_URLS` must match the ACA target port.** .NET 8+ base images default to `http://+:8080`. The `az containerapp create --target-port` value must match the port the ASP.NET Core process listens on. If the Dockerfile sets a non-standard `EXPOSE` port, the ingress target-port must be updated to match. A mismatch produces a 502 Bad Gateway with no informative log message.

- **Inactive revisions accumulate container images in ACR indefinitely.** ACA tracks up to 100 inactive revisions, but the images those revisions reference remain in ACR until explicitly purged. Without an ACR lifecycle policy, storage grows unbounded. Configure `az acr config retention set --registry <acr> --status enabled --days 30 --type UntaggedManifests` immediately after creating the registry.

- **ACA environments are regional and immutable.** The Container Apps environment must be in the same Azure region as the Postgres Flexible Server. The environment cannot be moved after creation. If the wrong region is chosen, everything must be deleted and recreated. Establish the region variable once (`LOCATION="westeurope"`) and use it for every `az` command.

---

## Operational Story

- **Preview deploys:** ACA supports multiple revision mode for traffic splitting. For MVP, single revision mode is sufficient — ACA ensures zero-downtime by keeping the old revision alive until the new one passes its startup probe. For staging: create a second container app (`vehicle-tracker-staging`) in the same ACA environment using the same image with a different tag. No tier upgrade required — the same Consumption plan covers multiple apps.

- **Secrets:** Secrets and connection strings are managed by `azd` via the environment store (`.azure/<env>/.env`). Aspire wires connection strings automatically when resources are referenced in `AppHost.cs` (e.g., `.WithReference(postgres)`). Application-level secrets (Postgres password, Storage Account key) are injected as environment variables into the container by Aspire's generated Bicep — they are never committed to the repo. To rotate: `azd env set KEY value` then `azd deploy`. `azd pipeline config` stores pipeline secrets as GitHub Actions secrets automatically (no manual secret creation).

- **Rollback:** `az containerapp revision list --name <app> --resource-group <rg>` lists all revisions. `az containerapp revision activate --revision <old-revision-name> --name <app> --resource-group <rg>` activates a previous revision and ACA reroutes 100% of traffic to it. Typical time: 30–90 seconds. Database migrations do not roll back automatically — any EF Core migration applied to Postgres must be manually reverted with `dotnet ef database update <prior-migration>` if the code rollback changes the schema.

- **Approval:** Actions an agent may perform unattended: build and push a new image to ACR, trigger a new revision via `az containerapp update --image`, update ACA secrets, stream logs. Actions that require a human: drop the database or any table, rotate the Postgres admin password, delete the ACA environment or the resource group, modify Azure RBAC role assignments, switch `min-replicas` to 0 on a live app with active sessions.

- **Logs:** Runtime logs: `az containerapp logs show --name <app> --resource-group <rg> --follow` streams live stdout/stderr. Filter by revision: `--revision <name>`. Structured logs are available via Log Analytics workspace (created automatically with the ACA environment): `az monitor log-analytics query --workspace <workspace-id> --analytics-query "ContainerAppConsoleLogs_CL | where AppName_s == '<app>' | order by TimeGenerated desc | take 100"`.

---

## Risk Register

| Risk | Source | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| Data Protection keys lost on scale-to-zero — silent user logout | Unknown unknowns | H | H | Add `AddDataProtection().PersistKeysToAzureBlobStorage(...)` to `Program.cs` before first deployment. Create an Azure Storage Account and Blob container for keys. |
| `UseHttpsRedirection()` causes infinite 301 redirect loop on ACA ingress | Unknown unknowns | H | H | Remove `UseHttpsRedirection()` from `Program.cs` and enable ACA's built-in `--allow-insecure false` ingress setting. Add `UseForwardedHeaders()` to preserve `X-Forwarded-Proto` for any code that checks the scheme. |
| ACR Basic 10 GB storage quota exceeded after ~2–3 months of daily pushes | Devil's advocate | H | M | Set ACR lifecycle retention policy on day 1: `az acr config retention set --days 30 --type UntaggedManifests`. Budget for ACR Standard upgrade (~$15/month extra) at month 3 if image size grows. |
| Cold start 8–15s with `min-replicas=0` causes 502 on first login request | Devil's advocate | H | M | Default to `min-replicas=1` for MVP (~$10/month extra). Configure a `startupProbe` in the container spec so ACA does not route traffic until ASP.NET Core is ready. |
| Revision accumulation fills ACR with unreferenced images over time | Unknown unknowns | M | M | Set ACR lifecycle policy on day 1 (see row above). Also configure ACA `--max-inactive-revisions 20` to limit revision metadata. |
| ACA environment created in wrong region — cannot be moved | Unknown unknowns | L | H | `azd provision` prompts for location once and uses it for all resources. Answer consistently. Verify with `azd show` after provisioning. |
| `ASPNETCORE_URLS` port mismatch causes 502 Bad Gateway with no useful log | Unknown unknowns | M | M | Explicitly set `ENV ASPNETCORE_URLS=http://+:8080` in the Dockerfile. Aspire reads the `EXPOSE` instruction and sets target-port automatically. |
| `azd pipeline config` requires Contributor rights on the **subscription** (not just the resource group) to create the service principal | Research finding | M | H | Verify subscription-level Contributor or Owner access before running `azd pipeline config`. If only resource-group access is available, create the service principal manually and pass credentials via `azd env set`. |
| ACR lifecycle policy is NOT set by the Aspire-generated Bicep — old image tags accumulate and hit the 10 GB Basic-tier quota | Research finding | H | M | Run `az acr config retention set --days 30 --type UntaggedManifests` immediately after `azd provision`. Add as a `postprovision` hook in `azure.yaml` to automate it on every provision. |
| Default min-replicas from Aspire ACA Bicep is 0. Without the `PublishAsAzureContainerApp(MinReplicas=1)` customization in AppHost.cs, the container scales to zero — causing cold starts and Data Protection key loss on first request. | Unknown unknowns | H | H | The `AppHost.cs` snippet in Getting Started includes `MinReplicas = 1`. This must be set before `azd provision`. |

---

## Getting Started

Provisioning uses `azd` (Azure Developer CLI) with the Aspire AppHost generating Bicep automatically. This replaces ~15 manual `az` commands with 4 `azd` commands. Terraform was evaluated and rejected — it is beta in `azd` and has no Aspire-native IaC generation.

**Prerequisites**

```bash
# Install azd (Windows PowerShell)
winget install Microsoft.Azd

# Install azd extension for the containerapp CLI (still needed for manual ops)
az extension add --name containerapp --upgrade

# Log in
azd auth login
az login
```

---

**1. Update `AppHost.cs` — add ACA environment and Azure Postgres**

Add `Aspire.Hosting.Azure.AppContainers` and `Aspire.Hosting.Azure.PostgreSQL` NuGet packages to `VehicleTracker.AppHost.csproj`.

Replace the current `AppHost.cs` with an Aspire-aware publish configuration:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// ACA environment — generates Bicep for managed identity, ACR (Basic),
// Log Analytics workspace, ACA environment, and AcrPull role assignment.
builder.AddAzureContainerAppEnvironment("aca-env");

// Postgres: local container in dev, Azure Postgres Flexible Server on publish.
var postgres = builder.AddPostgres("postgres")
    .PublishAsAzurePostgresFlexibleServer()   // B1ms Burstable by default
    .WithDataVolume()
    .AddDatabase("vehicletracker");

var api = builder.AddProject<Projects.VehicleTracker>("api")
    .WithReference(postgres)
    .WaitFor(postgres)
    .PublishAsAzureContainerApp((infra, containerApp) =>
    {
        // Always-on: prevents cold-start session cookie failures.
        containerApp.Template.Value!.Scale = new ContainerAppScale
        {
            MinReplicas = 1,
            MaxReplicas = 3
        };
    });

var frontend = builder
    .AddViteApp("frontend", "../VehicleTracker.Frontend", "start")
    .WithHttpsEndpoint(port: 4200, env: "DEV_SERVER_PORT")
    .WithReference(api)
    .WaitFor(api);

// Copies Angular build output into the API container's wwwroot/ at publish time.
api.PublishWithContainerFiles(frontend, "./wwwroot");

builder.Build().Run();
```

> **Note:** `AddAzureContainerAppEnvironment` also deploys the Aspire Dashboard as a managed ACA component — this is a zero-cost managed component, not a billable container app.

---

**2. Update `Program.cs` before first deploy — two required changes**

These are ASP.NET Core concerns that `azd` does not handle automatically:

```csharp
// 1. Data Protection key persistence — REQUIRED for ACA.
//    Without this, every container restart (including scale-to-zero) invalidates
//    all login cookies. azd provisions the Storage Account via Aspire automatically
//    when you reference it — but the Program.cs binding must be explicit.
builder.Services.AddDataProtection()
    .PersistKeysToAzureBlobStorage(
        new Uri(builder.Configuration["DataProtection:BlobUri"]!),
        new ManagedIdentityCredential());

// 2. Forward headers — REQUIRED for ACA.
//    ACA terminates TLS externally. Remove UseHttpsRedirection() and add:
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto);
```

Add before `UseAuthorization()`:
```csharp
app.UseForwardedHeaders();
// Remove: app.UseHttpsRedirection(); — ACA ingress enforces HTTPS
```

Add a Blob Storage resource to `AppHost.cs` so Aspire wires the connection string and blob URI automatically:

```csharp
var storage = builder.AddAzureStorage("storage")
    .AddBlobs("dataprotection");

var api = builder.AddProject<Projects.VehicleTracker>("api")
    .WithReference(postgres)
    .WithReference(storage)   // Aspire injects DataProtection__BlobUri env var
    ...
```

---

**3. Initialise `azd`**

From the repo root:

```bash
azd init
```

`azd` detects the Aspire AppHost and generates:
- `azure.yaml` (service map referencing the AppHost project)
- `.azure/<env-name>/` (environment-scoped configuration)

The generated `azure.yaml` will look like:

```yaml
name: vehicle-tracker
services:
  app:
    language: dotnet
    project: ./src/VehicleTracker.AppHost/VehicleTracker.AppHost.csproj
    host: containerapp
```

---

**4. Provision Azure resources**

```bash
# Creates everything: resource group, managed identity, ACR, Log Analytics,
# ACA environment, Postgres Flexible Server, Storage Account.
# Prompts for subscription, location, and environment name.
azd provision
```

Expected output confirms:
- `aca-env` environment created
- ACR (Basic) created with managed identity and AcrPull role
- Postgres Flexible Server B1ms created
- Storage Account created (for Data Protection keys)

---

**5. Run EF Core migrations**

`azd` does not run database migrations — this is a one-time manual step:

```bash
# azd env get-values prints all provisioned connection strings
azd env get-values

# Run migrations against the provisioned Postgres
dotnet ef database update \
  --project src/VehicleTracker \
  --connection "<DefaultConnection from azd env get-values>"
```

For subsequent migrations, add a `postprovision` hook in `azure.yaml`:

```yaml
hooks:
  postprovision:
    shell: pwsh
    run: >
      dotnet ef database update
      --project src/VehicleTracker
      --connection $env:AZURE_POSTGRESQL_CONNECTIONSTRING
```

---

**6. Deploy (build container + push to ACR + update ACA revision)**

```bash
# Build the multi-stage Docker image, push to ACR, deploy new ACA revision.
# On first run, use azd up (provision + deploy). For code-only changes, use azd deploy.
azd deploy
```

Verify:

```bash
azd show   # prints the live HTTPS endpoint
az containerapp logs show --name api --resource-group <rg> --follow
```

Expected: Angular SPA loads, `/api/health` returns 200, login flow completes without redirect loops.

---

**7. Set up GitHub Actions CI/CD (one command)**

```bash
# Creates a service principal, configures OIDC federated credentials,
# writes .github/workflows/azure-dev.yml, and pushes to GitHub.
# Requires Contributor rights on the subscription.
azd pipeline config --provider github
```

After this, every push to `main` triggers: `azd provision` (no-op if infra unchanged) + `azd deploy` (builds and deploys new image).

**ACR lifecycle policy — set immediately after first provision:**

```bash
ACR_NAME=$(azd env get-values | Select-String 'AZURE_CONTAINER_REGISTRY_NAME' | ForEach-Object { $_ -replace '.*="', '' -replace '"', '' })
az acr config retention set --registry $ACR_NAME --status enabled \
  --days 30 --type UntaggedManifests
```

---

**Rollback**

```bash
# List revisions
az containerapp revision list --name api --resource-group <rg> --output table

# Activate a previous revision (routes 100% traffic to it)
az containerapp revision activate \
  --revision <old-revision-name> \
  --name api --resource-group <rg>
```

---

## Out of Scope

The following were not evaluated in this research:

- Multi-stage Dockerfile implementation details (see `context/changes/` for implementation planning)
- Full CI/CD pipeline customisation beyond the `azd pipeline config` skeleton above
- Production-scale architecture (multi-region, zone-redundant HA, disaster recovery)
- Azure Front Door or CDN configuration for static asset caching
- Application Insights / Azure Monitor full observability setup
- ACA Dedicated plan (unnecessary for MVP scale)
- **Terraform**: evaluated and rejected. Terraform support in `azd` is beta (May 2026). Aspire generates only Bicep — not Terraform. Using Terraform would require hand-writing all `.tf` files and configuring remote state before CI/CD. Not appropriate for a solo MVP with an Aspire AppHost already in the project.
