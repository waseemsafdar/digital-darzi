# Digital Darzi — Frontend Developer Guide

> **Last updated:** 2026-06-26  
> Covers all endpoints currently implemented in the backend. Keep this in sync whenever the API changes.

---

## 1. System Overview

**Digital Darzi** is a multi-tenant tailor shop management system.

| Concept | Details |
|---------|---------|
| **Auth** | JWT Bearer — send `Authorization: Bearer <token>` on every request |
| **Multi-branch** | The `shopId` claim in the JWT determines the active branch. All operational data is automatically scoped to that branch. |
| **Branch switching** | Call `POST /api/auth/switch-branch` to move to a different branch — returns a new token. |
| **Tenant isolation** | The `tenantId` claim in the JWT is set at login. Never send it manually. |

### Standard Response Envelope

Every endpoint returns this shape:

```json
{
  "success": true,
  "message": "Optional message",
  "data": { ... },
  "errors": []
}
```

- `success: false` + non-2xx status → show `errors[]` to the user.
- `data` is `null` on error responses.

---

## 2. Roles & Permissions

| Role | Who | Capabilities |
|------|-----|-------------|
| `SystemAdmin` | Platform team | Admin provisioning only — crosses all tenants |
| `Owner` | Shop owner | Full access to everything in their tenant |
| `Manager` | Senior staff | Most operations; cannot delete users or manage billing |
| `Receptionist` | Front desk | Customers, orders (read + deliver), measurements |
| `Karigar` | Tailor / worker | Own task queue, stage logging, own attendance |

> **Note for UI:** Read the `roles` array from the login response to gate UI screens. Always check server responses for 403 — do not rely solely on frontend role logic.

---

## 3. Role-Based Workflows

### A. Owner / Manager — New Order Flow

```
1. Search or Create Customer
   GET /api/customers?Query=<phone-or-name>
   POST /api/customers   (if new)

2. (Optional) Add/Select Measurements
   GET /api/measurements/customer/<customerId>   — pick saved profile
   POST /api/measurements/profiles               — or create new

3. Create Order
   POST /api/orders
   - include customerId, deliveryDate, items[]
   - each item can include measurementProfileId or inlineMeasurements
   - each item can include stageAssignments[] to pre-assign karigar

4. Record Advance Payment
   Already part of POST /api/orders (advancePayment + paymentMethod fields)
   Later payments: POST /api/orders/{id}/payments

5. Monitor Progress
   GET /api/orders/{id}                  — full detail + stage progress per item
   GET /api/karigar/performance          — all karigar summary
```

### B. Karigar — Daily Workflow

```
1. Check In
   POST /api/attendance/check-in        — creates today's Present record with CheckIn = now
   (call again later with checkOut field to punch out)

2. View Assigned Tasks
   GET /api/karigar/my-queue            — pending + in-progress stages

3. Open Order Detail
   GET /api/orders/{id}                 — see garment details, fabric, measurements, style notes

4. Start Working a Stage
   POST /api/orders/stages/log          — records start of work on a stage

5. Complete a Stage
   POST /api/orders/stages/complete     — marks the stage done

6. View Own History & Earnings
   GET /api/karigar/my-completed        — completed stages + total earned amount
   GET /api/attendance/my-attendance    — own attendance records (defaults to current month)
```

### C. Receptionist — Delivery Flow

```
1. Search orders due today
   GET /api/orders?DueToday=true

2. Collect final payment
   POST /api/orders/{id}/payments

3. Mark order delivered
   POST /api/orders/{id}/deliver
```

### D. Owner — Finance & Reporting

```
GET /api/finance/summary       — revenue, expenses, net profit
GET /api/finance/cashflow      — cash in vs out breakdown
GET /api/reports/dashboard     — high-level KPIs
GET /api/karigar/performance   — karigar productivity
GET /api/attendance/summary?staffId=&month=&year=   — staff attendance summary
```

---

## 4. API Endpoint Reference

