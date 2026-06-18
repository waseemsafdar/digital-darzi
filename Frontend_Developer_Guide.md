# Digital Darzi - Frontend Developer Guide

Welcome to the Digital Darzi Frontend Developer Guide! This document is designed to give you a complete understanding of the system's UX flows, role-based workflows, and the complete list of REST API endpoints.

---

## 1. System Overview & Architecture
**Digital Darzi** is a multi-tenant tailor shop management system. 
- **Authentication:** JWT Bearer Token based authentication. Send `Authorization: Bearer <token>` in headers.
- **Tenant Context:** Requests made by authenticated users are scoped to their specific Shop ID automatically via JWT.

---

## 2. Role-Based Operational Workflows

The system heavily relies on roles (SystemAdmin, Owner, Manager, Receptionist, Karigar). Here is how the frontend should handle the two most important flows:

### A. The Shop Owner / Manager Flow (Order Creation)
The Owner or Manager handles the customer-facing operations. When a customer walks into the shop, the frontend should guide the user through this sequence:

1. **Find or Create Customer:**
   - Call `GET /api/customers?search={phoneOrName}`.
   - If not found, open a form and call `POST /api/customers` to register them.
2. **Take Measurements:**
   - Once the customer is selected, open the Measurements UI.
   - Call `POST /api/measurements` with the `customerId`, the garment type (e.g., "Shalwar Kameez"), and a JSON string of their physical measurements.
3. **Create the Order & Assign Worker:**
   - Open the New Order form.
   - Call `POST /api/orders` linking the `customerId` and the newly created `measurementId`.
   - The Owner sets the `totalAmount`, `advancePaid`, and an `expectedDeliveryDate`.
   - **Crucial Step:** The Owner can optionally select a worker from `GET /api/karigar` and pass their `assignedKarigarId` in the order payload. The status defaults to `Pending`.

### B. The Karigar (Worker) Flow (Task Execution)
The Karigar is the tailor actually sewing the clothes. Their UI must be extremely simple and mobile-friendly.

1. **Daily Attendance:**
   - When the Karigar logs in, they see a big "Punch In" button.
   - Call `POST /api/attendance` to record their start time.
2. **Viewing Assigned Tasks:**
   - The Karigar navigates to their "My Tasks" screen.
   - Call `GET /api/orders` (the backend automatically filters this list so the Karigar *only* sees orders assigned to their specific `KarigarId`).
3. **Updating Order Status:**
   - The Karigar clicks on an order to see the measurements (fetched via `GET /api/measurements/{id}`).
   - Once they start cutting/sewing, they click a status button. The frontend calls `PUT /api/orders/{id}/status` with `{"status": "Stitching"}`.
   - Once finished, they click again to call `PUT /api/orders/{id}/status` with `{"status": "ReadyForDelivery"}`.

---

## 3. Complete API Endpoints Reference

### Base URL
`https://{your-server-domain}/api`

---

### A. Authentication & Identity (`/api/auth`)
- **POST `/api/auth/register`**: Registers a new shop + owner.
- **POST `/api/auth/login`**: Authenticate and get JWT token (Accepts email/password or phone/PIN).
- **POST `/api/auth/refresh`**: Refresh an expired access token using the refresh token.
- **POST `/api/auth/change-password`**: Change user password.
- **POST `/api/auth/change-pin`**: Change user PIN.

---

### B. Shops (`/api/shops`)
*Managed primarily by System Admins.*
- **GET `/api/shops`**: Get a paginated list of all shops.
- **GET `/api/shops/{id}`**: Get details of a specific shop.
- **POST `/api/shops`**: Create a new shop.
- **PUT `/api/shops`**: Update an existing shop.
- **DELETE `/api/shops/{id}`**: Delete a shop.

---

### C. Users / Staff (`/api/users`)
*Manage staff accounts (Managers, Receptionists) under a shop.*
- **GET `/api/users`**: Get a paginated list of users for the shop.
- **GET `/api/users/{id}`**: Get details of a specific user.
- **POST `/api/users`**: Create a new user.
- **PUT `/api/users`**: Update a user.
- **DELETE `/api/users/{id}`**: Delete a user.

