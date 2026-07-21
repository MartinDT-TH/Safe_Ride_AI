# SafeRide Agent Instructions

## 1. Scope and Working Mode

These instructions apply to every coding agent working in this repository, including Codex, Antigravity, IDE agents, CLI agents, and subagents.

- Follow the user's explicit task and constraints.
- Treat this file as repository-wide guidance.
- A more specific `AGENTS.md` inside a subdirectory may add local rules for that directory.
- For read-only, review, analysis, diagram, or explanation tasks, do not modify files.
- For implementation tasks, complete the implementation and verification. Do not stop after producing a plan unless a high-impact unresolved decision prevents safe progress.
- Prefer the smallest coherent change that fully satisfies the requested outcome.
- Do not invent product requirements, business rules, credentials, infrastructure, or external dependencies.
- Distinguish production architecture from simulator, test, seed, and development-only shortcuts before reporting an architecture violation.

## 2. Architecture Overview

SafeRide is a modular monolith deployed as one ASP.NET Core host, with a Flutter mobile client.

Production features follow Clean Architecture boundaries. Development simulators may use explicitly isolated orchestration shortcuts and must not be treated as production architecture patterns.

SafeRide is not currently a microservice system.

- Do not introduce microservices, message brokers, distributed events, or new network boundaries unless explicitly requested.
- Keep durable business behavior inside the existing modular monolith unless there is an approved architectural change.
- Reuse existing application and infrastructure capabilities before creating parallel implementations.
- Architectural purity must not be pursued by breaking working simulator or demo flows that are intentionally isolated and guarded.

## 3. Project Map

Backend solution:

- Solution: `src/SRD_Master/SafeRide.slnx`
- API: `src/SRD_Master/SafeRide.API`
- Application: `src/SRD_Master/SafeRide.Application`
- Domain: `src/SRD_Master/SafeRide.Domain`
- Infrastructure: `src/SRD_Master/SafeRide.Infrastructure`
- Realtime: `src/SRD_Master/SafeRide.Realtime`
- Contracts: `src/SRD_Master/SafeRide.Contracts`
- Shared: `src/SRD_Master/SafeRide.Shared`
- Unit tests: `src/SRD_Master/SafeRide.UnitTests`
- Integration tests: `src/SRD_Master/SafeRide.IntegrationTests`

Flutter:

- App root: `src/safe_ride_flutter`
- Uses Provider, GetIt, Dio, SignalR client, Google Maps/VietMap services, and feature-based folders.

Before editing, locate the existing execution path and reuse established project patterns.

## 4. Backend Dependency Direction

The current project dependency direction is approximately:

```text
API
 ├── Application
 ├── Infrastructure
 ├── Realtime
 ├── Contracts
 └── Shared

Infrastructure
 ├── Application
 ├── Domain
 ├── Realtime
 ├── Contracts
 └── Shared

Realtime
 ├── Application
 ├── Contracts
 └── Shared

Application
 ├── Domain
 ├── Contracts
 └── Shared

Domain
 └── no dependency on outer SafeRide projects
```

Rules:

- Domain must not reference Application, Infrastructure, Realtime, API, configuration, EF Core, Redis, Hangfire, SignalR, or external providers.
- Application must not reference Infrastructure implementations or API.
- Realtime must not become the source of durable business state.
- Contracts contains cross-boundary contracts only. Do not put business logic or infrastructure implementation in Contracts.
- Shared contains truly shared primitives or utilities only. Do not turn Shared into a dumping ground.
- Production code must not depend on simulator types.
- Do not create circular project references.
- Do not copy simulator dependency shortcuts into production features.

## 5. Layer Responsibilities

### API

API owns:

- the ASP.NET Core composition root;
- controllers and HTTP concerns;
- middleware and filters;
- Swagger configuration;
- authentication and authorization pipeline wiring;
- SignalR hub mapping;
- Hangfire dashboard exposure;
- recurring background-job registration entrypoints;
- environment-specific startup behavior.

Rules:

- Controllers coordinate HTTP concerns only.
- Do not place durable business rules in controllers.
- Preserve middleware ordering unless the task explicitly requires a reviewed change.
- Do not return EF Core entities directly from endpoints.
- New API errors must follow the existing ProblemDetails contract.

