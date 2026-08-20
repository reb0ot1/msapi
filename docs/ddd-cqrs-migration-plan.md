# DDD and CQRS Migration Plan

## Goal

Gradually evolve the current ASP.NET Core Minimal API into a modular monolith using
Domain-Driven Design and pragmatic CQRS, without introducing unnecessary
microservices, separate databases, event sourcing, or messaging infrastructure.

## Current State

- ASP.NET Core Minimal APIs
- Endpoint modules contain HTTP binding and some business logic
- EF Core with PostgreSQL
- `User` and `RefreshToken` persistence models
- JWT authentication with refresh-token rotation
- Integration tests using `WebApplicationFactory`

## Target Architecture

```text
WebApplicationTeamCity/
  Api/
    Endpoints/
  Application/
    Identity/
      Commands/
      Queries/
      DTOs/
    Abstractions/
  Domain/
    Common/
    Identity/
      Entities/
      ValueObjects/
      Events/
      Repositories/
  Infrastructure/
    Persistence/
      Configurations/
      Migrations/
      Repositories/
    Authentication/
```

The application remains a modular monolith. All modules initially use the same
PostgreSQL database and deployment.

## Architectural Rules

1. API endpoints only bind HTTP requests, dispatch use cases, and map results to
   HTTP responses.
2. Application handlers coordinate use cases and transactions.
3. Domain entities and value objects enforce business invariants.
4. Domain code must not depend on ASP.NET Core or EF Core.
5. Infrastructure implements persistence and external-service abstractions.
6. Commands change state; queries return read models and do not mutate state.
7. Do not introduce generic repositories or abstractions without a concrete use
   case.

## Migration Phases

### Phase 1: Establish boundaries

- Create `Api`, `Application`, `Domain`, and `Infrastructure` projects or
  equivalent folders.
- Keep current endpoint URLs and response contracts unchanged.
- Move EF configurations out of `AppDbContext.OnModelCreating` into separate
  configuration classes.
- Add architecture rules through project references and dependency direction.

Exit criteria:

- Domain has no dependency on ASP.NET Core or EF Core.
- Existing tests still pass without API behavior changes.

### Phase 2: Extract the Identity bounded context

- Treat `User` as the Identity aggregate root.
- Keep `RefreshToken` inside the Identity boundary.
- Introduce `Email`, `UserRole`, and other value objects only where they enforce
  meaningful rules.
- Move registration, role assignment, refresh-token rotation, and deletion
  rules into domain/application code.
- Keep password hashing and JWT generation behind interfaces.

Suggested abstractions:

```text
IUserRepository
IPasswordHasher
IAccessTokenService
IRefreshTokenGenerator
IUnitOfWork
```

### Phase 3: Introduce command handlers

Create one command and handler per state-changing use case:

- `RegisterUserCommand`
- `LoginUserCommand`
- `RefreshTokenCommand`
- `LogoutCommand`
- `DeleteUserCommand`
- `ChangeUserRoleCommand`

Handlers should:

- Validate command input.
- Load the required aggregate.
- Invoke domain behavior.
- Persist changes.
- Return an application result that the endpoint maps to HTTP.

### Phase 4: Introduce query handlers

Create read-focused queries:

- `GetUsersQuery`
- `GetCurrentUserQuery`
- `GetUserByIdQuery`

Queries may use EF Core projections directly through read repositories. They
should return DTOs or read models and must not load full aggregates unless
business behavior is required.

### Phase 5: Add cross-cutting behavior

After there are enough handlers to justify it, add a dispatcher or MediatR and
pipeline behaviors for:

- Validation
- Logging and correlation IDs
- Transaction boundaries
- Performance timing
- Authorization checks where appropriate

Do not add a mediator library before handlers provide a real benefit.

### Phase 6: Apply the pattern to financial and crawler modules

Create bounded contexts around the business capabilities:

- Companies and financial snapshots
- Crawl jobs and crawl errors
- Data-source integrations

Prioritize commands for crawl scheduling, retrying, and status transitions.
Prioritize queries for dashboards, company history, data gaps, and job status.

## Testing Strategy

- Keep API integration tests for routing, authentication, authorization, and
  response contracts.
- Add domain unit tests for invariants and state transitions.
- Add application handler tests using mocked or in-memory ports.
- Test query projections separately from command behavior.
- Preserve existing endpoint compatibility during migration.

## Deliberately Deferred

Do not introduce these as part of the initial migration:

- Separate read and write databases
- Event sourcing
- Distributed transactions
- Microservices
- Message brokers
- Generic repository frameworks
- Full event-driven integration architecture

These can be evaluated later if scale, reporting requirements, or operational
boundaries justify them.

## Completion Criteria

The migration is complete when:

- Business rules are outside endpoint modules.
- Identity, crawling, and financial data are independently organized modules.
- Commands and queries have separate application paths.
- Domain code is framework-independent.
- Persistence and authentication details are isolated in Infrastructure.
- Existing API behavior and security guarantees remain covered by tests.
