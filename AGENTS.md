# SafeRide Agent Instructions

## Project Map
SafeRide is a .NET 8 Clean Architecture backend plus a Flutter mobile app.

Backend:
- Solution: `src/SRD_Master/SafeRide.slnx`
- API: `src/SRD_Master/SafeRide.API`
- Application: `src/SRD_Master/SafeRide.Application`
- Domain: `src/SRD_Master/SafeRide.Domain`
- Infrastructure: `src/SRD_Master/SafeRide.Infrastructure`
- Realtime: `src/SRD_Master/SafeRide.Realtime`
- Contracts, Shared, UnitTests, and IntegrationTests live beside those projects.

Flutter:
- App root: `src/safe_ride_flutter`
- Uses Provider, GetIt, Dio, SignalR client, Google Maps/VietMap services, and feature-based folders.

## Layer Boundaries
- API owns the composition root, controllers, middleware, Swagger, SignalR hub mapping, and recurring Hangfire registration.
- Application owns MediatR features, interfaces, DTOs, Options models, and business logic.
- Domain owns entities/enums only. It must not reference configuration, Redis, Hangfire, SignalR, or infrastructure.
- Infrastructure owns EF Core, Identity, SQL Server, Redis, Hangfire jobs, external providers, Cloudinary, SMS, auth implementations, and realtime notification implementations.
- Realtime owns SignalR hub and realtime notification plumbing, but hub mapping belongs in API composition.

## SafeRide Business Invariants
- Matching must create driver offers first. Do not create a Trip until the customer confirms a driver.
- A driver becomes Busy only after a confirmed/assigned trip, not merely because an offer exists.
- Multiple offers may exist for one booking, but guard against duplicate active trips for the same driver or booking.
- When a driver accepts or is assigned to another trip, remaining conflicting offers must expire or be ignored safely.
- Promotion usage must be counted only after a trip is completed. Cancelled or expired bookings must not increment promotion usage.
- Customer-facing app messages must remain Vietnamese.

## Redis, Realtime, and Background Jobs
- Redis GEO indexes are sorted sets. Removing a driver from `OnlineDriversGeo` requires a Redis abstraction method that uses sorted-set member removal; normal key deletion does not remove GEO members.
- Driver matching must validate GEO candidates against live Redis status/location data and database eligibility before creating offers.
- Use Redis TTL for short-lived cache expiry where appropriate. Do not add Redis key scanning jobs unless explicitly needed.
- Hangfire job implementations belong in Infrastructure.
- Recurring Hangfire jobs must be registered from the API composition root through an Infrastructure extension.
- Application and Domain must not reference Hangfire.
- SignalR hub mapping belongs in API composition. Realtime notification implementations belong outside Domain.

## Configuration and Secrets
- Do not hard-code operational values in services, handlers, hubs, or jobs.
- Move these values into strongly typed Options sections: timeouts, polling intervals, Redis TTLs, Hangfire cron expressions, Hangfire recurring job ids, cleanup retention days, cleanup batch sizes, notification titles/types, SignalR event names/types, matching expiration durations, offer expiration durations, and external provider timeouts.
- Each Options class should expose a `SectionName` constant and be bound/validated during dependency injection.
- Avoid direct `IConfiguration` access except in composition root or infrastructure setup.
- Never commit real secrets, connection strings, API keys, JWT secrets, Redis passwords, OAuth IDs, Cloudinary secrets, SMS keys, or map keys.
- Use user secrets, environment variables, ignored local files, or Flutter `env/*.local.json`.
- Ask before modify local/dev-only config , including `launchSettings.json`, `android/gradle.properties`, `appsettings.Development.json`, and local API key files.

## Agent and Git Safety
- Start with targeted search and inspect only relevant files. Do not scan the whole repository unless the task truly requires it.
- Prefer small, isolated patches. Do not rewrite unrelated files or introduce unrelated formatting churn.
- Do not edit generated/build output under `bin/`, `obj/`, `build/`, `.dart_tool/`, `.vs/`, or `artifacts/`.
- Use English for code identifiers.
- Use read-only subagents for mapping, review, and risk discovery when available.
- Only one writer agent should edit code for a task.
- Subagents should return compact summaries with relevant files, current flow, risks, and edit points. Do not paste full files unless necessary.
- Do not commit, push, reset, rebase, merge, delete branches, or run `git add .` unless explicitly requested.
- Stage and report only intentional files when Git operations are requested.

## Change Checks
- For database changes, check EF entities, configurations, migrations, DTO/contracts, handlers/services, and tests together.
- Only create, remove, update, drop, or rollback EF migrations/databases when explicitly requested. Use `SafeRide_EFCore_Migration_Workflow.md` for exact EF commands.
- For Flutter changes, check service, provider/state, page/widget, model DTO, and route usage together.

## Verification
Backend from `src/SRD_Master`:
- `dotnet build SafeRide.slnx`
- `dotnet test SafeRide.UnitTests/SafeRide.UnitTests.csproj`
- `dotnet test SafeRide.IntegrationTests/SafeRide.IntegrationTests.csproj`

Flutter from `src/safe_ride_flutter`:
- `flutter pub get`
- `flutter analyze`
- `flutter test`

Run targeted verification first based on the files changed. Avoid fixing unrelated existing warnings unless requested. For Markdown-only edits, manual diff review is enough.

## Output Style
- Report changed files.
- Summarize why each change was needed.
- Mention commands run and whether they passed.


## Clarification and Assumption Rules

* Do not guess important product, architecture, database, security, configuration, or workflow decisions.
* If a task has multiple valid implementation approaches, ask for confirmation before changing code.
* Ask before introducing new infrastructure, new packages, new database tables, new migrations, new background jobs, new external providers, or new business flows.
* Ask before changing existing booking, matching, offer, trip, payment, promotion, authentication, Redis, Hangfire, SignalR, or map-routing behavior.
* Ask before performing destructive or history-changing actions such as deleting files, removing migrations, dropping databases, resetting branches, rebasing, force-pushing, or deleting branches.
* For small, low-risk, obvious implementation details, proceed with the most consistent existing pattern and report the assumption in the final summary.
* When uncertain, prefer a short implementation plan and wait for approval before editing.
