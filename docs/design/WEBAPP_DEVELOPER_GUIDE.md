# Digital Darzi API — Web App Developer Guide

How to build a browser front end on top of the Digital Darzi API. This covers the conventions every
call shares, the sign-in and session model, and the screens a tailor shop actually needs, in the
order a developer would build them. The complete route list is in [API.md](API.md); this guide
explains how to use it.

Related: [Frontend_Developer_Guide.md](../Frontend_Developer_Guide.md) (older reference, still
valid for routes it lists), [ARCHITECTURE.md](ARCHITECTURE.md), [DATABASE.md](DATABASE.md).

---

## 1. Getting a local API

```bash
# once: configure secrets (never commit these)
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=55432;Database=digitaldarzi;Username=postgres;Password=..." --project API
dotnet user-secrets set "Jwt:Key" "<at least 32 random characters>" --project API
dotnet user-secrets set "Seed:AdminEmail" "admin@digitaldarzi.local" --project API
dotnet user-secrets set "Seed:AdminPassword" "DevAdmin!2026" --project API

# create / upgrade the database and seed roles + the platform admin
dotnet run --project API -- --migrate

# run
dotnet run --project API
```

| Item | Value |
|---|---|
| Base URL (dev) | `http://localhost:5294` or `https://localhost:7174` |
| Swagger UI | `/swagger` (Development only, or `Swagger:Enabled=true`) |
| Health | `/health/live`, `/health/ready` |
| CORS | open in dev; production reads `Cors:AllowedOrigins`, so give the backend team your origin |

The seeded SystemAdmin exists only to provision tenants. To get a real shop to work against, sign in
as SystemAdmin and call `POST /api/admin/provision-owner` then `POST /api/admin/provision-shop`
(see §10), or use the demo accounts already in the shared dev database:

| Role | Login |
|---|---|
| SystemAdmin | `admin@digitaldarzi.local` / `DevAdmin!2026` (from user secrets) |
| Owner, 2 branches | `ali@shop.test` / `Owner!2026` |
| Receptionist | `sana@shop.test` / `Recep!2026` |
| Karigar | phone `03009998888`, PIN `1234` |

These accounts live in the database, not in code. A fresh database has only the SystemAdmin.

---

## 2. Conventions every call shares

### Response envelope

Every endpoint returns the same JSON shape, on success and on failure:

```json
{ "success": true,  "data": { ... }, "message": null, "errors": [] }
{ "success": false, "data": null, "message": "Order not found.", "errors": ["Order not found."] }
```

Use the HTTP status to branch, and `errors[]` for text to show the user. Validation failures return
400 with one entry per problem. Unhandled errors return 500 with a generic message plus a
`traceId: ...` entry you should surface in a "report problem" link.

| Status | Meaning | What the UI should do |
|---|---|---|
| 200 / 201 | OK | Use `data` |
| 400 | Validation or business rule failed | Show `errors[]` next to the form |
| 401 | Token missing, expired or invalid | Try `POST /api/auth/refresh`, else go to login |
| 402 | Subscription suspended, cancelled or expired | Show a "contact support / billing" screen; `/api/billing/*` and `/api/auth/*` still work |
| 403 | Signed in, but no permission | Hide the control; see §4 |
| 404 | Record not in this tenant or branch | Go back to the list |
| 409 | Concurrent edit or duplicate | Reload the record and retry |
| 429 | Too many sign-in attempts from this IP | Wait a minute |

### JSON rules

- Property names are camelCase.
- **Enums are integers**, both in requests and responses. The full table is in §11.
- **Dates are ISO-8601 UTC strings** such as `2026-11-08T09:30:00Z`. Always send the `Z`; the
  server treats anything without a zone as UTC. Convert to the shop time zone for display
  (default `Asia/Karachi`).
- Money is a decimal number. Format with the `currencySymbol` returned by `GET /api/auth/me`.
- IDs are GUIDs.

### Two paging shapes

The API grew in two generations, so lists come in one of two shapes. Check which one a route uses.

**Generic CRUD lists** (customers, shops, users, expenses, salaries, attendance) take
`pageNumber`, `pageSize`, `keyword`, `sortBy`, `sortDirection`, `disablePagination` and return:

```json
{ "data": [ ... ], "pageNumber": 1, "pageSize": 10, "totalRecords": 123 }
```

**Order, inventory, audit and notification lists** take `page`, `pageSize` and return:

```json
{ "items": [ ... ], "totalCount": 123, "page": 1, "pageSize": 20, "totalPages": 7, "hasNextPage": true, "hasPreviousPage": false }
```

