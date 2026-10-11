---
description: Run Entity Framework Core SQLite migrations for the Expenses project.
---

Manage EF Core database migrations:
Arguments provided: $ARGUMENTS

If the user asks to add a migration (e.g. `add <MigrationName>`):
Run:
`dotnet ef migrations add $ARGUMENTS --project Expenses/Expenses/Expenses.csproj --startup-project Expenses/Expenses/Expenses.csproj --output-dir Data/Migrations`

If the user asks to update the database (or runs without arguments):
Run:
`dotnet ef database update --project Expenses/Expenses/Expenses.csproj --startup-project Expenses/Expenses/Expenses.csproj`
