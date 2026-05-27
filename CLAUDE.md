# Vehicle Tracker — Repository Guidelines

## Critical Conventions

### API Routing

All API controllers **must** use a route prefixed with `/api/`. The Angular dev proxy in `src/VehicleTracker.Frontend/src/proxy.conf.js` only forwards requests whose path starts with `/api`.

### Static Files

In **production**, ASP.NET Core serves the Angular bundle from `wwwroot/`. 
In **dev**, the Aspire-wired Angular dev server owns the frontend — the static file middleware is intentionally absent.

### Auth

ASP.NET Core Identity with HttpOnly session cookies — no JWT, no CORS. Do not introduce Bearer token or `Authorization` header patterns.

### Infrastructure as Code

All infrastructure configuration must be represented in IaC and committed to the repo. In this project, the default source of truth is the Aspire project and its related declarative configuration.

Do not make manual or ad hoc changes in Azure by default. If an emergency manual change is unavoidable, treat it as temporary and reflect it back into IaC before the work is considered complete.

## Architecture Overview

**BFF (Backend for Frontend) pattern.** ASP.NET Core 10 API + Angular 21 SPA in one deployable unit.

## Application Architecture

Prefer **vertical slices** organized by feature/use case rather than building broad horizontal layers up front.

Use **Clean Architecture** principles to keep boundaries clear, dependencies directed inward, and infrastructure concerns from leaking into application and domain code.

When a feature contains meaningful domain behavior, model it explicitly with **Domain-Driven Design** patterns and language. Put business rules in the domain, not in controllers or persistence code.

When a feature is simple CRUD with little or no domain behavior, keep the implementation straightforward. Do not introduce DDD or extra abstraction layers unless they solve a real problem in the current feature.

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
