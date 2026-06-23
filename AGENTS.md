# SafeRide Agent Instructions

## Project
SafeRide is a .NET 8 Clean Architecture + Flutter project.

Backend:
- src/SRD_Master/SafeRide.API
- src/SRD_Master/SafeRide.Application
- src/SRD_Master/SafeRide.Infrastructure
- src/SRD_Master/SafeRide.Domain

Flutter:
- src/safe_ride_flutter

## Rules
- Do not scan the whole repository unless the task truly requires it.
- Start with targeted search and inspect only relevant files.
- Prefer small, isolated patches.
- Do not rewrite unrelated files.
- Do not modify local/dev-only config unless explicitly asked:
  - launchSettings.json
  - android/gradle.properties
  - local api key files
- Use English for code identifiers.
- User-facing messages must be Vietnamese.
- For database changes, check EF entities, configurations, migrations, and DTOs together.
- For Flutter changes, check service, provider/state, page/widget, model DTO, and route usage together.

## Configuration Rules

Do not hard-code operational values in code.

Move these values into strongly typed Options sections in appsettings.Development.json:
- timeout values
- interval values
- Redis TTL values
- Hangfire cron expressions
- Hangfire recurring job ids
- cleanup retention days
- cleanup batch sizes
- notification titles
- notification types
- SignalR event names/types
- matching expiration durations
- offer expiration durations

Use the Options pattern:
- Each Options class must have a SectionName constant.
- Bind Options during dependency injection.
- Inject IOptions<T> or IOptionsMonitor<T>.
- Domain must not reference IConfiguration or IOptions.
- Avoid direct IConfiguration access except in composition root or infrastructure setup.

## Verification
Backend:
- dotnet build
- Run targeted tests if available

Flutter:
- flutter analyze
- Avoid fixing unrelated existing warnings unless requested

## Output style
- Report changed files.
- Summarize why each change was needed.
- Mention commands run and whether they passed.