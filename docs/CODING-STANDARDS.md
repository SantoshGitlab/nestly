# CODING-STANDARDS.md

Enterprise Coding Standards

## OBJECTIVE

Write code that is clean, consistent, maintainable, secure, testable, and production-ready.

Optimize for readability over cleverness.

Code is read far more often than it is written.

## GENERAL PRINCIPLES

Always follow:

- SOLID
- DRY
- KISS
- YAGNI
- Separation of Concerns
- Clean Code

Prefer simplicity over complexity.

## READABILITY

Write code that explains itself.

Prefer descriptive names over comments.

If comments are required, explain **why**, not **what**.

Avoid clever or cryptic code.

## NAMING

Use meaningful names.

Names should clearly express intent.

Avoid:

- temp
- data
- obj
- item
- value
- test
- misc

Prefer business terminology.

Maintain consistent naming throughout the project.

## METHODS

Methods should:

- Have a single responsibility
- Be easy to understand
- Be easy to test
- Minimize side effects

Avoid deeply nested logic.

Extract reusable logic into smaller methods.

## CLASSES

Each class should have one clear responsibility.

Keep classes focused.

Avoid large classes with unrelated responsibilities.

Prefer composition over inheritance.

## DEPENDENCIES

Depend on abstractions.

Inject dependencies.

Avoid creating dependencies inside business logic.

Keep coupling low.

## BUSINESS LOGIC

Business rules belong in the appropriate business layer.

Never place business logic inside:

- Controllers
- Repositories
- Middleware
- UI components
- Configuration

## DUPLICATION

Avoid duplicated code.

Extract reusable logic.

Prefer shared abstractions only when they improve clarity.

Do not over-engineer reuse.

## VALIDATION

Validate all external input.

Fail early.

Return meaningful validation errors.

Never trust user input.

## ERROR HANDLING

Handle errors intentionally.

Never ignore exceptions.

Never use empty catch blocks.

Return consistent error responses.

Log unexpected failures.

## NULL SAFETY

Assume values may be missing.

Validate before use.

Avoid null-related runtime failures.

Prefer explicit handling over assumptions.

## ASYNC

Use asynchronous operations for I/O.

Avoid blocking operations.

Do not mix synchronous and asynchronous patterns unnecessarily.

## PERFORMANCE

Write efficient code.

Avoid:

- Unnecessary loops
- Repeated database calls
- Large object allocations
- Premature optimization

Measure before optimizing.

## SECURITY

Always:

- Validate input
- Sanitize output
- Protect sensitive data
- Follow least privilege

Never expose secrets.

Never hardcode credentials.

## CONFIGURATION

Configuration belongs outside code.

Use environment-specific configuration.

Never hardcode:

- URLs
- Keys
- Passwords
- Tokens
- Connection strings

## LOGGING

Log meaningful events.

Avoid excessive logging.

Never log:

- Passwords
- Secrets
- Tokens
- Sensitive personal data

Use structured logging whenever possible.

## COMMENTS

Write comments only when necessary.

Good code should require minimal comments.

Remove outdated comments.

Keep documentation synchronized with code.

## MAGIC VALUES

Avoid magic numbers and hardcoded strings.

Use named constants or configuration where appropriate.

## CODE ORGANIZATION

Group related code together.

Keep files focused.

Avoid unrelated utilities in the same file.

Maintain consistent project structure.

## REFACTORING

Continuously improve code quality.

Remove:

- Dead code
- Duplicate code
- Unused variables
- Unused methods
- Obsolete implementations

Leave the codebase better than you found it.

## TESTABILITY

Write code that is easy to test.

Keep business logic independent.

Avoid hidden dependencies.

Prefer deterministic behavior.

## CONSISTENCY

Follow existing project conventions.

Maintain consistent:

- Naming
- Formatting
- Folder structure
- Error handling
- Logging
- Validation

Consistency is more valuable than personal preference.

## BEFORE SUBMITTING

Verify:

- Code compiles
- No warnings
- No dead code
- No duplication
- No obvious performance issues
- No security concerns
- Naming is clear
- Error handling is complete
- Validation is present
- Formatting is consistent

## FORBIDDEN

Never:

- Write unnecessary complexity
- Duplicate business logic
- Ignore validation
- Ignore exceptions
- Leave debugging code
- Leave commented-out code
- Hardcode secrets
- Bypass project conventions
- Mix unrelated responsibilities
- Introduce technical debt without justification

## DEFINITION OF GOOD CODE

Good code is:

- Simple
- Clear
- Predictable
- Reusable
- Secure
- Performant
- Testable
- Maintainable
- Consistent
- Easy to review

If another experienced developer can understand and safely modify your code without additional explanation, the standard has been met.