### Application

Application owns:

- MediatR commands, queries, handlers, and use-case orchestration;
- application interfaces for infrastructure capabilities;
- DTOs and request/response models;
- validation;
- strongly typed application option models where appropriate;
- stateless application and business services such as fare estimation or license compatibility.

Rules:

- Application must depend on abstractions, not infrastructure implementations.
- Do not access EF Core, Redis, Hangfire, SignalR hubs, HTTP clients, or external SDKs directly from Application.
- Keep orchestration in handlers or application services and reusable business rules in focused services.
- Do not create abstractions that provide no real boundary, reuse, or testability.

### Domain

Domain owns:

- entities;
- enums;
- value objects;
- domain invariants and domain behavior that can remain infrastructure-independent.

Rules:

- Domain must remain free of framework and infrastructure dependencies.
- Do not put configuration, persistence, transport, serialization, or provider-specific logic in Domain.

### Infrastructure

Infrastructure owns:

- EF Core and SQL Server;
- NetTopologySuite spatial persistence;
- ASP.NET Core Identity stores and implementations;
- repositories and unit of work;
- Redis and resilient in-memory fallback;
- Hangfire jobs and schedulers;
- hosted/background services;
- authentication implementations;
- external providers such as maps, SMS, email, payment, and Cloudinary;
- realtime dispatch and tracking implementations;
- development-only simulator orchestration.

Rules:

- Group substantial registrations by capability instead of indefinitely expanding one root dependency-injection file.
- Prefer focused registration extensions for large capabilities such as matching, payment, realtime tracking, or background jobs.
- Simulator orchestration is an intentional exception and must remain isolated from production use cases.

### Realtime

Realtime owns:

- SignalR hubs;
- realtime connection and notification plumbing;
- realtime contracts and dispatch coordination where already established.

Rules:

- Hub mapping belongs in API composition.
- Durable state must be persisted or recoverable through an API.
- SignalR events are notifications, not the sole source of truth.

## 6. Simulator and Development Architecture Exceptions

SafeRide contains mock drivers, mock customers, booking generators, and other simulation utilities used for development, demonstrations, and testing.

The simulator may use controlled shortcuts that are not permitted in normal production features. These exceptions exist to orchestrate realistic application flows without duplicating the full mobile-client behavior.

### Allowed simulator exceptions

Code under established simulator, seed, test, or development-only locations may:

- access Infrastructure services directly;
- resolve repositories, Redis services, EF Core, realtime dispatchers, or application services from dependency injection;
- coordinate multiple application operations inside one hosted service;
- generate mock users, locations, bookings, offers, trip updates, or payments;
- imitate Customer or Driver application behavior;
- perform development-only cleanup of known mock data;
- use direct orchestration when routing every action through the mobile application would make the simulator impractical.

These exceptions are allowed only when all of the following are true:

- the code remains inside an established simulator, seed, test, or development-only area;
- registration is protected by environment and configuration checks;
- it is disabled by default outside approved development or demo environments;
- it does not change production business rules;
- production code does not depend on simulator types;
- Domain and Application do not reference simulator implementations;
- production services are reused where practical instead of reimplementing business rules;
- failures cannot corrupt unrelated production data.

### Simulator boundaries

- Do not treat simulator code as the architectural pattern for production features.
- Do not move simulator shortcuts into controllers, application handlers, domain entities, repositories, or production background jobs.
- Do not weaken authorization, validation, concurrency protection, or state-transition checks merely to support simulated clients.
- Prefer explicit mock-user identifiers, configuration flags, or simulator metadata.
- Do not identify mock data using fragile assumptions such as names, ordering, or list positions.
- Simulator cleanup must target only known mock records and Redis members.
- Simulator jobs must be idempotent and safe after application restarts.
- Simulator code must not send real SMS, email, push notifications, payments, Cloudinary uploads, or paid map requests unless explicitly enabled for a controlled test.
- Do not enable simulator hosted services in production unless explicitly requested for a controlled demonstration environment.

### Dependency review rule

When reviewing dependency direction:

1. Determine whether the code belongs to a production path or a simulator, seed, test, or development-only path.
2. Enforce normal Clean Architecture boundaries for production paths.
3. Preserve intentional simulator shortcuts when they are isolated, guarded, and necessary for orchestration.
4. Do not refactor an intentional simulator dependency solely to make the dependency graph appear theoretically pure.
5. Report simulator exceptions separately from production architecture violations.

