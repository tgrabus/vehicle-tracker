# Vehicle Tracker — Repository Guidelines

## Critical Conventions

### API Routing

All API controllers **must** use a route prefixed with `/api/`. The Angular dev proxy in `src/VehicleTracker.Frontend/src/proxy.conf.js` only forwards requests whose path starts with `/api`.

### Static Files

In **production**, ASP.NET Core serves the Angular bundle from `wwwroot/`. 
In **dev**, the Aspire-wired Angular dev server owns the frontend — the static file middleware is intentionally absent.

### Auth

ASP.NET Core Identity with HttpOnly session cookies — no JWT, no CORS. Do not introduce Bearer token or `Authorization` header patterns.

## Architecture Overview

**BFF (Backend for Frontend) pattern.** ASP.NET Core 10 API + Angular 21 SPA in one deployable unit.

## Project Map

| Path | Purpose |
|---|---|
| `src/VehicleTracker/` | ASP.NET Core 10 Web API + production static file host |
| `src/VehicleTracker.AppHost/` | .NET Aspire orchestrator — runs everything in dev |
| `src/VehicleTracker.Frontend/` | Angular 21 SPA  |
| `src/VehicleTracker.ServiceDefaults/` | Shared Aspire service defaults (telemetry, health checks) |
| `context/foundation/prd.md` | Full product requirements |
| `context/foundation/tech-stack.md` | Stack decisions and rationale |
| `brief.md` | Domain brief — alert logic, service item catalogue, persona |

## Build & Run Commands

```bash
# Run everything (preferred — starts Aspire dashboard, Postgres, API, Angular dev server)
dotnet run --project src/VehicleTracker.AppHost

```