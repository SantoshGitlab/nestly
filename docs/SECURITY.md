# SECURITY.md

Authentication, Authorization & Security Standards

## PURPOSE

This document defines the security standards, principles, and best practices for the Nestly platform.

It establishes consistent rules for authentication, authorization, data protection, secret management, secure development, and application security.

This document is the single source of truth for security-related standards.

## SECURITY OBJECTIVES

The application must ensure:

- Confidentiality
- Integrity
- Availability
- Accountability
- Least Privilege
- Defense in Depth
- Secure by Default

Security requirements take priority over convenience.

## AUTHENTICATION

Authentication verifies the identity of a user or system.

Project standards:

- Use ASP.NET Core Identity.
- Use JWT Access Tokens.
- Support Refresh Tokens.
- Enforce secure password policies.
- Require email verification where applicable.
- Support Multi-Factor Authentication (future-ready).

Never create custom authentication mechanisms.

## AUTHORIZATION

Authorization controls access to application resources.

Guidelines:

- Follow Role-Based Access Control (RBAC).
- Enforce least privilege.
- Validate permissions on every protected request.
- Perform authorization on the server.
- Never rely solely on frontend authorization.

## PASSWORD SECURITY

Passwords must:

- Be hashed using approved algorithms.
- Never be stored or logged in plain text.
- Meet minimum complexity requirements.
- Support secure reset workflows.

Passwords must never be reversible.

## TOKEN SECURITY

Guidelines:

- Keep access tokens short-lived.
- Rotate refresh tokens.
- Validate token expiry.
- Revoke compromised tokens.
- Transmit tokens only over HTTPS.

Never expose tokens in logs or URLs.

## SECRET MANAGEMENT

Secrets include:

- Connection strings
- API keys
- JWT signing keys
- OTP hashing pepper
- Certificates
- Third-party credentials

Rules:

- Never hardcode secrets.
- Store secrets outside source code.
- Use environment-specific configuration.
- Rotate secrets periodically.
- Restrict access to secrets.

## INPUT VALIDATION

Validate all external input.

Include:

- Required fields
- Length limits
- Data types
- Allowed values
- File validation

Reject invalid input before business processing.

## OUTPUT SECURITY

Responses should:

- Return only necessary data.
- Mask sensitive information.
- Avoid exposing internal implementation details.
- Use standardized error responses.

## DATA PROTECTION

Sensitive data should:

- Be encrypted where appropriate.
- Be masked in logs and reports.
- Be transmitted only over secure channels.
- Be retained according to business requirements.

Protect Personally Identifiable Information (PII).

## API SECURITY

All APIs should:

- Require authentication where appropriate.
- Validate authorization.
- Validate all input.
- Return consistent error responses.
- Apply rate limiting where required.

Public endpoints should be explicitly identified.

## FILE SECURITY

For file uploads:

- Validate file type.
- Validate file size.
- Sanitize file names.
- Scan uploaded files when applicable.
- Store files outside publicly accessible locations.

Never trust uploaded content.

## SESSION SECURITY

Guidelines:

- Expire inactive sessions.
- Invalidate sessions after logout.
- Prevent session fixation.
- Protect against session hijacking.

## COMMUNICATION SECURITY

All communication must:

- Use HTTPS.
- Use modern TLS protocols.
- Prevent insecure transport.
- Protect sensitive headers.

Never transmit sensitive information over insecure channels.

## LOGGING & AUDITING

Log security-relevant events such as:

- Login attempts
- Failed authentication
- Permission failures
- Administrative actions
- Critical configuration changes

Never log:

- Passwords
- Tokens
- Secrets
- Sensitive personal data

## DEPENDENCY SECURITY

Use only trusted dependencies.

Requirements:

- Keep libraries updated.
- Remove unused packages.
- Monitor known vulnerabilities.
- Prefer officially supported packages.

## SECURE DEVELOPMENT

Developers should:

- Follow secure coding practices.
- Validate all inputs.
- Handle errors safely.
- Minimize attack surface.
- Avoid unnecessary privileges.

Security should be considered throughout development, not added later.

## SECURITY REVIEW CHECKLIST

Before releasing any feature, verify:

- Authentication is enforced.
- Authorization is validated.
- Input validation exists.
- Sensitive data is protected.
- Secrets are externalized.
- APIs expose only required data.
- Logs contain no sensitive information.
- Dependencies have no known critical vulnerabilities.

## OUT OF SCOPE

This document does not define:

- Business requirements
- System architecture
- API design
- Database schema
- Coding conventions
- Testing strategy
- Deployment process

Refer to the corresponding project documents for these topics.