Page size is capped at 200 on the server.

### Update calls

Generic CRUD updates are `PUT /api/<resource>` with the `id` **in the body**, not the URL.
Newer resources (orders alterations, measurements, inventory) use `PUT /api/<resource>/{id}`.
Fields you leave out of an update body are left unchanged, so send only what the user edited.

Deletes are soft. Deleted records disappear from lists but history that references them still renders.

---

## 3. Authentication and session

### Sign in

Two credential styles share one endpoint. Staff with an email use a password; karigars and
reception staff usually sign in with a phone number and a 4-digit PIN.

```http
POST /api/auth/login
{ "email": "owner@shop.pk", "password": "..." }
```
```http
POST /api/auth/login
{ "phone": "03001234567", "passcode": "1234" }
```

Response `data`:

```json
{
  "accessToken": "eyJ...", "expiresAt": "2026-11-09T09:30:00Z",
  "refreshToken": "c3f...", "refreshTokenExpiresAt": "2026-12-08T09:30:00Z",
  "userId": "...", "tenantId": "...", "shopId": "...",
  "userName": "Ali", "roles": ["Owner"]
}
```

Send `Authorization: Bearer <accessToken>` on every call. Never send `tenantId` or `shopId`
yourself; the server reads them from the token and scopes every query to that tenant and branch.

### Keep the session alive

Access tokens last 24 hours by default. On a 401, exchange the refresh token once:

```http
POST /api/auth/refresh
{ "refreshToken": "c3f..." }
```

You get a new pair. **Each refresh token works once**, so store the new one before retrying the
failed call, and serialise concurrent refreshes in your HTTP client. Sign-in routes are rate limited
per IP (20 per minute), but `/api/auth/me` is exempt.

Sign out with `POST /api/auth/logout` and `{ "refreshToken": "...", "allDevices": false }`.

### Load the session

Call this once after login and again on every page load. It drives the whole UI.

```http
GET /api/auth/me
```
```json
{
  "userId": "...", "name": "Ali", "email": "...", "phone": "...",
  "tenantId": "...", "shopId": "...", "shopName": "Main Branch",
  "currency": "PKR", "currencySymbol": "₨",
  "roles": ["Manager"],
  "permissions": ["orders.view", "orders.create", "payments.record", "..."],
  "branches": [ { "id": "...", "name": "Main Branch", "city": "Lahore" }, { "id": "...", "name": "DHA" } ],
  "subscription": { "status": 2, "trialEndsAt": null, "subscriptionEndsAt": "2027-01-01T00:00:00Z", "planName": "Standard" }
}
```

Use `permissions` to decide what to render, `branches` for the branch switcher, and
`subscription` to show a banner before access is cut off (status 1 Trial, 2 Active, 3 Suspended,
4 Cancelled).

### Switch branch

```http
POST /api/auth/switch-branch
{ "shopId": "<branch id from me.branches>" }
```

Returns a fresh token pair scoped to that branch. Replace both tokens, then refetch `/me` and
reload any cached lists, because every list is branch scoped.

### Password and PIN management

| Route | Body |
|---|---|
| `POST /api/auth/change-password` | `{ currentPassword, newPassword }` |
| `POST /api/auth/change-pin` | `{ currentPin, newPin }` |
| `POST /api/auth/forgot-password` | `{ email }` or `{ phone }`. Always returns success. Sends a 6-digit code. |
| `POST /api/auth/reset-password` | `{ email or phone, code, newPassword }` or `{ ..., newPin }` |

---

## 4. Permissions, not roles

Controls are gated by **named permissions**, and the server enforces them. Hide a button when the
permission is missing from `me.permissions`, but still handle 403 gracefully because an Owner can
change the role matrix while a user is signed in.

Roles and their default grants:

| Role | Default permissions |
|---|---|
| Owner | everything in the tenant |
| Manager | all day-to-day operations: orders, payments, production, customers, measurements, users view/edit, finance view/manage, reports, karigar pay, inventory, cash, notifications, audit |
| Receptionist | orders.view, orders.list, orders.deliver, payments.record, customers.view/edit, measurements.view/edit, inventory.view, cash.manage |
| Karigar | orders.view, production.work, measurements.view |
| SystemAdmin | platform routes under `/api/admin` only |

