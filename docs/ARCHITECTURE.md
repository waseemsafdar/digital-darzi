# Digital Darzi API — Architecture

Status: describes the **current** architecture as implemented on 2026-09-27, then a **proposed
target** architecture. Proposed items are labelled and are not implemented. Large structural
changes wait for approval.

---

## 1. Current architecture

### 1.1 Style

A layered monolith with clean-architecture dependency direction. Single deployable, single
PostgreSQL database, row-level multi-tenancy. There is no frontend, message bus, cache, or
background worker in the repository.

```
┌─────────────────────────────────────────────────────────────┐
│ API                controllers · middleware · JSON · DI      │
├─────────────────────────────────────────────────────────────┤
│ Infrastructure     DbContext · UoW · Identity · services     │
│ Repositories       query-heavy repositories per aggregate    │
├─────────────────────────────────────────────────────────────┤
│ Application        interfaces · ViewModels · mappings · common│
├─────────────────────────────────────────────────────────────┤
│ Domain             entities · enums                          │
└─────────────────────────────────────────────────────────────┘
```

Project references: `API → Application, Infrastructure, Repositories`;
`Infrastructure → Application, Domain`; `Repositories → Application, Domain, Infrastructure`;
`Application → Domain`.

### 1.2 Request pipeline

`GlobalExceptionMiddleware → Swagger → CORS (any origin) → HTTPS redirect → Authentication (JWT)
→ Authorization → SubscriptionMiddleware → controllers`
([Program.cs:169-181](../API/Program.cs#L169-L181)).

Controllers derive from `BaseController`, which converts `ApiResponse<T>` into an HTTP status.
Generic list/detail/create/update/delete controllers derive from `BaseCrudController<TDetail,
TCreate, TUpdate>` backed by `BaseCrudService`. All responses use the envelope
`{ success, message, data, errors }`.

### 1.3 Multi-tenancy

- `TenantId` = a shop business (the paying customer). `BranchId` = one physical shop; the root
  branch has `BranchId == Shop.Id`. Both are stamped on every `BaseDBModel` row.
- `ApplicationDbContext` is built per request from the `tenantId` and `shopId` JWT claims
  ([Program.cs:20-31](../API/Program.cs#L20-L31)) and applies global query filters:
  `Shop`, `User` on `TenantId`; every operational entity on `TenantId + BranchId + !IsDeleted`
  ([ApplicationDbContext.cs:46-65](../Infrastructure/Data/ApplicationDbContext.cs#L46-L65)).
- SystemAdmin and login flows bypass filters with `IgnoreQueryFilters()` and supply explicit
  predicates; `MeasurementRepository` bypasses them **without** a predicate (defect A3 in the
  audit).
- Branch switching re-issues the JWT with a different `shopId`. The `User.ShopIds` array is the
  membership list.
- Writes are stamped by hand in each service. There is no `SaveChanges` interceptor.

### 1.4 Modules as they exist

| Conceptual module | Where it lives today | Boundary quality |
|---|---|---|
| Identity | `AuthService`, `ApplicationUser`, Identity tables | Mixed with domain `User` (two user records per person, linked by `AuthId`) |
| Tenants / Shops | `AdminProvisioningService`, `TenantSetupService`, `ShopService`, `SubscriptionMiddleware` | Clear |
| Users / Staff | `UserService` | Clear |
| Customers | `CustomerService` (generic), `CustomerRepository` | Clear; denormalised totals updated from `OrderService` |
| Measurements | `MeasurementService`, `MeasurementRepository` | Clear |
| Orders | `OrderService` (477 lines: create, update, status, payment, alteration, stage assign, stage log, cancel, deliver) | Too many responsibilities in one class |
| Karigars / Workflow | `KarigarService` (queues, earnings) + stage methods inside `OrderService` | Split across two services |
| Payments | inside `OrderService` | Not separated |
| Expenses / Salaries / Attendance | `FinanceService` **and** three generic services | Duplicated |
| Reports | `ReportingService`, `FinanceRepository` | Clear |
| Notifications, Audit, Inventory, Delivery, Alterations (as workflow) | absent | — |

### 1.5 Evaluation against the brief's principles

| Principle | Assessment |
|---|---|
| Separation of concerns | Good at the layer level. `OrderService` and `FinanceService` mix several bounded responsibilities. |
| SOLID | DI everywhere, interfaces for every service and repository. Open generics for CRUD services are a reasonable reuse mechanism. `OrderService` violates single responsibility. |
| DRY | Two duplicated surfaces (finance vs generic CRUD) and manual tenant stamping repeated in every service. |
| KISS | Yes. No over-abstraction. |
| DTOs | Consistent; entities never leave the API. |
| Validation | Almost none at the boundary. |
| Domain logic separation | Entities are anaemic; all rules are in services. Acceptable for this size, but order state rules should move to a single place. |
| API design | RESTful, consistent envelope, paged lists. Status codes partially inferred by string matching. |
| Database design | Reasonable schema; duplicate `ShopId`/`BranchId`; indexes on the wrong one; several unused tables and enums. |
| Testability | Services depend on interfaces and `ICurrentUserService`, so unit tests are feasible. `ApplicationDbContext` per-request construction makes integration tests slightly awkward but not hard. |

---

## 2. Target architecture (PROPOSED)

The recommendation is to **keep the modular monolith** and keep the five projects. No
microservices, no new hosting model. Changes are internal boundaries, cross-cutting plumbing, and
a handful of new modules. Every item below is proposed, not implemented.

### 2.1 Module boundaries inside the existing projects

Group services, repositories, ViewModels and mappings by module folder instead of by type, so each
module can be read in one place. Suggested folder set, matching the brief:

```
Identity · Tenants · Customers · Measurements · Orders · Garments · Workflow (stages, cutting,
stitching, QC, trial) · Karigars · Payments · Alterations · Delivery · Expenses · Inventory
(later) · Notifications · Reports · Settings · Audit
```

Concrete splits:

- `OrderService` → `OrderService` (create/update/cancel/deliver), `OrderWorkflowService` (status
  transitions, stage assignment, stage log, item status derivation), `PaymentService` (record,
  refund, balance), `AlterationService`.
- Delete the generic `ExpensesController`, `SalariesController`, `AttendanceController` **or** make
  them the only surface and remove the copies from `FinanceController`. Recommendation: keep
  `/api/finance/*` (it works today) and remove the generic three, with a deprecation note in the
  frontend guide.
- Move `SubscriptionMiddleware` logic into a `SubscriptionGuard` service with cached tenant status,
  called from the middleware.

### 2.2 Cross-cutting plumbing

| Concern | Proposal |
|---|---|
| Tenant stamping | `SaveChangesAsync` override (or `ISaveChangesInterceptor`) in `ApplicationDbContext`: on `Added` set `TenantId`, `BranchId`, `CreatedBy`, `CreatedOn`; on `Modified` set `UpdatedBy`, `UpdatedOn`; reject an `Added` operational entity whose `TenantId` would be empty when the caller is not SystemAdmin. Removes an entire bug class. |
| DbContext lifetime | Inject `ICurrentUserService` into the context so `AddDbContextPool` and `EnableRetryOnFailure` become possible. Keep the per-request scoping semantics. |
| Concurrency | `UseXminAsConcurrencyToken()` on `Order`, `Customer`, `OrderPayment`, later `KarigarPayout`. Retry-once on `DbUpdateConcurrencyException` for payments. |
| Date handling | Add a `DateTime` model binder that applies the same UTC normalisation as the JSON converter. Migrate calendar-only columns (`ExpenseDate`, `StaffAttendance.Date`, `DeliveryDate`, `Salary Month/Year`) to `DateOnly`/`date`. Store shop time zone in `Shop.TimeZone` (default `Asia/Karachi`) and compute "today" per shop. |
| Validation | FluentValidation with one validator per create/update ViewModel, registered automatically; controllers stay thin. |
| Errors | `ApiResponse` carries an explicit status code; exception middleware maps only known exception types and logs the rest with a correlation id. |
| Logging | Serilog with request logging, tenant and user enrichment, JSON console sink. |
| Health / limits | `AddHealthChecks().AddNpgSql()`, `/health/live` and `/health/ready`; ASP.NET rate limiter on `/api/auth/*`. |
| Configuration | Secrets from environment / user-secrets only; `appsettings.json` keeps non-secret defaults; an `appsettings.template.json` documents the keys. |
| Startup | Migrations run by a `--migrate` command or CI step, not on web startup. Seeding limited to roles and system measurement catalogue; admin user only in Development from configuration. |
| Background work | A hosted service (or Hangfire/Quartz when justified) for notification dispatch, trial-expiry, and reminder jobs. Not needed until notifications land. |
| Caching | `IMemoryCache` for tenant subscription status and the measurement catalogue. Distributed cache only if a second instance is deployed. |

### 2.3 Order workflow (module Workflow)

Single `OrderStateMachine` with an explicit transition table. Proposed status set, adapted to what
exists rather than the brief's full list:

```
Pending → Received → Cutting → Stitching → Embroidery? → Finishing → QualityCheck?
      → TrialReady → TrialDone → AlterationRequired? → ReadyToDeliver
      → PartiallyDelivered → Delivered
any non-terminal → Cancelled
```

- Payment state becomes a separate `PaymentStatus` (`Unpaid`, `Partial`, `Paid`, `Refunded`) and
  `FullyPaid` leaves `OrderStatus`.
- Every transition writes `OrderStatusHistory` (from, to, user, time, note). The table already
  exists.
- `OrderItem.Status` is derived from its stage logs; `Order.Status` advances when all items reach a
  stage. Manual override remains available to Owner/Manager and is recorded as such.
- Cutting, QC and Trial are stages, not new tables, until fabric tracking requires a `CuttingJob`.

### 2.4 Karigar payments (module Karigars)

- `KarigarRate` (tenant, karigar or role, garment type, stage, rate) replaces the per-assignment
  `StagePrice` as the default; `StagePrice` remains as an override.
- `KarigarPayout` ledger: earned lines from completed stage logs, advances, deductions, payments,
  running balance. Earnings only from `OrderItemStageLog.CompletedAt != null`.

### 2.5 Notifications (module Notifications)

`INotificationSender` abstraction with `WhatsAppSender`, `SmsSender`, `NullSender`. Business
services raise domain events (`OrderReady`, `BalanceDue`) into an outbox table; a hosted service
delivers them. Templates per tenant with placeholders. Providers are configuration, never
referenced from business code.

### 2.6 Audit (module Audit)

`AuditLog` table (tenant, branch, user, action, entity, entity id, old json, new json, at).
Written by the `SaveChanges` interceptor for a whitelist of entities and by explicit calls for
actions that are not entity mutations (login, role change, payment reversal).

### 2.7 Authorization

Keep the five roles. Add a `Cutter` role only when cutting gets its own queue. Replace per-action
role lists with named policies (`Orders.Write`, `Payments.Record`, `Users.ManageRoles`,
`Reports.View`) mapped from roles in one file, so a future permission table can replace the mapping
without touching controllers. Resource checks (caller is assigned karigar; shop belongs to tenant)
live in the services.

### 2.8 What does not change

Project layout, PostgreSQL, EF Core, Identity + JWT, AutoMapper (upgrade version), the response
envelope, the existing routes (except the three generic duplicates), the measurement template model,
and the per-tenant row-level isolation strategy.

---

## 3. SaaS readiness path

The current design already supports Shop A / Shop B isolation at the row level. To open
self-service later without a rewrite:

1. Fix isolation defects (audit A2, A3, S5) and add the stamping interceptor.
2. Introduce `SubscriptionPlan` (limits: branches, users, orders/month) and enforce in the guard.
3. Add tenant self-signup that reuses `AdminProvisioningService` steps.
4. Add per-tenant settings (`Shop.TimeZone`, currency, notification templates, garment attribute
   catalogue).
5. Only if tenant count or data volume justifies it, add Postgres row-level security as a second
   line of defence behind the EF filters.
