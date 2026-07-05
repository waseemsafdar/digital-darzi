# Digital Darzi API

A multi-tenant **tailor shop management system** built with ASP.NET Core 8. It handles customers, measurements, orders, tailoring workflow (karigar task stages), payments, expenses, staff attendance & salaries, and reporting — all scoped per tenant and per branch (shop) via JWT claims.

> Building a frontend? See **[Frontend_Developer_Guide.md](Frontend_Developer_Guide.md)** for the full endpoint reference, response envelope, roles, and role-based workflows. The raw API surface is also in [api_endpoints.yml](api_endpoints.yml).

---

## Tech Stack

| Concern | Choice |
|---------|--------|
| Runtime | .NET 8 (`net8.0`) |
| Web | ASP.NET Core Web API + Swagger (Swashbuckle) |
| Database | PostgreSQL via EF Core (`Npgsql`) |
| Auth | ASP.NET Core Identity + JWT Bearer |
| Password hashing | BCrypt.Net-Next |
| Mapping | AutoMapper |

---

## Architecture

The solution follows a clean / layered architecture. Dependencies point inward toward `Domain`.

```
API              → Controllers, middleware, DI wiring, JWT + Swagger setup (entry point)
Application      → Interfaces, ViewModels, mappings, common models, service contracts
Domain           → Entities and enums (no external dependencies)
Infrastructure   → EF Core DbContext, Identity, migrations, service implementations, file storage
Repositories     → Repository implementations (data access)
```

### Multi-tenancy & branch scoping
- Every request resolves `tenantId` and `shopId` (branch) from the JWT claims.
- `ApplicationDbContext` is constructed **per-request** with those IDs (see [API/Program.cs](API/Program.cs#L19-L31)), so all queries are automatically filtered to the caller's tenant and active branch.
- Branch switching issues a new token (`POST /api/auth/switch-branch`).

### Key domain areas
- **Customers & Measurements** — customer records, reusable measurement profiles/templates, per-order measurement snapshots.
- **Orders** — order items, stage assignments to karigars, stage logs, status history, images, payments, alterations.
- **Finance** — order payments, shop expenses.
- **Staff** — attendance and salaries.
- **Reporting** — cross-cutting summaries and karigar performance.
- **Admin provisioning** — platform-level tenant setup (`SystemAdmin`).

### Roles
`SystemAdmin` · `Owner` · `Manager` · `Receptionist` · `Karigar` — see the frontend guide for capabilities.

---

## Getting Started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- PostgreSQL (local or hosted)

### Configuration
Set the following (via `appsettings.json`, `appsettings.Development.json`, environment variables, or user-secrets):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=digitaldarzi;Username=postgres;Password=***"
  },
  "Jwt": {
    "Key": "a-long-random-signing-secret",
    "Issuer": "DigitalDarzi",
    "Audience": "DigitalDarziClients"
  }
}
```

> `Jwt:Key` is required — the app throws at startup if it is missing.

### Run

```bash
dotnet restore
dotnet run --project API
```

On startup the app **applies EF Core migrations and seeds** roles and initial data automatically (see [API/Program.cs](API/Program.cs#L147-L159)). No manual `database update` step is needed for normal runs.

Once running, open **Swagger UI** at `/swagger` to explore and try endpoints (use the **Authorize** button to paste a JWT).

### Database migrations (when the model changes)

```bash
dotnet ef migrations add <Name> --project Infrastructure --startup-project API
dotnet ef database update --project Infrastructure --startup-project API
```

---

## Conventions

- **Response envelope** — every endpoint returns `{ success, message, data, errors }`. On failure, `data` is `null` and `errors[]` carries the messages.
- **Auth** — send `Authorization: Bearer <token>` on every request. Never send `tenantId` manually; it comes from the token.
- **CORS** — currently open (any origin/method/header); tighten before production.
- Base CRUD behavior is shared via `BaseCrudController` / `BaseCrudService`; generic services (Shop, Customer, Expense, Salary, Attendance) are registered as open generics in DI.

---

## Project Layout

```
DigitalDarziApi/
├── API/                     ASP.NET Core host
│   ├── Controllers/         HTTP endpoints
│   ├── Middleware/          Global exception handler, subscription check
│   ├── Services/            CurrentUserService, etc.
│   └── Program.cs           DI, auth, migrate + seed, pipeline
├── Application/             Interfaces, ViewModels, Mappings, Common
├── Domain/                  Entities, Enums
├── Infrastructure/          Data (DbContext, UnitOfWork), Identity, Migrations, Services
├── Repositories/            Repository implementations
├── Frontend_Developer_Guide.md
└── api_endpoints.yml
```