---

### D. Customers (`/api/customers`)
*Manage shop customers.*
- **GET `/api/customers`**: Get a paginated list of customers.
- **GET `/api/customers/{id}`**: Get details of a specific customer.
- **POST `/api/customers`**: Add a new customer.
- **PUT `/api/customers`**: Update customer details.
- **DELETE `/api/customers/{id}`**: Delete a customer.

---

### E. Measurements (`/api/measurements`)
*Store tailoring measurements for specific customers.*
- **GET `/api/measurements`**: Get a paginated list of measurements.
- **GET `/api/measurements/{id}`**: Get details of a specific measurement record.
- **POST `/api/measurements`**: Add a new measurement.
- **PUT `/api/measurements`**: Update existing measurement.
- **DELETE `/api/measurements/{id}`**: Delete a measurement record.

---

### F. Orders (`/api/orders`)
*Track customer orders, billing, and worker assignment.*
- **GET `/api/orders`**: Get a paginated list of orders.
- **GET `/api/orders/{id}`**: Get details of a specific order.
- **POST `/api/orders`**: Create a new order (Include `assignedKarigarId` to assign it).
- **PUT `/api/orders`**: Update order details.
- **DELETE `/api/orders/{id}`**: Delete an order.
- **PUT `/api/orders/{id}/status`**: Update the status of an order (e.g., Pending, Stitching, ReadyForDelivery).

---

### G. Karigar / Workers (`/api/karigar`)
*Manage the worker profiles who sew the garments.*
- **GET `/api/karigar`**: Get a paginated list of workers.
- **GET `/api/karigar/{id}`**: Get details of a specific worker.
- **POST `/api/karigar`**: Add a new worker.
- **PUT `/api/karigar`**: Update a worker profile.
- **DELETE `/api/karigar/{id}`**: Delete a worker.

---

### H. Expenses (`/api/expenses`)
*Record daily shop expenses.*
- **GET `/api/expenses`**: Get a paginated list of expenses.
- **GET `/api/expenses/{id}`**: Get a specific expense record.
- **POST `/api/expenses`**: Add a new expense (e.g., electricity bill).
- **PUT `/api/expenses`**: Update an expense.
- **DELETE `/api/expenses/{id}`**: Delete an expense.

---

### I. Salaries (`/api/salaries`)
*Record salary payouts to workers and staff.*
- **GET `/api/salaries`**: Get a paginated list of salary payouts.
- **GET `/api/salaries/{id}`**: Get a specific salary record.
- **POST `/api/salaries`**: Record a new salary payout.
- **PUT `/api/salaries`**: Update a salary record.
- **DELETE `/api/salaries/{id}`**: Delete a salary record.

---

### J. Attendance (`/api/attendance`)
*Track daily check-in/check-out for staff and workers.*
- **GET `/api/attendance`**: Get a paginated list of attendance records.
- **GET `/api/attendance/{id}`**: Get a specific attendance record.
- **POST `/api/attendance`**: Mark attendance (Punch in/out).
- **PUT `/api/attendance`**: Update an attendance record.
- **DELETE `/api/attendance/{id}`**: Delete an attendance record.

---

### K. Finance & Analytics (Read-Only)
*High-level reporting endpoints.*
- **GET `/api/finance/summary`**: Returns total revenue, expenses, balances, and net profit.
- **GET `/api/finance/cashflow`**: Returns breakdown of cash in vs. cash out.
- **GET `/api/reports/dashboard`**: High-level dashboard metrics (Active Orders, Revenue, Top workers).

---

## 4. Error Handling
- **401 Unauthorized:** Call `/api/auth/refresh` to get a new token. If it fails, redirect to Login.
- **400 Bad Request:** Indicates form validation failure. Map the `errors` array in the response to your UI inputs.
- **403 Forbidden:** The authenticated user does not have the required Role to access this endpoint.