### Base URL
```
https://{your-server}/api
```

---

### Authentication (`/api/auth`)

| Method | Endpoint | Roles | Description |
|--------|----------|-------|-------------|
| POST | `/api/auth/login` | Public | Email+password or phone+PIN login |
| POST | `/api/auth/refresh` | Public | Refresh expired access token |
| POST | `/api/auth/switch-branch` | Any | Switch active branch; returns new JWT |
| POST | `/api/auth/change-password` | Any | Change own password |
| POST | `/api/auth/change-pin` | Any | Change own PIN |

**Login request body:**
```json
{ "email": "owner@shop.com", "password": "P@ssw0rd" }
```
or
```json
{ "phone": "03001234567", "passcode": "1234" }
```

**Login response `data`:**
```json
{
  "accessToken": "eyJ...",
  "refreshToken": "...",
  "expiresIn": 3600,
  "user": {
    "id": "guid",
    "name": "Ahmed",
    "roles": ["Owner"],
    "shopId": "guid",
    "tenantId": "guid"
  }
}
```

---

### Shops (`/api/shops`)

| Method | Endpoint | Roles | Description |
|--------|----------|-------|-------------|
| GET | `/api/shops` | Owner, SystemAdmin | List all branches in tenant |
| GET | `/api/shops/{id}` | Owner, Manager, SystemAdmin | Branch detail |
| POST | `/api/shops` | Owner, SystemAdmin | Create a new branch |
| PUT | `/api/shops` | Owner, SystemAdmin | Update branch info |

---

### Users / Staff (`/api/users`)

| Method | Endpoint | Roles | Description |
|--------|----------|-------|-------------|
| GET | `/api/users` | Owner, Manager | Paginated list; filter `?shopId=` |
| GET | `/api/users/{id}` | Owner, Manager | Staff detail including role names |
| POST | `/api/users` | **Owner** | Create staff account |
| PUT | `/api/users` | Owner, Manager | Update name, phone, status |
| DELETE | `/api/users/{id}` | **Owner** | Soft-delete (locks login, removes all roles) |
| POST | `/api/users/{id}/deactivate` | **Owner** | Deactivate without full delete |
| PUT | `/api/users/{id}/shops` | **Owner** | Assign branches to user |
| PUT | `/api/users/{id}/roles` | **Owner** | Assign roles to user |

> **Important:** `DELETE /api/users/{id}` will return 400 if you try to delete your own account or an Owner/SystemAdmin account.

**Create user body:**
```json
{
  "name": "Rizwan",
  "email": "rizwan@shop.com",
  "password": "P@ss123",
  "phone": "03009876543",
  "passcode": "5678",
  "roleIds": ["<manager-role-guid>"],
  "shopIds": ["<shop-guid>"]
}
```

---

### Customers (`/api/customers`)

| Method | Endpoint | Roles | Description |
|--------|----------|-------|-------------|
| GET | `/api/customers` | Owner, Manager, Receptionist | Paginated list; filter `?Query=`, `?City=`, `?Gender=` |
| GET | `/api/customers/{id}` | Owner, Manager, Receptionist | Customer detail |
| GET | `/api/customers/{id}/ledger` | Owner, Manager | Full payment ledger |
| POST | `/api/customers` | Owner, Manager, Receptionist | Create customer |
| PUT | `/api/customers` | Owner, Manager, Receptionist | Update customer info |
| DELETE | `/api/customers/{id}` | Owner, Manager | Soft-delete customer |

---

### Measurements (`/api/measurements`)

| Method | Endpoint | Roles | Description |
|--------|----------|-------|-------------|
| GET | `/api/measurements/fields` | Owner, Manager | List measurement field definitions |
| POST | `/api/measurements/fields` | Owner, Manager | Create a field (e.g., "Chest") |
| GET | `/api/measurements/templates` | Owner, Manager | List templates |
| POST | `/api/measurements/templates` | Owner, Manager | Create a template |
| GET | `/api/measurements/profiles` | Owner, Manager | List saved customer profiles |
| GET | `/api/measurements/profiles/{id}` | Owner, Manager | Profile detail |
| GET | `/api/measurements/customer/{customerId}` | Owner, Manager | All profiles for a customer |
| POST | `/api/measurements/profiles` | Owner, Manager | Save a new measurement profile |
| PUT | `/api/measurements/profiles/{id}/values` | Owner, Manager | Update profile values |