Full permission list: `orders.view`, `orders.list`, `orders.create`, `orders.edit`, `orders.status`,
`orders.cancel`, `orders.deliver`, `orders.deliver_with_balance`, `payments.record`,
`payments.refund`, `alterations.manage`, `production.assign`, `production.work`, `production.qc`,
`customers.view`, `customers.edit`, `customers.delete`, `measurements.view`, `measurements.edit`,
`measurements.configure`, `users.view`, `users.edit`, `users.manage`, `shops.manage`, `shops.edit`,
`finance.view`, `finance.manage`, `finance.delete`, `reports.view`, `reports.tenant_wide`,
`karigar.view_all`, `karigar.payouts`, `inventory.view`, `inventory.manage`, `cash.manage`,
`notifications.manage`, `audit.view`, `settings.permissions`, `billing.view`.

An Owner edits the matrix for Manager, Receptionist and Karigar:

```http
GET /api/permissions                      → matrix of role → permissions, plus which are delegable
PUT /api/permissions/{role}               → { "permissions": ["orders.view", ...] }
```

`settings.permissions`, `users.manage`, `billing.view`, `shops.manage` and `reports.tenant_wide`
can never be delegated.

---

## 5. Screens to build, in order

A minimal usable web app is: login → dashboard → customers → new order → order detail with
payments → delivery board → karigar queue. Everything else layers on.

### 5.1 Customers

```http
GET  /api/customers?query=ali&pageNumber=1&pageSize=20&customerType=2&hasBalance=true
GET  /api/customers/{id}
GET  /api/customers/{id}/ledger
POST /api/customers
PUT  /api/customers            (body includes "id")
DELETE /api/customers/{id}
```

Create body:

```json
{
  "name": "Ali Raza", "phone": "03001234567", "whatsAppNumber": "923001234567",
  "customerType": 1, "gender": 1, "city": "Lahore", "address": "...",
  "email": null, "dateOfBirth": null, "preferredKarigarId": null,
  "preferredStyles": "Slim fit", "notificationsOptOut": false, "notes": null
}
```

List rows carry `totalOrders`, `totalSpend` and `loyaltyTier`. The ledger returns every order,
alteration charge, payment and refund with a running balance, plus `totalDue`. Use it for the
"customer owes" view.

### 5.2 Measurements

Measurements are configurable per shop, so build the forms from the API rather than hard coding.

1. **Fields** are the vocabulary: `GET /api/measurements/fields` returns `{ id, name, unit, valueType, sortOrder }`. `unit` 1 inch, 2 cm, 3 mm. `valueType` 1 number, 2 text.
2. **Templates** group fields per garment: `GET /api/measurements/templates`, `GET /api/measurements/templates/{id}` returns ordered fields with `isRequired`.
3. **Profiles** are a customer's saved values for a template:

```http
GET  /api/measurements/profiles/customer/{customerId}
GET  /api/measurements/profiles/{id}
GET  /api/measurements/profiles/{id}/versions     (history; every save creates a version)
POST /api/measurements/profiles
PUT  /api/measurements/profiles/{id}
```
```json
{
  "customerId": "...", "templateId": "...", "profileName": "Kameez - summer",
  "values":     { "<fieldId>": 42.5, "<fieldId>": 17 },
  "textValues": { "<fieldId>": "Round collar" },
  "notes": null
}
```

Render the form by iterating the template's fields, binding number fields to `values` and text
fields to `textValues`. Creating or editing fields and templates needs `measurements.configure`
(`POST/PUT/DELETE /api/measurements/fields`, `/templates`).

**Garment attributes** (collar, cuff, pocket style and similar) are a second configurable list:
`GET /api/garment-attributes?garmentType=1` returns name and options per garment type. Call
`POST /api/garment-attributes/seed-defaults` once for a new shop. Selected values go on the order
item as a free `attributes` dictionary of `name → value`.

### 5.3 Create an order

```http
POST /api/orders        → 201, data = order detail
```
```json
{
  "customerId": "...",
  "deliveryDate": "2026-11-20T00:00:00Z",
  "priority": 1,
  "trialRequired": true,
  "trialDate": "2026-11-17T00:00:00Z",
  "discount": 500,
  "advancePayment": 3000,
  "paymentMethod": 1,
  "notes": null,
  "specialInstructions": "Customer wants extra buttons",
  "items": [
    {
      "garmentType": 1, "qty": 2, "price": 4500,
      "measurementProfileId": "...",
      "fabricSource": 1, "fabricDescription": "Blue lawn", "fabricColor": "Navy", "fabricCharge": 0,
      "styleNotes": "Slim fit",
      "attributes": { "Collar": "Ban", "Cuff": "Single" },
      "stageAssignments": [
        { "stage": 1, "assignedKarigarId": "...", "stagePrice": 300, "estimatedDays": 1 },
        { "stage": 2, "assignedKarigarId": "...", "stagePrice": 1200, "estimatedDays": 3 }
      ]
    }
  ]
}
```

