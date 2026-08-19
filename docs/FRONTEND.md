# FRONTEND.md

Frontend Development Standards & Architecture

## PURPOSE

This document defines the standards, conventions, and architectural guidelines for developing the Nestly frontend application.

It establishes a consistent approach for building scalable, maintainable, reusable, and high-performance user interfaces using React, Next.js, and TypeScript.

This document is the single source of truth for frontend development.

## TECHNOLOGY STACK

### Framework

- Next.js

### UI Library

- React

### Language

- TypeScript

### Styling

- Tailwind CSS

### State Management

- TanStack Query
- React Context (where appropriate)

### Forms

- React Hook Form
- Zod Validation

## FRONTEND ARCHITECTURE

The application follows a component-driven architecture.

Application

↓

Pages / Routes

↓

Feature Modules

↓

Reusable Components

↓

Shared Utilities

Each layer has a clear responsibility.

## PROJECT STRUCTURE

Organize the application into logical feature modules.

Typical structure:

- App / Pages
- Features
- Components
- Layouts
- Services
- Hooks
- Context
- Types
- Utilities
- Assets

Group code by business feature rather than technical type whenever practical.

## COMPONENT DESIGN

Components should be:

- Small
- Reusable
- Independent
- Composable
- Easy to test

Prefer composition over inheritance.

Avoid large, monolithic components.

## COMPONENT RESPONSIBILITIES

A component should have a single responsibility.

Separate:

- UI rendering
- Business logic
- API communication
- State management

Keep presentation components focused on rendering.

## STATE MANAGEMENT

Choose the simplest appropriate solution.

Use:

- Local State for component-specific data
- Context for shared application state
- TanStack Query for server state

Avoid unnecessary global state.

## DATA FETCHING

Server communication should:

- Be centralized
- Be reusable
- Handle loading states
- Handle error states
- Support caching
- Support retry where appropriate

Components should not contain direct API implementation logic.

## ROUTING

Routing should be:

- Predictable
- Feature-oriented
- Easy to navigate

Protect secured routes appropriately.

## FORM MANAGEMENT

Forms should:

- Use React Hook Form
- Validate using Zod
- Display user-friendly validation messages
- Prevent invalid submissions

Separate validation logic from presentation.

## TYPE SAFETY

Use TypeScript throughout the application.

Guidelines:

- Avoid any
- Prefer explicit types
- Use interfaces or type aliases appropriately
- Share common types across features
- Keep types close to the business domain

## ERROR HANDLING

Frontend should gracefully handle:

- API failures
- Validation errors
- Network issues
- Unexpected exceptions

Display meaningful messages to users.

Avoid exposing technical details.

## LOADING STATES

Every asynchronous operation should provide:

- Loading indicators
- Disabled actions where appropriate
- Smooth user experience

Avoid blocking the entire interface unnecessarily.

## PERFORMANCE

Optimize for performance by:

- Lazy loading pages and components
- Code splitting
- Memoization where beneficial
- Avoiding unnecessary re-renders
- Optimizing images and assets

Measure before optimizing.

## REUSABILITY

Prefer reusable:

- Components
- Hooks
- Utilities
- Layouts

Avoid duplicated UI logic.

## ACCESSIBILITY

The application should support:

- Semantic HTML
- Keyboard navigation
- Screen readers
- Proper labels
- Sufficient color contrast

Accessibility should be considered during development.

## RESPONSIVE DESIGN

**Mobile is the primary platform, not one of three equally-weighted targets.**
The large majority of real usage is mobile — design and build for a phone
screen first, then verify the result still holds up on tablet and desktop,
not the other way around.

This applies differently by app, because the three apps have different
real-world users:

- **customer-web** and **provider-web** are mobile-first without
  qualification. Customers browse and book on their phones; providers work
  the entire job lifecycle (accept, navigate, complete, upload proof) from
  the field, usually one-handed, often on a mobile network, sometimes with
  a phone in one hand and a tool in the other. Every screen in these two
  apps must be designed for a phone viewport (~375–430px) first.
- **admin-web** is desk-first — an operations tool typically used at a
  workstation — but must stay usable on a tablet for on-the-go checks
  (approve a provider, look up a booking) without horizontal scrolling or
  broken layout. It does not need bottom-tab navigation or a phone-first
  redesign.

Concretely, "mobile-first" means, at minimum:

- Every interactive element meets a minimum touch target (44×44pt),
  not a size tuned for mouse pointers.
- Primary actions (Continue, Pay Now, Accept Job) stay reachable without
  scrolling to find them — a sticky/fixed action bar on long screens, not
  a button at the bottom of a page.
- Forms use the correct mobile keyboard per field (`inputMode`, `type`,
  `autoComplete`) — numeric for OTP/pincode, `tel` for phone, etc.
- Modals and dialogs use a full-screen or bottom-sheet pattern below a
  breakpoint instead of a small centered desktop dialog.
- Data tables collapse to a card/list layout below a breakpoint instead of
  forcing horizontal scroll.
- Root layouts respect iOS/Android safe areas (notch, home indicator) for
  any fixed header or footer.
- Performance is budgeted for a mobile network (3G/4G), not just desktop
  broadband — this is where a slow page costs the most real users.

Layouts should adapt consistently across supported devices, with mobile as
the baseline every other breakpoint is verified against, not an
afterthought checked once desktop is done.

## SECURITY

Frontend should never:

- Store secrets
- Trust client-side validation alone
- Expose sensitive information
- Assume authorization

Treat all client input as untrusted.

## CODE QUALITY

Frontend code should prioritize:

- Readability
- Simplicity
- Maintainability
- Reusability
- Consistency

Follow project coding standards.

## FRONTEND REVIEW CHECKLIST

Before completing a feature, verify:

- Components are reusable.
- TypeScript types are defined.
- Validation is implemented.
- Loading and error states exist.
- Responsive behavior is verified.
- Accessibility requirements are considered.
- No duplicated UI logic exists.
- Performance impact is acceptable.

## OUT OF SCOPE

This document does not define:

- Business requirements
- System architecture
- API design
- Backend implementation
- Database design
- Security implementation
- Testing strategy
- Deployment process

Refer to the corresponding project documents for these topics.