## 7. SafeRide Business Invariants

- Matching creates driver offers first. Do not create a Trip until the customer confirms a driver.
- A driver becomes Busy only after a confirmed or assigned trip, not merely because an offer exists.
- Multiple offers may exist for one booking, but duplicate active trips for the same driver or booking are forbidden.
- When a driver accepts or is assigned to another trip, remaining conflicting offers must expire or be ignored safely.
- Booking, offer, trip, payment, promotion, wallet, and withdrawal state transitions must validate the current persisted state before changing it.
- A completed, cancelled, or expired operation must not return to an active state unless an approved business flow explicitly permits it.
- Promotion usage is counted only after a trip is completed. Cancelled or expired bookings must not increment promotion usage.
- Wallet balance changes must have a corresponding durable wallet transaction.
- Repeated commands, retries, SignalR reconnects, callbacks, and Hangfire retries must not create duplicate Trips, Offers, Payments, Notifications, or WalletTransactions.
- Scheduled and background operations must re-check current persisted state immediately before acting.
- Customer-facing and driver-facing application messages must remain Vietnamese.
- Do not change approved booking, matching, offer, trip, payment, promotion, authentication, pricing, wallet, or withdrawal behavior unless the task explicitly requests that behavior change.

## 8. Data Ownership, Transactions, and Time

- SQL Server is the source of truth for durable user, KYC, vehicle, booking, offer, trip, payment, promotion, wallet, withdrawal, rating, report, and notification state.
- Redis is for ephemeral state, cache, live location, short-lived tokens, matching acceleration, coordination, and temporary job metadata.
- Do not make Redis the only source of truth for durable business state.
- Durable multi-record changes must be transactional when partial completion could leave inconsistent state.
- Protect state-changing handlers and jobs against duplicate requests and concurrent execution.
- Prefer idempotent commands, handlers, callbacks, and background jobs.
- Persist timestamps in UTC.
- Convert to local time only at system boundaries or presentation layers.
- Prefer the existing time abstraction when available; otherwise use `DateTime.UtcNow`, not `DateTime.Now`.

## 9. Redis Rules

- Redis GEO indexes are sorted sets.
- Removing a driver from `OnlineDriversGeo` requires sorted-set member removal. Deleting a normal Redis key does not remove the GEO member.
- Driver matching must validate GEO candidates against live Redis status and location data plus database eligibility before creating offers.
- Use Redis TTL for short-lived data where appropriate.
- Do not introduce key-scanning cleanup jobs unless explicitly required.
- `IRedisService` may fall back to in-memory storage when Redis is unavailable.
- Do not assume every `IRedisService` operation is distributed or durable.
- Do not rely on the in-memory fallback for correctness-critical distributed locks, payment idempotency, or durable state without evaluating multi-instance behavior.
- A Redis fallback must not silently make unsafe multi-instance behavior appear correct.
- Redis failures should degrade optional realtime or cache behavior where possible, but must not corrupt durable SQL state.

## 10. Realtime and SignalR Rules

- SignalR hub mapping belongs in API composition.
- Realtime notification implementations belong outside Domain.
- Avoid duplicate SignalR event registration after reconnect, page re-entry, provider recreation, or widget rebuild.
- Dispose SignalR subscriptions, streams, timers, controllers, and other long-lived resources.
- A SignalR event is not durable state.
- Clients must be able to recover current trip, booking, payment, or notification state through an API or persisted state.
- Authenticate and authorize hub connections and hub methods server-side.
- Validate trip or booking membership before joining groups or receiving protected realtime data.
- Do not expose sensitive data through broad groups or client-supplied group names.

## 11. Background Execution Rules

SafeRide uses both Hangfire and ASP.NET Core hosted services.

Use Hangfire for:

- delayed one-off jobs;
- durable retryable jobs;
- jobs whose execution state must survive process restarts;
- scheduled lifecycle actions tied to a booking or trip.

Use `BackgroundService` or hosted services for:

- continuous polling loops;
- runtime coordination;
- work that may restart safely with the API process;
- development-only simulator loops.

Rules:

- Hangfire job implementations belong in Infrastructure.
- Recurring Hangfire jobs are registered from API through Infrastructure extensions.
- Application and Domain must not reference Hangfire.
- Jobs that may become obsolete must safely no-op or be cancelled when the underlying state changes.
- Job retries must not repeat irreversible business effects.
- Hosted services must handle cancellation and application shutdown cleanly.
- Do not implement the same responsibility in both Hangfire and a hosted service without an explicit reason.
- Do not start a new background mechanism when an existing scheduler already owns the lifecycle.

## 12. Security and Privacy

- Enforce authentication, authorization, role checks, and resource ownership in the backend, not only in Flutter.
- Never trust client-supplied CustomerId, DriverId, BookingId, TripId, role, price, status, payment result, or ownership without server-side validation.
- Prefer deny-by-default authorization behavior.
- Do not expose phone numbers, identity documents, tokens, precise historical locations, payment references, or internal-only fields unless required by an approved API contract.
- Do not log OTPs, access tokens, refresh tokens, share tokens, API keys, passwords, identity document numbers, connection strings, or full sensitive payloads.
- Store security-sensitive temporary tokens in hashed or otherwise non-recoverable form when practical.
- Validate uploaded file type, size, ownership, and authorization before storing or returning content.
- Do not weaken TLS, certificate validation, CORS, authentication, authorization, or secret handling merely to make local testing pass.

Public or shared-trip endpoints must:

- use an unguessable token;
- validate expiry and revocation on every access;
- be scoped to one trip;
- return a minimal explicit DTO;
- avoid exposing customer or driver private information.

## 13. Configuration and Secrets

- Do not hard-code operational values in services, handlers, hubs, widgets, or jobs.
- Put timeouts, polling intervals, Redis TTLs, Hangfire cron expressions, recurring job IDs, cleanup retention, batch sizes, notification names, SignalR event names, matching durations, offer expiry, and provider timeouts in strongly typed Options or established environment configuration.
- Each backend Options class should expose a `SectionName` constant and be bound and validated during dependency injection.
- Avoid direct `IConfiguration` access except in composition roots or infrastructure setup.
- Never commit real secrets, connection strings, API keys, JWT secrets, Redis passwords, OAuth credentials, Cloudinary secrets, SMS keys, or map keys.
- Use user secrets, environment variables, ignored local files, or Flutter `env/*.local.json`.

Ask before modifying local or developer-only configuration, including:

- `launchSettings.json`
- `android/gradle.properties`
- `appsettings.Development.json`
- `appsettings.Local.json`
- local environment files
- local API-key files

Do not change production configuration, deploy services, create cloud resources, send real SMS, email, or push notifications, or call paid external services unless explicitly requested.

## 14. API and Backend Conventions

- Follow existing MediatR, DTO, validation, dependency injection, repository, response, and error-handling conventions.
- Preserve existing public API contracts unless the task explicitly requires a breaking change.
- Propagate `CancellationToken` through controllers, handlers, EF Core, Redis, and external-provider calls where supported.
- Use async APIs for I/O-bound work.
- Validate input at the application boundary and enforce business invariants inside the use case.
- Avoid loading unnecessary columns, entities, or large collections.
- Use deterministic ordering before pagination.
- Prevent N+1 database queries and unbounded queries.
- Keep exception messages and logs useful without exposing sensitive data.
- Do not silently swallow exceptions.
- Do not introduce a second response envelope, mapping approach, or logging convention without an explicit reason.

All new API errors must follow the existing ProblemDetails contract:

- content type: `application/problem+json`;
- stable machine-readable `code`;
- `traceId`;
- Vietnamese user-facing `detail` where the response is shown to end users.

Do not introduce ad-hoc error responses such as `{ success, message }` when ProblemDetails is already used.

## 15. Middleware and Startup Rules

Current startup responsibilities include:

- application registration;
- infrastructure registration;
- authorization registration;
- realtime registration;
- optional background-job registration;
- authentication and authorization middleware;
- profile-completion middleware;
- SignalR hub mapping;
- optional Hangfire dashboard.

Rules:

