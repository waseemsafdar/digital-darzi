# Digital Darzi API — Codebase Audit

Audit date: 2026-09-27. Scope: the whole repository at the working tree of branch `master`
(HEAD `6f0af9f`, plus uncommitted changes in `OrderService`, `OrderRepository`, `Program.cs`,
`StaffAttendanceService` and the new `API/Json/` folder). Every finding below was verified against
the code; where a finding could not be executed against a live database it is marked *not
executed*. Nothing in this audit changed production code.

Companion documents: [ARCHITECTURE.md](ARCHITECTURE.md), [DATABASE.md](DATABASE.md),
[API.md](API.md), [FEATURE_ROADMAP.md](FEATURE_ROADMAP.md),
[IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md).

---

## 1. Summary

The application is a backend-only ASP.NET Core 8 Web API for multi-tenant tailor-shop
management. There is no frontend in this repository. The layering is sound and the core tailoring
flow (customer → measurements → order with items → karigar stage assignment → payment → delivery)
exists end to end. The solution builds with zero errors.

It is **not production-safe or scale-ready as it stands**. The blockers are a small cluster of
correctness and security defects rather than architectural problems:

| # | Severity | Finding | Location |
|---|---|---|---|
| A1 | Critical | Live database password and JWT signing key committed to git | `API/appsettings.json` (tracked since first commit) |
| A2 | Critical | Subscription enforcement never fires: middleware compares `Shop.Id` to the tenant id, which are different GUIDs | [SubscriptionMiddleware.cs:48](../API/Middleware/SubscriptionMiddleware.cs#L48) |
| A3 | Critical | Cross-tenant read: measurement fields and templates are listed for every tenant | [MeasurementRepository.cs:20](../Repositories/Implementations/MeasurementRepository.cs#L20), `:59`, `:76` |
| A4 | High | Manager can grant any role, including Owner, through user update | [UserService.cs:195](../Infrastructure/Services/UserService.cs#L195) |
| A5 | High | Expenses, salaries and attendance created through the generic CRUD controllers are saved with empty TenantId/BranchId and are invisible afterwards | [ShopExpenseService.cs:35](../Infrastructure/Services/ShopExpenseService.cs#L35), `StaffSalaryService.cs:35`, `StaffAttendanceService.cs:139` |
| A6 | High | Tenant filter column `BranchId` has no index; all composite indexes are on the duplicate `ShopId` column | [ApplicationDbContext.cs:51-65](../Infrastructure/Data/ApplicationDbContext.cs#L51-L65) vs `:80-138` |
| A7 | High | Payment recording is read-modify-write with no concurrency token; concurrent payments double-count | [OrderService.cs:261](../Infrastructure/Services/OrderService.cs#L261) |
| A8 | High | Query-string `DateTime` parameters are not normalised to UTC; Npgsql rejects `Kind=Unspecified` for `timestamptz`, so every report or filter with an explicit date is expected to fail with 500 (*not executed*) | `ReportsController.cs`, `FinanceController.cs`, `AttendanceController.cs`, `KarigarController.cs` |
| A9 | High | Dashboard and finance reports load entire order/payment sets into memory | [ReportingService.cs:28](../Infrastructure/Services/ReportingService.cs#L28), `:87`, `:309`, `:363` |
| A10 | High | No test projects of any kind | solution |

---

## 2. Technology stack

| Concern | Choice | Version |
|---|---|---|
| Runtime | .NET | 8.0 (`net8.0`) |
| Web | ASP.NET Core Web API, Swashbuckle | 8.0 / 6.6.2 |
| ORM | EF Core with Npgsql provider | 8.0.4 |
| Database | PostgreSQL (hosted on Neon serverless) | — |
| Identity | ASP.NET Core Identity (`IdentityRole<Guid>`), JWT Bearer | 8.0.0 |
| Passwords | Identity hasher (email users), BCrypt.Net-Next (phone PIN users) | 4.0.3 |
| Mapping | AutoMapper | 14.0.0 — **NU1903 high-severity advisory GHSA-rvv3-g6hj-g44x** |
| Validation | DataAnnotations only, present in a single ViewModel file | — |
| Logging | Default `ILogger`, used in 2 files; no structured sink | — |
| Tests | none | — |

---

## 3. Current architecture

Five projects, dependencies pointing inward:

```
API ──────────► Application ◄──── Infrastructure ◄──── Repositories
 │                   ▲                   │
 └───────────────────┴───── Domain ◄─────┘
```

| Project | Responsibility | Notes |
|---|---|---|
| `Domain` | 22 entity files, 19 enums in one file | `LightEntity`, `ImmutableEntity`, `OrderMeasurementSnapshot` are unused; `OrderImage.cs` actually declares the `Attachment` class |
| `Application` | Service and repository interfaces, ViewModels (DTOs), AutoMapper profiles, `ApiResponse<T>`, `PagedResult<T>` | ViewModels are the API contract; entities are never returned directly (good) |
| `Infrastructure` | `ApplicationDbContext`, `UnitOfWork`, `BaseRepository`, Identity `ApplicationUser`, migrations, all service implementations, `DbInitializer` | Services live here rather than in Application; acceptable for a modular monolith |
| `Repositories` | Query-heavy repositories per aggregate | Uses `AsNoTracking` and projection consistently |
| `API` | 17 controllers, 2 middleware, JSON converters, DI wiring, migrate-and-seed on startup | `WeatherForecast` template leftover is still routed and anonymous |

Multi-tenancy is row-level. `ApplicationDbContext` is constructed **per request** with `tenantId`
and `shopId` taken from JWT claims ([Program.cs:20-31](../API/Program.cs#L20-L31)) and applies
global query filters on all 17 `DbSet`s. Owner-level entities (`Shop`, `User`) filter on
`TenantId`; operational entities filter on `TenantId + BranchId + !IsDeleted`. Branch switching
re-issues a token with a new `shopId` claim.

A detailed module map and the proposed target structure are in [ARCHITECTURE.md](ARCHITECTURE.md).

---

## 4. Modules and functionality that exist today

| Module | Endpoints | What works | Maturity |
|---|---|---|---|
| Auth | login (email+password or phone+PIN), change-password, change-pin, switch-branch, refresh (stub) | JWT with `nameid`, `tenantId`, `shopId`, roles | Basic. No refresh, lockout, password reset, OTP |
| Admin provisioning | provision owner → shop → subscription, activate/suspend/extend, list owners/shops, change user role | Transactional; clones 18 system measurement fields and 7 templates per tenant | Solid |
| Shops (branches) | CRUD, subscription update | Owner can add branches | New branch is not added to owner's `ShopIds` and gets no measurement seeding |
| Users / staff | CRUD, deactivate, assign shops, assign roles | Restricted-role guard on create and assign | Guard missing on update (A4) |
| Customers | generic CRUD, ledger | Phone unique per shop, soft delete, denormalised totals, loyalty points | Totals never reversed on cancel; points never redeemable |
| Measurements | fields, templates, profiles CRUD | jsonb field values, system templates protected, snapshot into order items | Cross-tenant leak (A3); no history/versioning; no text values |
| Orders | create with items and stage plan, update, status patch, cancel, deliver, due-today, overdue, search | Transactional create; order number generation; advance payment | No state machine; status history never written; cancel/deliver incomplete |
| Payments | record payment, alteration charge | Validates amount ≤ balance, atomic insert | `FullyPaid` overwrites production status; no refunds; race (A7) |
| Karigar | assign/reassign stage, log start, complete, my-queue, my-completed, performance | Queue split into pending/in-progress | Earnings counted from assignments not completions; no payout record; caller not verified |
| Attendance | check-in/out, CRUD, monthly summary, my-attendance | Unique per staff/date | UTC "today" is wrong for UTC+5; generic CRUD broken (A5) |
| Salaries | record, list, update | Base + bonus − deduction | Manual only; not derived from attendance; duplicated across two services |
| Expenses | CRUD, filters | Date and category filters | Free-text category; hard delete; generic CRUD broken (A5) |
| Finance & reports | summary, dashboard, revenue by garment/method, daily revenue, top customers, staff performance, order-status, pending balances | Broad and useful | In-memory aggregation (A9); no export |
| Attachments | called from order create | — | Placeholder service; nothing stored; `IFormFile` inside a JSON body |
| Notifications | — | — | Absent |
| Audit log | — | Created/Updated By/On columns only | No action log; `OrderStatusHistory` table unused |

---

## 5. Main workflows as implemented

**Tenant onboarding (SystemAdmin).** `POST /api/admin/provision-owner` creates an Identity user (or
PIN user), a domain `User` with a fresh `TenantId`, then `provision-shop` creates the first `Shop`
with `BranchId = Shop.Id`, adds it to the owner's `ShopIds`, and calls `TenantSetupService` to clone
the system measurement catalogue inside a transaction. `provision-subscription` sets plan and dates.

**Login and branch scoping.** `AuthService` finds the user across tenants (`IgnoreQueryFilters`),
verifies password or PIN, and issues a token whose `shopId` is the user's `BranchId`.
`switch-branch` checks `ShopIds.Contains(shopId)` and re-issues. The token does not verify the shop
belongs to the user's tenant.

**Order creation.** Validates customer and karigars, builds `Order` + `OrderItem`s, copies the
measurement profile jsonb into `OrderItem.MeasurementSnapshot`, creates `OrderItemStageAssignment`
rows for the stage plan, records an advance as an `OrderPayment`, updates customer totals and loyalty
points, all inside one transaction with rollback. Attachments are passed to a stub.

**Production.** Owner/Manager assign or reassign a `ProductionStage` to a karigar. A karigar calls
`stages/log` (start) and `stages/complete`. Completing a stage stamps the log but does not update
`OrderItem.Status`, does not touch `Order.Status`, and does not verify the caller is the assignee.
`PATCH /api/orders/{id}/status` sets any status directly.

**Payment.** `POST /api/orders/{id}/payments` validates amount, appends a payment, mutates
`AmountPaid`/`BalanceDue` in memory, and sets `Status = FullyPaid` when balance reaches zero.

**Delivery and cancellation.** `deliver` sets `Status = Delivered` without checking balance or
stamping `ActualDeliveryDate` or item delivery flags. `cancel` sets `Status = Cancelled` and a
reason, ignores the `CancellationStage`/`Refund*` columns, and does not reverse customer totals.

---

## 6. Technical debt

| Area | Debt | Evidence |
|---|---|---|
| Duplicate columns | Every operational entity carries both `ShopId` and `BranchId` with the same meaning; filters use one, indexes the other | A6 |
| Duplicate surfaces | Expenses, salaries and attendance are exposed twice: `/api/finance/*` (correct) and generic `/api/expenses`, `/api/salaries`, `/api/attendance` (broken, A5) | `FinanceController.cs`, `ExpensesController.cs`, `SalariesController.cs`, `AttendanceController.cs` |
| Duplicate logic | Salary and attendance recording implemented in both `FinanceService` and the `StaffSalaryService`/`StaffAttendanceService` generics | `FinanceService.cs:1-167` |
| Manual stamping | No `SaveChanges` override; every service hand-sets `TenantId`, `BranchId`, `CreatedBy`, `CreatedOn`, `UpdatedOn`; three services forgot | A5 |
| Unused model | `OrderMeasurementSnapshot` (not in the model snapshot), `LightEntity`, `ImmutableEntity`; enums `FabricStatus`, `AlterationStatus`, `StageAssignmentMode`, `ExpenseCategory`, `SalaryStatus` never referenced; `Order.Priority`, `CancellationStage`, `RefundStatus` never written; `OrderStatusHistory` never written | `Domain/` |
| Template leftovers | `WeatherForecastController`, `WeatherForecast.cs` | `API/` |
| Accidental schema | EF picked up the computed `MeasurementProfile.TemplateFields` property as a navigation and created a shadow `TemplateFields.MeasurementProfileId` column and index; no `ShopId` column has a foreign key to `Shops`, and `User.AuthId` has none to `AspNetUsers` | [DATABASE.md](DATABASE.md) |
| Hard-coded values | 14-day trial, loyalty +10/order and tier thresholds, `PKR`/`₨`, 30-day default report window, 24-hour token | see [FEATURE_ROADMAP.md](FEATURE_ROADMAP.md) §Config |
| Documentation drift | `Frontend_Developer_Guide.md` and `api_endpoints.yml` disagree with the controllers on roughly a dozen routes | [API.md](API.md) §Drift |
| DI | `DbContextOptions` registered as a singleton is redundant; per-request hand construction prevents `AddDbContextPool` and has no `EnableRetryOnFailure` against a serverless Postgres | [Program.cs:34](../API/Program.cs#L34) |
| Uncommitted work | Working tree carries stage-assignment repository methods and UTC JSON converters; internally consistent and complete, but uncommitted | `git status` |

---

## 7. Security issues

| # | Severity | Issue | Fix |
|---|---|---|---|
| A1 | Critical | Secrets in git. Rotating both credentials is required regardless of any code change | Rotate; move to env vars or user-secrets; gitignore `appsettings*.json` except a template |
| A2 | Critical | Suspended, cancelled and trial-expired tenants retain full access. The `/api/shops` non-GET exemption also lets a suspended Owner create branches | Look up by `shopId` claim or `TenantId`; cache status per tenant; drop exemption |
| A3 | Critical | IDOR and enumeration on measurement fields/templates across tenants | Add `TenantId == Guid.Empty || (TenantId == tenant && BranchId == shop)` predicate |
| A4 | High | Manager → Owner privilege escalation via `PUT /api/users`; Manager can also change `ShopIds` and `ActiveStatus` | Reuse the restricted-role guard; limit role/shop changes to Owner |
| S5 | High | Branch assignment and `switch-branch` never verify `shop.TenantId == user.TenantId`; a foreign `shopId` claim is issuable | Validate shop ids belong to the tenant |
| S6 | High | Class-level-only `[Authorize]` lets Karigar and Receptionist create orders, record payments, add alterations, CRUD customers and edit any attendance record | Add role lists per action, or introduce permission policies |
| S7 | Medium | Karigar can log/complete any stage; `KarigarId` is free-form in the request | Force `KarigarId = current user` for the Karigar role |
| S8 | Medium | No account lockout, no refresh-token persistence, 24-hour access tokens with no revocation | `AccessFailedAsync` + lockout; stored refresh tokens |
| S9 | Medium | Global exception handler returns `InvalidOperationException.Message` and raw DB error text to clients | Map known exceptions only; log the rest |
| S10 | Medium | Hard-coded SystemAdmin password seeded in every environment on startup | Seed from configuration in Development only; force rotation |
| S11 | Low | CORS `AllowAnyOrigin`; anonymous `WeatherForecast` endpoint; no rate limiting | Restrict origins; delete leftover; add rate limiter on auth |
| S12 | Low | Phone+PIN login searches phone across all tenants; a duplicate phone in another tenant blocks login | Scope by tenant or enforce global uniqueness |

SQL injection: not found (all queries through EF parameters). XSS/CSRF: not applicable to a
bearer-token JSON API with no server-rendered HTML.

---

## 8. Performance and scalability issues

| # | Issue | Evidence | Fix |
|---|---|---|---|
| A6 | Unindexed tenant filter column | filters on `BranchId`, indexes on `ShopId` | Index `(TenantId, BranchId, …)` or collapse the two columns |
| A9 | Whole-table loads for reports | dashboard loads all non-cancelled orders; stage report runs 3 queries × 5 stages | Server-side `GroupBy`/`Sum`/`Count`; one grouped query for stages |
| P3 | Aggregate loaded for scalar updates | `GetByIdAsync` includes Items + StageAssignments + Payments + Alterations, then status update loads again | `AnyAsync` / `ExecuteUpdateAsync` |
| P4 | Search cannot use indexes | `ToLower().Contains()` on order number, customer name, phone | `EF.Functions.ILike` + trigram GIN index |
| P5 | Subscription check queries `Shops` on every authenticated request | `SubscriptionMiddleware.cs:48` | `IMemoryCache` per tenant, 1–5 min |
| P6 | Unbounded lists | due-today, overdue, by-customer, attendance list, pending balances, karigar queues with deep Includes | Page or cap |
| P7 | Finance summary is 7 round-trips | `FinanceRepository.cs:198-222` | Single grouped query |
| P8 | Multiple `SaveChanges` per transaction | `BaseRepository.AddAsync/UpdateAsync` each save; order create saves 3 times | Save once at the UoW boundary |
| P9 | No pooling, no retry, no caching, no health checks | `Program.cs` | `AddDbContextPool` with tenant from `ICurrentUserService`; `EnableRetryOnFailure`; health checks |
| P10 | Startup migrations and seeding race under multiple instances | `Program.cs:155-166` | Migrate as deploy step |

None of the performance findings were measured against a loaded database (*not executed*). They are
identified from query shape and index configuration. Measure before optimising anything beyond A6
and A9.

---

## 9. Correctness defects in business logic

| # | Defect | Location |
|---|---|---|
| B1 | Any status transition accepted, including `Cancelled → Delivered`; no history written | [OrderRepository.cs:155](../Repositories/Implementations/OrderRepository.cs#L155) |
| B2 | `FullyPaid` is a status, so paying in full erases the production stage | `OrderService.cs:263` |
| B3 | Order update recomputes `GrandTotal = SubTotal − Discount`, dropping alteration charges; no check that discount ≤ subtotal | `OrderService.cs:210-215` |
| B4 | Cancel does not reverse `TotalOrders`/`TotalSpend`/loyalty and ignores refund columns | `OrderService.cs` cancel path |
| B5 | Deliver ignores outstanding balance, `ActualDeliveryDate`, item `IsDelivered` | `OrderService.cs` deliver path |
| B6 | Completing a stage never sets `OrderItem.Status = Done`; order status never derived from item progress | `OrderService.cs:332-386` |
| B7 | Karigar earnings summed from assignments (`StagePrice`) rather than completed logs; `TotalInProgress` hard-coded 0 | `KarigarService.cs:152` |
| B8 | Date-only fields (`ExpenseDate`, attendance `Date`, `DeliveryDate`) stored as `timestamptz`; a `+05:00` midnight becomes the previous day in UTC; "today" computed in UTC | `UtcDateTimeConverter.cs`, `StaffAttendanceService.cs` |
| B9 | Owner-created branch not added to `ShopIds` and not seeded | `ShopService.cs:39-51` |
| B10 | `PUT /api/shops/{id}/subscription` is tenant-filtered so SystemAdmin always gets "Shop not found." (returned as 400) | `ShopService.cs:64-77` |
| B11 | Refresh token issued but never stored; `RefreshAsync` returns "not yet implemented" | `AuthService.cs:82` |
| B12 | `AttachmentService` returns a fabricated URL and persists nothing | `AttachmentService.cs:12-24` |

---

## 10. Missing validation

- DataAnnotations exist in one ViewModel file (`AdminProvisioningViewModels.cs`). No
  FluentValidation. Order items, payments, measurements, expenses and salaries rely on ad-hoc
  checks inside services or none.
- No validation that `Discount ≤ SubTotal`, `Qty ≥ 1`, `Price ≥ 0`, `DeliveryDate ≥ today`,
  `StagePrice ≥ 0`, attendance `CheckOut > CheckIn`, salary month in range.
- Route id vs body id mismatch is not checked on `PUT` endpoints that take `Id` in the body.
- Enum values arrive as integers with no range validation.

---

## 11. Missing tests

There are no test projects. The highest-value tests, in order, are tenant isolation (a second
tenant must see zero rows from the first), payment concurrency and balance arithmetic, order status
transitions once a state machine exists, authorization per role per endpoint, and order creation
totals with discount and advance. See [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) for the
test scaffold.

---

## 12. Missing tailoring functionality

Summarised here; prioritised with business value and change surface in
[FEATURE_ROADMAP.md](FEATURE_ROADMAP.md).

- Order status state machine with recorded history and reasons.
- Separate payment state from production state.
- Karigar piece-rate catalogue (rate per garment per stage), payout ledger with advances and
  deductions, earnings from completed work only.
- Cutting as a first-class step with fabric issued/consumed and recut tracking.
- Trial/fitting step and an alteration workflow with status, assignee and history.
- Delivery scheduling and a "due today / due tomorrow / overdue / ready" board.
- Customer communication: WhatsApp and SMS templates for order received, ready, balance due.
- Measurement history/versioning, text and optional fields, unit and field name frozen into the
  order snapshot.
- Configurable garment attributes (collar, cuff, pocket, button, style) instead of free-text
  `StyleNotes`.
- Fabric and accessories inventory with movement ledger, and customer-provided fabric flag.
- Audit log of financial and production actions.
- Digital receipt / invoice printing, QR order tracking.
- Real subscription plans with limits and billing.

---

## 13. Recommended improvements, in order

1. **Secrets and access control (days).** Rotate credentials; fix A2, A3, A4, S5, S6, S7.
2. **Data integrity (days).** `SaveChanges` stamping for tenant, branch and audit columns (fixes A5
   permanently); `BranchId` indexes or column collapse; xmin concurrency token on money-bearing
   entities; query-string date binder.
3. **Order lifecycle (one sprint).** Transition table, `OrderStatusHistory` written on every change,
   payment state separated from production state, deliver/cancel completed, item status derived from
   stages.
4. **Operational baseline (days).** Health checks, Serilog, rate limiting on auth, restricted CORS,
   sanitized exception handler, migrations as a deploy step, remove template leftovers, first tests.
5. **Reporting (days).** Server-side aggregation for dashboard and finance summary.
6. **Features (sprints).** Notifications, karigar payouts, alterations and trial, cutting, delivery
   board, inventory. Order and dependencies in the roadmap.

Large architectural changes wait for approval, per the brief.
