# API.md

REST API Standards & Design Guidelines

## PURPOSE

This document defines the standards, conventions, and best practices for designing REST APIs within the Nestly platform.

It ensures that all APIs are consistent, predictable, scalable, versioned, and easy to consume.

This document is the single source of truth for API design.

## API DESIGN PRINCIPLES

Every API should be:

- RESTful
- Stateless
- Resource-Oriented
- Consistent
- Predictable
- Secure
- Versioned
- Backward Compatible

## API URL STRUCTURE

All endpoints should follow a consistent URI structure.

/api/v1/{resource} /api/v1/{resource}/{id} /api/v1/{resource}/{id}/{sub-resource}

Examples:

GET /api/v1/customers GET /api/v1/customers/{id} POST /api/v1/customers PUT /api/v1/customers/{id} PATCH /api/v1/customers/{id} DELETE /api/v1/customers/{id}

## RESOURCE NAMING

Rules:

- Use nouns, not verbs.
- Use plural resource names.
- Keep URLs lowercase.
- Use hyphens for multiple words.
- Avoid implementation-specific names.

Good Examples

/customers /bookings /service-providers /payments

Avoid

/GetCustomer /CreateBooking /DeleteUser

## HTTP METHODS

Use HTTP methods according to their intended purpose.

| Method | Usage |
|---|---|
| GET | Read data |
| POST | Create resources |
| PUT | Replace resources |
| PATCH | Partial update |
| DELETE | Remove resources |

## API VERSIONING

All public APIs must be versioned.

Preferred format

/api/v1/ /api/v2/

Rules:

- Never introduce breaking changes without a new version.
- Older versions remain supported until officially deprecated.

## REQUEST DESIGN

Requests should:

- Use DTOs
- Be strongly typed
- Be validated
- Be self-descriptive
- Avoid unnecessary parameters

Do not expose domain entities directly.

## DTO DESIGN

Use separate DTOs for different operations.

Examples:

- Create DTO
- Update DTO
- Response DTO
- Search DTO
- Summary DTO

Rules:

- Never expose internal entities.
- Keep DTOs focused.
- Avoid unnecessary properties.
- Keep request and response models independent.

## RESPONSE DESIGN

Responses should be:

- Consistent
- Predictable
- Self-descriptive

Typical response should include:

- Data
- Metadata (if applicable)
- Pagination (for collections)
- Error details (for failures)

## HTTP STATUS CODES

Use standard HTTP status codes.

| Code | Meaning |
|---|---|
| 200 | OK |
| 201 | Created |
| 204 | No Content |
| 400 | Bad Request |
| 401 | Unauthorized |
| 403 | Forbidden |
| 404 | Not Found |
| 409 | Conflict |
| 422 | Unprocessable Entity |
| 500 | Internal Server Error |

Do not return HTTP 200 for failed operations.

## PAGINATION

Collection APIs should support pagination.

Standard parameters:

- Page Number
- Page Size

Responses should include:

- Total Records
- Total Pages
- Current Page
- Page Size

Avoid returning excessively large datasets.

## FILTERING

Support filtering where appropriate.

Examples:

?status=Active ?category=Cleaning ?city=Delhi

Filtering should remain optional and consistent.

## SORTING

Support sorting using query parameters.

Example

?sort=name ?sort=-createdDate

Use ascending order by default.

## SEARCH

Search endpoints should:

- Support keyword searches
- Support filtering
- Support pagination
- Return predictable results

Search operations should remain fast and optimized.

## IDENTITY

Every resource must have a stable unique identifier.

Identifiers must remain immutable after creation.

## IDEMPOTENCY

Follow HTTP idempotency rules.

- GET should be idempotent.
- PUT should be idempotent.
- DELETE should be idempotent.
- POST should create new resources unless explicitly designed otherwise.

## VALIDATION

Validate every incoming request.

Validation should include:

- Required fields
- Data format
- Business constraints
- Input length
- Allowed values

Reject invalid requests before processing.

## ERROR RESPONSE

Return consistent error responses.

Every error should clearly communicate:

- Error Code
- Message
- Validation Errors (if applicable)
- Correlation Identifier (if available)

Never expose internal implementation details.

## API DOCUMENTATION

Every endpoint must include:

