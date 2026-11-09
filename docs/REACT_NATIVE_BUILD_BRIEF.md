# Digital Darzi — React Native App Build Brief

You are building the Digital Darzi mobile app in React Native (Expo, TypeScript) for tailor shops. The backend API already
exists and is complete for this scope. The screen designs are done. Your job is to build the app
so it matches the designs and talks to the real API.

Read this brief first, then the files in §1 in the order given.

---

## 1. What you are given

| Order | File | Use it for |
|---|---|---|
| 1 | `docs/design/mobile-redesign.html` | **The design.** Open it in a browser. It has 29 screens, a design system, and a yellow note on every screen naming its API calls. Build what you see. |
| 2 | `docs/MOBILE_APP_HANDOFF.md` | **The API reference for mobile.** It covers conventions, auth, dates, paging, every screen recipe, response shapes, enums, and the full endpoint catalogue. Includes Dart tips. |
| 3 | `docs/API_MAPPING.md` | Feature → endpoint → request → response table, plus **known backend gaps** (M1–M13) and validation rules. |
| 4 | `docs/ROLE_PERMISSIONS.md` | Which permission shows which screen and button. |
| 5 | `docs/BUSINESS_WORKFLOWS.md` | How the shop works: order lifecycle, the measurement model, and the new-order flow. |
| — | Swagger at `{baseUrl}/swagger` | Exact schemas when the API is running in Development. |

The rule when sources disagree: the **API code** wins over the docs, and the **docs** win over the
design. If you find a disagreement, stop and report it. Do not guess.

---

## 2. Scope

Build **only the mobile app** for these users. The Super Admin console is out of scope.

| User | Lands on | Screens (board numbers) |
|---|---|---|
| Owner, Manager | Home 1.2 | All of 1–5 and 7 |
| Receptionist | Orders 1.3 | Whatever their permissions allow, typically customers, measurements, payments, deliveries, cash |
| Karigar | My work 6.1 | 6.1 My work, 6.2 Job card, 6.3 Earnings, plus a "Done" history tab and a "Me" tab |

The menu, tabs and buttons come from `GET /api/auth/me` → `permissions`. Never decide access from
the role name.

---

## 3. Technical requirements

| Concern | Use |
|---|---|
| Framework | Expo (latest SDK) with TypeScript in strict mode |
| Navigation | Expo Router, file-based, with a guard that redirects by session and permissions |
| Server state | TanStack Query (`@tanstack/react-query`) |
| HTTP | `axios`, one shared client with interceptors |
| Forms | `react-hook-form` + `zod` |
| Token storage | `expo-secure-store` on device. Never AsyncStorage. |
| Styling | `StyleSheet` with typed theme tokens from the board's design system. No UI kit. |
| Icons | `lucide-react-native` (same icon family as the board) |
| Font | Inter via `@expo-google-fonts/inter` |
| Tests | `jest-expo` for logic: envelope, dates, money, totals, permissions |

Structure: `app/` holds routes only. `src/api` holds the client, types and one file per resource.
`src/auth` holds the session. `src/ui` holds shared components. `src/features/<area>` holds screen
bodies and hooks. Enums are `const` objects of numbers, matching the API.

If an existing React Native codebase is supplied, refactor it into this shape rather than starting
a second app.

---

## 4. The HTTP layer — get this right first

These cause most integration bugs. All are explained in MOBILE_APP_HANDOFF §3–4.

1. **Envelope.** Every response is `{ success, data, message, errors }`. Unwrap it in one place.
   Show `errors[]` on 400.
2. **Status handling.** 401: refresh once, then sign out. 402: go to the "Subscription ended"
   screen (7.3). 403: refetch `/auth/me`, hide the action, show a short message. 409: reload and
   retry. 429: wait. 500: show the `traceId`.
3. **Refresh tokens are single-use.** Queue concurrent 401s behind one refresh call. Store the new
   pair before retrying.
4. **Enums are integers** in both directions.
5. **Two kinds of date.** Instants are UTC and shown in local time. Calendar dates (`deliveryDate`,
   `trialDate`) are sent as `YYYY-MM-DDT00:00:00Z` and **formatted in UTC**, never converted.
