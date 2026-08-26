# Order Aggregation Service

[![CI](https://github.com/vlastimilvajnorak/order-aggregation-service/actions/workflows/ci.yml/badge.svg)](https://github.com/vlastimilvajnorak/order-aggregation-service/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A .NET 10 service that accepts batches of order lines over HTTP, accumulates the ordered
quantity per product in a thread-safe store, and exposes the running totals through a REST API
and a small Blazor dashboard. The aggregate is prepared to be handed over to a downstream
system by a periodic dispatch pipeline.

## Project status

Working foundation, not a finished product. The aggregation, validation, API, dashboard,
health reporting, containerisation and CI are implemented and covered by tests. The periodic
dispatch pipeline is wired up end to end but ships with an inert dispatcher and is disabled by
default, because no downstream integration exists yet. See
[Future improvements](#future-improvements) for what a production rollout would still need.

## Technology stack

| Concern | Choice |
| --- | --- |
| Language | C# 14 |
| Runtime | .NET 10 (`net10.0`) |
| Web framework | ASP.NET Core Minimal APIs |
| UI | Blazor Web App with interactive server components |
| API documentation | Built-in OpenAPI (`Microsoft.AspNetCore.OpenApi`) |
| Testing | xUnit, `WebApplicationFactory` integration tests |
| Container | Multi-stage Dockerfile, non-root runtime user |
| CI | GitHub Actions |

Cross-cutting build settings live in `Directory.Build.props` (nullable reference types,
implicit usings, deterministic builds, .NET analyzers, warnings as errors) and all NuGet
versions are pinned centrally in `Directory.Packages.props`.

## Architecture overview

The service is a single deployable ASP.NET Core application that hosts both the REST API and
the Blazor UI. Inside it, three seams keep the layers independent:

```
HTTP request
     │
     ▼
Endpoints/OrderEndpoints ──▶ Endpoints/OrderBatchValidator   (pure validation, no HTTP context)
     │                              │
     │                              ▼
     │                       Models/OrderLine                (validated domain value)
     ▼
Services/IOrderAggregator  ◀── the only contract the API layer depends on
     │
     ├── Services/InMemoryOrderAggregator   (thread-safe, process-local)
     │
     ▼
Services/OrderDispatchBackgroundService ──▶ Services/IOrderDispatcher
                                                   │
                                                   └── Services/LoggingOrderDispatcher
```

- **`IOrderAggregator`** is the single aggregation contract. Every member is asynchronous and
  takes a `CancellationToken`, so replacing `InMemoryOrderAggregator` with a database-backed or
  distributed implementation is a registration change in
  `OrderAggregationServiceCollectionExtensions` — the endpoints and the Blazor components do
  not change.
- **`InMemoryOrderAggregator`** guards a dictionary of per-product counters with a single
  `System.Threading.Lock`. The critical sections are a few integer additions, which keeps
  contention negligible while making snapshots and drains genuinely atomic.
- **`IOrderDispatcher`** is the seam for the eventual downstream integration. The background
  service drains the aggregate on a fixed interval and hands the result over; the shipped
  implementation only logs what it received.
- **Validation** is a pure function returning either the projected domain lines or an error
  dictionary shaped for `ValidationProblemDetails`, so it is unit tested without an HTTP host.
- **Errors** are split by path: requests under `/api` produce RFC 9457 problem details, while
  browser traffic gets the Blazor error and not-found pages.

## Repository structure

```
.
├── .agents/skills/                # Canonical agent skills, read directly by Codex
├── .claude/skills/                # Generated mirror for Claude Code, do not edit
├── .github/workflows/
│   ├── ci.yml                     # Build and test on every push and pull request
│   └── pr-policy.yml              # Enforces that master only accepts PRs from develop
├── docs/
│   ├── requirements.md            # Authoritative specification and acceptance criteria
│   └── ai-agent-development.md    # How Codex and Claude Code are configured
├── scripts/
│   └── validate-agent-skills.py   # Validates the skills and syncs the mirror
├── src/OrderAggregationService/
│   ├── Components/                # Blazor layout and pages (Home, Dashboard, Error, NotFound)
│   ├── Endpoints/                 # Minimal API endpoints, validation, health endpoint
│   ├── Models/                    # Request, response and domain records
│   ├── Services/                  # Aggregator, dispatcher, background service, options, logs
│   ├── wwwroot/                   # Static assets
│   ├── Program.cs
│   ├── appsettings.json
│   └── OrderAggregationService.csproj
├── tests/OrderAggregationService.Tests/
│   ├── InMemoryOrderAggregatorTests.cs
│   ├── OrderBatchValidatorTests.cs
│   ├── OrderApiIntegrationTests.cs
│   └── OrderAggregationService.Tests.csproj
├── .editorconfig
├── .dockerignore
├── .gitignore
├── AGENTS.md                      # Working agreements for AI coding agents
├── CLAUDE.md                      # Imports AGENTS.md, adds Claude Code specifics
├── Directory.Build.props
├── Directory.Packages.props
├── Dockerfile
├── docker-compose.yml
├── LICENSE
├── README.md
└── OrderAggregationService.slnx
```

## Requirements for local development

- [.NET SDK 10.0](https://dotnet.microsoft.com/download/dotnet/10.0) or newer
- Optionally Docker (Engine 20.10+) to build and run the container
- Any editor with C# support: Visual Studio 2026, VS Code with the C# Dev Kit, or JetBrains
  Rider

## Running the application

```bash
dotnet run --project src/OrderAggregationService
```

The launch profiles bind to `http://localhost:5212` and `https://localhost:7078`. Once the app
is up:

| Address | What it serves |
| --- | --- |
| `/` | Landing page with a quick-start example |
| `/dashboard` | Interactive server-rendered dashboard, refreshing every two seconds |
| `/openapi/v1.json` | OpenAPI document |
| `/health` | Health report as JSON |

## Running the tests

```bash
dotnet test
```

The suite mixes unit tests over the aggregator and the validator with `WebApplicationFactory`
integration tests over the real HTTP pipeline. Each integration test boots and disposes its own
application instance, so no state is shared between tests and the suite is order independent.

To reproduce the full local verification the CI pipeline is derived from:

```bash
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
dotnet format --verify-no-changes
```

## Running with Docker

```bash
docker compose up --build
```

The service is then reachable on `http://localhost:8080`. To build and run the image directly:

```bash
docker build --tag order-aggregation-service:local .
```

```bash
docker run --rm --publish 8080:8080 order-aggregation-service:local
```

The runtime image drops to the non-root `app` user provided by the .NET base image and declares
a `HEALTHCHECK` that probes `/health`.

## API

| Method | Route | Purpose | Success |
| --- | --- | --- | --- |
| `POST` | `/api/orders` | Submit a batch of order lines for aggregation | `202 Accepted` |
| `GET` | `/api/orders/aggregates` | Read the current aggregate | `200 OK` |
| `GET` | `/health` | Health report | `200 OK` |
| `GET` | `/openapi/v1.json` | OpenAPI document | `200 OK` |

### Validation rules

A submitted batch is rejected as a whole when any rule is violated:

- the body must be a JSON array with at least one order line;
- `productId` must be present and must not be empty or whitespace;
- `quantity` must be greater than zero;
- the batch must not exceed `OrderAggregation:MaxLinesPerRequest` lines (1000 by default).

### Example request

```bash
curl -i -X POST http://localhost:5212/api/orders \
  -H "Content-Type: application/json" \
  -d '[{"productId":"456","quantity":5},{"productId":"789","quantity":42}]'
```

### Expected response

`202 Accepted`, with a `Location` header pointing at `/api/orders/aggregates`:

```json
{
  "batchId": "019906f2-9c1e-7a3d-8f41-2b7c5d3e9a10",
  "acceptedLineCount": 2,
  "acceptedQuantity": 47,
  "receivedAtUtc": "2026-08-25T10:00:00+00:00"
}
```

Reading the aggregate back:

```bash
curl http://localhost:5212/api/orders/aggregates
```

```json
{
  "generatedAtUtc": "2026-08-25T10:00:05+00:00",
  "productCount": 2,
  "totalQuantity": 47,
  "acceptedBatchCount": 1,
  "acceptedLineCount": 2,
  "items": [
    {
      "productId": "456",
      "totalQuantity": 5,
      "lineCount": 1,
      "firstSeenUtc": "2026-08-25T10:00:00+00:00",
      "lastUpdatedUtc": "2026-08-25T10:00:00+00:00"
    },
    {
      "productId": "789",
      "totalQuantity": 42,
      "lineCount": 1,
      "firstSeenUtc": "2026-08-25T10:00:00+00:00",
      "lastUpdatedUtc": "2026-08-25T10:00:00+00:00"
    }
  ]
}
```

An invalid batch produces `400 Bad Request` as RFC 9457 problem details:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Invalid order batch",
  "status": 400,
  "detail": "One or more order lines were rejected.",
  "errors": {
    "[0].quantity": ["quantity must be greater than zero."],
    "[1].productId": ["productId is required and must not be empty."]
  }
}
```

## Configuration

All settings live under the `OrderAggregation` section and can be overridden with environment
variables using `OrderAggregation__Key`.

| Key | Default | Meaning |
| --- | --- | --- |
| `DispatchEnabled` | `false` | Enables the periodic dispatch background service |
| `DispatchInterval` | `00:00:30` | Interval between two dispatch cycles |
| `MaxLinesPerRequest` | `1000` | Maximum number of order lines accepted per request |

Logging is structured: production writes JSON to the console with scopes included, development
uses the single-line readable formatter. Log messages are generated at compile time with
`[LoggerMessage]`, so their named placeholders reach structured sinks as separate fields.

> Enabling `DispatchEnabled` with the shipped `LoggingOrderDispatcher` will drain the aggregate
> on every cycle and only write it to the log. Turn it on once a real `IOrderDispatcher` is
> registered.

## Branching model and CI

| Branch | Role | Rules |
| --- | --- | --- |
| `develop` | Default branch, integration target | Changes only via pull request, no approvals required, all review threads must be resolved, CI must pass |
| `master` | Stable branch | Changes only via pull request **from `develop`**, no review required, CI and the source-branch check must pass |
| `main` | Not used | Creation and updates are blocked by a repository ruleset |

The `CI` workflow validates the agent skills, restores, builds in `Release` and runs the tests
on every push and pull request to `develop` and `master`, and uploads the `.trx` test results
as an artifact. The `PR policy` workflow runs only on pull requests into `master` and fails
when the source branch is not `develop`; it is registered as a required status check so the
rule cannot be skipped.

## AI agent development

Codex and Claude Code are configured from `AGENTS.md` and version-controlled skills. See
[docs/ai-agent-development.md](docs/ai-agent-development.md).

## Future improvements

- **Durable persistence.** Replace `InMemoryOrderAggregator` with a store that survives a
  restart, for example PostgreSQL with an upsert per product, or Redis with `HINCRBY`. The
  `IOrderAggregator` contract is already async and cancellable so no caller changes.
- **A real dispatcher.** Implement `IOrderDispatcher` against the actual downstream system and
  add retries with exponential backoff, a dead-letter path for permanently failing batches, and
  an outbox so a crash between drain and dispatch cannot lose aggregates.
- **Horizontal scale-out.** The current drain-and-dispatch cycle assumes a single instance.
  Running several replicas needs either a shared store with a distributed lock or a leader
  election so only one instance dispatches.
- **Idempotency.** Accept a client-supplied idempotency key on `POST /api/orders` so retried
  submissions cannot be counted twice.
- **Authentication and rate limiting.** The API is currently open; production would need
  authentication, per-client authorisation and ASP.NET Core rate limiting.
- **Observability.** Add OpenTelemetry traces and metrics (accepted batches, rejected batches,
  dispatch latency, backlog size) and export them to the target monitoring stack.
- **Richer health reporting.** Split the current single check into liveness and readiness
  probes once external dependencies exist.
- **Container hardening.** Move to a chiselled or distroless runtime image and replace the
  curl-based `HEALTHCHECK` with a dependency-free probe.

## About this repository

This repository is an implementation of a technical assignment. It is a personal project and is
not affiliated with, endorsed by, or an official product of any company.

## License

Released under the [MIT License](LICENSE).