---

### Orders (`/api/orders`)

| Method | Endpoint | Roles | Description |
|--------|----------|-------|-------------|
| GET | `/api/orders` | Owner, Manager, Receptionist | Paginated search; filter `?Query=`, `?Status=`, `?DateFrom=`, `?DateTo=`, `?DueToday=true` |
| GET | `/api/orders/{id}` | **Owner, Manager, Karigar, Receptionist** | Full detail: items, stage progress, payments, alterations |
| GET | `/api/orders/customer/{customerId}` | Owner, Manager | All orders for a customer |
| GET | `/api/orders/due-today` | Owner, Manager, Receptionist | Orders due for delivery today |
| GET | `/api/orders/overdue` | Owner, Manager | Orders past their delivery date |
| POST | `/api/orders` | Owner, Manager | Create a new order |
| PUT | `/api/orders` | Owner, Manager | Update order (delivery date, discount, notes) |
| PATCH | `/api/orders/{id}/status` | Owner, Manager | Set order status directly |
| POST | `/api/orders/{id}/payments` | Owner, Manager, Receptionist | Record a payment |
| POST | `/api/orders/{id}/cancel` | Owner, Manager | Cancel order |
| POST | `/api/orders/{id}/deliver` | Owner, Manager, Receptionist | Mark as delivered |
| POST | `/api/orders/alterations` | Owner, Manager | Add an alteration to an order item |
| POST | `/api/orders/stages/assign` | Owner, Manager | Assign a karigar to a production stage |
| POST | `/api/orders/stages/log` | **Owner, Manager, Karigar** | Record stage start or progress note |
| POST | `/api/orders/stages/complete` | **Owner, Manager, Karigar** | Mark a production stage complete |

**Create order body:**
```json
{
  "customerId": "guid",
  "deliveryDate": "2026-07-10T00:00:00Z",
  "notes": "Rush job",
  "specialInstructions": "No starch on collar",
  "discount": 500,
  "advancePayment": 2000,
  "paymentMethod": "Cash",
  "items": [
    {
      "garmentType": "ShalwarKameez",
      "measurementProfileId": "guid",
      "fabricDescription": "Lawn",
      "fabricColor": "Blue",
      "styleNotes": "Collar with piping",
      "price": 3500,
      "qty": 1,
      "stageAssignments": [
        {
          "stage": "Cutting",
          "assignedKarigarId": "guid",
          "stagePrice": 200,
          "estimatedDays": 1
        }
      ]
    }
  ]
}
```

**Record payment body:**
```json
{ "amount": 1500, "paymentMethod": "Cash", "note": "Balance payment" }
```

**Stage log body:**
```json
{ "orderItemId": "guid", "stage": "Stitching", "karigarId": "guid", "notes": "Started cutting" }
```

**Complete stage body:**
```json
{ "orderItemId": "guid", "stage": "Stitching", "notes": "All done" }
```

**Assign stage body:**
```json
{ "orderItemId": "guid", "stage": "Finishing", "assignedKarigarId": "guid", "stagePrice": 150, "estimatedDays": 1 }
```

---

### Karigar Dashboard (`/api/karigar`)

| Method | Endpoint | Roles | Description |
|--------|----------|-------|-------------|
| GET | `/api/karigar/my-queue` | **Any** | Logged-in karigar's pending + in-progress tasks |
| GET | `/api/karigar/my-completed` | **Any** | Own completed stage history + total earnings; filter `?from=&to=` |
| GET | `/api/karigar/{karigarId}/queue` | Owner, Manager, SystemAdmin | Work queue for a specific karigar |
| GET | `/api/karigar/performance` | Owner, Manager, SystemAdmin | All karigars: assigned, completed, earnings breakdown |