- Purpose
- HTTP Method
- URL
- Request Model
- Response Model
- Status Codes
- Validation Rules
- Example Requests
- Example Responses

Documentation must remain synchronized with implementation.

## BACKWARD COMPATIBILITY

Maintain backward compatibility whenever possible.

Breaking changes require:

- New API version
- Migration strategy
- Deprecation notice

## API REVIEW CHECKLIST

Before publishing an API, verify:

- Resource naming is correct.
- Correct HTTP method is used.
- DTOs are defined appropriately.
- Validation rules exist.
- Status codes are correct.
- Pagination is supported where needed.
- Error responses are consistent.
- API is documented.
- Backward compatibility is maintained.

## OUT OF SCOPE

This document does not define:

- ASP.NET Core implementation
- Controller implementation
- Dependency Injection
- Authentication
- Authorization
- Business Logic
- Database Access
- ORM Configuration
- Security Policies
- Deployment

Refer to the corresponding project documents for these topics.

---

# PART 2 — AUTH AND PROFILE SERVICE REFERENCE

Everything above this line is the general standard that all Nestly APIs
follow. This part is the concrete endpoint reference for the services
implemented in Phase 1: **Auth**, **Profile**, and **Address**
(SRS 24.1, 24.2). It is generated from — and must stay in step with — the
controllers in `backend/consumer-api/ConsumerApi/Controllers/`.

## CONVENTIONS USED IN THIS REFERENCE

- **Base URL**: all paths below are relative to the Consumer API host and
  carry the version segment, e.g. `POST /api/v1/auth/login/password`.
- **Content type**: `application/json` on request and response.
- **Casing**: request and response properties are camelCase. The C# records
  they map to are PascalCase; `System.Text.Json` applies the camelCase policy.
- **Auth column**: `None` means the endpoint is reachable unauthenticated.
  `Bearer` means it requires `Authorization: Bearer <accessToken>`; the
  customer is identified by the token's `sub` claim, never by a route or body
  parameter.