Item measurements come from one of three sources, pick one per item:

- `measurementProfileId` to snapshot a saved profile (the snapshot is frozen on the order, later
  profile edits do not change it),
- `templateId` plus `inlineMeasurements` / `inlineTextMeasurements` keyed by field id,
- nothing, for items that need no measurements.

`stageAssignments` are optional. Leave them out and assign karigars later from the order detail or
the workflow board. Totals are computed server side: `subTotal` = Σ (price × qty + fabricCharge),
`grandTotal` = subTotal − discount + alteration charges, `balanceDue` = grandTotal − payments + refunds.
`advancePayment` is recorded as the first payment.

To update header fields use `PUT /api/orders` with `{ "id", "deliveryDate", "notes",
"specialInstructions", "discount", "priority", "trialRequired", "trialDate" }`. Items cannot be edited
after creation; cancel and recreate, or use `POST /api/orders/{id}/reorder` with
`{ "deliveryDate", "useLatestMeasurements": true }` to clone a past order for a repeat customer.

### 5.4 Order list and search

```http
GET /api/orders?query=ORD-2026&status=3&paymentStatus=1&priority=2&customerId=&karigarId=&garmentType=
    &dateFrom=&dateTo=&deliveryFrom=&deliveryTo=&dueToday=true&overdue=&hasBalance=true
    &sortBy=deliveryDate&sortDirection=asc&page=1&pageSize=20
```

`query` matches order number, customer name or phone. `sortBy` accepts `createdOn` (default),
`deliveryDate`, `grandTotal`, `balanceDue`, `orderNumber`. Each row has `status`, `paymentStatus`,
`grandTotal`, `amountPaid`, `balanceDue`, `deliveryDate`, `readyDate`, `itemCount`.

Shortcuts: `GET /api/orders/due-today`, `GET /api/orders/overdue`,
`GET /api/orders/customer/{customerId}`.

### 5.5 Order detail page

`GET /api/orders/{id}` returns everything the page needs in one call:

- header fields and totals, `status`, `paymentStatus`, `priority`, `trialRequired`, `trialDate`,
- **`allowedNextStatuses`**, use this to populate the status dropdown instead of guessing,
- `items[]` each with `status`, `attributes`, `measurements[]` (field name, unit, value or text),
  `stageProgress[]` (stage, karigar, status Pending / InProgress / Done / Rework, dates, reworkCount),
  `isDelivered`,
- `payments[]` with `isRefund`,
- `alterations[]`,
- `history[]` (status timeline with who, when, notes, manual override flag). Also available alone
  at `GET /api/orders/{id}/history`.

Receipt: `GET /api/orders/{id}/receipt` returns receipt data as JSON. Add `?format=html` to get a
ready-to-print HTML page you can open in a new tab or an iframe.

### 5.6 Order lifecycle

Production status values: 1 Received → 2 Cutting → 3 Stitching → 4 Embroidery → 5 Finishing →
6 TrialReady → 7 TrialDone → 8 ReadyToDeliver → 9 PartiallyDelivered → 10 Delivered. 11 is
Cancelled. 0 Pending and 12 FullyPaid are legacy values you may read but must never send.

```http
PATCH /api/orders/{id}/status
{ "status": 3, "notes": "optional", "force": false }
```

Rules the server enforces, so mirror them in the UI:

- Only values in `allowedNextStatuses` are accepted without `force`. A disallowed move returns 400
  listing the allowed targets.
- Moving **backwards** requires `notes`.
- `force: true` lets a supervisor jump anywhere from a non-terminal state, with a mandatory note,
  and is flagged in history as a manual override.
- Delivered and Cancelled are terminal.
- Status usually advances **automatically** as karigars complete stages, so most users never touch
  this control.

**Payments** (`payments.record`, `payments.refund`):

```http
POST /api/orders/{id}/payments   { "amount": 2000, "paymentMethod": 1, "note": null }
POST /api/orders/{id}/refunds    { "amount": 500,  "paymentMethod": 1, "note": "..." }
```

`paymentStatus` becomes 0 Unpaid, 1 Partial, 2 Paid or 3 Refunded on its own. Payment never
changes the production status.

**Trial / fitting** (`alterations.manage`):