**`my-queue` response `data`:**
```json
{
  "karigarId": "guid",
  "karigarName": "Rashid",
  "totalPending": 3,
  "totalInProgress": 1,
  "totalCompletedToday": 2,
  "pendingTasks": [
    {
      "orderItemId": "guid",
      "orderId": "guid",
      "orderNumber": "ORD-260626-A1B2",
      "customerName": "Sara",
      "garmentType": "ShalwarKameez",
      "stage": "Stitching",
      "orderDeliveryDate": "2026-06-28T00:00:00Z",
      "isUrgent": true,
      "stagePrice": 250,
      "fabricColor": "Green",
      "styleNotes": "Collar with piping",
      "assignedOn": "2026-06-26T08:00:00Z"
    }
  ],
  "inProgressTasks": [ "..." ]
}
```

**`my-completed` response `data`:**
```json
{
  "karigarId": "guid",
  "karigarName": "Rashid",
  "totalCompleted": 12,
  "totalEarnings": 3600.00,
  "completedStages": [
    {
      "orderItemId": "guid",
      "orderId": "guid",
      "orderNumber": "ORD-260620-X1Y2",
      "customerName": "Fatima",
      "garmentType": "Kurti",
      "stage": "Cutting",
      "stagePrice": 200.00,
      "startedAt": "2026-06-20T09:00:00Z",
      "completedAt": "2026-06-20T11:30:00Z",
      "durationMinutes": 150
    }
  ]
}
```

---

### Attendance (`/api/attendance`)

| Method | Endpoint | Roles | Description |
|--------|----------|-------|-------------|
| GET | `/api/attendance/my-attendance` | **Any** | Own attendance; defaults to current month; filter `?month=&year=` or `?from=&to=` |
| POST | `/api/attendance/check-in` | **Any** | Self check-in — creates today's Present record. Call again with `checkOut` to punch out. |
| GET | `/api/attendance/summary` | Owner, Manager | Monthly summary for a staff member (`?staffId=&month=&year=`) |
| GET | `/api/attendance` | Owner, Manager | Paginated full list; filter `?StaffId=&From=&To=` |
| GET | `/api/attendance/{id}` | Owner, Manager | Single record |
| POST | `/api/attendance` | Owner, Manager | Manually record attendance for any staff |
| PUT | `/api/attendance` | Owner, Manager | Update an attendance record |

**`check-in` body:**
```json
{
  "checkOut": null,
  "notes": "On time"
}
```
> Send `"checkOut": "17:30:00"` on the second call to record end of shift.

**`my-attendance` response `data`:**
```json
[
  {
    "id": "guid",
    "staffUserId": "guid",
    "staffName": "Rashid",
    "date": "2026-06-26T00:00:00Z",
    "status": "Present",
    "checkIn": "09:05:00",
    "checkOut": "17:30:00",
    "notes": null
  }
]
```

---

### Finance (`/api/finance`)

| Method | Endpoint | Roles | Description |
|--------|----------|-------|-------------|
| GET | `/api/finance/summary` | Owner, Manager | Revenue, expenses, balance due, net profit for active period |
| GET | `/api/finance/cashflow` | Owner, Manager | Cash inflow vs outflow breakdown |
| GET | `/api/finance/salary/staff/{staffId}` | Owner, Manager | Salary history for a staff member |
| POST | `/api/finance/salary` | **Owner** | Record a salary payout |

---

### Expenses (`/api/expenses`)

| Method | Endpoint | Roles | Description |
|--------|----------|-------|-------------|
| GET | `/api/expenses` | Owner, Manager | Paginated list; filter `?From=&To=&Category=` |
| GET | `/api/expenses/{id}` | Owner, Manager | Expense detail |
| POST | `/api/expenses` | Owner, Manager | Add a new expense |
| PUT | `/api/expenses` | Owner, Manager | Update expense |
| DELETE | `/api/expenses/{id}` | **Owner** | Delete expense |

