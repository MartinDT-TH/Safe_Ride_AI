# Integration test database providers

Tests declare their persistence dependency through the `DatabaseProvider` trait.

- `InMemory`: service-level integration tests that do not claim relational constraint,
  transaction, filtered-index, or SQL Server concurrency coverage.
- `SQLite`: API-host and model-metadata tests using an isolated SQLite file or connection.
- `SQLServer`: tests that require SQL Server semantics. These use a unique database whose
  name starts with `SafeRide_Test_`, apply migrations, and delete only that owned database.

`SQLServer` tests use `SAFERIDE_TEST_SQLSERVER` when it is set; otherwise they use
`(localdb)\\MSSQLLocalDB`. Any database name in the supplied connection string is replaced
with a generated test database name, so development databases are never selected.
Set `SAFERIDE_RUN_SQLSERVER_TESTS=true` to opt into the LocalDB fallback. Supplying
`SAFERIDE_TEST_SQLSERVER` opts in automatically. Without either variable, relational tests
are reported as skipped instead of silently using a developer database.

Run only relational tests:

```powershell
dotnet test SafeRide.IntegrationTests.csproj --filter "DatabaseProvider=SQLServer"
```

The SQL Server fixture resets data by dropping and recreating only its own generated test
database. Its name/connection-string guard rejects cleanup outside that scope.
