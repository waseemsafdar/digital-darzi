# Digital Darzi API — Implementation Plan

Incremental plan derived from [CODEBASE_AUDIT.md](CODEBASE_AUDIT.md),
[ARCHITECTURE.md](ARCHITECTURE.md) and [FEATURE_ROADMAP.md](FEATURE_ROADMAP.md). One module at a
time; build and run tests after each phase; update docs with each phase. Phases 1–4 are fixes to
existing functionality and carry no new product features. **Phase 3 onward changes behaviour that
clients see and waits for approval.**

Rules applied throughout (from the brief): no rewrite, preserve existing routes and payloads unless
listed under "API", DTOs at the boundary, transactions for multi-step writes, no whole-table loads,
no silent modification of historical financial or production records, audit history for important
operations, never edit production code only to make a test pass.

Reporting format after each phase: Changed · Why · Files · Database · API · Tests · Risks · Next.

---

## Phase 0 — Immediate, outside the codebase (today)

| Step | Action |
|---|---|
| 0.1 | Rotate the Neon database password and the JWT signing key. |
| 0.2 | Provide both through environment variables (`ConnectionStrings__DefaultConnection`, `Jwt__Key`) or `dotnet user-secrets` for development. |
| 0.3 | Commit or stash the current working-tree changes (stage assignment repository methods, UTC converters) so later phases start from a clean tree. They are complete and consistent. |

## Phase 1 — Security and isolation fixes (2–3 days)

| Step | Change | Files | Hazard to check |
|---|---|---|---|
| 1.1 | Remove secrets from `appsettings.json`; add `appsettings.template.json`; gitignore real settings; keep `Jwt:Key` startup check | `API/appsettings*.json`, `.gitignore` | App must still boot from env vars |
| 1.2 | Subscription guard: look up shop by `shopId` claim, fall back to any shop of the tenant; cache status per tenant 2 min; remove `/api/shops` non-GET exemption; use endpoint metadata instead of path substring | `API/Middleware/SubscriptionMiddleware.cs`, new `Infrastructure/Services/SubscriptionGuard.cs` | SystemAdmin bypass retained; login still exempt |
| 1.3 | Measurement queries: replace bare `IgnoreQueryFilters()` with explicit `TenantId == Guid.Empty || (TenantId == tenant && BranchId == shop)` predicate | `Repositories/Implementations/MeasurementRepository.cs` | System templates still visible; soft-deleted excluded |
| 1.4 | `UserService.UpdateAsync`: apply restricted-role guard; only Owner may change `RoleIds`, `ShopIds`, `ActiveStatus` | `Infrastructure/Services/UserService.cs` | Manager can still edit name/phone |
| 1.5 | Verify shop belongs to tenant in `AssignShopsAsync`, user create/update, `SwitchBranchAsync` | `UserService.cs`, `AuthService.cs` | — |
| 1.6 | Per-action roles on Orders, Customers, Measurements profiles, Attendance to match the frontend guide; Karigar `stages/log|complete` force `KarigarId = current user` | controllers, `OrderService.cs` | Receptionist keeps create order + payment + deliver |
| 1.7 | Delete `WeatherForecastController.cs`, `WeatherForecast.cs`; upgrade AutoMapper to a patched version | `API/` , `*.csproj` | Mapping profiles still load |
| 1.8 | Delete `ExpensesController`, `SalariesController`, `AttendanceController` **or** fix stamping (Phase 2.1 fixes them; decision: keep them until 2.1 lands, then remove and note deprecation) | `API/Controllers/` | Frontend guide references `/api/attendance/check-in` — keep check-in under `/api/finance/attendance/check-in` |
| Tests | New `Tests/DigitalDarzi.UnitTests` (xUnit, Moq, AwesomeAssertions): `UserService.UpdateAsync` rejects Owner role from Manager; `SubscriptionGuard` blocks suspended/expired; `AuthService.SwitchBranch` rejects foreign shop | new project | — |
| Acceptance | `dotnet build`, tests green, manual: login → suspended tenant gets 402; tenant B cannot list tenant A templates | | |

## Phase 2 — Data integrity (3–4 days)

