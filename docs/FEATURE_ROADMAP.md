# Digital Darzi API — Feature Roadmap

Prioritised gap analysis for a commercial tailor-shop management SaaS. Priorities:

- **P0 Critical** — required for a reliable system; includes defects in shipped features.
- **P1 High value** — major operational improvements for a single shop.
- **P2 Growth** — needed by larger shops, multiple branches, or many tenants.
- **P3 Optional** — advanced or nice-to-have.

Complexity: S (≤1 day), M (2–5 days), L (1–2 weeks), XL (>2 weeks). "Phase" refers to
[IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md). Every item was evaluated against the existing
code; items the product does not need are listed at the end as rejected or deferred.

---

## P0 — Critical

| Feature | Business value | Database | API | UI | Depends on | Cx | Phase |
|---|---|---|---|---|---|---|---|
| Secrets out of git, credentials rotated | Prevents database takeover | — | — | — | — | S | 1 |
| Subscription enforcement that works | Trial/suspension is the entire SaaS revenue control | — | 402 now actually returned | Handle 402 | — | S | 1 |
| Tenant isolation fixes (measurements, branch-tenant check, stamping interceptor) | No data leak between shops | none (interceptor only) | — | — | — | M | 1 |
| Role hardening (Manager escalation, per-action roles, karigar self-only) | Staff cannot exceed their role | — | 403 on more paths | Hide actions per role | — | M | 1 |
| Generic expenses/salaries/attendance create fixed or removed | POST currently succeeds then record vanishes | — | remove 3 duplicate controllers | Switch to `/api/finance/*` | Stamping | S | 1 |
| Query-string date binding | Every filtered report works | — | — | — | — | S | 1 |
| Payment concurrency + balance derived from payments | Money is never double-counted; balance = total − Σ payments − refunds | xmin token; recompute on read or on write | — | — | — | M | 2 |
| Order status state machine + history | Auditable, predictable lifecycle; no `Cancelled → Delivered` | write `OrderStatusHistory` (exists); add `PaymentStatus` column | `PATCH status` validates; `GET /{id}/history` | Status timeline | — | M | 3 |
| Separate payment state from production state | Paying in full no longer erases the stage | `Order.PaymentStatus` | detail DTO gains field | Badge | State machine | S | 3 |
| Deliver/cancel completed (balance check, actual date, item flags, refund fields, customer stat reversal) | Correct history and ledger | none new | same routes | Confirm dialogs | State machine | M | 3 |
| Order update keeps alteration charges; discount validation | Totals are right | — | 400 on bad discount | — | — | S | 3 |
| Indexes on the filter column; collapse `ShopId`/`BranchId` | Queries stay fast as orders grow | migration | — | — | — | M | 2 |
| Server-side dashboard and finance aggregation | Dashboard usable at thousands of orders | — | same routes | — | — | M | 4 |
| Health checks, structured logging, exception sanitising, rate limit on auth, restricted CORS | Operable in production | — | `/health` | — | — | M | 4 |
| Migrations as a deploy step; dev-only admin seed | Safe multi-instance deploy | — | — | — | — | S | 4 |
| Test project with isolation, payment, transition and authz tests | Regressions caught | — | — | — | — | L | 2–4 |
| Remove template leftovers, upgrade AutoMapper | Hygiene, closes NU1903 | — | delete `/WeatherForecast` | — | — | S | 1 |

## P1 — High value