```http
POST /api/orders/{id}/trial-result
{ "outcome": 1, "notes": "Fits well" }
{ "outcome": 2, "notes": "...", "alterations": [ { "orderItemId": "...", "description": "Sleeves 1 inch shorter", "requiredChange": "...", "additionalCharge": 0, "assignedKarigarId": null } ] }
```

Outcome 1 Approved moves the order on; 2 AlterationRequired creates alteration records.

**Alterations** can also be raised directly, including after delivery:

```http
POST  /api/orders/alterations              { orderItemId, description, requiredChange, additionalCharge, deliveryDate, assignedKarigarId, notes }
GET   /api/orders/{id}/alterations
PATCH /api/orders/alterations/{alterationId}   { status, assignedKarigarId, requiredChange, deliveryDate, notes }
```

Alteration status: 1 Received, 2 InProgress, 3 Done, 4 Delivered, 5 Cancelled.
`additionalCharge` is added to the order's grand total.

**Deliver** (`orders.deliver`):

```http
POST /api/orders/{id}/deliver
{ "itemIds": null, "allowBalance": false, "notes": null }
```

With no `itemIds` every item is delivered. Passing a subset marks those items and sets the order to
PartiallyDelivered. If `balanceDue` is above zero the call fails unless `allowBalance: true`, which
needs the `orders.deliver_with_balance` permission. Show the balance and a confirm dialog first.

**Cancel** (`orders.cancel`):

```http
POST /api/orders/{id}/cancel
{ "reason": "Customer changed mind", "refundAmount": 3000, "refundMethod": 1, "waiveRefund": false }
```

The body is optional. The server records the cancellation stage from the current status, writes the
refund as a negative payment line, and reverses the customer's order and spend counters.

### 5.7 Delivery board

One call for the reception "today" screen:

```http
GET /api/orders/board?perBucket=20
```

Returns counts and order lists for `dueToday`, `dueTomorrow`, `overdue`, `readyForPickup` and
`deliveredToday`. Poll it every minute or refresh after each delivery.

### 5.8 Production: assigning and doing work

Three roles touch this.

**Manager assigns** (`production.assign`):

```http
POST /api/orders/stages/assign
{ "orderItemId": "...", "stage": 2, "assignedKarigarId": "...", "stagePrice": 1200, "estimatedDays": 3 }
```

Stages: 1 Cutting, 2 Stitching, 3 Embroidery, 4 Ironing, 5 Finishing, 6 QualityCheck.
If `stagePrice` is omitted the server looks up the karigar's piece rate (§6).

**Karigar works** (`production.work`). The karigar app is three screens:

```http
GET  /api/karigar/my-queue        → pendingTasks[], inProgressTasks[], alterationTasks[], counts
POST /api/orders/stages/log       { "orderItemId", "stage", "karigarId": "<self>", "notes" }   → starts the stage
POST /api/orders/stages/complete  { "orderItemId", "stage", "notes", "fabricUsed", "cutQuantity", "defectNotes", "recutRequired" }
GET  /api/karigar/my-completed?from=&to=
GET  /api/karigar/my-ledger?from=&to=
```

Each queue task carries the garment, measurements come from the order detail, `attributes`,
`styleNotes`, `fabricColor`, `isUrgent`, `isOverdue`, `isRework` and `reworkReason`. Completing a
stage writes a ledger earning at the stage price and may advance the whole order. A karigar can
only see and act on their own items; the API returns 403 or 404 otherwise.

**QC rejects** (`production.qc`):

```http
POST /api/orders/stages/reject
{ "orderItemId": "...", "stage": 2, "reason": "Loose stitching", "deductAmount": 200 }
```

The item goes back to that karigar as rework and an optional deduction hits their ledger.

**Manager views** (`karigar.view_all`):

```http
GET /api/karigar/{karigarId}/queue
GET /api/karigar/performance                    → per-karigar assigned / completed / rework / earnings
GET /api/workflow/board?stage=2&perColumn=50    → kanban columns: pending, assigned, inProgress, done, rework
```

---

## 6. Karigar pay

Piece rates and a per-karigar ledger (`karigar.payouts`).

```http
GET    /api/karigar/rates?karigarId=            → rates; karigarId null means the shop default for that garment + stage
PUT    /api/karigar/rates                        { "karigarId": null | "...", "garmentType": 1, "stage": 2, "rate": 1200 }
DELETE /api/karigar/rates/{id}
GET    /api/karigar/balances                     → every karigar's totalEarned, totalPaid, balance
GET    /api/karigar/{karigarId}/ledger?from=&to= → opening balance, earnings, advances, deductions, payments, closing balance, entries[] with runningBalance
POST   /api/karigar/{karigarId}/ledger           { "entryType": 4, "amount": 5000, "paymentMethod": 1, "entryDate": null, "notes": "Weekly payout" }
```

