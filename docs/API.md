# Digital Darzi API — HTTP Reference (as implemented)

Generated from the controllers in `API/Controllers` on 2026-09-27. This document describes the code **as written**, not the intended design. Where `Frontend_Developer_Guide.md` or `api_endpoints.yml` disagree with the code, the code wins; see [Drift](#6-drift-vs-existing-docs).

Line links point at the current source, e.g. [OrdersController.cs:39](../API/Controllers/OrdersController.cs#L39).

---

## 1. Conventions

| Topic | Behaviour | Source |
|---|---|---|
| Base URL | All routes are `/api/...` except the leftover `GET /WeatherForecast`. Swagger UI at `/swagger` (`/swagger/v1/swagger.json`). | [Program.cs:172](../API/Program.cs#L172) |
| Auth | `Authorization: Bearer <JWT>`. HS256, issuer/audience validated, `ClockSkew = 0`, default expiry 24h (`Jwt:ExpiryHours`). | [Program.cs:57](../API/Program.cs#L57), [AuthService.cs:134](../Infrastructure/Services/AuthService.cs#L134) |
| Claims issued | `nameid` (user Id), `unique_name` (name), `tenantId`, `shopId` (active branch), one `role` claim per role. `shopId` at login = first entry of `User.ShopIds`; `POST /api/auth/switch-branch` re-issues with another. | [AuthService.cs:144](../Infrastructure/Services/AuthService.cs#L144) |
| Tenant scoping | `ApplicationDbContext` is built per request from `tenantId`/`shopId` claims. Global query filters: `Shop`/`User` by TenantId; all operational entities by TenantId **and** BranchId. Missing claims → `Guid.Empty`. | [Program.cs:20](../API/Program.cs#L20), [ApplicationDbContext.cs:46](../Infrastructure/Data/ApplicationDbContext.cs#L46) |
| Response envelope | `{ "success": bool, "data": T, "message": string?, "errors": string[] }` (camelCase). Non-generic `ApiResponse` has no `data`. The `statusCode` argument of the failure constructor is **discarded**. | [ApiResponse.cs](../Application/Common/ApiResponse.cs) |
| Status mapping (`BaseController`) | success → 200; else first error contains "not found" → 404, contains "unauthorized" → 401, otherwise **400**. Non-generic overload always returns 400 on failure (not currently hit — every service returns `ApiResponse<T>`). Services' 500 hints (`BaseCrudService` catch blocks) therefore surface as 400. | [BaseController.cs:19](../API/Controllers/BaseController.cs#L19) |
| Create → 201 | Shops, Users, Customers, Orders, Expenses, Salaries, Attendance and `attendance/check-in` return `201 Created` with `Location`. All other writes return 200. | e.g. [OrdersController.cs:44](../API/Controllers/OrdersController.cs#L44) |
| Auth controller | Bypasses `BaseController`: login/refresh failures → 401, other failures → 400. | [AuthController.cs:25](../API/Controllers/AuthController.cs#L25) |
| Unhandled exceptions | `GlobalExceptionMiddleware`: `UnauthorizedAccessException` 401, `KeyNotFoundException` 404, `ArgumentException` 400, `DbUpdateException` 409, `InvalidOperationException` 422, else 500. Body is the envelope with `errors` (inner exception appended in Development). | [GlobalExceptionMiddleware.cs:37](../API/Middleware/GlobalExceptionMiddleware.cs#L37) |
| Pagination — shape A | `PagedResult<T>`: `{ items, totalCount, page, pageSize, totalPages, hasNextPage, hasPreviousPage }`. Used by Orders search, Users, Admin lists, Finance lists. | [ApiResponse.cs:55](../Application/Common/ApiResponse.cs#L55) |
| Pagination — shape B | `PaginatedResultModel<T>`: `{ data, pageNumber, pageSize, totalRecords }`. Used by every generic `GET /` from `BaseCrudController` (Shops, Customers, Expenses, Salaries, Attendance). Query: `Keyword, PageNumber=1, PageSize=10, DisablePagination, SortBy, SortDirection` + model-specific filters. Items are the **Detail** VM, not the List VM. | [PaginatedResultModel.cs](../Application/Common/Models/PaginatedResultModel.cs), [BaseCrudService.cs:49](../Application/Services/BaseCrudService.cs#L49) |
| Enums | Serialised as **integers** (no string-enum converter registered). `PATCH /api/orders/{id}/status` body is a bare integer, e.g. `8`. | [Program.cs:111](../API/Program.cs#L111), [Domain/Enums](../Domain/Enums) |
| Dates — JSON bodies | `UtcDateTimeConverter`/`NullableUtcDateTimeConverter` normalise every body `DateTime` to Kind=Utc (offsets converted, zone-less treated as UTC). | [UtcDateTimeConverter.cs](../API/Json/UtcDateTimeConverter.cs) |
| Dates — query string | **Not normalised.** `[FromQuery] DateTime?` (`from`, `to`, `DateFrom`, `DateTo`, `From`, `To`) binds with Kind=Unspecified and is compared directly against `timestamptz` columns, which Npgsql rejects. Send full ISO-8601 with `Z`. Defaults (`UtcNow`) are safe. | [FinanceRepository.cs:24](../Repositories/Implementations/FinanceRepository.cs#L24), [KarigarService.cs:185](../Infrastructure/Services/KarigarService.cs#L185) |
| 402 Payment Required | `SubscriptionMiddleware` returns `{ success:false, message }` with 402 for suspended/cancelled/expired shops. Skips `/api/auth`, `/api/admin`, non-GET `/api/shops`, SystemAdmin. **Currently a no-op** — see Defects. | [SubscriptionMiddleware.cs:48](../API/Middleware/SubscriptionMiddleware.cs#L48) |
| CORS | Default policy `AllowAnyOrigin/Method/Header`. | [Program.cs:149](../API/Program.cs#L149) |
| Validation | Only `[Required]`/`[MinLength]` on `ProvisionOwnerRequest.Name` and `ProvisionShopRequest.ShopName` (→ automatic 400 `ValidationProblemDetails`, **not** the envelope). Everything else is checked ad hoc in services or not at all. | [AdminProvisioningViewModels.cs:11](../Application/ViewModels/Admin/AdminProvisioningViewModels.cs#L11) |

**Authorization composition.** ASP.NET Core ANDs `[Authorize]` attributes. A class-level `[Authorize(Roles="Owner,Manager")]` plus an action-level `[Authorize]` (bare) still requires Owner or Manager — it does not widen access ([FinanceController.cs:79](../API/Controllers/FinanceController.cs#L79)). Conversely a class-level bare `[Authorize]` means **any authenticated role** reaches every action that lacks its own role list (Orders, Customers, Attendance, Measurements, Karigar, Shops).

---

## 2. Roles

| Role | What the code lets it reach |
|---|---|
| **SystemAdmin** | Everything under `/api/admin/*`, `/api/reports/*`, karigar queue/performance, `POST/PUT/DELETE /api/shops`, `PUT /api/shops/{id}/subscription`, plus all bare-`[Authorize]` actions. Excluded from `/api/users`, `/api/finance`, `/api/expenses`, `/api/salaries`, `GET /api/orders/{id}`, order status/stage/cancel actions. Bypasses subscription check. In practice tenant-scoped reads return nothing because its `tenantId` is the seeded system tenant ([DbInitializer.cs:63](../Infrastructure/Data/DbInitializer.cs#L63)). |
| **Owner** | Full tenant surface: users (create/delete/assign), shops, customers, measurements, orders (all actions), finance incl. expense delete, salaries, attendance, reports. Cannot call `/api/admin/*`. |
| **Manager** | Same as Owner minus: `POST/DELETE /api/users`, `deactivate`, `assign shops/roles`, `DELETE /api/shops`, `DELETE /api/finance/expenses/{id}`, `DELETE /api/expenses/{id}`. **Can** call `PUT /api/users` (see Defects). |
| **Receptionist** | Any bare-`[Authorize]` action: order search/create/update/payments/alterations/deliver, customers CRUD (not delete), measurements profiles (read fields/templates), attendance CRUD, karigar `my-queue`/`my-completed`, `GET /api/shops`. No finance, reports, stage actions, users. |
| **Karigar** | Same bare-`[Authorize]` surface as Receptionist (including **creating orders, customers, payments, and editing any attendance row**) plus `stages/log`, `stages/complete`. Excluded from `deliver`, `cancel`, `status`, `stages/assign`, finance, reports, users. |

---

## 3. Endpoint reference

Legend: **Roles** = effective roles after class+action composition. "Any" = any authenticated user. `→` = `data` type. Generic CRUD rows come from [BaseCrudController.cs](../API/Controllers/BaseCrudController.cs).

### 3.1 Auth — `/api/auth` ([AuthController.cs](../API/Controllers/AuthController.cs))

| Method | Route | Roles | Request | → data | Notes |
|---|---|---|---|---|---|
| POST | `/login` | Anonymous | `AuthLoginRequest { email?, phone?, password?, passcode? }` | `AuthTokenResponse { accessToken, refreshToken, expiresAt, userName, roles[], userId, tenantId, shopId }` | Email+password (Identity) **or** phone+PIN (BCrypt). Inactive → 401 "Account is inactive." Increments `LoginCount`. |
| POST | `/refresh` | Anonymous | JSON string `"<refreshToken>"` | `AuthTokenResponse` | **Stub** — always 401 "Refresh token not yet implemented." [AuthService.cs:82](../Infrastructure/Services/AuthService.cs#L82) |
| POST | `/change-password` | Any | `ChangePasswordRequest { currentPassword, newPassword }` | null | Identity users only. |
| POST | `/change-pin` | Any | `ChangePinRequest { currentPin, newPin }` | null | PIN users only; no PIN format validation. |
| POST | `/switch-branch` | Any | `SwitchBranchRequest { shopId }` | `AuthTokenResponse` | Requires `shopId ∈ User.ShopIds`; returns new JWT. |

### 3.2 Admin provisioning — `/api/admin` ([AdminProvisioningController.cs](../API/Controllers/AdminProvisioningController.cs)) — class `[Authorize(Roles="SystemAdmin")]`

| Method | Route | Request | → data | Notes |
|---|---|---|---|---|
| POST | `/provision-owner` | `ProvisionOwnerRequest { name*, email?, password?, phone?, passcode? }` | `ProvisionOwnerResult { userId, tenantId, name, email }` | Creates a new TenantId. `[Required]`/`[MinLength(2)]` on name. |
| POST | `/provision-shop` | `ProvisionShopRequest { userId, shopName*, address?, city?, phone?, whatsAppNumber?, logoUrl?, currency="PKR", currencySymbol }` | `ProvisionShopResult { shopId, tenantId, shopName }` | Sets `BranchId = Shop.Id`, appends to owner `ShopIds`, seeds measurement fields/templates. |
| POST | `/provision-subscription` | `ProvisionSubscriptionRequest { shopId, status=Trial, trialDays=14, subscriptionEndsAt?, planName? }` | `bool` | |
| POST | `/subscription/activate/{shopId:guid}` | — | `bool` | |
| POST | `/subscription/suspend/{shopId:guid}` | — | `bool` | Intended to block tenant; see Defects. |
| POST | `/subscription/extend/{shopId:guid}` | `ExtendSubscriptionRequest { days }` | `bool` | |
| PUT | `/user-role/{userId:guid}` | `UpdateUserRoleRequest { newRole }` | `bool` | Role **name**, cross-tenant. |
| GET | `/owners?page=1&pageSize=20` | — | `PagedResult<UserListViewModel>` | Cross-tenant. |
| GET | `/shops?page=1&pageSize=20` | — | `PagedResult<ShopListViewModel>` | Cross-tenant. |

### 3.3 Shops — `/api/shops` ([ShopsController.cs](../API/Controllers/ShopsController.cs)) — class `[Authorize]`

| Method | Route | Roles | Request | → data | Notes |
|---|---|---|---|---|---|
| GET | `/{id}` | Any | — | `ShopDetailViewModel` (id, tenantId, branchId, name, address, city, phone, whatsAppNumber, logoUrl, currency, currencySymbol, ownerId, ownerName, activeStatus, status, trialEndsAt, subscriptionEndsAt, subscriptionPlanName, createdOn…) | Tenant-filtered; repo mapping omits `tenantId/branchId/status/…` so those are default values [ShopRepository.cs:53](../Repositories/Implementations/ShopRepository.cs#L53). |
| GET | `/` | Any | `BaseSearchModel` query | `PaginatedResultModel<ShopDetailViewModel>` | All branches of caller's tenant. |
| POST | `/` | SystemAdmin, Owner | `CreateShopViewModel { name, address?, city?, phone?, whatsAppNumber?, logoUrl?, currency, currencySymbol }` | `Guid` (201) | `BranchId = Id`; **does not** add shop to caller's `ShopIds` — see Defects. |
| PUT | `/` | SystemAdmin, Owner, Manager | `UpdateShopViewModel { id, …nullable fields, activeStatus? }` | `Guid` | Null fields ignored. |
| DELETE | `/{id}` | SystemAdmin, Owner | — | `string` (id) | Soft delete + Inactive. |
| PUT | `/{id}/subscription` | SystemAdmin | `UpdateShopSubscriptionRequest { status, subscriptionEndsAt?, subscriptionPlanName? }` | `bool` | Bypasses `ReturnProcessedResponse` → failure is **400**. Always fails for real tenants — see Defects. |

### 3.4 Users — `/api/users` ([UsersController.cs](../API/Controllers/UsersController.cs)) — class `[Authorize(Roles="Owner,Manager")]`

| Method | Route | Roles | Request | → data | Notes |
|---|---|---|---|---|---|
| GET | `/?shopId=&page=1&pageSize=20` | Owner, Manager | — | `PagedResult<UserListViewModel>` | Tenant-wide; `shopId` filters by `ShopIds` array. |
| GET | `/{id}` | Owner, Manager | — | `UserDetailViewModel { id, name, email, phone, roleIds[], roleNames[], shopIds[], activeStatus, lastLoginDate, loginCount, createdOn }` | |
| POST | `/` | Owner | `CreateUserViewModel { name, email?, password?, phone?, passcode?, roleIds[], shopIds[] }` | `UserDetailViewModel` (201) | Rejects Owner/SystemAdmin role ids. Email or phone required. |
| PUT | `/` | Owner, Manager | `UpdateUserViewModel { id, name?, phone?, roleIds?, shopIds?, activeStatus? }` | `UserDetailViewModel` | **No role guard** — see Defects. |
| POST | `/{id}/deactivate` | Owner | — | null | |
| PUT | `/{id}/shops` | Owner | JSON `Guid[]` | null | Replaces `ShopIds`. |
| PUT | `/{id}/roles` | Owner | JSON `Guid[]` | null | Rejects Owner/SystemAdmin. Syncs Identity roles. |
| DELETE | `/{id}` | Owner | — | null | Soft delete; blocks self and Owner/SystemAdmin targets. |

### 3.5 Roles — `/api/roles` ([RolesController.cs](../API/Controllers/RolesController.cs)) — class `[Authorize]`

| Method | Route | Roles | → data | Notes |
|---|---|---|---|---|
| GET | `/` | Any | `List<RoleViewModel { id, name }>` | SystemAdmin excluded. |

### 3.6 Customers — `/api/customers` ([CustomersController.cs](../API/Controllers/CustomersController.cs)) — class `[Authorize]`

| Method | Route | Roles | Request | → data | Notes |
|---|---|---|---|---|---|
| GET | `/{id}` | Any | — | `CustomerDetailViewModel` | |
| GET | `/` | Any | `CustomerSearchViewModel { query?, city?, gender?, activeStatus? } + BaseSearchModel` | `PaginatedResultModel<CustomerDetailViewModel>` | Branch-scoped. |
| POST | `/` | Any | `CreateCustomerViewModel { name, phone, email?, address?, city?, gender=Male, dateOfBirth?, notes? }` | `Guid` (201) | Unique `(TenantId, ShopId, Phone)` → duplicate = 409 via middleware. Karigar can create. |
| PUT | `/` | Any | `UpdateCustomerViewModel { id, …nullable, activeStatus? }` | `Guid` | |
| DELETE | `/{id}` | Owner, Manager | — | `string` | Soft delete. |
| GET | `/{id}/ledger` | Any | — | `CustomerLedgerViewModel { customerId, name, phone, orders[{orderId, orderNumber, grandTotal, amountPaid, balanceDue, status, deliveryDate}], totalGrand, totalPaid, totalDue }` | |

### 3.7 Measurements — `/api/measurements` ([MeasurementsController.cs](../API/Controllers/MeasurementsController.cs)) — class `[Authorize]`

| Method | Route | Roles | Request | → data | Notes |
|---|---|---|---|---|---|
| GET | `/fields` | Any | — | `List<MeasurementFieldViewModel { id, name, unit, sortOrder, isSystemField }>` | **Returns all tenants' fields** — see Defects. Includes soft-deleted (Inactive) fields. |
| POST | `/fields` | Owner, Manager | `CreateMeasurementFieldViewModel { name, unit=Inch, sortOrder }` | `MeasurementFieldViewModel` | |
| PUT | `/fields/{id}` | Owner, Manager | `UpdateMeasurementFieldViewModel { name?, unit?, sortOrder? }` | `MeasurementFieldViewModel` | System fields (TenantId=Empty) rejected. |
| DELETE | `/fields/{id}` | Owner, Manager | — | null | Sets `ActiveStatus=Inactive` only. |
| GET | `/templates` | Any | — | `List<TemplateListViewModel { id, name, garmentType, isDefault, isSystemTemplate, displayOrder, fieldCount }>` | **All tenants** — see Defects. |
| GET | `/templates/{id}` | Any | — | `TemplateDetailViewModel { …, fields[{measurementFieldId, fieldName, unit, isRequired, sortOrder}] }` | **Any tenant's template by id** — see Defects. |
| POST | `/templates` | Owner, Manager | `CreateTemplateViewModel { name, garmentType?, isDefault, displayOrder, fields[{measurementFieldId, isRequired, sortOrder}] }` | `TemplateDetailViewModel` | |
| PUT | `/templates/{id}` | Owner, Manager | `UpdateTemplateViewModel` (all nullable; `fields` replaces the set) | `TemplateDetailViewModel` | System templates rejected. |
| DELETE | `/templates/{id}` | Owner, Manager | — | null | |
| GET | `/profiles/customer/{customerId}` | Any | — | `List<MeasurementProfileListViewModel { id, templateId, templateName, profileName, garmentType, updatedOn }>` | Branch-scoped. |
| GET | `/profiles/{id}` | Any | — | `MeasurementProfileDetailViewModel { …, values: {fieldName: decimal}, notes, updatedOn }` | |
| POST | `/profiles` | Any | `SaveMeasurementProfileViewModel { customerId, templateId, profileName?, values: {fieldId: decimal}, notes? }` | `MeasurementProfileDetailViewModel` | No check that customer/template exist → FK error 409. |
| PUT | `/profiles/{id}` | Any | `SaveMeasurementProfileViewModel` | `MeasurementProfileDetailViewModel` | |
| DELETE | `/profiles/{id}` | Any | — | null | Hard delete. |

### 3.8 Orders — `/api/orders` ([OrdersController.cs](../API/Controllers/OrdersController.cs)) — class `[Authorize]`

| Method | Route | Roles | Request | → data | Notes |
|---|---|---|---|---|---|
| GET | `/` | Any | `OrderSearchViewModel { query?, status?, dateFrom?, dateTo?, dueToday?, page=1, pageSize=20 }` | `PagedResult<OrderListViewModel>` | Query dates not UTC-normalised. |
| GET | `/{id}` | Owner, Manager, Karigar, Receptionist | — | `OrderDetailViewModel { …, items[{…, measurementSnapshot, stageProgress[]}], payments[], alterations[] }` | SystemAdmin excluded. |
| GET | `/customer/{customerId}` | Any | — | `List<OrderListViewModel>` | |
| GET | `/due-today` | Any | — | `List<OrderListViewModel>` | UTC day. |
| GET | `/overdue` | Any | — | `List<OrderListViewModel>` | |
| POST | `/` | Any | `CreateOrderViewModel { customerId, deliveryDate, notes?, specialInstructions?, discount=0, advancePayment=0, paymentMethod=Cash, items[{garmentType, measurementProfileId?, templateId?, inlineMeasurements?, fabricDescription?, fabricColor?, styleNotes?, price, qty=1, stageAssignments?[{stage, assignedKarigarId?, stagePrice?, estimatedDays}]}], attachments? }` | `OrderDetailViewModel` (201) | Validates customer only. `GrandTotal = Σ(price×qty) − discount`; advance creates a payment row. `attachments` is `List<IFormFile>` in a JSON body — unusable; see Defects. Karigar/Receptionist can create. |
| PUT | `/` | Any | `UpdateOrderViewModel { id, deliveryDate?, notes?, specialInstructions?, discount? }` | `OrderDetailViewModel` | Blocked for Delivered/Cancelled. Recomputes `GrandTotal = SubTotal − Discount` — drops alteration charges; see Defects. |
| PATCH | `/{id}/status` | Owner, Manager | bare `OrderStatus` int | null | Any transition, no history — see Defects. |
| POST | `/{id}/payments` | Any | `RecordOrderPaymentViewModel { amount, paymentMethod=Cash, note? }` | null | Validates `0 < amount ≤ balanceDue`. Sets `Status=FullyPaid` when balance hits 0 — see Defects. |
| POST | `/alterations` | Any | `CreateOrderAlterationViewModel { orderItemId, description, additionalCharge=0, deliveryDate? }` | null | Adds charge to GrandTotal/BalanceDue. |
| POST | `/stages/log` | Owner, Manager, Karigar | `RecordStageLogViewModel { orderItemId, stage, karigarId, notes? }` | null | Validates karigar exists in tenant; does **not** check caller == karigarId. Sets item `InProgress`. |
| POST | `/stages/complete` | Owner, Manager, Karigar | `CompleteStageViewModel { orderItemId, stage, notes? }` | null | Completes latest open log for the stage; no caller check. |
| POST | `/stages/assign` | Owner, Manager | `AssignStageViewModel { orderItemId, stage, assignedKarigarId, stagePrice?, estimatedDays }` | null | Upserts per (item, stage). |
| POST | `/{id}/cancel` | Owner, Manager | — | null | Blocked if Delivered. |
| POST | `/{id}/deliver` | Owner, Manager, Receptionist | — | null | Blocked if Cancelled. No balance check. |

### 3.9 Karigar — `/api/karigar` ([KarigarController.cs](../API/Controllers/KarigarController.cs)) — class `[Authorize]`

| Method | Route | Roles | Request | → data |
|---|---|---|---|---|
| GET | `/my-queue` | Any | — | `KarigarWorkQueueViewModel { karigarId, karigarName, totalPending, totalInProgress, totalCompletedToday, pendingTasks[], inProgressTasks[] }` |
| GET | `/{karigarId}/queue` | SystemAdmin, Owner, Manager | — | `KarigarWorkQueueViewModel` |
| GET | `/performance` | SystemAdmin, Owner, Manager | — | `List<KarigarPerformanceSummaryViewModel>` |
| GET | `/my-completed?from=&to=` | Any | — | `KarigarCompletedHistoryViewModel { …, completedStages[] }` — query dates not UTC-normalised |

### 3.10 Finance — `/api/finance` ([FinanceController.cs](../API/Controllers/FinanceController.cs)) — class `[Authorize(Roles="Owner,Manager")]`

| Method | Route | Roles | Request | → data | Notes |
|---|---|---|---|---|---|
| GET | `/summary?from=&to=` | Owner, Manager | — | `FinanceSummaryViewModel { totalRevenue, totalCollected, totalBalanceDue, totalExpenses, totalSalaries, netProfit, totalOrders, pendingOrders, expenseBreakdown[] }` | Defaults last 30 days. |
| GET | `/expenses?from=&to=&category=&page=1&pageSize=20` | Owner, Manager | — | `PagedResult<ShopExpenseListViewModel>` | |
| POST | `/expenses` | Owner, Manager | `CreateShopExpenseViewModel { category, description, amount, expenseDate, paymentMethod=Cash, receiptRef? }` | `ShopExpenseListViewModel` | Stamps TenantId/BranchId/ShopId. |
| PUT | `/expenses/{id}` | Owner, Manager | `UpdateShopExpenseViewModel` (nullable fields; body `id` ignored) | `ShopExpenseListViewModel` | |
| DELETE | `/expenses/{id}` | Owner | — | null | |
| GET | `/salaries?month=&year=&staffId=&page=1&pageSize=20` | Owner, Manager | — | `PagedResult<StaffSalaryListViewModel>` | |
| POST | `/salaries` | Owner, Manager | `RecordStaffSalaryViewModel { staffUserId, month, year, baseSalary, bonus=0, deduction=0, paymentMethod=Cash, notes? }` | `StaffSalaryListViewModel` | Unique `(Tenant, Staff, Month, Year)` → 409 on repeat. |
| GET | `/attendance?staffId=&from=&to=&page=1&pageSize=50` | Owner, Manager | — | `PagedResult<AttendanceListViewModel>` | |
| POST | `/attendance` | Owner, Manager (bare `[Authorize]` does not widen) | `RecordAttendanceViewModel { staffUserId, date, status=Present, checkIn?, checkOut?, notes? }` | `AttendanceListViewModel` | Unique `(Tenant, Staff, Date)`. |
| GET | `/attendance/summary?staffId=&month=&year=` | Owner, Manager | — | `AttendanceSummaryViewModel` | |

### 3.11 Expenses — `/api/expenses` ([ExpensesController.cs](../API/Controllers/ExpensesController.cs)) — class `[Authorize(Roles="Owner,Manager")]`, generic CRUD

| Method | Route | Roles | Request | → data | Notes |
|---|---|---|---|---|---|
| GET | `/{id}` | Owner, Manager | — | `ShopExpenseDetailViewModel` | |
| GET | `/` | Owner, Manager | `ShopExpenseSearchModel { from?, to?, category? } + BaseSearchModel` | `PaginatedResultModel<ShopExpenseDetailViewModel>` | |
| POST | `/` | Owner, Manager | `CreateShopExpenseViewModel` | `Guid` (201) | **TenantId/BranchId not stamped** — see §4. |
| PUT | `/` | Owner, Manager | `UpdateShopExpenseViewModel { id, … }` | `Guid` | |
| DELETE | `/{id}` | Owner | — | `string` | |

### 3.12 Salaries — `/api/salaries` ([SalariesController.cs](../API/Controllers/SalariesController.cs)) — class `[Authorize(Roles="Owner,Manager")]`, generic CRUD

| Method | Route | Roles | Request | → data | Notes |
|---|---|---|---|---|---|
| GET | `/{id}` | Owner, Manager | — | `StaffSalaryDetailViewModel` | |
| GET | `/` | Owner, Manager | `StaffSalarySearchModel { month?, year?, staffId? } + BaseSearchModel` | `PaginatedResultModel<StaffSalaryDetailViewModel>` | |
| POST | `/` | Owner, Manager | `RecordStaffSalaryViewModel` | `Guid` (201) | **TenantId/BranchId not stamped** — see §4. |
| PUT | `/` | Owner, Manager | `UpdateStaffSalaryViewModel { id, baseSalary?, bonus?, deduction?, paymentMethod?, notes? }` | `Guid` | Recomputes `NetSalary`. |
| DELETE | `/{id}` | Owner, Manager | — | `string` | Base implementation. |

### 3.13 Attendance — `/api/attendance` ([AttendanceController.cs](../API/Controllers/AttendanceController.cs)) — class `[Authorize]`, generic CRUD

| Method | Route | Roles | Request | → data | Notes |
|---|---|---|---|---|---|
| GET | `/{id}` | Any | — | `AttendanceDetailViewModel` | |
| GET | `/` | Any | `AttendanceSearchModel { staffId?, from?, to? } + BaseSearchModel` | `PaginatedResultModel<AttendanceDetailViewModel>` | Any role sees whole branch. |
| POST | `/` | Any | `RecordAttendanceViewModel` | `Guid` (201) | **TenantId/BranchId not stamped** — see §4. Any role can record for any staff. |
| PUT | `/` | Any | `UpdateAttendanceViewModel { id, status?, checkIn?, checkOut?, notes? }` | `Guid` | Any role can edit any row. |
| DELETE | `/{id}` | Any | — | `string` | |
| GET | `/my-attendance?from=&to=&month=&year=` | Any | — | `List<AttendanceListViewModel>` | Defaults to current month. |
| POST | `/check-in` | Any | `StaffCheckInViewModel { checkOut?, notes? }` | `Guid` (201) | Creates today's Present row (TenantId/BranchId stamped correctly); second call updates checkOut. |
| GET | `/summary?staffId=&month=&year=` | Owner, Manager | — | `AttendanceSummaryViewModel` | |

### 3.14 Reports — `/api/reports` ([ReportsController.cs](../API/Controllers/ReportsController.cs)) — class `[Authorize(Roles="SystemAdmin,Owner,Manager")]`

All `from`/`to` default to last 30 days (UTC); supplied values are not UTC-normalised.

| Method | Route | → data |
|---|---|---|
| GET | `/dashboard?from=&to=` | `DashboardSummaryViewModel` |
| GET | `/revenue?from=&to=` | `RevenueReportViewModel` |
| GET | `/daily-revenue?from=&to=` | `List<DailyRevenueViewModel>` |
| GET | `/top-customers?from=&to=&limit=10` | `List<TopCustomerViewModel>` |
| GET | `/staff-performance?from=&to=` | `List<StaffPerformanceViewModel>` |
| GET | `/order-status` | `OrderStatusReportViewModel` |
| GET | `/pending-balances` | `List<PendingBalanceViewModel>` |

### 3.15 WeatherForecast — `/WeatherForecast` ([WeatherForecastController.cs](../API/Controllers/WeatherForecastController.cs))

| Method | Route | Roles | → |
|---|---|---|---|
| GET | `/WeatherForecast` | **Anonymous** | Raw array (no envelope) of template sample data. Leftover from `dotnet new webapi`; should be deleted. |

---

## 4. Duplicated surfaces

| Concern | `/api/finance/*` (FinanceController) | Generic controller | Verdict |
|---|---|---|---|
| Expenses | `POST /api/finance/expenses` sets `TenantId`, `BranchId`, `ShopId`, `AddedByUserId` [FinanceService.cs:28](../Infrastructure/Services/FinanceService.cs#L28) | `POST /api/expenses` sets only `ShopId`/`AddedByUserId` [ShopExpenseService.cs:35](../Infrastructure/Services/ShopExpenseService.cs#L35); AutoMapper profile does not map TenantId/BranchId [DomainMappingProfile.cs:40](../Application/Mappings/DomainMappingProfile.cs#L40); `BaseRepository.AddAsync` does not stamp either [BaseRepository.cs:78](../Infrastructure/Repositories/BaseRepository.cs#L78) | **Use `/api/finance/expenses`.** Rows created via `/api/expenses` have `TenantId = BranchId = Guid.Empty`, so the global filter hides them: `POST` returns 201 but the `Location` `GET /api/expenses/{id}` returns 404 and the row never appears in any list or report. |
| Salaries | `POST /api/finance/salaries` stamps all three ids [FinanceService.cs:95](../Infrastructure/Services/FinanceService.cs#L95) | `POST /api/salaries` sets only `ShopId` [StaffSalaryService.cs:36](../Infrastructure/Services/StaffSalaryService.cs#L36) | **Use `/api/finance/salaries`.** Same orphan-row problem. `PUT /api/salaries` and `DELETE` work only on rows created through the finance route. |
| Attendance | `POST /api/finance/attendance` stamps all three ids [FinanceService.cs:134](../Infrastructure/Services/FinanceService.cs#L134); Owner/Manager only | `POST /api/attendance` sets only `ShopId` [StaffAttendanceService.cs:140](../Infrastructure/Services/StaffAttendanceService.cs#L140); any role | **Use `/api/finance/attendance` for manual entry and `POST /api/attendance/check-in` for self-service** (check-in stamps correctly [StaffAttendanceService.cs:110](../Infrastructure/Services/StaffAttendanceService.cs#L110)). The generic `GET /api/attendance`, `/my-attendance`, `/summary`, `PUT`, `DELETE` read/modify correctly-stamped rows fine. |

A side effect: the unique indexes on attendance `(TenantId, StaffUserId, Date)` and salary `(TenantId, StaffUserId, Month, Year)` include `TenantId`, so an orphan row (`TenantId = Empty`) does not collide with a later, correctly-stamped insert for the same staff/date or staff/month — the record is silently duplicated rather than rejected ([ApplicationDbContext.cs:126](../Infrastructure/Data/ApplicationDbContext.cs#L126), [:136](../Infrastructure/Data/ApplicationDbContext.cs#L136)).

---

## 5. Known API defects

Severity: **H** = data leak / privilege / data corruption, **M** = wrong behaviour, **L** = cosmetic or dead code. Every row was verified against the cited source.

| Sev | Endpoint | Issue | Evidence |
|---|---|---|---|
| H | all tenant routes | `SubscriptionMiddleware` looks up `Shops.FirstOrDefault(s => s.Id == tenantId)`. Shops are created with `Id = Guid.NewGuid()` and `TenantId = owner.TenantId`, so `Shop.Id ≠ TenantId` and the lookup is always null → **402 is never returned; suspend/expiry are unenforced**. | [SubscriptionMiddleware.cs:48](../API/Middleware/SubscriptionMiddleware.cs#L48), [AdminProvisioningService.cs:145](../Infrastructure/Services/AdminProvisioningService.cs#L145) |
| H | `GET /api/measurements/fields`, `/templates`, `/templates/{id}` | Repository calls `IgnoreQueryFilters()` with no tenant predicate (only `IsSystemField = TenantId == Empty` is derived). Returns **every tenant's** custom fields and templates, and any template by id. Also returns soft-deleted (Inactive) fields. | [MeasurementRepository.cs:20](../Repositories/Implementations/MeasurementRepository.cs#L20), [:59](../Repositories/Implementations/MeasurementRepository.cs#L59), [:76](../Repositories/Implementations/MeasurementRepository.cs#L76) |
| H | `PUT /api/users` | Reachable by Manager; `UpdateAsync` copies `RoleIds` and syncs Identity roles with no restricted-role check (unlike `POST` and `PUT /{id}/roles`). A Manager can make any user (or themself) Owner or SystemAdmin. | [UsersController.cs:40](../API/Controllers/UsersController.cs#L40), [UserService.cs:195](../Infrastructure/Services/UserService.cs#L195) |
| H | `POST /api/orders`, `POST /api/orders/{id}/payments`, `POST /api/orders/alterations`, `POST/PUT /api/customers`, `POST/PUT/DELETE /api/attendance` | Class-level bare `[Authorize]` with no action roles: **Karigar** and **Receptionist** can create orders, record payments, add alteration charges, create/edit customers, and create/edit/delete **any** staff member's attendance. | [OrdersController.cs:12](../API/Controllers/OrdersController.cs#L12), [CustomersController.cs:10](../API/Controllers/CustomersController.cs#L10), [AttendanceController.cs:10](../API/Controllers/AttendanceController.cs#L10) |
| H | `POST /api/expenses`, `/api/salaries`, `/api/attendance` | Rows saved with `TenantId = BranchId = Guid.Empty`; invisible to every read (`GET /{id}` after `POST` → 404). See §4. | [ShopExpenseService.cs:35](../Infrastructure/Services/ShopExpenseService.cs#L35) |
| M | `PATCH /api/orders/{id}/status` | Accepts any `OrderStatus` value for any current status (Delivered→Pending, Cancelled→Delivered…). Writes only `Status`/`UpdatedOn`; `OrderStatusHistory` entity exists but nothing writes to it. | [OrderService.cs:224](../Infrastructure/Services/OrderService.cs#L224), [OrderRepository.cs:155](../Repositories/Implementations/OrderRepository.cs#L155) |
| M | `POST /api/orders/{id}/payments` | When balance reaches 0, sets `Status = FullyPaid` (enum 12), **overwriting the production status** (e.g. Stitching). Payment state and production state share one field. | [OrderService.cs:263](../Infrastructure/Services/OrderService.cs#L263) |
| M | `PUT /api/orders` | When `discount` is supplied, `GrandTotal = SubTotal − Discount`, discarding any `AdditionalCharge` previously added by `/alterations`; `BalanceDue` shrinks accordingly. | [OrderService.cs:210](../Infrastructure/Services/OrderService.cs#L210) vs [:314](../Infrastructure/Services/OrderService.cs#L314) |
| M | `POST /api/auth/refresh` | Stub: always `401 "Refresh token not yet implemented."`. Login still issues a random `refreshToken` that is never stored. | [AuthService.cs:82](../Infrastructure/Services/AuthService.cs#L82), [:158](../Infrastructure/Services/AuthService.cs#L158) |
| M | `PUT /api/shops/{id}/subscription` | SystemAdmin-only, but `_repo.GetByIdAsync` runs under the caller's tenant filter and SystemAdmin's `tenantId` is the seeded system tenant → `"Shop not found."` for every real shop. Controller returns it as **400** (bypasses 404 inference). Use `/api/admin/provision-subscription` instead. Same filter makes `GET /api/shops/{id}` useless for SystemAdmin. | [ShopService.cs:66](../Infrastructure/Services/ShopService.cs#L66), [ShopsController.cs:44](../API/Controllers/ShopsController.cs#L44), [ApplicationDbContext.cs:46](../Infrastructure/Data/ApplicationDbContext.cs#L46) |
| M | `POST /api/shops` (Owner) | New branch gets `BranchId = Id` but the owner's `User.ShopIds` is not updated (the admin route does this; this one doesn't), so `switch-branch` to it fails with "You do not have access to this branch." until `PUT /api/users/{id}/shops` is called. `OwnerId` is also left null. | [ShopService.cs:39](../Infrastructure/Services/ShopService.cs#L39) vs [AdminProvisioningService.cs:173](../Infrastructure/Services/AdminProvisioningService.cs#L173) |
| M | any `[FromQuery] DateTime` (`from`, `to`, `DateFrom`, `DateTo`, `From`, `To`) | Query-string dates bind with `Kind=Unspecified` (or Local with an offset) and are compared to `timestamptz` columns; the UTC JSON converters do not apply to query binding. Npgsql throws at execution → surfaces through `GlobalExceptionMiddleware` as 400 or 500 depending on exception type (not verified at runtime). Only the `UtcNow` defaults are safe. | [FinanceRepository.cs:24](../Repositories/Implementations/FinanceRepository.cs#L24), [KarigarService.cs:185](../Infrastructure/Services/KarigarService.cs#L185), [UtcDateTimeConverter.cs](../API/Json/UtcDateTimeConverter.cs) |
| M | `POST /api/orders/stages/log`, `/stages/complete` | Neither checks that the caller is the assigned (or named) karigar: any Karigar can log work under another karigar's id (`KarigarId` is caller-supplied) and complete any stage on any item in the branch. | [OrderService.cs:332](../Infrastructure/Services/OrderService.cs#L332), [:369](../Infrastructure/Services/OrderService.cs#L369) |
| M | `POST /api/orders` `attachments` | `CreateOrderViewModel.Attachments` is `List<IFormFile>?` inside a `[FromBody]` JSON model — System.Text.Json cannot bind it, so any client supplying it gets a 400 and multipart is impossible. `AttachmentService.AddAsync` is a placeholder that fabricates an `Attachment` object and persists nothing. | [OrderViewModels.cs:18](../Application/ViewModels/Order/OrderViewModels.cs#L18), [AttachmentService.cs:14](../Infrastructure/Services/AttachmentService.cs#L14) |
| L | `GET /WeatherForecast` | Anonymous template endpoint left in production build. | [WeatherForecastController.cs](../API/Controllers/WeatherForecastController.cs) |
| L | all | `ApiResponse(string error, int statusCode)` ignores `statusCode`; HTTP status is inferred from error text. `BaseCrudService` 500-class failures return 400. | [ApiResponse.cs:20](../Application/Common/ApiResponse.cs#L20) |
| L | `GET /api/shops/{id}` | `ShopDetailViewModel` declares `tenantId`, `branchId`, `status`, `trialEndsAt`, `subscriptionEndsAt`, `subscriptionPlanName`, `updatedOn` but the repository never populates them (always default). | [ShopRepository.cs:53](../Repositories/Implementations/ShopRepository.cs#L53) |

---

## 6. Drift vs existing docs

### 6.1 Documented but **not implemented**

| Source | Route | Reality |
|---|---|---|
| Guide §Measurements | `GET /api/measurements/customer/{customerId}` | Actual: `GET /api/measurements/profiles/customer/{customerId}` |
| Guide §Measurements | `PUT /api/measurements/profiles/{id}/values` | Does not exist; use `PUT /api/measurements/profiles/{id}` with full `SaveMeasurementProfileViewModel` |
| Guide §Finance | `GET /api/finance/cashflow` | Does not exist |
| Guide §Finance | `POST /api/finance/salary`, `GET /api/finance/salary/staff/{staffId}` | Actual: `POST /api/finance/salaries`, `GET /api/finance/salaries?staffId=` |
| Guide §Admin | `GET /api/admin/tenants`, `PUT /api/admin/tenants/{id}/subscription` | Actual: `GET /api/admin/shops`, `POST /api/admin/provision-subscription` / `subscription/{activate|suspend|extend}/{shopId}` |
| Guide §Errors | "401 → call `POST /api/auth/refresh`" | Refresh is a stub; clients must re-login |
| Guide §Attendance | `POST /api/attendance`, `PUT /api/attendance` restricted to Owner/Manager | Code allows any authenticated role |
| Guide §Expenses/Salaries | `/api/expenses`, `/api/salaries` as the primary write surface | Writes through these orphan rows (§4); yml correctly omits them |

### 6.2 Implemented but **undocumented** (absent from `api_endpoints.yml`)

| Route | Notes |
|---|---|
| `POST /api/auth/refresh` | Listed only in the guide (as if working) |
| `GET /api/roles` | Absent from both docs |
| `GET /api/karigar/my-completed` | In guide, absent from yml |
| `GET /api/attendance/my-attendance`, `POST /api/attendance/check-in`, `GET /api/attendance/summary`, generic `/api/attendance` CRUD (5) | In guide, absent from yml |
| `/api/expenses` CRUD (5), `/api/salaries` CRUD (5) | In guide, absent from yml |
| `DELETE /api/salaries/{id}` | Inherited from base; absent from both docs |
| `GET /WeatherForecast` | Absent from both |

### 6.3 Shape drift

- yml documents a single pagination shape; the API has two (§1, shape A vs B).
- yml/guide describe enums by name; the API emits and expects integers.
- Guide says `GET /api/orders/{id}` is available to SystemAdmin; the action-level role list excludes it.

---

*104 endpoints documented (Auth 5, Admin 9, Shops 6, Users 8, Roles 1, Customers 6, Measurements 14, Orders 15, Karigar 4, Finance 10, Expenses 5, Salaries 5, Attendance 8, Reports 7, WeatherForecast 1).*