| Feature | Business value | Database | API | UI | Depends on | Cx | Phase |
|---|---|---|---|---|---|---|---|
| WhatsApp / SMS notifications (order received, ready, balance due, trial reminder) | The most requested feature in this market; fewer phone calls, faster pickup | `NotificationTemplate`, `NotificationOutbox` | `POST /notifications/test`, templates CRUD | Template editor, send log | Hosted service | L | 5 |
| Karigar piece-rate catalogue and payout ledger | Owners pay karigars correctly per piece; earnings only from completed work | `KarigarRate`, `KarigarPayout`, `KarigarPayoutLine` | `/karigar/{id}/ledger`, `/karigar/{id}/payouts` | Karigar ledger page | State machine (completion events) | L | 6 |
| Item status derived from stage logs; order status advances automatically | Staff stop updating status by hand | — | — | Kanban-ready data | State machine | M | 3 |
| Alteration workflow (problem, required change, assignee, status, history; original snapshot untouched) | Alterations are tracked, not lost | extend `OrderAlteration` with `Status`, `AssignedKarigarId`, `CompletedOn`, `OrderItemId` required | `PATCH alterations/{id}/status`, list by order | Alteration list | State machine | M | 6 |
| Trial / fitting step (trial date, fitting notes, outcome → alteration or approve) | Fewer redeliveries | `Order.TrialDate`, `OrderItem.FittingNotes` | `POST /{id}/trial-result` | Trial calendar | State machine | M | 6 |
| Delivery board (due today / tomorrow / overdue / ready / delivered) with promised vs ready vs actual dates | Nobody misses a delivery | `Order.ReadyDate` | `GET /orders/board?bucket=` (single grouped query) | Board | Aggregation | M | 5 |
| Work queues per stage (pending / assigned / in progress / done / rework) | Manager sees who is doing what | `OrderItemStageLog.Status` incl. `Rework` | `GET /workflow/board?stage=` | Kanban | Derived status | M | 6 |
| Measurement history / versioning; text and optional fields; unit and field names frozen in the order snapshot | Reuse last measurements safely; snapshot readable years later | `MeasurementProfileVersion`; `MeasurementField.ValueType`, `IsOptional`; richer snapshot json | `GET profiles/{id}/versions` | Version picker | — | M | 7 |
| Configurable garment attributes (collar, cuff, pocket, button, style) per garment type | Structured instructions for karigar | `GarmentAttribute`, `OrderItemAttributeValue` (or jsonb on item) | templates CRUD, item DTO gains `Attributes` | Dynamic form | — | M | 7 |
| Shop time zone and `DateOnly` calendar columns | Check-ins, due dates and expense dates land on the right day | `Shop.TimeZone`; column type migration | — | — | — | M | 2 |
| Validation layer (FluentValidation) | Bad input rejected with 400, not 500 or silent | — | consistent 400 body | Inline errors | — | M | 4 |
| Attachments actually stored (order images, expense receipts) via `IFileStorage` (S3/Blob/local) | Photos of fabric and finished garments | `Attachment` rows written | multipart `POST /orders/{id}/attachments` | Upload | — | M | 5 |
| Refresh tokens, lockout, password reset (OTP by SMS) | Secure and usable login | `RefreshToken` | `/auth/refresh` real, `/auth/forgot`, `/auth/reset` | — | Notifications for OTP | M | 5 |
| Audit log | Owners see who changed a price or payment | `AuditLog` | `GET /audit?entity=` | Audit view | Interceptor | M | 6 |
| Digital receipt / invoice (PDF or print view) | Customer gets a receipt; balance printed | — | `GET /orders/{id}/receipt` | Print | — | M | 7 |
| Customer profile enrichment (WhatsApp number, address, gender, preferred karigar, notes) | Better service, correct notifications | columns on `Customer` | DTO fields | Form | — | S | 5 |

## P2 — Growth