Entry types: 1 Earning (system only), 2 Advance, 3 Deduction, 4 Payment, 5 Adjustment.
Earnings are created by stage completion and cannot be posted by hand.

---

## 7. Finance, staff and cash

All under `/api/finance` with `finance.view` to read and `finance.manage` to write. Use these
rather than the older `/api/expenses`, `/api/salaries` and `/api/attendance` controllers, which
are kept only for compatibility.

| Area | Routes |
|---|---|
| Summary | `GET /api/finance/summary?from=&to=` revenue, collected, expenses, salaries, net |
| Expenses | `GET/POST /api/finance/expenses`, `PUT/DELETE /api/finance/expenses/{id}` |
| Expense categories | `GET/POST /api/finance/expense-categories`, `PUT/DELETE .../{id}` |
| Salaries | `GET/POST /api/finance/salaries`, `POST /api/finance/salaries/{id}/payments` for partial payments |
| Salary rules | `GET/PUT /api/finance/salary-rules` (per-staff base pay, deductions per absence, overtime) |
| Payroll run | `GET /api/finance/salaries/compute?month=` preview, `POST /api/finance/salaries/compute` to commit |
| Advances | `GET/POST /api/finance/salary-advances` |
| Attendance | `GET/POST /api/finance/attendance`, `GET /api/finance/attendance/summary?from=&to=` |

Salary status: 1 Pending, 2 PartiallyPaid, 3 Paid. Attendance status: 1 Present, 2 Absent,
3 HalfDay, 4 Leave, 5 Holiday. Staff self check-in is `POST /api/attendance/check-in` and
`GET /api/attendance/my-attendance`.

**Cash drawer** (`cash.manage`), for shops that reconcile the till:

```http
POST /api/cash/open        { "openingFloat": 5000, "notes": null }
GET  /api/cash/current     → session with expected cash from payments and movements
POST /api/cash/movements   { "direction": 2, "amount": 300, "reason": "Tea" }      1 In, 2 Out
POST /api/cash/close       { "countedCash": 18200, "notes": null }                 → variance recorded
GET  /api/cash/sessions?page=1&pageSize=20
```

Attach a receipt image to an expense with the attachments API (§8).

---

## 8. Attachments, inventory, notifications, audit

**Attachments** are multipart uploads, 10 MB max by default:

```http
POST /api/attachments          form fields: entityType, entityId, file, description
GET  /api/attachments?entityType=1&entityId=<orderId>
GET  /api/attachments/{id}/file        → binary; set <img src> to this URL with the bearer header, or fetch to a blob
DELETE /api/attachments/{id}
```

`entityType`: 1 Order, 2 Expense, 3 Customer, 4 OrderItem. Because the file route needs the
bearer token, load images with `fetch` and `URL.createObjectURL` rather than a plain `<img src>`.

**Inventory** (`inventory.view` / `inventory.manage`):

```http
GET/POST /api/inventory/suppliers, PUT/DELETE /api/inventory/suppliers/{id}
GET /api/inventory/items?query=&category=&lowStockOnly=&page=&pageSize=
GET /api/inventory/items/{id}, GET /api/inventory/low-stock
POST /api/inventory/items, PUT/DELETE /api/inventory/items/{id}
GET /api/inventory/movements?itemId=&orderId=&page=&pageSize=
POST /api/inventory/items/{id}/purchase   { quantity, unitCost, supplierId, reference, notes, recordAsExpense: true, paymentMethod: 1 }
POST /api/inventory/items/{id}/issue      { orderItemId, quantity, notes }
POST /api/inventory/items/{id}/return     { orderItemId, quantity, notes }
POST /api/inventory/items/{id}/adjust     { movementType: 4 | 5, quantity (+/-), notes }   4 Adjustment, 5 Waste
POST /api/inventory/items/{id}/transfer   { targetShopId, quantity, notes }
```

Category: 1 Fabric, 2 Button, 3 Thread, 4 Zipper, 5 Lining, 6 Accessory, 7 Packaging, 99 Other.
Unit: 1 Meter, 2 Yard, 3 Piece, 4 Roll, 5 Spool, 6 Kg, 7 Pack.

**Notifications** (`notifications.manage`). WhatsApp and SMS go out automatically from an outbox
when orders are confirmed, ready, overdue and so on. The UI manages templates and shows the log:

```http
GET /api/notifications/templates           → one per event and channel, with placeholders
PUT /api/notifications/templates           { eventType, channel, body, isEnabled }
GET /api/notifications?orderId=&status=&page=&pageSize=
POST /api/notifications/orders/{orderId}   { eventType, customBody }   → send now on the customer's channel
POST /api/notifications/test               { channel, recipient, body }
```

Events: 1 OrderConfirmed, 2 OrderReady, 3 TrialReady, 4 DeliveryReminder, 5 PaymentReminder,
6 AlterationReady, 7 PaymentReceived, 8 OrderOverdue, 99 Custom. Channels: 1 WhatsApp, 2 Sms.
Message status: 0 Pending, 1 Sending, 2 Sent, 3 Failed, 4 Skipped (customer opted out).

**Audit** (`audit.view`): `GET /api/audit?entityType=Order&entityId=&userId=&from=&to=&page=&pageSize=`
shows who changed what, with before and after values for prices and payments.

---

## 9. Reports

All under `/api/reports` with `reports.view`. Date range defaults to the last 30 days. Add
`scope=tenant` to aggregate across every branch; that needs `reports.tenant_wide` (Owner only)
and returns 403 otherwise.

| Route | Returns |
|---|---|
| `GET /dashboard?from=&to=` | order counts by state, revenue, collected, balance due, expenses, salaries, net profit, top garments, recent orders |
| `GET /revenue?from=&to=` | revenue by payment method and garment |
| `GET /daily-revenue?from=&to=` | one row per day: orders, revenue, collected |
| `GET /top-customers?from=&to=&limit=10` | spend, orders, balance, tier |
| `GET /staff-performance?from=&to=` | per karigar |
| `GET /order-status` | count per status, for a pie or funnel |
| `GET /pending-balances?limit=200` | orders with money owed |
| `GET /branches?from=&to=` | one row per branch, Owner only |
| `GET /export/{report}?from=&to=` | CSV download; report is `daily-revenue`, `top-customers`, `pending-balances`, `staff-performance`, `expenses` or `branches` |

For the CSV, fetch with the bearer header, turn the response into a blob and trigger a download.

---

## 10. Shops, users, billing and platform admin

**Branches** (`shops.edit` / `shops.manage`): `GET /api/shops`, `GET /api/shops/{id}`,
`POST /api/shops`, `PUT /api/shops` (id in body), `DELETE /api/shops/{id}`. A shop has
`currency`, `currencySymbol`, `timeZone`, address and contact fields.

**Staff** (`users.view` / `users.edit` / `users.manage`):

```http
GET  /api/users?pageNumber=&pageSize=&keyword=
GET  /api/users/{id}
POST /api/users      { "name", "email", "password" }  or  { "name", "phone", "passcode": "1234" }, plus "roleIds": [], "shopIds": []
PUT  /api/users      { "id", "name", "phone", ... }
PUT  /api/users/{id}/roles   body is a bare array: ["<roleId>", ...]      Owner only
PUT  /api/users/{id}/shops   body is a bare array: ["<shopId>", ...]      Owner only
POST /api/users/{id}/deactivate
DELETE /api/users/{id}
GET  /api/roles      → role ids and names for the pickers
```

A user may belong to several branches; the branch switcher in §3 lists them.

**Billing** (Owner, `billing.view`), still reachable when the tenant is blocked with 402:
`GET /api/billing/subscription`, `GET /api/billing/invoices`, `GET /api/billing/plans`.

**Platform admin** (SystemAdmin role, separate admin console):

```http
POST /api/admin/provision-owner      { name, email, password } or { name, phone, passcode }   → { userId, tenantId }
POST /api/admin/provision-shop       { userId, shopName, address, city, phone, whatsAppNumber, logoUrl, currency, currencySymbol, timeZone }
POST /api/admin/provision-subscription   { shopId, status: 1, trialDays: 14, subscriptionEndsAt, planName, planId }
POST /api/admin/subscription/activate|suspend/{shopId},  POST .../extend/{shopId} { days }
GET  /api/admin/owners, GET /api/admin/shops
GET/POST /api/admin/plans, PUT /api/admin/plans/{id}, PUT /api/admin/shops/{shopId}/plan
GET/POST /api/admin/invoices, POST /api/admin/invoices/{id}/paid, POST /api/admin/invoices/{id}/void
PUT  /api/admin/user-role/{userId}   { newRole: "Manager" }
```

---

## 11. Enum reference

Send and receive these as integers.

