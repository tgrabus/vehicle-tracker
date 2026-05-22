---
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
---

## Why this stack

Vehicle Tracker is a 1-week solo MVP using a BFF (Backend for Frontend) pattern: .NET 10 (ASP.NET Core webapi) serves the compiled Angular 21 + Angular Material SPA directly from `wwwroot/`, deployed as a single Azure App Service. This eliminates CORS configuration, simplifies authentication to ASP.NET Core Identity with HttpOnly session cookies, and collapses CI/CD to a single pipeline that builds the Angular bundle, copies it into the publish output, and deploys one artifact. PostgreSQL via EF Core handles the relational data model (vehicles, service items, history entries, users) with strongly-typed migrations. Angular Material covers the alert dashboard, vehicle cards, and mileage-update flow with accessible components out of the box. The full-stack .NET + Angular combination is strongly typed end-to-end (C# on the server, TypeScript on the client), convention-based in both ecosystems, and well-represented in training data — all four agent-friendly gates pass. For a solo after-hours project constrained to one week, a single deployable unit with no CORS surface and cookie-based auth is the right call over a decoupled SPA + API architecture.