- Preserve middleware order unless the change is explicitly reviewed.
- Authentication must run before authorization.
- Profile-completion checks must not bypass authentication or authorization.
- Exception handling must remain early enough to convert downstream failures consistently.
- SignalR token extraction and hub mapping must remain compatible with the Flutter client.
- Do not expose Hangfire dashboard without the existing authorization protection.
- Environment-specific simulator and seed behavior must remain guarded.

## 16. Database and EF Core Rules

For schema changes, inspect together:

- Domain entities and enums;
- EF Core configurations;
- DbContext and registrations;
- migrations;
- DTOs and contracts;
- handlers and services;
- seed data;
- unit and integration tests.

Rules:

- Use `SafeRide_EFCore_Migration_Workflow.md` for exact migration commands.
- Never edit or delete an already-applied migration to disguise a new schema change.
- Create a new migration for a new schema change.
- Do not generate or apply a migration unless the task explicitly requires a database schema change.
- Do not apply migrations to shared, staging, or production databases unless explicitly requested.
- Review generated migration operations for accidental drops, renames, data loss, or provider-specific behavior.
- Add indexes or constraints only when justified by access patterns or invariants.
- Preserve existing data compatibility whenever practical.
- Use explicit transactions for multi-record durable state changes when partial completion is unsafe.

## 17. Flutter Conventions

- Follow existing Provider, GetIt, Dio, routing, theme, localization, and feature-folder patterns.
- Do not call backend services directly from widgets when an existing service or provider layer owns that responsibility.
- Keep API models, provider state, and widget rendering responsibilities separated according to existing patterns.
- Handle relevant loading, success, empty, expired, unauthorized, offline, validation, and failure states.
- Do not show raw backend exceptions to users.
- Guard asynchronous UI updates with lifecycle checks where required.
- Dispose controllers, focus nodes, streams, subscriptions, timers, and SignalR listeners.
- Prevent duplicate API calls and duplicate realtime listener registration during rebuilds or reconnects.
- Keep customer-facing and driver-facing messages in Vietnamese.
- Do not embed secrets or production-only endpoints directly in Dart source.
- Preserve existing responsive behavior and Android compatibility unless the task says otherwise.
- For cross-cutting API changes, verify backend serialization and Flutter consumption together.

## 18. Agent and Git Safety

- Start with targeted search and inspect only relevant files.
- Do not scan the entire repository unless the task truly requires it.
- Check the existing implementation before proposing a new pattern.
- Prefer small, isolated patches.
- Do not rewrite unrelated files or introduce formatting churn.
- Do not edit generated or build output under:
  - `bin/`
  - `obj/`
  - `build/`
  - `.dart_tool/`
  - `.vs/`
  - `artifacts/`
- Use English for code identifiers.
- Preserve existing newline, formatting, analyzer, and naming conventions.
- Use read-only subagents for mapping, review, and risk discovery when available.
- Only one writer agent should edit the same task or file set at a time.
- Parallel agents must have non-overlapping write scopes.
- Subagents should return compact summaries containing relevant files, current flow, risks, and edit points.
- Do not paste full files unless necessary.
- Do not discard pre-existing user changes.

Do not commit, push, reset, rebase, merge, force-push, delete branches, create releases, or run `git add .` unless explicitly requested.

When Git operations are requested:

- stage only intentional files;
- report staged files;
- preserve unrelated working-tree changes;
- inspect the diff before committing.

Before risky filesystem or Git operations, verify:

- current directory;
- repository root;
- target path;
- Git status;
- requested scope.

Never run destructive broad commands such as:

- deleting a drive, home directory, repository root, or parent directory;
- `rm -rf` or equivalent against an unverified path;
- `git clean -fd` or `git clean -fdx`;
- `git reset --hard`;
- bulk deletion outside the explicitly approved scope.

Ask before deleting files, removing migrations, clearing databases, truncating tables, or replacing large directories.

Do not start persistent servers, simulators, watchers, or tunnels unless needed for validation. Stop processes started by the agent when the task is complete.

## 19. Decision and Clarification Rules

Proceed without asking when:

- the requested outcome is clear;
- the decision is low risk and reversible;
- an established project pattern provides the answer;
- the detail does not change product behavior, public contracts, security, durable data, or infrastructure.

Choose the simplest approach consistent with the existing codebase and report meaningful assumptions in the final summary.

Ask before making a high-impact decision not already implied by the request, including:

- adding or removing database tables;
- changing public API contracts;
- introducing packages, infrastructure, external providers, or background jobs;
- changing authentication, authorization, payment, pricing, booking, matching, offer, trip, promotion, wallet, or withdrawal behavior;
- changing data ownership or durable state;
- performing destructive actions or Git history changes;
- modifying production or local-only secrets and configuration.

When ambiguity does not block safe progress, state a reasonable assumption and proceed.

When ambiguity could cause data loss, security exposure, incompatible business behavior, irreversible changes, or significant rework, stop and request clarification.

## 20. Task Execution Workflow

For implementation tasks:

1. Identify the requested outcome, explicit constraints, and affected surface.
2. Determine whether the execution path is production, simulator, seed, test, or development-only.
3. Inspect the smallest relevant execution path.
4. Locate existing patterns, contracts, tests, configuration, and business invariants.
5. For multi-file or cross-layer changes, form a short implementation plan.
6. Make the smallest coherent patch that fully satisfies the task.
7. Add or update focused tests when the changed behavior is testable.
8. Run the narrowest meaningful verification first.
9. Review the final diff for unrelated changes, secrets, generated files, debug code, temporary workarounds, and formatting churn.
10. Report changed files, decisions, assumptions, verification results, and remaining risks.

Do not claim a command passed unless it was actually run successfully.

## 21. Change Checks

For backend database changes, inspect together:

- Domain entities and enums
- EF Core configurations
- DbContext and registrations
- migrations
- DTOs and contracts
- handlers and services
- seed data
- unit and integration tests

For Flutter changes, inspect together where relevant:

- API service
- model or DTO
- provider or state
- page or widget
- navigation and route usage
- realtime listener lifecycle
- tests

For simulator changes, inspect together where relevant:

- environment and configuration guards;
- mock identifiers;
- cleanup behavior;
- production-service reuse;
- paid or real external side effects;
- restart and idempotency behavior.

## 22. Verification Strategy

Run targeted verification first based on the files and behavior changed.

Backend commands from `src/SRD_Master`:

```bash
dotnet build SafeRide.slnx
dotnet test SafeRide.UnitTests/SafeRide.UnitTests.csproj
dotnet test SafeRide.IntegrationTests/SafeRide.IntegrationTests.csproj
```

Flutter commands from `src/safe_ride_flutter`:

```bash
flutter pub get
flutter analyze
flutter test
```

Use this order:

- Backend single-project change: build the affected project and run related tests.
- Cross-layer or high-risk backend change: build `SafeRide.slnx` and run relevant unit and integration tests.
- Database or state-transition change: run affected integration tests and review migration SQL when applicable.
- Realtime or concurrency change: run focused integration tests and verify duplicate or retry behavior.
- Simulator change: verify environment guards, safe cleanup, and absence of real external side effects.
- Flutter logic, model, provider, or widget change: run targeted tests and `flutter analyze`.
- Flutter dependency change: run `flutter pub get`, `flutter analyze`, and relevant tests.
- Configuration change: validate binding, startup, and environment-variable naming.
- Markdown-only change: manual diff review is sufficient.

Run the full relevant test suite when the change is cross-cutting, security-sensitive, concurrency-sensitive, payment-related, migration-related, or explicitly requested.

Avoid fixing unrelated existing warnings or failures unless requested. Report them separately.

## 23. Definition of Done

A task is complete only when:

- The requested behavior is implemented across all affected layers.
- The production or simulator execution path has been classified correctly.
- Relevant business invariants, authorization, ownership, and privacy rules are preserved.
- Important success, error, retry, expiry, cancellation, and concurrency cases are handled where applicable.
- Public contracts remain compatible unless a change was explicitly requested.
- Targeted verification was run, or the exact reason it could not be run is stated.
- The final diff contains no unrelated edits, generated outputs, secrets, local-only configuration, debug code, or accidental formatting churn.
- Remaining risks or unverified assumptions are clearly reported.

## 24. Final Response Format

Keep the final response concise and include:

- summary of the implemented outcome;
- changed files and why each changed;
- important design decisions or assumptions;
- commands run and pass/fail results;
- tests not run and the reason;
- remaining risks or follow-up work.

Do not paste entire files unless the user asks for them.