| Enum | Values |
|---|---|
| OrderStatus | 1 Received, 2 Cutting, 3 Stitching, 4 Embroidery, 5 Finishing, 6 TrialReady, 7 TrialDone, 8 ReadyToDeliver, 9 PartiallyDelivered, 10 Delivered, 11 Cancelled. Legacy read-only: 0 Pending, 12 FullyPaid |
| PaymentStatus | 0 Unpaid, 1 Partial, 2 Paid, 3 Refunded |
| OrderPriority | 1 Normal, 2 Rush, 3 VIP |
| OrderItemStatus | 0 Pending, 1 InProgress, 2 Done, 3 Delivered, 4 Cancelled |
| ProductionStage | 1 Cutting, 2 Stitching, 3 Embroidery, 4 Ironing, 5 Finishing, 6 QualityCheck |
| StageLogStatus | 0 InProgress, 1 Completed, 2 ReworkRequired |
| GarmentType | 1 ShalwarKameez, 2 Suit, 3 Sherwani, 4 Coat, 5 Trouser, 6 Shirt, 7 Frock, 8 Lehnga, 9 Kurta, 10 Waistcoat, 99 Other |
| PaymentMethod | 1 Cash, 2 JazzCash, 3 EasyPaisa, 4 BankTransfer, 5 Other |
| FabricSource | 1 CustomerProvided, 2 ShopProvided, 3 PurchasedForOrder |
| TrialOutcome | 1 Approved, 2 AlterationRequired |
| AlterationStatus | 1 Received, 2 InProgress, 3 Done, 4 Delivered, 5 Cancelled |
| CancellationStage | 1 BeforeCutting, 2 AfterCutting, 3 AfterStitching |
| RefundStatus | 1 Pending, 2 Processed, 3 Waived |
| CustomerType | 1 Regular, 2 VIP, 3 Corporate, 4 WalkIn |
| Gender | 1 Male, 2 Female, 3 Other |
| MeasurementUnit | 1 Inch, 2 CM, 3 MM |
| MeasurementValueType | 1 Number, 2 Text |
| ActiveStatus | 0 Inactive, 1 Active |
| AttendanceStatus | 1 Present, 2 Absent, 3 HalfDay, 4 Leave, 5 Holiday |
| SalaryStatus | 1 Pending, 2 PartiallyPaid, 3 Paid |
| KarigarLedgerEntryType | 1 Earning, 2 Advance, 3 Deduction, 4 Payment, 5 Adjustment |
| AttachmentType | 1 Order, 2 Expense, 3 Customer, 4 OrderItem |
| NotificationChannel | 1 WhatsApp, 2 Sms, 3 Email, 4 InApp |
| NotificationEvent | 1 OrderConfirmed, 2 OrderReady, 3 TrialReady, 4 DeliveryReminder, 5 PaymentReminder, 6 AlterationReady, 7 PaymentReceived, 8 OrderOverdue, 9 PasswordReset, 99 Custom |
| NotificationStatus | 0 Pending, 1 Sending, 2 Sent, 3 Failed, 4 Skipped |
| InventoryCategory | 1 Fabric, 2 Button, 3 Thread, 4 Zipper, 5 Lining, 6 Accessory, 7 Packaging, 99 Other |
| InventoryUnit | 1 Meter, 2 Yard, 3 Piece, 4 Roll, 5 Spool, 6 Kg, 7 Pack |
| InventoryMovementType | 1 Purchase, 2 IssueToOrder, 3 ReturnFromOrder, 4 Adjustment, 5 Waste, 6 TransferOut, 7 TransferIn, 8 SupplierReturn |
| CashSessionStatus | 1 Open, 2 Closed |
| CashMovementDirection | 1 In, 2 Out |
| SubscriptionStatus | 1 Trial, 2 Active, 3 Suspended, 4 Cancelled |
| InvoiceStatus | 1 Pending, 2 Paid, 3 Void |

---

## 12. Client checklist

- One HTTP client wrapper that adds the bearer header, unwraps the envelope, retries once after a
  refresh on 401, routes 402 to the billing screen, and surfaces `errors[]` on 400.
- Serialise token refresh so parallel requests do not each burn the single-use refresh token.
- Load `/api/auth/me` on start and after branch switch; derive menu and buttons from `permissions`.
- Build measurement and attribute forms from the API, never from a hard-coded field list.
- Always send dates as UTC with `Z`; display in the shop time zone.
- Use `allowedNextStatuses` from order detail for the status control.
- Fetch files and CSV exports through the authenticated client, then open as blobs.
- Clear every cached list on branch switch; all data is branch scoped.
