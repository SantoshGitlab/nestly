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
