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