---

### Salaries (`/api/salaries`)

| Method | Endpoint | Roles | Description |
|--------|----------|-------|-------------|
| GET | `/api/salaries` | Owner, Manager | Paginated list; filter `?Month=&Year=&StaffId=` |
| GET | `/api/salaries/{id}` | Owner, Manager | Single salary record |
| POST | `/api/salaries` | Owner, Manager | Record a salary payout |
| PUT | `/api/salaries` | Owner, Manager | Update salary record |

---

### Reports (`/api/reports`)

| Method | Endpoint | Roles | Description |
|--------|----------|-------|-------------|
| GET | `/api/reports/dashboard` | Owner, Manager, SystemAdmin | KPIs: active orders, revenue this month, top karigars, overdue count |

---

### Admin Provisioning (`/api/admin`)
*SystemAdmin only — used by platform team.*

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/admin/provision-owner` | Create new tenant + owner account |
| GET | `/api/admin/tenants` | List all tenants |
| PUT | `/api/admin/tenants/{id}/subscription` | Update subscription plan |

---

## 5. Pagination

Paginated endpoints accept `?PageNumber=1&PageSize=20` query params.

Response `data` for paginated endpoints:
```json
{
  "items": [ "..." ],
  "totalCount": 120,
  "pageNumber": 1,
  "pageSize": 20,
  "totalPages": 6
}
```

---

## 6. Error Handling

| HTTP Status | Meaning | Recommended Frontend Action |
|-------------|---------|----------------------------|
| `401` | Token expired or missing | Call `POST /api/auth/refresh`; if fails → redirect to Login |
| `403` | Role insufficient | Show "You do not have permission" message; hide the button next time |
| `404` | Record not found | Show "Not found" message |
| `400` | Validation / business rule failure | Map `errors[]` array to form field errors |
| `500` | Server error | Show generic error toast; log `errors[]` to dev console |

---

## 7. Enums Reference

Use these exact string values when sending enum fields in request bodies.

| Enum | Values |
|------|--------|
| `OrderStatus` | `Pending` · `InProgress` · `FullyPaid` · `Delivered` · `Cancelled` |
| `OrderItemStatus` | `Pending` · `InProgress` · `Done` · `Delivered` · `Cancelled` |
| `ProductionStage` | `Cutting` · `Stitching` · `Finishing` · `Embroidery` · `QualityCheck` · `Packing` |
| `GarmentType` | `ShalwarKameez` · `Kurti` · `Suit` · `Pants` · `Shirt` · `Other` |
| `PaymentMethod` | `Cash` · `Card` · `BankTransfer` · `JazzCash` · `EasyPaisa` |
| `AttendanceStatus` | `Present` · `Absent` · `HalfDay` · `Leave` |
| `Gender` | `Male` · `Female` · `Other` |
| `ActiveStatus` | `Active` · `Inactive` |

---

## 8. Changelog

### 2026-06-26
| Type | Change |
|------|--------|
| **Bug fix** | `DELETE /api/customers/{id}` was silently doing nothing — now correctly soft-deletes |
| **Bug fix** | `POST /api/orders/{id}/payments`, `POST /api/orders/alterations`, `POST /api/orders/stages/*` were returning 500 — fixed |
| **New endpoint** | `DELETE /api/users/{id}` — Owner can delete staff accounts (with safeguards) |
| **Role change** | `GET /api/orders/{id}` now accessible to Karigar + Receptionist roles |
| **New endpoint** | `GET /api/karigar/my-completed` — Karigar views own completed stage history + earnings |
| **New endpoint** | `GET /api/attendance/my-attendance` — Any staff views their own attendance history |
| **New endpoint** | `POST /api/attendance/check-in` — Self-service punch in/out for any staff |
| **Code quality** | `Karigar` and `Receptionist` are now first-class role constants (were magic strings) |