- **Errors**: every failure is RFC 7807 Problem Details — see
  [Error Response](#error-response) above and the shared shape at the end of
  this part.

## AUTH ENDPOINT INDEX

| # | Method | URL | Purpose | Auth | Rate limit |
|---|--------|-----|---------|------|------------|
| 1 | POST | `/api/v1/auth/registration/otp` | Send a registration OTP | None | `otp` |
| 2 | POST | `/api/v1/auth/registration` | Complete registration | None | — |
| 3 | POST | `/api/v1/auth/login/otp` | Send a login OTP | None | `otp` |
| 4 | POST | `/api/v1/auth/login/otp/verify` | Log in with mobile + OTP | None | `login` |
| 5 | POST | `/api/v1/auth/login/password` | Log in with email + password | None | `login` |
| 6 | POST | `/api/v1/auth/refresh` | Rotate the token pair | None | — |
| 7 | POST | `/api/v1/auth/logout` | Invalidate a session | None | — |
| 8 | POST | `/api/v1/auth/password/forgot` | Request a reset code | None | `otp` |
| 9 | POST | `/api/v1/auth/password/reset` | Set a new password | None | `login` |

Rate-limit policies are partitioned by client IP (`Program.cs`): `otp` permits
5 requests/hour, `login` permits 10 requests/15 minutes. Both return `429`
when exceeded. These are separate from the per-identifier account lockout,
which is what stops a slow distributed brute force.

## PROFILE AND ADDRESS ENDPOINT INDEX

| #  | Method | URL | Purpose | Auth |
|----|--------|-----|---------|------|
| 10 | GET | `/api/v1/profile` | View profile | Bearer |
| 11 | PUT | `/api/v1/profile` | Edit name and optional profile data | Bearer |
| 12 | POST | `/api/v1/profile/mobile/otp` | Send a code to a new mobile number | Bearer |
| 13 | POST | `/api/v1/profile/mobile` | Apply the mobile change | Bearer |
| 14 | POST | `/api/v1/profile/email/otp` | Send a code to a new email address | Bearer |
| 15 | POST | `/api/v1/profile/email` | Apply the email change | Bearer |
| 16 | GET | `/api/v1/profile/preferences` | Read communication preferences | Bearer |
| 17 | PUT | `/api/v1/profile/preferences` | Replace communication preferences | Bearer |
| 18 | GET | `/api/v1/addresses` | List the caller's addresses | Bearer |
| 19 | POST | `/api/v1/addresses` | Add an address | Bearer |
| 20 | PUT | `/api/v1/addresses/{id}` | Edit an address | Bearer |
| 21 | DELETE | `/api/v1/addresses/{id}` | Delete an address | Bearer |
| 22 | POST | `/api/v1/addresses/{id}/default` | Set the default address | Bearer |

---

## 1. POST /api/v1/auth/registration/otp

**Purpose** — Step 1 of registration (SRS 11.2.1): send a one-time code to a
mobile number to prove the caller controls it.

**Request**

```json
{ "mobile": "+919876543210" }
```

| Field | Type | Required | Rules |
|-------|------|----------|-------|
| `mobile` | string | yes | `^\+?[1-9]\d{7,14}$` |

**Responses** — `200` empty body on success.

| Status | When |
|--------|------|
| 200 | Code sent |
| 400 | Mobile fails validation, or a code was requested within the last 30s (`Otp.TooManyRequests`) |
| 409 | Mobile already registered (`Registration.MobileAlreadyRegistered`) |
| 429 | `otp` rate limit exceeded |

## 2. POST /api/v1/auth/registration

**Purpose** — Step 2 of registration: create the customer once the OTP
verifies. Email + password is optional; mobile + OTP always works.

**Request**

```json
{
  "mobile": "+919876543210",
  "otpCode": "123456",
  "name": "Asha Menon",
  "email": "asha@example.com",
  "password": "a-strong-password",
  "consentAccepted": true
}
```

| Field | Type | Required | Rules |
|-------|------|----------|-------|
| `mobile` | string | yes | `^\+?[1-9]\d{7,14}$` |
| `otpCode` | string | yes | exactly 6 digits |
| `name` | string | yes | 1–200 chars |
| `email` | string \| null | no | valid email; required if `password` is set |
| `password` | string \| null | no | min 8 chars; rejected when password auth is disabled |
| `consentAccepted` | boolean | yes | must be `true` |

**Response** — `201`

```json
{
  "id": "3f2504e0-4f89-11d3-9a0c-0305e82c3301",
  "mobile": "+919876543210",
  "email": "asha@example.com",
  "name": "Asha Menon",
  "status": "Active"
}
```

The account starts `Active`: the OTP already proved mobile ownership, so
there is nothing left to verify.

| Status | When |
|--------|------|
| 201 | Customer created |
| 400 | Validation failure, wrong/expired OTP, consent not accepted, password without email |
| 409 | Mobile or email already registered |

## 3. POST /api/v1/auth/login/otp

**Purpose** — Send a login code to an already-registered mobile number
(SRS 11.2.2).

**Request** — `{ "mobile": "+919876543210" }`

| Status | When |
|--------|------|
| 200 | Code sent |
| 400 | Validation failure or resend cooldown |
| 404 | No account for that number (`Login.NotFound`) |
| 429 | `otp` rate limit exceeded |

## 4. POST /api/v1/auth/login/otp/verify

**Purpose** — Exchange a mobile + OTP pair for a session.

**Request**

```json
{ "mobile": "+919876543210", "otpCode": "123456" }
```

**Response** — `200`, the shared `LoginResponse`:

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "accessTokenExpiresAtUtc": "2026-07-28T13:45:00Z",
  "refreshToken": "b1c2d3e4f5..."
}
```

The refresh token is returned in plaintext exactly once; only its SHA-256
hash is stored server-side.

| Status | When |
|--------|------|
| 200 | Signed in |
| 400 | Validation failure or wrong/expired OTP |
| 401 | — |
| 403 | Account locked (`Login.AccountLocked`) or not active (`Login.AccountNotActive`) |
| 404 | No account for that number |
| 429 | `login` rate limit exceeded |

## 5. POST /api/v1/auth/login/password

**Purpose** — Log in with email + password, when password auth is enabled.

**Request**

```json
{ "email": "asha@example.com", "password": "a-strong-password" }
```

**Response** — `200`, `LoginResponse` (as above).

| Status | When |
|--------|------|
| 200 | Signed in |
| 400 | Validation failure |
| 401 | `Login.InvalidCredentials` — returned identically for an unknown email and a wrong password, so the endpoint cannot be used to enumerate accounts |
| 403 | Account locked or not active |
| 429 | `login` rate limit exceeded |

## 6. POST /api/v1/auth/refresh

**Purpose** — Exchange a still-valid refresh token for a new pair. The old
token is revoked on use (rotation, SRS 28.3), so a captured token cannot be
replayed after the legitimate client has used it.

**Request** — `{ "refreshToken": "b1c2d3e4f5..." }`

**Response** — `200`, `LoginResponse`.

| Status | When |
|--------|------|
| 200 | New pair issued |
| 400 | Missing token |
| 401 | Token unknown, expired, or already revoked |

## 7. POST /api/v1/auth/logout

**Purpose** — Invalidate one session's refresh token (SRS 11.2.2).

**Request** — `{ "refreshToken": "b1c2d3e4f5..." }`

| Status | When |
|--------|------|
| 204 | Session invalidated. Also returned for an already-invalid token — the caller's desired end state (no active session) holds either way |
| 400 | Missing token |

## 8. POST /api/v1/auth/password/forgot

**Purpose** — Step 1 of the reset flow (SRS 11.2.2). The code is sent to the
**mobile number on the account**, not to the email: the mobile was proven by
OTP at registration, whereas the email is only ever an unverified identifier.

**Request** — `{ "email": "asha@example.com" }`

| Status | When |
|--------|------|
| 200 | Always, whether or not the email is registered — an honest 404 would turn this endpoint into an email-address oracle |
| 400 | Email fails validation, resend cooldown, or password auth is disabled |
| 429 | `otp` rate limit exceeded |

## 9. POST /api/v1/auth/password/reset

**Purpose** — Step 2: set the new password once the code verifies. Every
active session for the customer is revoked, so tokens issued before the reset
stop working immediately.

**Request**

```json
{
  "email": "asha@example.com",
  "otpCode": "123456",
  "newPassword": "a-new-strong-password"
}
```

| Field | Type | Required | Rules |
|-------|------|----------|-------|
| `email` | string | yes | valid email |
| `otpCode` | string | yes | exactly 6 digits |
| `newPassword` | string | yes | min 8 chars — the same floor registration enforces |

| Status | When |
|--------|------|
| 204 | Password changed and sessions revoked |
| 400 | Validation failure; wrong/expired code; unknown email or inactive account (all reported as the single `PasswordReset.Invalid`, so a caller cannot tell them apart) |
| 429 | `login` rate limit exceeded |

---

## 10. GET /api/v1/profile

**Purpose** — View profile (SRS 11.2.3). Scoped to the caller's own `sub`
claim.

**Response** — `200`, the `CustomerProfileResponse` schema (below).

| Status | When |
|--------|------|
| 200 | Profile returned |
| 401 | Missing or invalid bearer token |
| 404 | Customer record not found |

## 11. PUT /api/v1/profile

**Purpose** — Edit name and optional profile data (SRS 11.2.3).

`mobile` and `email` are **deliberately absent** from this request: both are
identity-bearing (mobile is the OTP login identifier, email the password
one), so they move only through the re-verified endpoints 12–15. A PUT that
could change them would let anyone holding a valid token take over a login
identifier.

**Request** — the `UpdateProfileRequest` schema (below).

| Status | When |
|--------|------|
| 200 | Updated; returns the full `CustomerProfileResponse` |
| 400 | Validation failure |
| 401 | Missing or invalid bearer token |
| 404 | Customer record not found |

## 12. POST /api/v1/profile/mobile/otp

**Purpose** — Step 1 of a mobile change: send a code to the number being
claimed.

**Request** — `{ "newMobile": "+919000000001" }`

| Status | When |
|--------|------|
| 200 | Code sent to the new number |
| 400 | Validation failure; same as the current number (`Profile.MobileUnchanged`); resend cooldown |
| 401 | Missing or invalid bearer token |
| 409 | Number already registered to another account |
| 429 | `otp` rate limit exceeded |

## 13. POST /api/v1/profile/mobile

**Purpose** — Step 2: apply the change once the code verifies. Updates the
customer record *and* repoints the mobile auth identity, so OTP login follows
the new number.

**Request** — `{ "newMobile": "+919000000001", "otpCode": "123456" }`

| Status | When |
|--------|------|
| 200 | Changed; returns the updated `CustomerProfileResponse` |
| 400 | Validation failure or wrong/expired code |
| 401 | Missing or invalid bearer token |
| 409 | Number claimed by another account since the code was issued |

## 14. POST /api/v1/profile/email/otp

**Purpose** — Step 1 of an email change. Unlike every other OTP in the
system, this one is delivered over **email**, to the address being claimed —
sending it to the address already on file would prove nothing about the new
one.

**Request** — `{ "newEmail": "new.address@example.com" }`

| Status | When |
|--------|------|
| 200 | Code sent to the new address |
| 400 | Validation failure; same as the current address (`Profile.EmailUnchanged`); resend cooldown |
| 401 | Missing or invalid bearer token |
| 409 | Address already registered (only when `Identity:RequireUniqueEmail` is true) |
| 429 | `otp` rate limit exceeded |

## 15. POST /api/v1/profile/email

**Purpose** — Step 2: apply the change. Also repoints the email+password auth
identity when one exists, so password login follows the new address.

**Request** — `{ "newEmail": "new.address@example.com", "otpCode": "123456" }`

| Status | When |
|--------|------|
| 200 | Changed; returns the updated `CustomerProfileResponse` |
| 400 | Validation failure or wrong/expired code |
| 401 | Missing or invalid bearer token |
| 409 | Address claimed by another account since the code was issued |

## 16. GET /api/v1/profile/preferences

**Purpose** — Read communication preferences (SRS 11.2.3, channels per
SRS 30.2).

A customer who has never saved a preference gets the defaults without a row
being created — a GET stays side-effect free.

| Status | When |
|--------|------|
| 200 | Preferences returned |
| 401 | Missing or invalid bearer token |
| 404 | Customer record not found |

## 17. PUT /api/v1/profile/preferences

**Purpose** — Replace communication preferences. Every flag is required, so a
save always states the full desired state rather than leaving a channel
ambiguously unset.

**Scope note** — these flags govern transactional (booking/account) and
promotional traffic. OTP and other security messages are **not** covered and
cannot be switched off; disabling them would lock a customer out of their own
account.

| Status | When |
|--------|------|
| 200 | Saved; returns the stored preferences |
| 401 | Missing or invalid bearer token |
| 404 | Customer record not found |

---

## 18–22. ADDRESS ENDPOINTS

Full field rules live with the address module; the identity-relevant
guarantees are:

- Every action resolves the customer from the JWT `sub` claim. An address id
  belonging to another customer returns `404`, not `403` — the API does not
  confirm that someone else's id exists (SRS 28.3 IDOR).
- The first address a customer creates is always made default, so they are
  never left with none.
- Exactly one default per customer is enforced by a partial unique index, not
  only by service code.

| Endpoint | Success | Other |
|----------|---------|-------|
| `GET /api/v1/addresses` | 200 (array, possibly empty) | 401 |
| `POST /api/v1/addresses` | 201 | 400, 401 |
| `PUT /api/v1/addresses/{id}` | 200 | 400, 401, 404 |
| `DELETE /api/v1/addresses/{id}` | 204 | 401, 404 |
| `POST /api/v1/addresses/{id}/default` | 204 | 401, 404 |

---

# PAYLOAD SCHEMAS

The schemas below are the normative definition of the Profile payloads. They
are expressed as **JSON Schema (draft 2020-12)**, which is the schema
language OpenAPI 3.1 itself uses and the one `System.Text.Json` payloads are
actually validated against in practice.

> **On XSD** — an earlier plan item asked for `.xsd` files for these payloads.
> XSD describes XML documents; these endpoints neither accept nor produce
> XML, and ASP.NET Core is not configured with an XML formatter. Shipping
> XSDs would mean maintaining a second schema that nothing reads and that
> would silently drift from the contract. JSON Schema is the correct
> equivalent artefact and is what follows. If an XML-only consumer ever
> appears, the right move is to add an XML formatter and generate the XSD
> from these types then — not to hand-write one now.

## CustomerProfileResponse

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "https://nestly.app/schemas/customer-profile-response.json",
  "title": "CustomerProfileResponse",
  "type": "object",
  "additionalProperties": false,
  "required": ["id", "mobile", "email", "name", "dateOfBirth", "city",
               "state", "pincode", "country", "status", "createdAt", "updatedAt"],
  "properties": {
    "id": { "type": "string", "format": "uuid" },
    "mobile": { "type": "string", "pattern": "^\\+?[1-9]\\d{7,14}$" },
    "email": { "type": ["string", "null"], "format": "email", "maxLength": 200 },
    "name": { "type": "string", "minLength": 1, "maxLength": 200 },
    "dateOfBirth": { "type": ["string", "null"], "format": "date-time" },
    "city": { "type": ["string", "null"], "maxLength": 200 },
    "state": { "type": ["string", "null"], "maxLength": 200 },
    "pincode": { "type": ["string", "null"], "pattern": "^\\d{6}$" },
    "country": { "type": ["string", "null"], "maxLength": 200 },
    "status": { "type": "string", "enum": ["Active", "Blocked", "Unverified", "SoftDeleted"] },
    "createdAt": { "type": "string", "format": "date-time" },
    "updatedAt": { "type": "string", "format": "date-time" }
  }
}
```

