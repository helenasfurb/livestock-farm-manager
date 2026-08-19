# MuuBoi — Livestock Farm Manager

ASP.NET Core 8 REST API for livestock property management. Academic project (TCC). Multi-tenant by `PropertyId` — every user belongs to a single `Property`.

## Solution structure

```
livestock-farm-manager/
├── MuuBoi/               # Main API project (net8.0)
│   ├── Api/              # Controllers, Middleware
│   ├── Application/      # Services, Interfaces, DTOs, AutoMapper profiles
│   ├── Domain/           # Models, Exceptions
│   └── Infrastructure/   # Repositories, Migrations, DbContext
└── MuuBoi.Tests/         # Unit test project (net10.0) — xUnit + Moq
```

## Commands

```bash
# Run the API
dotnet run --project MuuBoi/MuuBoi.csproj

# Run tests
dotnet test MuuBoi.Tests/MuuBoi.Tests.csproj

# Add a migration (ask before running)
dotnet ef migrations add <MigrationName> --project MuuBoi

# Apply migrations (ask before running)
dotnet ef database update --project MuuBoi
```

The local database is SQL Server at `Server=localhost;Database=MuuBoiDb`. Connection string lives in `appsettings.json`; secrets (JWT key, etc.) should use `dotnet user-secrets`.

## Namespace convention

All namespaces follow `MuuBoi.<Layer>.<Subfolder>`. **Do not use flat namespaces** (`MuuBoi.Models`, `MuuBoi.Services`, etc.) — the codebase is being migrated to the layered form.

| Folder | Namespace |
|--------|-----------|
| `Api/Controllers/` | `MuuBoi.Api.Controllers` |
| `Api/Middleware/` | `MuuBoi.Api.Middleware` |
| `Application/Services/` | `MuuBoi.Application.Services` |
| `Application/Interfaces/` | `MuuBoi.Application.Interfaces` |
| `Application/DTOs/` | `MuuBoi.Application.DTOs` |
| `Application/Mappings/` | `MuuBoi.Application.Mappings` |
| `Domain/Models/` | `MuuBoi.Domain.Models` |
| `Domain/Exceptions/` | `MuuBoi.Domain.Exceptions` |
| `Infrastructure/Repositories/` | `MuuBoi.Infrastructure.Repositories` |
| `Infrastructure/Migrations/` | `MuuBoi.Infrastructure.Migrations` |

## Architecture rules

### Layering
- Controllers call Services only — never repositories directly.
- Services depend on repository interfaces (`Application/Interfaces/`), never on concrete repositories.
- Domain models have no dependencies on other layers.
- Repositories depend only on `ApplicationDbContext` and domain models.

### Multi-tenancy
Tenant isolation is enforced **at the repository layer**. Repositories receive `ITenantProvider` via DI and filter every query by `PropertyId`. Services must not add tenant filters — they trust the repository to return only data belonging to the current tenant.

### Entities
- All domain entities inherit from `BaseEntity` (Id, IsActive, CreatedAt, UpdatedAt).
- Entities with tenant scope implement `ITenantEntity` (adds `PropertyId`).
- Soft delete is preferred: set `IsActive = false` instead of removing rows, unless there is a clear reason to hard-delete.

### DTOs
Use separate DTO classes per operation:
- `<Entity>CreateDto` — input for POST
- `<Entity>UpdateDto` — input for PATCH (all fields optional)
- `<Entity>Dto` / `<Entity>ResponseDto` — output

Map between domain and DTOs exclusively via AutoMapper profiles in `Application/Mappings/`.

### HTTP conventions
- `GET` — read, no side effects
- `POST` — create, returns `201 Created` with `CreatedAtAction`
- `PATCH` — partial update, returns `200 OK` with updated resource
- `DELETE` — returns `204 No Content`
- Return `404 Not Found` (not `null`) when a resource does not exist

### Error handling

The service layer uses domain exceptions for all error signaling. `ExceptionMiddleware` in `Api/Middleware/` catches them and maps to HTTP responses. **Never return `null` to signal an error — always throw.**

| Exception | HTTP Status | When to use |
|-----------|-------------|-------------|
| `NotFoundException` | `404 Not Found` | Required entity does not exist |
| `ConflictException` | `409 Conflict` | State conflict — e.g., duplicate unique field, entity already in target state |
| `BusinessRuleException` | `422 Unprocessable Entity` | Business rule violation that requires a DB query and cannot be caught by DTO validation alone |

All three exception classes live in `Domain/Exceptions/`. `ExceptionMiddleware` must handle all three.

## Testing conventions

Framework: **xUnit** with **Moq**. All tests live in `MuuBoi.Tests/`.

- Test **services only** — repositories are always mocked via their interfaces.
- One test class per service class.
- Naming: `MethodName_Condition_ExpectedResult`
  - Example: `CreateAnimalAsync_WithValidDto_ReturnsCreatedAnimalDto`
- Arrange / Act / Assert blocks, no blank lines between them.
- Do not test mapping logic inside service tests — trust AutoMapper.

## Enum conventions

- Enum **member names** must be in **English** (e.g., `Holstein`, `Crossbred`, `BornOnFarm`).
- Enum members must have a `[Description("...")]` attribute with the **Portuguese** display label (e.g., `[Description("Holandesa")]`).
- This applies to all enums in `Domain/Enums/`.

## Things that require confirmation before doing

Always ask before:
- Creating or modifying migration files
- Changing `Program.cs` or any DI registration
- Committing or pushing to remote
- Any destructive git operation (reset, force-push, branch delete)
