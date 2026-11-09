# Digital Darzi — API Mapping

Phase 4. Every workflow step mapped to the endpoint that serves it. Endpoints, methods and field
names were read from the controllers and ViewModels on branch `feature/roadmap-2026-09`.
Full request and response details are in [WEBAPP_DEVELOPER_GUIDE.md](WEBAPP_DEVELOPER_GUIDE.md)
and Swagger.

**Status key**

| Status | Meaning |
|---|---|
| ✅ API | Endpoint exists and supports the workflow fully |
| 🟡 Partial | Endpoint exists but misses part of the workflow; see §3 |
| ❌ Missing | No endpoint. Backend work required; see §3 |

The current app's own integration status cannot be filled in: the frontend code is not available
(see [SYSTEM_ANALYSIS.md §1](SYSTEM_ANALYSIS.md#1-scope-and-what-was-available)). Where the
screenshots show a problem, it is noted as **App:**.

All responses use the envelope `{ success, data, message, errors }`. "Response" below describes
`data`.

---

## 1. Super Admin

| Feature | Screen | API Endpoint | Method | Request Data | Response Data | Status |
|---|---|---|---|---|---|---|
| Sign in | Login | `/api/auth/login` | POST | `email`, `password` | tokens, `roles` | ✅ API |
| Platform statistics | Admin dashboard | — | — | — | — | ❌ Missing (M1) |
| List shops | Shops | `/api/admin/shops` | GET | `page`, `pageSize` | paged `id, name, city, phone, activeStatus, createdOn` | 🟡 Partial: no subscription status, plan, owner, end date (M2) |
| List owners | Owners | `/api/admin/owners` | GET | `page`, `pageSize` | paged `id, name, email, phone, roles, activeStatus, lastLoginDate` | ✅ API |
| Create owner | Create shop wizard step 1 | `/api/admin/provision-owner` | POST | `name`, `email`+`password` or `phone`+`passcode` | `userId`, `tenantId`, `name`, `email` | ✅ API |
| Create first branch | Wizard step 2 | `/api/admin/provision-shop` | POST | `userId`, `shopName`, `address`, `city`, `phone`, `whatsAppNumber`, `logoUrl`, `currency`, `currencySymbol`, `timeZone` | `shopId`, `tenantId`, `shopName` | ✅ API |
| Start trial or activate | Wizard step 3 | `/api/admin/provision-subscription` | POST | `shopId`, `status`, `trialDays`, `subscriptionEndsAt`, `planName`, `planId` | `true` | ✅ API |
| Assign plan | Shop detail | `/api/admin/shops/{shopId}/plan` | PUT | `planId` | — | ✅ API |
| Suspend | Shop detail | `/api/admin/subscription/suspend/{shopId}` | POST | — | `true` | ✅ API |
| Re-activate | Shop detail | `/api/admin/subscription/activate/{shopId}` | POST | — | `true` | ✅ API |
| Extend by days | Shop detail | `/api/admin/subscription/extend/{shopId}` | POST | `days` | `true` | ✅ API |
| Set status / end date directly | Shop detail | `/api/shops/{id}/subscription` | PUT | `status`, `subscriptionEndsAt`, `subscriptionPlanName` | — | ✅ API |
| Edit tenant's shop profile | Shop detail | — | — | — | — | ❌ Missing (M3) |
| Deactivate a shop (not subscription) | Shop detail | — | — | — | — | ❌ Missing (M3). Suspend covers the business need. |
| Plans list / create / edit | Plans | `/api/admin/plans` | GET / POST / PUT `{id}` | `name`, `description`, `monthlyPrice`, `currency`, `maxBranches`, `maxUsers`, `maxOrdersPerMonth`, `trialDays`, `isActive` | plan | ✅ API |
| Plan feature flags | Plans | — | — | — | — | ❌ Missing (M4) |
| Renewal invoice | Billing | `/api/admin/invoices` | POST | `shopId`, `months`, `amount`, `notes`, `paymentMethod`, `paymentReference` | invoice | ✅ API |
| Invoices list | Billing | `/api/admin/invoices` | GET | `tenantId`, `status`, `page`, `pageSize` | paged invoices | ✅ API |
| Record renewal payment | Billing | `/api/admin/invoices/{id}/paid` | POST | `paymentMethod`, `paymentReference` | invoice; shop set Active, end date = period end | ✅ API |
| Void invoice | Billing | `/api/admin/invoices/{id}/void` | POST | — | invoice | ✅ API |
| Change a user's role | Users | `/api/admin/user-role/{userId}` | PUT | `newRole` | `true` | ✅ API |
| Shop usage and activity | Shop detail | — | — | — | — | ❌ Missing (M1) |
| Platform settings | Settings | — | — | — | — | ❌ Missing (M5) |

---

## 2. Shop (Owner and staff)

### Session and settings

| Feature | Screen | API Endpoint | Method | Request Data | Response Data | Status |
|---|---|---|---|---|---|---|
| Sign in | Login | `/api/auth/login` | POST | `email`+`password` or `phone`+`passcode` | tokens, ids, roles | ✅ API |
| Refresh token | (client) | `/api/auth/refresh` | POST | `refreshToken` | new token pair | ✅ API |
| Session, permissions, branches | App shell | `/api/auth/me` | GET | — | identity, `permissions[]`, `branches[]`, currency, `subscription` | ✅ API |
| Switch branch | Top bar | `/api/auth/switch-branch` | POST | `shopId` | new token pair | ✅ API |
| Forgot / reset password or PIN | Login | `/api/auth/forgot-password`, `/reset-password` | POST | `email` or `phone`; `code`, `newPassword` or `newPin` | — | ✅ API |
| Shop profile | Shop settings | `/api/shops`, `/api/shops/{id}` | GET / PUT | `id`, `name`, `address`, `city`, `phone`, `whatsAppNumber`, `logoUrl`, `currency`, `currencySymbol`, `timeZone`, `notificationsEnabled` | shop | ✅ API |
| Add branch | Shop settings | `/api/shops` | POST | as above without `id` | id | ✅ API (plan limit) |
| My subscription | Subscription | `/api/billing/subscription`, `/invoices`, `/plans` | GET | — | status, dates, plan, active users, outstanding invoices | ✅ API |

### Staff and permissions

| Feature | Screen | API Endpoint | Method | Request Data | Response Data | Status |
|---|---|---|---|---|---|---|
| Staff list | Staff | `/api/users` | GET | `keyword`, `pageNumber`, `pageSize` | `id, name, email, phone, roles[], activeStatus, lastLoginDate` | ✅ API |
| Filter by designation (Master, Cutter, Helper) | Staff | — | — | — | — | ❌ Missing (M6). **App:** chips shown with no backing data. |
| Add staff | Staff form | `/api/users` | POST | `name`, `email`+`password` or `phone`+`passcode`, `roleIds[]`, `shopIds[]` | id | ✅ API (plan limit) |
| Edit staff | Staff form | `/api/users` | PUT | `id`, `name`, `phone`, ... | — | ✅ API |
| Set roles / branches | Staff form | `/api/users/{id}/roles`, `/api/users/{id}/shops` | PUT | bare JSON array of ids | — | ✅ API |
| Role list for picker | Staff form | `/api/roles` | GET | — | roles (SystemAdmin excluded) | ✅ API |
| Deactivate / delete | Staff | `/api/users/{id}/deactivate`, `/api/users/{id}` | POST / DELETE | — | — | ✅ API |
| Permission matrix | Roles & permissions | `/api/permissions`, `/api/permissions/{role}` | GET / PUT | `permissions[]` | matrix | ✅ API |

### Customers

| Feature | Screen | API Endpoint | Method | Request Data | Response Data | Status |
|---|---|---|---|---|---|---|
| Search / list | Customers | `/api/customers` | GET | `query`, `customerType`, `hasBalance`, `city`, `gender`, `activeStatus`, `pageNumber`, `pageSize`, `sortBy`, `sortDirection` | `{ data[], pageNumber, pageSize, totalRecords }` rows with totals and loyalty tier | ✅ API |
| Create | New customer | `/api/customers` | POST | `name`, `phone`, `whatsAppNumber`, `customerType`, `gender`, `city`, `address`, `email`, `dateOfBirth`, `preferredKarigarId`, `preferredStyles`, `notificationsOptOut`, `notes` | id; 409 if phone exists in branch | ✅ API. **App:** sends an "Alternate Phone" the API does not have (M7). |
| Edit / deactivate | Customer detail | `/api/customers` | PUT | `id` + changed fields, `activeStatus` | — | ✅ API |
| Delete | Customer detail | `/api/customers/{id}` | DELETE | — | — | ✅ API |
| Detail | Customer detail | `/api/customers/{id}` | GET | — | full profile, totals, loyalty | ✅ API |
| Ledger | Customer detail | `/api/customers/{id}/ledger` | GET | — | orders, entries with running balance, totals | ✅ API (needs `finance.view`) |
| Orders of a customer | Customer detail | `/api/orders/customer/{customerId}` | GET | — | order rows | ✅ API |

### Measurements

| Feature | Screen | API Endpoint | Method | Request Data | Response Data | Status |
|---|---|---|---|---|---|---|
| Garment types list | Garment types | — (enum) | — | — | fixed 11 values | 🟡 Partial: read-only (M8) |
| Fields list | Measurement setup | `/api/measurements/fields` | GET | — | `id, name, unit, valueType, sortOrder` | ✅ API |
| Add / edit / delete field | Measurement setup | `/api/measurements/fields`, `/fields/{id}` | POST / PUT / DELETE | `name`, `unit`, `valueType`, `sortOrder` | — | 🟡 Partial: no label, dropdown type or default value (M9) |
| Templates list | Templates | `/api/measurements/templates` | GET | — | templates with garment type | ✅ API |
| Template detail | Template editor | `/api/measurements/templates/{id}` | GET | — | ordered fields with `isRequired` | ✅ API |
| Create / edit template | Template editor | `/api/measurements/templates`, `/templates/{id}` | POST / PUT | `name`, `garmentType`, `isDefault`, `displayOrder`, `fields[{ measurementFieldId, isRequired, sortOrder }]` | — | ✅ API. **App:** no required toggle or ordering per field. |
| Deactivate template | Templates | — | — | — | — | ❌ Missing (M10). Delete only. |
| Customer's profiles | Customer detail, new order | `/api/measurements/profiles/customer/{customerId}` | GET | — | all profiles, with template | ✅ API. **App:** not filtered by garment. |
| Profile detail | Measurement view | `/api/measurements/profiles/{id}` | GET | — | values per field | ✅ API |
| Profile history | Measurement view | `/api/measurements/profiles/{id}/versions` | GET | — | versions | ✅ API. **App:** not seen. |
| Create profile | Measurement form, new order | `/api/measurements/profiles` | POST | `customerId`, `templateId`, `profileName`, `values{fieldId: number}`, `textValues{fieldId: text}`, `notes` | id; 400 if required fields missing | ✅ API |
| Update profile (new version) | Measurement form | `/api/measurements/profiles/{id}` | PUT | as above | — | ✅ API |
| Garment attributes | Measurement setup, new order | `/api/garment-attributes?garmentType=` | GET / POST / PUT / DELETE | `garmentType`, `name`, `options[]`, `isRequired`, `sortOrder` | list | ✅ API |
| Seed default attributes | Measurement setup | `/api/garment-attributes/seed-defaults` | POST | — | — | ✅ API |

### Orders

| Feature | Screen | API Endpoint | Method | Request Data | Response Data | Status |
|---|---|---|---|---|---|---|
| Create order (multi-item) | New order | `/api/orders` | POST | `customerId`, `deliveryDate`, `priority`, `trialRequired`, `trialDate`, `discount`, `advancePayment`, `paymentMethod`, `notes`, `specialInstructions`, `items[{ garmentType, qty, price, measurementProfileId` or `templateId + inlineMeasurements + inlineTextMeasurements, fabricSource, fabricDescription, fabricColor, fabricCharge, styleNotes, attributes, stageAssignments[] }]` | order detail, 201 | ✅ API. **App:** no inline measurements, fabric source/charge, attributes. |
| Save measurements with the order atomically | New order | — | — | — | — | 🟡 Partial: two calls (M11) |
| List / search / filter | Orders | `/api/orders` | GET | `query`, `status`, `paymentStatus`, `priority`, `customerId`, `karigarId`, `garmentType`, `dateFrom`, `dateTo`, `deliveryFrom`, `deliveryTo`, `dueToday`, `overdue`, `hasBalance`, `sortBy`, `sortDirection`, `page`, `pageSize` | `{ items[], totalCount, page, pageSize, totalPages }` rows include `itemCount` | ✅ API. **App:** shows "0 item". |
| Due today / overdue | Orders, dashboard | `/api/orders/due-today`, `/api/orders/overdue` | GET | — | rows | ✅ API |
| Delivery board | Deliveries | `/api/orders/board` | GET | `perBucket` | counts + lists: due today, tomorrow, overdue, ready, delivered today | ✅ API |
| Detail | Order detail | `/api/orders/{id}` | GET | — | header, totals, `allowedNextStatuses`, items with measurements and stage progress, payments, alterations, history | ✅ API |
| Edit header | Order detail | `/api/orders` | PUT | `id`, `deliveryDate`, `notes`, `specialInstructions`, `discount`, `priority`, `trialRequired`, `trialDate` | — | ✅ API |
| Edit items after creation | Order detail | — | — | — | — | ❌ Missing by design. Cancel and recreate, or reorder. |
| Repeat order | Order detail | `/api/orders/{id}/reorder` | POST | `deliveryDate`, `useLatestMeasurements` | new order, 201 | ✅ API |
| Change status | Order detail | `/api/orders/{id}/status` | PATCH | `status`, `notes`, `force` | — | ✅ API |
| History | Order detail | `/api/orders/{id}/history` | GET | — | timeline | ✅ API |
| Receipt | Order detail | `/api/orders/{id}/receipt` | GET | `format=html` optional | receipt data or HTML | ✅ API |
| Payment | Order detail | `/api/orders/{id}/payments` | POST | `amount`, `paymentMethod`, `note` | — | ✅ API |
| Refund | Order detail | `/api/orders/{id}/refunds` | POST | `amount`, `paymentMethod`, `note` | — | ✅ API |
| Trial result | Order detail | `/api/orders/{id}/trial-result` | POST | `outcome`, `notes`, `alterations[]` | — | ✅ API |
| Alterations | Order detail | `/api/orders/alterations`, `/{id}/alterations`, `/alterations/{alterationId}` | POST / GET / PATCH | `orderItemId`, `description`, `requiredChange`, `additionalCharge`, `deliveryDate`, `assignedKarigarId`, `status`, `notes` | — | ✅ API |
| Deliver (full or partial) | Order detail, deliveries | `/api/orders/{id}/deliver` | POST | `itemIds[]`, `allowBalance`, `notes` | — | ✅ API |
| Cancel | Order detail | `/api/orders/{id}/cancel` | POST | `reason`, `refundAmount`, `refundMethod`, `waiveRefund` | — | ✅ API |
| Attach photos | Order detail | `/api/attachments` | POST multipart | `entityType`, `entityId`, `file`, `description` | attachment | ✅ API |

### Production

| Feature | Screen | API Endpoint | Method | Request Data | Response Data | Status |
|---|---|---|---|---|---|---|
| Assign karigar to stage | Order detail, production board | `/api/orders/stages/assign` | POST | `orderItemId`, `stage`, `assignedKarigarId`, `stagePrice`, `estimatedDays` | — | ✅ API |
| Production board | Production | `/api/workflow/board` | GET | `stage`, `perColumn` | columns pending, assigned, in progress, done, rework | ✅ API |
| Karigar's queue | My work | `/api/karigar/my-queue` | GET | — | pending, in progress, alteration tasks | ✅ API |
| Start stage | My work | `/api/orders/stages/log` | POST | `orderItemId`, `stage`, `karigarId`, `notes` | — | ✅ API |
| Complete stage | My work | `/api/orders/stages/complete` | POST | `orderItemId`, `stage`, `notes`, `fabricUsed`, `cutQuantity`, `defectNotes`, `recutRequired` | — | ✅ API |
| QC reject | Production | `/api/orders/stages/reject` | POST | `orderItemId`, `stage`, `reason`, `deductAmount` | — | ✅ API |
| Karigar performance | Production, reports | `/api/karigar/performance` | GET | — | per karigar | ✅ API |

### Money, reports and back office

| Feature | Screen | API Endpoint | Method | Request Data | Response Data | Status |
|---|---|---|---|---|---|---|
| Dashboard | Dashboard | `/api/reports/dashboard` | GET | `from`, `to`, `scope` | counts, today's sales and collections, active, in production, ready, overdue, due tomorrow, rework, open alterations, outstanding balance, karigar workload, daily revenue, top customers, stage queues | ✅ API. **App:** "+12%" has no source field. |
| Reports | Reports | `/api/reports/revenue`, `daily-revenue`, `top-customers`, `staff-performance`, `order-status`, `pending-balances`, `branches` | GET | `from`, `to`, `scope`, `limit` | report rows | ✅ API |
| CSV export | Reports | `/api/reports/export/{report}` | GET | `from`, `to`, `scope` | CSV file | ✅ API |
| Finance summary, expenses, categories | Finance | `/api/finance/summary`, `/expenses`, `/expense-categories` | GET / POST / PUT / DELETE | see guide | — | ✅ API |
| Salaries, rules, payroll, advances | Payroll | `/api/finance/salaries`, `/salary-rules`, `/salaries/compute`, `/salary-advances` | GET / POST / PUT | see guide | — | ✅ API |
| Attendance | Attendance | `/api/finance/attendance`, `/api/attendance/check-in`, `/my-attendance` | GET / POST | see guide | — | ✅ API |
| Cash drawer | Cash | `/api/cash/open`, `/current`, `/movements`, `/close`, `/sessions` | GET / POST | see guide | session with expected cash and variance | ✅ API |
| Karigar pay | Karigar pay | `/api/karigar/rates`, `/balances`, `/{id}/ledger` | GET / PUT / POST / DELETE | see guide | ledger with running balance | ✅ API |
| Inventory | Inventory | `/api/inventory/*` | GET / POST / PUT / DELETE | see guide | — | ✅ API |
| Notifications | Notifications | `/api/notifications/*` | GET / PUT / POST | see guide | — | ✅ API |
| Audit log | Audit | `/api/audit` | GET | `entityType`, `entityId`, `userId`, `from`, `to` | entries with old/new values | ✅ API |

---

## 3. Missing or insufficient APIs

Proposed contracts below are **proposals**, not existing endpoints.

| # | Need | Today | Proposed backend change | Size |
|---|---|---|---|---|
| M1 | Platform dashboard and per-shop usage | Nothing | `GET /api/admin/stats` (tenant, shop, user counts by subscription status; trials ending in 7 days; overdue invoices; orders this month) and `GET /api/admin/shops/{id}/usage` (users, orders this month vs plan limit, last activity) | S–M |
| M2 | Admin shop list with subscription | List returns basic shop fields | Add `status`, `trialEndsAt`, `subscriptionEndsAt`, `planName`, owner name and phone to `ShopListViewModel` for the admin route; add `status` filter | S |
| M3 | Admin edits or deactivates a tenant's shop | `/api/shops` is tenant-filtered | `PUT /api/admin/shops/{id}` with `IgnoreQueryFilters` | S |
| M4 | Plan-based feature access | Plans have limits only | `features[]` on `SubscriptionPlan`, checked in the permission handler | M |
| M5 | Platform settings | None | Decide what settings exist first (default trial days, support contact, WhatsApp credentials per platform) | M |
| M6 | Staff designation | Roles only | `designation` string on user create, update and list; list filter | S |
| M7 | Customer alternate phone | `whatsAppNumber` only | Either relabel the app field as WhatsApp, or add `alternatePhone` | XS |
| M8 | Owner-managed garment types | Fixed enum | Per-shop `GarmentType` table, migration of all `garmentType` columns, rates, attributes, reports | L |
| M9 | Field label, dropdown type, default value | `name`, Number/Text | `label`, `valueType = Choice` with `options[]`, `defaultValue` on `MeasurementField` | S–M |
| M10 | Deactivate a template | Delete only | `activeStatus` on template update; list hides inactive by default | XS |
| M11 | Save new measurements with the order in one transaction | Client must POST profile, then order | `saveAsProfile: true` and optional `profileName` on order item when inline values are used | S |
| M12 | Required-field check on inline order measurements | Not checked | Validate inline values against template required fields | XS |
| M13 | Karigar order detail without prices | Full detail returned | Trim money fields when caller lacks `orders.list` | S |

---

## 4. Integration problems visible in the current app

These are inferred from screenshots and must be confirmed in the code once available.

| # | Screen | Problem | Correct integration |
|---|---|---|---|
| I1 | Orders list | Item count shows 0 | Read `itemCount` from the list row |
| I2 | Orders list | Legacy "Pending" | Map status `0` and `12` to a neutral label and treat as Received |
| I3 | Orders list | No overdue / due-today / trial filters | Use `overdue=true`, `dueToday=true`, `status=6` query parameters |
| I4 | Dashboard | "+12%" | Remove, or compute from `newCustomersThisPeriod` over two periods |
| I5 | Create order | Profiles not filtered by garment | Filter profiles by their template's `garmentType` |
| I6 | Create order | Templates shown as garment types ("Pant Coat") | Choose garment type first, then template, then profile |
| I7 | Create order | No measurement entry in the flow | Add dynamic form from template fields; save as profile |
| I8 | New customer | Alternate Phone not stored | Map to `whatsAppNumber` or add M7 |
| I9 | Staff | Designation chips | Remove or implement M6 |
| I10 | Templates | No per-field required or order | Send `fields[{ measurementFieldId, isRequired, sortOrder }]` |

---

## 5. Validation and error handling the client must implement

| Area | Server behaviour | Client requirement |
|---|---|---|
| Any form | 400 with `errors[]`, one message per failed rule | Show messages next to fields; keep the user's input |
| Customer phone | 409 when the phone exists in the branch | Offer to open the existing customer |
| Order create | At least 1 item, at most 50; qty 1–500; money ≥ 0; profile and inline values not both | Disable submit until valid; same rules client-side |
| Measurement profile | Required template fields enforced | Mark required fields; block save if empty |
| Status change | 400 lists allowed targets; backwards needs a note | Use `allowedNextStatuses`; prompt for note on backward moves |
| Deliver | Fails with balance owed unless `allowBalance` and permission | Show balance and a confirm dialog |
| Plan limits | 400 with an upgrade message on branch, user or order create | Show the message with a link to subscription |
| Subscription | 402 on all tenant routes | Route to subscription page |
| Concurrency | 409 on conflicting edits, especially payments | Reload the record and retry |
| Permissions | 403 | Refetch `/auth/me`, hide the action, show a short message |
| Server error | 500 with `traceId` | Show generic message with the trace id |
| Dates | Must be UTC with `Z` | Convert on send; display in shop time zone |