| Feature | Business value | Database | API | UI | Depends on | Cx | Phase |
|---|---|---|---|---|---|---|---|
| Cutting management (cutting queue, cutter assignment, fabric issued/consumed, recut) | Larger shops separate cutting from stitching | `CuttingJob` or stage-log extension with fabric fields | `/workflow/cutting/*` | Cutting board | Work queues | L | 8 |
| Fabric inventory and movement ledger (purchase, issue to order, adjust, waste, transfer); customer-provided vs shop fabric | Stock control and fabric charges | `Fabric`, `InventoryMovement`, `Supplier` | `/inventory/*` | Stock pages | — | XL | 8 |
| Accessories inventory (buttons, thread, lining) | Same ledger, more item types | reuse `InventoryItem` | same | same | Fabric inventory | M | 8 |
| Subscription plans with limits and billing (JazzCash/EasyPaisa, invoices, automatic expiry) | Revenue for the SaaS | `SubscriptionPlan`, `TenantInvoice` | admin + owner billing routes | Billing page | Working guard | L | 9 |
| Owner multi-branch consolidated reports; branch transfer of orders | Multi-branch owners | — | `branchId=all` on reports | Filters | Aggregation | M | 9 |
| Report exports (Excel, PDF) | Accountants | — | `?format=xlsx` | Download | — | M | 9 |
| Cash register / daily closing (opening float, cash in/out, closing count, variance) | Cash control per branch | `CashSession`, `CashMovement` | `/finance/cash/*` | Closing screen | Payments | M | 9 |
| Salary from attendance (per-day rate, late/absent rules, advances, partial pay) | Payroll without a spreadsheet | `SalaryRule`, `SalaryAdvance`; use `SalaryStatus` | `/finance/salaries/compute` | Preview | Time zone | M | 9 |
| Expense categories as configurable list with receipt attachment | Clean reports | `ExpenseCategory` table (replace enum) | CRUD | Dropdown | Attachments | S | 9 |
| Repeat-order shortcut (clone last order with measurements) | Faster counter service | — | `POST /orders/{id}/reorder` | Button | — | S | 7 |
| Permission table replacing fixed role→policy mapping | Custom roles per tenant | `Permission`, `RolePermission` | `/roles/*` | Matrix | Policies | M | 9 |
| Postgres row-level security as a second isolation layer | Defence in depth at scale | policies per table | — | — | Column collapse | M | 9 |

## P3 — Optional

| Feature | Business value | Notes | Cx |
|---|---|---|---|
| Customer portal / QR or link order tracking | Self-service status | Public read-only endpoint with signed token | M |
| Barcode/QR order labels, thermal printer receipts | Faster intake at counter | ESC/POS template | M |
| Offline/PWA and mobile app | Field use | Frontend scope; API needs idempotency keys | XL |
| WhatsApp Business API (two-way) | Customer replies handled | Provider dependent | L |
| Online booking / appointment for measurements | Convenience | `Appointment` table | M |
| Loyalty redemption | Points exist but cannot be spent | Redeem as payment line | S |
| Analytics beyond reports (trends, forecasting) | Owners of many branches | Read replica or materialised views | L |
| Karigar skills/specialisation/availability | Smarter assignment suggestions | columns on `User` or `KarigarProfile` | S |

---

## Rejected or deferred

| Item from the brief | Decision | Reason |
|---|---|---|
| Microservices | Rejected | Single team, single database, no scaling evidence; modular monolith is the right call |
| Full 16-state status list | Deferred | Adopt a subset (see ARCHITECTURE §2.3); add `MeasurementPending`, `FabricPending` only when fabric management ships |
| Roles Cashier, Accountant, Delivery Staff, Admin | Deferred | Existing five roles cover current flows; add via permission table when a tenant needs them |
| Separate `Garment` table | Rejected for now | `OrderItem` + configurable attributes covers it; a catalogue table only helps with price lists, which can be `GarmentPrice` later |
| `Worker` / `WorkerAssignment` tables | Rejected | `User` with Karigar role + `OrderItemStageAssignment` already model this |
| Profit reporting | Deferred | Cost data (fabric, karigar payouts) is not reliable until inventory and payouts exist |
| Distributed cache, read replicas | Deferred | Single instance today; revisit at second instance |

---

## Configuration to externalise (hard-coded today)

| Value | Location | Target |
|---|---|---|
| Trial length 14 days | `AdminProvisioningService.cs:158` | `SubscriptionPlan.TrialDays` or config |
| Loyalty +10 per order, tiers 100/500/1000 | `OrderService.cs:468-475` | tenant settings |
| Currency `PKR` / `₨` | `Shop.cs`, ViewModels | tenant setting (exists on Shop; defaults only) |
| Default report window 30 days | `ReportsController.cs`, `FinanceController.cs` | client parameter with documented default |
| Token expiry 24 h | `appsettings.json` | shorten to 1 h once refresh tokens exist |
| Urgent = due within 1 day (UTC) | `KarigarViewModels.cs:27` | tenant setting + shop time zone |
| SystemAdmin seed credentials | `DbInitializer.cs` | environment / dev only |