No property carries credential material: password hashes, refresh tokens, and
session rows never leave the persistence layer.

## UpdateProfileRequest

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "https://nestly.app/schemas/update-profile-request.json",
  "title": "UpdateProfileRequest",
  "type": "object",
  "additionalProperties": false,
  "required": ["name"],
  "properties": {
    "name": { "type": "string", "minLength": 1, "maxLength": 200 },
    "dateOfBirth": {
      "type": ["string", "null"],
      "format": "date-time",
      "description": "Must be in the past."
    },
    "city": { "type": ["string", "null"], "maxLength": 200 },
    "state": { "type": ["string", "null"], "maxLength": 200 },
    "pincode": { "type": ["string", "null"], "pattern": "^\\d{6}$" },
    "country": { "type": ["string", "null"], "maxLength": 200 }
  }
}
```

`mobile` and `email` are absent by design — see endpoint 11.

## CommunicationPreferencesRequest / Response

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "https://nestly.app/schemas/communication-preferences.json",
  "title": "CommunicationPreferences",
  "type": "object",
  "additionalProperties": false,
  "required": ["transactionalSms", "transactionalEmail", "transactionalWhatsApp",
               "promotionalSms", "promotionalEmail", "promotionalWhatsApp", "push"],
  "properties": {
    "transactionalSms": { "type": "boolean", "default": true },
    "transactionalEmail": { "type": "boolean", "default": true },
    "transactionalWhatsApp": { "type": "boolean", "default": false },
    "promotionalSms": { "type": "boolean", "default": false },
    "promotionalEmail": { "type": "boolean", "default": false },
    "promotionalWhatsApp": { "type": "boolean", "default": false },
    "push": {
      "type": "boolean",
      "default": false,
      "description": "SRS 30.2 lists Push as a future channel; the flag is stored now so enabling it needs no schema change."
    }
  }
}
```