| Step | Change | Files | Hazard |
|---|---|---|---|
| 2.1 | `SaveChangesAsync` override: stamp `TenantId`, `BranchId`, `CreatedBy/On`, `UpdatedBy/On`; throw if a non-SystemAdmin adds an operational row with empty tenant | `Infrastructure/Data/ApplicationDbContext.cs` | Provisioning and seeding run with `Guid.Empty` context: allow when caller is SystemAdmin or unauthenticated startup |
| 2.2 | Inject `ICurrentUserService` into the context; register with `AddDbContextPool`; `EnableRetryOnFailure`; remove singleton options | `Program.cs`, `ApplicationDbContext.cs` | Identity stores resolve the same scoped context |
| 2.3 | Migration: add `(TenantId, BranchId, …)` indexes mirroring every `ShopId` index; mark `ShopId` for removal in a later phase (keep both columns for now, no data loss) | `Infrastructure/Migrations/` | Run `migration-guard` review; index build on Neon is online |
| 2.4 | `UseXminAsConcurrencyToken()` on `Order`, `Customer`, `OrderPayment`; wrap `RecordPaymentAsync` in a retry-once on `DbUpdateConcurrencyException`; compute `BalanceDue` as `GrandTotal − Σ payments + Σ refunds` on write | `ApplicationDbContext.cs`, `OrderService.cs` | Existing rows: xmin is a system column, no migration data |
| 2.5 | `UtcDateTimeModelBinder` for query-string `DateTime`/`DateTime?` | `API/Json/`, `Program.cs` | Same rules as JSON converter |
| 2.6 | `Shop.TimeZone` (default `Asia/Karachi`); "today" helpers use it; migrate `StaffAttendance.Date`, `ShopExpense.ExpenseDate` to `date` (`DateOnly`); `DeliveryDate` stays `timestamptz` for now | migration, `StaffAttendanceService.cs`, `ShopExpense*` | Data conversion in migration: `AT TIME ZONE 'Asia/Karachi'` |
| 2.7 | Remove the three generic duplicate controllers (see 1.8) and `FinanceService` duplicates of salary/attendance | `API/Controllers/`, `Infrastructure/Services/FinanceService.cs` | Update frontend guide |
| Tests | Integration project `Tests/DigitalDarzi.IntegrationTests` (Testcontainers Postgres, `WebApplicationFactory`): tenant isolation across two tenants for customers, orders, measurements; expense create then get by id; concurrent payments sum correctly; query-string date filter returns 200 | new project | — |
| Acceptance | build + both test projects green; `migration-guard` on 2.3 and 2.6 | | |

## Phase 3 — Order lifecycle (one sprint) — **approval required**

| Step | Change | Files | Hazard |
|---|---|---|---|
| 3.1 | `OrderStateMachine` with transition table (ARCHITECTURE §2.3); `UpdateStatusAsync` validates; Owner/Manager override recorded with `IsManualOverride` | new `Infrastructure/Services/Orders/OrderStateMachine.cs`, `OrderService.cs`, `OrderRepository.cs` | Existing orders in any state remain readable |
| 3.2 | Write `OrderStatusHistory` on every transition (create, status, deliver, cancel, stage-driven) | `OrderService.cs` | Table exists; no migration |
| 3.3 | `Order.PaymentStatus` column (`Unpaid/Partial/Paid/Refunded`); remove `FullyPaid` from `OrderStatus` **without** renumbering existing enum values; data migration maps `FullyPaid` rows to last history status or `ReadyToDeliver` | migration, `Enums.cs`, DTOs | Enum value removal: keep numeric slot reserved |
| 3.4 | Deliver: require `PaymentStatus == Paid` unless `allowBalance` flag with reason; set `ActualDeliveryDate`, item `IsDelivered/DeliveredOn`; support `PartiallyDelivered` when a subset of items is delivered | `OrderService.cs`, `OrderViewModels.cs` | — |
| 3.5 | Cancel: set `CancellationStage` from current status, `RefundAmount`, `RefundStatus`; reverse `Customer.TotalOrders/TotalSpend/LoyaltyPoints`; refund recorded as a negative `OrderPayment` line, never by editing history | `OrderService.cs` | Ledger stays append-only |
| 3.6 | Order update: `GrandTotal = SubTotal − Discount + Σ alterations`; validate `Discount ≤ SubTotal`, `Qty ≥ 1`, `Price ≥ 0` | `OrderService.cs` | — |
| 3.7 | Stage completion sets `OrderItem.Status` (`InProgress` on first log, `Done` when all planned stages complete); `Order.Status` advances when all items pass a stage | `OrderService.cs` | Manual status still allowed |
| 3.8 | Split `OrderService` into `OrderService`, `OrderWorkflowService`, `PaymentService` (interfaces preserved by delegating; controllers unchanged) | `Infrastructure/Services/Orders/` | Pure refactor; tests from Phase 2 must stay green |
| API | `GET /api/orders/{id}/history` (new); detail DTO gains `PaymentStatus`, `History`; `PATCH status` returns 400 with allowed transitions on invalid | | |
| Tests | Unit: every allowed and disallowed transition; deliver with balance; cancel reversal; totals with alterations. Integration: payment → status unchanged, `PaymentStatus` updated | | |

## Phase 4 — Operational baseline and reporting (3–4 days)

