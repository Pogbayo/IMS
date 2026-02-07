# IMS

Inventory Management System (IMS) — a sample .NET application demonstrating a layered architecture (API, Application, Domain, Infrastructure) for managing inventory and stock transactions.

> Targets .NET 9 (TFM: `net9.0`). Update commands and tool versions accordingly.

## Table of Contents

- [Overview](#overview)
- [Key Features](#key-features)
- [Tech Stack](#tech-stack)
- [Repository Layout](#repository-layout)
- [Architecture](#architecture)
- [Quick Start](#quick-start)
- [Configuration and Secrets](#configuration-and-secrets)
- [Database & Migrations](#database--migrations)
- [Docker](#docker)
- [API Docs & Examples](#api-docs--examples)
- [Coding Standards and Tooling](#coding-standards-and-tooling)
- [Contributing](#contributing)
- [Troubleshooting](#troubleshooting)
- [FAQ](#faq)
- [Roadmap](#roadmap)
- [License](#license)

## Overview

IMS provides a core set of APIs and services for managing inventory items and processing stock transactions. The codebase favors a layered approach so business rules live in the domain/application layers and infrastructure concerns are replaceable.

This README focuses on developer setup, common workflows, and project conventions.

## Key Features

- CRUD for products and inventory items
- Transactional stock movements (add, remove, transfer)
- Audit-friendly domain model
- API surface designed for integrations
- Unit and integration test support

## Tech Stack

- .NET 9 (`net9.0`)
- C#
- ASP.NET Core Web API
- (Optional) Entity Framework Core or another data provider
- Test frameworks: xUnit / NUnit / MSTest (check `tests/`)

## Repository Layout

A typical repository layout (top-level projects and folders):

- `src/IMS.API` — Web API project (controllers, startup, configuration)
- `src/IMS.Application` — Application services and use-cases (e.g., `StockTransactionService`)
- `src/IMS.Domain` — Domain entities, value objects, interfaces
- `src/IMS.Infrastructure` — Persistence, implementations, migrations
- `.editorconfig` — Enforced coding rules
- `CONTRIBUTING.md` — Contribution guidelines

Adjust paths if your layout differs.

## Architecture

The project follows a layered architecture:

- **API Layer**: HTTP endpoints, request/response DTOs, authentication
- **Application Layer**: Orchestration, use-cases, application DTOs
- **Domain Layer**: Business rules and invariants
- **Infrastructure Layer**: Persistence and external integrations

This separation helps keep business rules isolated and makes the codebase easier to test and maintain. Keep controllers thin; place business behavior in the application/domain layers.

## Quick Start

### Prerequisites

- .NET 9 SDK: [Download here](https://dotnet.microsoft.com)
- Git
- Optional: Docker, database server for local integration tests

### Clone

git clone https://github.com/Pogbayo/IMS.git
cd IMS

### Build and Run Locally

Restore, build, and run the API project:

dotnet restore
dotnet build
dotnet run --project src/IMS.API/IMS.API.csproj

Open `https://localhost:5001` (or the URL shown in console output). Use `dotnet watch` inside the API project for a live development loop.

## Configuration and Secrets

- Configuration uses ASP.NET Core providers: `appsettings.json`, environment files, environment variables, and user secrets.
- For local secrets (avoid committing):

cd src/IMS.API
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Default" "YourConnectionString"

- Local user secrets path (Windows): `%APPDATA%/Microsoft/UserSecrets/<user-secrets-id>/secrets.json`.
- For production, use environment variables or a secret manager (Azure Key Vault, etc.).

### Example `appsettings.json` (format required)

Copy this structure into your `appsettings.Development.json` (or use the user secrets store) and update values for your environment. Do not commit secrets to source control.

```json
{
    "Jwt": {
        "Key": "your_jwt_key_here",
        "Issuer": "InventoryManagementSystem",
        "Audience": "IMS_Users",
        "ExpireHours": 60
    },
    "ConnectionStrings": {
        "DefaultConnection": "Server=.\\SQLEXPRESS;Database=IMS;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"
    },
    "ExternalApis": {
        "SomeApiKey": ""
    },
    "FeatureFlags": {
        "EnableNewFeatureX": false
    },
    "CloudinarySettings": {
        "CloudName": "your_cloud_name",
        "ApiKey": "your_api_key",
        "ApiSecret": "your_api_secret"
    },
    "AWS": {
        "AccessKeyId": "your_access_key_id",
        "SecretAccessKey": "your_secret_access_key",
        "Region": "us-west-1",
        "LogGroupName": "your__log__hroup__name",
        "LogStreamName": "your__log__stream__name"
    }
}
```

**Notes:**
- Replace secret values (keys, passwords, API secrets) with values appropriate for your environment.
- Use the user secrets store or environment variables for sensitive values in development/production.
- The sample includes `Jwt`, `ConnectionStrings`, `CloudinarySettings`, and `AWS` sections used by the application.

## Database & Migrations

If using EF Core:

- Add a migration (from solution root):

dotnet ef migrations add InitialCreate --project src/IMS.Infrastructure --startup-project src/IMS.API

- Apply migrations:

dotnet ef database update --project src/IMS.Infrastructure --startup-project src/IMS.API

Adjust provider, project, and startup project names to match your solution.

## Docker

A `Dockerfile` for the API and `docker-compose.yml` for a database make local development repeatable.

## API Docs & Examples

### Swagger / OpenAPI

- In development, the API typically exposes Swagger at `/swagger`.
- Keep controllers thin and map to application layer DTOs.

### Example curl Requests

Get items:

curl -k "https://localhost:5001/api/items" -H "accept: application/json"

Create a stock transaction:

curl -k -X POST "https://localhost:5001/api/stocktransactions" \
  -H "Content-Type: application/json" \
  -d '{"itemId":"123","quantity":10,"movementType":"Add"}'

Adjust routes and payloads to match your controllers.

## Testing

Run all tests from the repository root:

dotnet test

Guidance:

- Unit-test application and domain logic in isolation; use mocking libraries (Moq, NSubstitute).
- For integration tests, use ephemeral databases, in-memory providers, or testcontainers.
- Mark long-running or external-dependent tests to exclude from fast CI runs.

## CI / CD

Recommended basics for CI (GitHub Actions / Azure Pipelines):

- Run `dotnet restore`, `dotnet build`, and `dotnet test` on pull requests.
- Optionally run static analyzers and code-format checks.
- Deploy artifacts to staging and run migration steps as part of deployment.

## Coding Standards and Tooling

- Follow `.editorconfig` for formatting and analyzer severities (e.g., `IDE0320` is configured as a warning).
- Preserve existing code comment blocks when refactoring.
- Use Visual Studio or VS Code with the C# extension. Configure IDE to surface analyzer warnings (see __Tools__ > __Options__).

## Contributing

1. Fork the repository and create a feature branch.
2. Add tests for new behavior.
3. Run `dotnet build` and `dotnet test`.
4. Open a pull request with a clear description and any related issue references.

See `CONTRIBUTING.md` for CI, commit message, and PR requirements. If missing, request a CONTRIBUTING.md and I will generate one aligned with `.editorconfig`.

## Troubleshooting

- Build/restore errors: confirm .NET 9 SDK is installed and run `dotnet restore`.
- API not available: check console output for binding URLs and port conflicts.
- Migration failures: validate connection strings and database accessibility.
- Check Visual Studio __Output__ window or `dotnet run` console for detailed logs.

## FAQ

Q: Is this production-ready?

A: This repository is a starting point. Production readiness requires security hardening, monitoring, deployment pipelines, backups, and operational runbooks.

Q: Where should logging and telemetry be added?

A: Register logging and telemetry providers in the API startup and keep domain code free of infrastructure concerns.

## Roadmap

Planned enhancements:

- Pagination, filtering, and sorting for list endpoints
- Role-based authorization and policy-driven access
- Background processing for heavy or async operations
- Prometheus metrics, structured logging, and distributed tracing

## License

No license file is included. Add an appropriate `LICENSE` file if you plan to open-source the project.