The response carries the same properties plus `updatedAt`
(`string`, `date-time`).

## Error (all endpoints)

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "https://nestly.app/schemas/problem-details.json",
  "title": "ProblemDetails",
  "type": "object",
  "properties": {
    "type": { "type": "string", "format": "uri" },
    "title": { "type": "string" },
    "status": { "type": "integer" },
    "detail": { "type": "string" },
    "correlationId": { "type": "string" },
    "errors": {
      "description": "Present on validation failures; keyed by field name.",
      "type": "object",
      "additionalProperties": { "type": "array", "items": { "type": "string" } }
    }
  }
}
```

Error codes follow the `Module.Reason` convention (`Login.AccountLocked`,
`Profile.MobileUnchanged`, `Otp.Expired`). They are stable and safe to branch
on; `detail` is human-facing and may be reworded.

# OPENAPI / SWAGGER

Swagger UI is served in Development only, at `/swagger`, from the generated
OpenAPI document at `/swagger/v1/swagger.json` (`Program.cs`). Because
`AddSwaggerGen` reflects over the controllers, the operation list, request and
response types, and the status codes declared via `[ProducesResponseType]`
are generated from the code itself and cannot drift from it.

What reflection cannot infer, and what this document therefore carries, is
intent: which of two identical-looking 400s is deliberate, why `forgot`
always returns 200, and why `mobile` is missing from the profile PUT. Keep the
two in step — the generated document for shape, this reference for reasoning.
