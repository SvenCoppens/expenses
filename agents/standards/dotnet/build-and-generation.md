# Build, Generation & CLI Workflows

## 1. Solution & Project Build Commands

### 1.1 Building the Project
From the repository root (`/Users/svencoppens/Projects/Personal/Expenses`):
```bash
dotnet build Expenses/Expenses.sln
```

From the solution directory (`/Users/svencoppens/Projects/Personal/Expenses/Expenses`):
```bash
dotnet build Expenses.sln
```

### 1.2 Running the Application
```bash
dotnet run --project Expenses/Expenses/Expenses.csproj
```

### 1.3 Running Tests
```bash
dotnet test Expenses/Expenses.sln
```

---

## 2. Entity Framework Core Migration Workflows

The startup project and target project are located at `Expenses/Expenses/Expenses.csproj`.

### 2.1 Creating a New Migration
Run from repo root:
```bash
dotnet ef migrations add <MigrationName> \
  --project Expenses/Expenses/Expenses.csproj \
  --startup-project Expenses/Expenses/Expenses.csproj \
  --output-dir Data/Migrations
```

### 2.2 Applying Pending Migrations to the Database
```bash
dotnet ef database update \
  --project Expenses/Expenses/Expenses.csproj \
  --startup-project Expenses/Expenses/Expenses.csproj
```

### 2.3 Removing the Last Migration (if not applied)
```bash
dotnet ef migrations remove \
  --project Expenses/Expenses/Expenses.csproj \
  --startup-project Expenses/Expenses/Expenses.csproj
```

---

## 3. Docker & Container Workflows

The solution includes `Expenses/compose.yaml` and `Expenses/Expenses/Dockerfile`.

### 3.1 Local Container Run
From the `Expenses` folder:
```bash
docker compose up --build -d
```

---

## 4. Troubleshooting Build & Runtime Issues

- **SQLite Locked (`busy`)**: If migrations fail due to file locking on `app.db`, ensure all running instances of `Expenses.dll` or IDE database visualizers are stopped.
- **Static Assets / Scoped CSS**: If Blazor component styles (`.razor.css`) or imported static assets are not refreshing, run `dotnet clean Expenses/Expenses.sln && dotnet build Expenses/Expenses.sln`.
