# Digital Darzi — Business Workflows

Phases 2 and 3: how the platform admin, the shop owner and shop staff work, mapped to the API
that exists today. Every endpoint named here is real; anything the backend cannot do is marked
**GAP** and listed in [API_MAPPING.md §3](API_MAPPING.md#3-missing-or-insufficient-apis).

Companions: [SYSTEM_ANALYSIS.md](SYSTEM_ANALYSIS.md), [ROLE_PERMISSIONS.md](ROLE_PERMISSIONS.md).

---

## 1. Super Admin workflow

```mermaid
flowchart TD
    L["Admin login<br/>POST /api/auth/login"] --> D["Admin dashboard<br/>GAP: no platform stats API<br/>build from /admin/shops + /admin/invoices"]
    D --> SM["Shop management<br/>GET /api/admin/shops"]
    D --> PL["Plans<br/>GET/POST/PUT /api/admin/plans"]
    D --> INV["Invoices<br/>GET/POST /api/admin/invoices"]

    SM --> NEW["Create shop"]
    NEW --> O1["1. Create owner<br/>POST /api/admin/provision-owner<br/>returns userId + tenantId"]
    O1 --> O2["2. Create first branch<br/>POST /api/admin/provision-shop<br/>clones fields, templates, attributes, expense categories"]
    O2 --> O3["3. Set subscription<br/>POST /api/admin/provision-subscription<br/>Trial with trialDays, or Active with end date"]
    O3 --> O4["4. Assign plan<br/>PUT /api/admin/shops/{shopId}/plan"]
    O4 --> ACT["Shop live<br/>owner can sign in"]

    ACT --> MON["Monitor"]
    MON --> SUS["Suspend<br/>POST /admin/subscription/suspend/{shopId}<br/>tenant gets 402"]
    MON --> REA["Re-activate<br/>POST /admin/subscription/activate/{shopId}"]
    MON --> BILL["Renew: invoice N months<br/>POST /admin/invoices"]
    BILL --> PAID["Mark paid<br/>POST /admin/invoices/{id}/paid<br/>shop Active, end date = period end"]
    MON --> EXT["Goodwill extension<br/>POST /admin/subscription/extend/{shopId} days"]
    MON --> USG["Usage and activity<br/>GAP"]
```

What happens behind each step:

| Step | Server behaviour |
|---|---|
| Provision owner | Creates the Identity account (email + password or phone + PIN), a domain user with role Owner, and a new tenant id. Rejects duplicate email or phone. |
| Provision shop | Creates the branch under that tenant, links it to the owner's `ShopIds`, and seeds the tenant's measurement catalogue, seven templates, garment attributes and expense categories. |
| Provision subscription | Sets `Status`, `TrialEndsAt` (from `trialDays`) or `SubscriptionEndsAt`, and plan name/id on the shop. |
| Assign plan | Links a plan. Its `MaxBranches`, `MaxUsers`, `MaxOrdersPerMonth` are enforced when the tenant creates branches, users or orders. |
| Suspend / cancel / expiry | `SubscriptionMiddleware` returns 402 on every tenant route for that tenant. The owner can still sign in and open billing. |
| Extend | Pushes `SubscriptionEndsAt` (or trial end) forward by N days. |
| Invoices | Admin creates an invoice for N months (`POST /api/admin/invoices` with `shopId`, `months`, optional `amount`). The period starts at the current subscription end, or today if already expired, so renewals chain. Marking it paid (`POST /admin/invoices/{id}/paid` with method and reference) **sets the shop Active and moves `SubscriptionEndsAt` to the invoice period end**. This is the normal renewal path; extend-by-days is for goodwill or corrections. |

Not supported today: **GAP** platform statistics, **GAP** per-tenant usage and activity,
**GAP** platform settings, **GAP** plan-based feature flags, **GAP** editing a tenant's shop
profile from the admin side, **GAP** subscription status and plan in the admin shop list
(the list returns name, city, phone, active flag, created date only).

---

## 2. Shop Owner workflow

```mermaid
flowchart TD
    L["Login<br/>POST /api/auth/login"] --> ME["Load session<br/>GET /api/auth/me<br/>permissions, branches, subscription"]
    ME --> SUB{"Subscription OK?"}
    SUB -- 402 / expiring --> BILL["Billing page<br/>GET /api/billing/*"]
    SUB -- ok --> DASH["Dashboard<br/>GET /api/reports/dashboard<br/>GET /api/orders/board"]

    DASH --> SET["Shop setup, once"]
    SET --> S1["Shop profile<br/>PUT /api/shops"]
    SET --> S2["Staff<br/>POST /api/users, PUT /users/{id}/roles, /shops"]
    SET --> S3["Permissions<br/>PUT /api/permissions/{role}"]
    SET --> S4["Measurement fields<br/>/api/measurements/fields"]
    S4 --> S5["Templates per garment<br/>/api/measurements/templates"]
    S5 --> S6["Garment attributes<br/>/api/garment-attributes"]
    SET --> S7["Karigar piece rates<br/>PUT /api/karigar/rates"]

    DASH --> CUST["Customers<br/>GET/POST /api/customers"]
    CUST --> PROF["Measurement profiles<br/>POST /api/measurements/profiles"]
    CUST --> NEWO["New order<br/>see §4"]
    PROF --> NEWO

    NEWO --> OD["Order detail<br/>GET /api/orders/{id}"]
    OD --> ASG["Assign karigars<br/>POST /api/orders/stages/assign"]
    ASG --> PROD["Production progresses<br/>karigars start / complete stages<br/>order status advances automatically"]
    PROD --> TRIAL{"Trial required?"}
    TRIAL -- yes --> TR["Trial result<br/>POST /orders/{id}/trial-result"]
    TR -- alteration --> ALT["Alteration<br/>PATCH /orders/alterations/{id}"]
    ALT --> PROD
    TR -- approved --> READY
    TRIAL -- no --> READY["Ready to deliver"]
    READY --> PAY["Collect balance<br/>POST /orders/{id}/payments"]
    PAY --> DEL["Deliver<br/>POST /orders/{id}/deliver"]
    DEL --> HIST["History, receipt<br/>GET /orders/{id}/history, /receipt"]

    DASH --> FIN["Expenses, payroll, cash<br/>/api/finance/*, /api/cash/*"]
    DASH --> REP["Reports<br/>/api/reports/*"]
    DASH --> KP["Karigar pay<br/>/api/karigar/balances, /{id}/ledger"]
```

### Order lifecycle (enforced by the server)

```mermaid
stateDiagram-v2
    [*] --> Received
    Received --> Cutting
    Received --> Stitching
    Cutting --> Stitching
    Cutting --> Embroidery
    Cutting --> Finishing
    Stitching --> Embroidery
    Stitching --> Finishing
    Stitching --> TrialReady
    Stitching --> ReadyToDeliver
    Embroidery --> Finishing
    Embroidery --> TrialReady
    Embroidery --> ReadyToDeliver
    Finishing --> TrialReady
    Finishing --> ReadyToDeliver
    TrialReady --> TrialDone
    TrialDone --> ReadyToDeliver
    ReadyToDeliver --> PartiallyDelivered
    ReadyToDeliver --> Delivered
    PartiallyDelivered --> Delivered
    Delivered --> [*]
    Cancelled --> [*]
```

Forward moves only. Also allowed, with a mandatory note because they go backwards:
Stitching → Cutting, Embroidery → Stitching, Finishing → Stitching, TrialDone → Stitching or
Finishing, ReadyToDeliver → Stitching or Finishing. **Cancelled** is reachable from every state
except PartiallyDelivered and Delivered.

The authoritative list for any order is
`allowedNextStatuses` on `GET /api/orders/{id}`. Backward moves need a note; supervisors can
force any move from a non-terminal state with a note. Payment state (`Unpaid`, `Partial`, `Paid`,
`Refunded`) is separate and never changes production status.

---

## 3. Staff workflows

Staff is not one role. The backend has three staff roles with different defaults.

```mermaid
flowchart LR
    subgraph Receptionist
        R1["Find or add customer"] --> R2["Take / update measurements"]
        R2 --> R3["Record payment"]
        R3 --> R4["Delivery board → deliver"]
        R4 --> R5["Cash drawer open / close"]
    end
    subgraph Manager
        M1["Create orders"] --> M2["Assign karigars"]
        M2 --> M3["Production board"]
        M3 --> M4["QC reject / approve"]
        M4 --> M5["Trial and alterations"]
        M5 --> M6["Expenses, karigar payouts, reports"]
    end
    subgraph Karigar
        K1["My queue<br/>GET /api/karigar/my-queue"] --> K2["Start stage<br/>POST /orders/stages/log"]
        K2 --> K3["Complete stage<br/>POST /orders/stages/complete"]
        K3 --> K4["My earnings<br/>GET /api/karigar/my-ledger"]
        K1 --> K5["Check in<br/>POST /api/attendance/check-in"]
    end
```

| Task | Owner | Manager | Receptionist | Karigar |
|---|---|---|---|---|
| View customers | yes | yes | yes | no |
| Create orders | yes | yes | **no by default** | no |
| Update measurements | yes | yes | yes | no |
| Update order status | yes | yes | no | indirectly, by completing stages |
| Stitching progress | yes | yes, all karigars | no | own assignments only |
| Deliveries | yes | yes | yes, not with balance owed | no |
| View / record payments | yes | yes | record yes, refund no | no |
| Change staff permissions | yes | no | no | no |

Full matrix in [ROLE_PERMISSIONS.md](ROLE_PERMISSIONS.md).

---

## 4. Measurement and order creation

### 4.1 How the pieces relate

```mermaid
flowchart LR
    GT["Garment type<br/>fixed enum"] -.optional link.-> T
    F["Measurement fields<br/>shop vocabulary"] --> TF["Template fields<br/>required + order"]
    T["Template<br/>e.g. Gents Shalwar Kameez"] --> TF
    T --> P["Customer profile<br/>values per field"]
    C["Customer"] --> P
    P --> V["Profile versions<br/>every save"]
    P -- snapshot copied --> OI["Order item"]
    T -- or inline values --> OI
    GT --> OI
    OI --> O["Order"]
    C --> O
    GA["Garment attributes<br/>collar, cuff..."] -- chosen values --> OI
```

- A **garment type** says what is being made. It is fixed in code.
- A **template** says which measurements are taken for it. Several templates may share a garment
  type (Gents vs Ladies). Owners manage templates.
- A **profile** holds one customer's values for one template, with full version history.
- An **order item** carries a frozen **snapshot** of the measurements used, so later profile edits
  never change a past order.

### 4.2 New order flow

```mermaid
flowchart TD
    A["Step 1: Customer"] --> A1{"Existing?"}
    A1 -- search --> A2["GET /api/customers?query="]
    A1 -- new --> A3["Quick add<br/>POST /api/customers"]
    A2 --> B
    A3 --> B

    B["Step 2: Add garment item<br/>choose garment type"] --> B1["Load templates for that type<br/>GET /api/measurements/templates<br/>filter by garmentType"]
    B1 --> B2["Load customer's profiles<br/>GET /measurements/profiles/customer/{id}<br/>filter to those templates"]

    B2 --> C{"Step 3: Measurements"}
    C -- use saved profile --> C1["Show values read-only<br/>GET /measurements/profiles/{id}"]
    C1 --> C1b{"Changes needed?"}
    C1b -- no --> C4["item.measurementProfileId = profile.id"]
    C1b -- yes --> C2["Edit → PUT /measurements/profiles/{id}<br/>creates new version"]
    C2 --> C4
    C -- new for customer --> C3["Dynamic form from template fields<br/>GET /measurements/templates/{id}"]
    C3 --> C3a{"Save to customer?"}
    C3a -- yes, default --> C3b["POST /api/measurements/profiles<br/>server checks required fields"]
    C3b --> C4
    C3a -- one-off --> C5["item.templateId + inlineMeasurements<br/>client must check required fields"]
    C -- none needed --> C6["No measurement source"]

    C4 --> D
    C5 --> D
    C6 --> D
    D["Step 4: Fabric and style<br/>fabricSource, description, colour, charge<br/>attributes from /api/garment-attributes?garmentType=<br/>style notes"] --> D1{"Another item?"}
    D1 -- yes --> B
    D1 -- no --> E

    E["Step 5: Pricing and payment<br/>price × qty + fabric per item, discount<br/>advance + method; balance shown live"] --> F
    F["Step 6: Dates and confirm<br/>delivery date, priority, trial date<br/>optional karigar per stage"] --> G["POST /api/orders"]
    G --> H["Order detail + receipt<br/>GET /orders/{id}/receipt?format=html"]
```

Rules the UI must follow:

1. Show only the fields of the chosen template, in template order, marking required fields.
2. Show only profiles whose template matches the item's garment type. The current app does not
   do this.
3. Default to **saving new measurements to the customer**. Inline one-off values are for
   exceptional cases, because the server does not keep them on the customer or validate them.
4. Multiple garments per order are fully supported (up to 50 items). Each item has its own
   measurement source.
5. Totals are computed by the server; the client preview must use the same formula:
   `subTotal = Σ(price × qty + fabricCharge)`, `grandTotal = subTotal − discount`, advance becomes
   the first payment.
6. Stage assignment at creation is optional; most shops assign from the order detail later.

---

## 5. Daily operations at a glance

```mermaid
sequenceDiagram
    participant R as Reception
    participant M as Manager
    participant K as Karigar
    participant API
    R->>API: POST /customers, /measurements/profiles
    M->>API: POST /orders (advance recorded)
    API-->>R: WhatsApp "order confirmed" (outbox)
    M->>API: POST /orders/stages/assign
    K->>API: GET /karigar/my-queue
    K->>API: POST /orders/stages/log, /complete
    API-->>API: earning to karigar ledger, order status advances
    M->>API: POST /orders/stages/reject (if QC fails)
    API-->>R: "order ready" notification
    R->>API: POST /orders/{id}/payments, /deliver
    M->>API: POST /karigar/{id}/ledger (weekly payout)
```

---

## 6. Decisions that need your approval

| # | Decision | Options | Recommendation |
|---|---|---|---|
| D1 | **Where is the frontend code**, and which framework | — | Needed before any UI work. |
| D2 | **Target platform** | Mobile app as in the screenshots, web SaaS with sidebar as in the brief, or both | Web for Owner/Manager/Admin, mobile for Karigar and reception. Same API. |
| D3 | **Owner-managed garment types** | (a) Keep the fixed list and let templates be the owner's "order types". (b) Backend change to a per-shop garment type table. | (a) now. It meets the need with zero backend risk; (b) is a multi-day migration touching rates, attributes and reports. |
| D4 | **Master / Cutter / Helper** designations | (a) Drop and use Karigar. (b) Add a display-only designation field to users. | (b), a small backend change, permissions stay on roles. |
| D5 | **Save measurements entered during an order** | (a) Always save as customer profile. (b) Ask each time. (c) Never. | (a) with an "one-off, don't save" option. |
| D6 | **Receptionist creating orders** | Keep default (cannot) or grant `orders.create` | Grant, if counter staff take orders. Owner can do this today without code. |
| D7 | **Karigar sees order prices** | Allow as today, or restrict (backend change) | Restrict in the UI now by using only `my-queue`; decide on backend trimming later. |
| D8 | **Super Admin console** | Separate app, or a separate area of the same app | Separate area of the same web app, routed by role after sign-in. |
| D9 | **Backend gaps** in API_MAPPING §3 | Build them in this API, or design the UI around their absence | Build the small ones (admin shop list with subscription, admin stats, template deactivate, field label/default, designation). Defer feature flags and garment-type table. |