| Step | Change | Files |
|---|---|---|
| 4.1 | Serilog (console JSON, request logging, tenant/user enrichment); correlation id in error envelope | `Program.cs`, `GlobalExceptionMiddleware.cs` |
| 4.2 | Exception middleware maps known types only; hides EF and DB messages outside Development | `GlobalExceptionMiddleware.cs` |
| 4.3 | Health checks (`/health/live`, `/health/ready` with Npgsql); rate limiter on `/api/auth/*`; CORS origins from config | `Program.cs` |
| 4.4 | FluentValidation validators for create/update ViewModels of orders, payments, customers, measurements, expenses, salaries, attendance, users | `Application/Validation/` |
| 4.5 | `ApiResponse` carries `StatusCode`; `BaseController` stops string-matching "not found" | `Application/Common/`, `API/Controllers/BaseController.cs` |
| 4.6 | Migrations as `dotnet run --project API -- --migrate` (or CI step); seeding: roles + system catalogue always; admin user only in Development from config | `Program.cs`, `DbInitializer.cs` |
| 4.7 | Reporting: dashboard, finance summary, stage report, pending balances rewritten as grouped server-side queries; `ILike` + trigram index for search | `ReportingService.cs`, `FinanceRepository.cs`, `OrderRepository.cs`, migration |
| 4.8 | Update `README.md`, `Frontend_Developer_Guide.md`, `api_endpoints.yml` from [API.md](API.md) | docs |
| Tests | Integration: dashboard numbers equal a brute-force computation on seeded data; validation returns 400 with field errors | |

## Phase 5 — Customer communication and delivery (one sprint) — approval required

5.1 `INotificationSender` + `NullSender` + one real provider (WhatsApp Business API or SMS gateway,
chosen by config) · 5.2 `NotificationTemplate`, `NotificationOutbox`, hosted dispatcher · 5.3 events:
order received, ready to deliver, balance due, trial reminder · 5.4 delivery board endpoint (single
grouped query) with `ReadyDate` · 5.5 attachments via `IFileStorage` (local disk in dev, S3/Blob in
prod), multipart endpoint, `Attachment` rows written; remove `IFormFile` from JSON DTO · 5.6 refresh
tokens, lockout, forgot/reset password by OTP · 5.7 customer profile fields.

## Phase 6 — Production workflow depth (one sprint) — approval required

6.1 `KarigarRate` + `KarigarPayout` ledger; earnings from completed logs; `TotalInProgress`
computed · 6.2 alteration workflow with status, assignee, history · 6.3 trial/fitting result
endpoint · 6.4 stage work queues with `Rework` · 6.5 `AuditLog` via interceptor whitelist + explicit
calls.

## Phase 7 — Measurements and garments (one sprint) — approval required

7.1 measurement profile versioning · 7.2 `ValueType`/`IsOptional` on fields; snapshot stores field
name, unit, value · 7.3 garment attribute catalogue per garment type and per-item values · 7.4
repeat-order · 7.5 receipt/invoice.

## Phase 8 — Cutting and inventory (two sprints) — approval required

8.1 cutting queue with fabric issued/consumed and recut · 8.2 fabric and accessories inventory with
movement ledger, suppliers, customer-provided fabric flag.

## Phase 9 — SaaS and multi-branch growth — approval required

9.1 subscription plans, limits, billing · 9.2 consolidated branch reports and exports · 9.3 cash
register / daily closing · 9.4 salary from attendance · 9.5 permission table · 9.6 optional Postgres
row-level security.

---

## Test scaffold (created in Phase 1, grown every phase)

```
Tests/
  DigitalDarzi.UnitTests/          xUnit · Moq · AwesomeAssertions
    Services/…                     one class per service, ServiceResult-style assertions
  DigitalDarzi.IntegrationTests/   xUnit · Testcontainers.PostgreSql · WebApplicationFactory · Respawn
    Fixtures/ApiFactory.cs         real DI, migrations applied once per collection
    Tenancy/IsolationTests.cs      two tenants, every list endpoint returns only own rows
    Orders/…, Payments/…, Auth/…
```

Every new or modified test file is handed to the matching auditor (`test-auditor`,
`integration-test-auditor`) before a phase is reported done. Production code is never edited to
make a test pass; a discovered defect is pinned with a `// KNOWN ISSUE (<ticket>)` comment and
raised separately.

## Acceptance per phase

1. `dotnet build` zero errors, no new warnings.
2. `git diff --name-only` equals the phase's Files list (scope check).
3. Hazard column reviewed for each step.
4. Cold audit: `migration-guard` for any migration, `test-auditor`/`integration-test-auditor` for
   tests, `contract-checker` when a DTO or route changes.
5. Phase report in the eight-heading format; docs updated.

## Risks

| Risk | Mitigation |
|---|---|
| Neon serverless connection limits during index builds and Testcontainers runs | Tests use a local container; migrations run off-peak |
| Enum value changes (`FullyPaid`) break stored rows | Reserve numeric value; data migration; never renumber |
| Removing the three generic controllers breaks a frontend already calling them | Confirm with frontend team first; they are broken today, so risk is low |
| Per-action role tightening blocks a workflow a shop relies on | Mirror the frontend guide exactly; log 403s for two weeks after deploy |
| `DateOnly` migration shifts historical dates | Convert with explicit `Asia/Karachi` offset; verify counts before/after |