6. **Two paging shapes.** Customers and users return `{ data, pageNumber, pageSize, totalRecords }`.
   Orders return `{ items, totalCount, page, pageSize, totalPages }`.
7. **Update conventions.** Customer, user and shop updates send `id` in the body with `PUT` to the
   collection. User roles and branches take a **bare JSON array**.
8. **Branch switch** returns new tokens. Replace both, refetch `/auth/me`, clear all cached lists.

---

## 5. Build order and acceptance

Build in this order. Each milestone ends with the app working against the **real API**, signed in
as the demo accounts. Do not start a milestone until the previous one is accepted.

| # | Milestone | Done when |
|---|---|---|
| M0 | Project setup, theme from the design system, API client, types for enums and DTOs | `tsc` passes. App opens in Expo Go and on web. Unit tests for the envelope, date and money helpers pass. |
| M1 | Sign in (email, and phone + PIN), refresh, session, permission-based router, More menu, branch switch, 402 screen | Each demo account lands on the right home. A receptionist does not see Reports or Staff. An expired token refreshes silently. |
| M2 | Customers list, new customer, customer page, measurement sets with history, dynamic measurement form | A new customer can be saved and measured in one flow. Required fields are enforced. Versions show changed values. |
| M3 | New order, all six steps plus success | Can create a two-garment order with one saved set and one new set, attributes, fabric, discount and advance. The server's totals equal the screen's. The new measurements appear on the customer afterwards. |
| M4 | Orders list with filters, order detail, payment, status change, deliver (full and partial), cancel, receipt share, deliveries board | Status choices equal `allowedNextStatuses`. A backward move demands a note. Delivering with a balance is blocked without permission. |
| M5 | Karigar app: my work, start, complete, rework, job card, earnings, check-in | A karigar can only act on their own items. No prices appear anywhere in their app. Completing a stage moves the order. |
| M6 | Setup: measurement styles and fields, garment options, staff list and add staff, assign karigar to a stage | A style's required flags and order save and show up in the new-order form. |
| M7 | Loading, empty and error states on every list; pull to refresh; trial banner; accessibility pass | Every list has all three states. Text scales to 130% without breaking layouts. |

---

## 6. Rules

1. **No mock or hard-coded business data** in any screen. If the API cannot provide something,
   leave it out and report it. The board's sample names are for design only.
2. **Do not invent endpoints or fields.** Use only what MOBILE_APP_HANDOFF, API_MAPPING or Swagger
   show.
3. **Backend gaps** (API_MAPPING §3) are handled like this:
   - Staff label "Master, Cutter, Helper" (M6): show it only if the API returns a `designation`
     field. Otherwise show the role only.
   - Karigar prices (M13): the API still returns them. The karigar UI must not display them.
   - Measurements entered during an order (M11): call `POST /api/measurements/profiles` first,
     then send the new `measurementProfileId` on the order item. If the order call fails, tell
     the user the measurements were saved.
   - Inline one-off measurements (M12): check required fields in the app, because the server does
     not.
   - Anything else missing: build the screen without it and list it in your report.
4. **Totals preview** must use the server formula: `Σ(price × qty + fabricCharge) − discount`.
   After creating, show the server's numbers.
5. **Never edit the backend.** If you believe it is wrong, report it with the request and response.
6. **Secrets.** No tokens, passwords or base URLs in source. Use `EXPO_PUBLIC_API_URL` in `.env` for the base URL.
7. Match the board's spacing, type and colours. Where the board is silent, follow Material 3
   defaults, not your own style.

---

## 7. Environment

| Item | Value |
|---|---|
| Base URL | `https://digitaldarzi.runasp.net` (hosted). Must run the API version from branch `feature/roadmap-2026-09` or later. |
| Test accounts | Supplied by the API owner for the hosted environment: one Owner, one Receptionist, one Karigar. |

The hosted API is the only backend the app uses. Before testing, confirm `GET /api/auth/me` exists on it;
if it returns 404 the hosted build is too old for this app.

---

## 8. What to report after each milestone

- What was built, as screen numbers from the board.
- How it was tested against the API, with which account.
- Any place the API differed from the docs, with the request and response.
- Any backend gap hit, and how the UI handled it.
- Screenshots of each new screen next to the board frame it implements.
