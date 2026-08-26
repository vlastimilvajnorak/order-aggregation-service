---
name: dotnet-10-development
description: Production rules for the .NET 10 and ASP.NET Core code in this repository - target framework and language level, central package management, dependency injection lifetimes, hosted services, options with startup validation, structured logging, cancellation and async correctness, TimeProvider, ProblemDetails, health checks, OpenAPI, and the required clean Release build. Use when adding or changing .cs files, csproj, slnx, Directory.Build.props, Directory.Packages.props, Program.cs, endpoints, services, configuration or NuGet packages. Do not use for naming decisions, Razor components or test authoring; those have their own skills.
---

# .NET 10 development

## Actual solution layout

```text
OrderAggregationService.slnx
src/OrderAggregationService/          Blazor Web App + minimal API in one host
  Components/  Endpoints/  Models/  Services/  wwwroot/
tests/OrderAggregationService.Tests/  xUnit unit + integration tests
```

This is a deliberately flat layout for the current scope. Do **not** split it into
`Domain`/`Application`/`Infrastructure` projects unless the user asks. If that split
ever happens, project references must point inward only: presentation to
application, application to domain, infrastructure to domain.

## Inspect first

1. `Directory.Build.props` - shared compiler and analyzer settings
2. `Directory.Packages.props` - every NuGet version lives here
3. `src/OrderAggregationService/Program.cs` - composition root and pipeline order
4. `src/OrderAggregationService/Services/OrderAggregationServiceCollectionExtensions.cs` - registrations
5. The nearest existing file of the same kind, and match its shape

## Project and package rules

- Every project declares `<TargetFramework>net10.0</TargetFramework>` explicitly.
- Use the current stable C# shipped with the .NET 10 SDK. Do not set
  `<LangVersion>preview</LangVersion>`.
- Central package management is on. Add a `PackageVersion` to
  `Directory.Packages.props` and a `PackageReference` **without** `Version` to the
  csproj. A `Version` attribute in a csproj is an error here.
- No prerelease or preview packages without the user agreeing to it in the task.
- Nullable reference types and implicit usings are enabled repository-wide in
  `Directory.Build.props`. Do not disable them per project, and do not silence a
  nullable warning with `!` when the real fix is a null check.
- Warnings are errors. `WarningsNotAsErrors` covers NuGet audit advisories only.
  Fix the warning; do not widen the exclusion list to make a build pass.

## Composition and lifetimes

- Register services through the `AddOrderAggregation` extension, not inline in
  `Program.cs`, so registration stays in one reviewable place.
- Singleton for process-wide state such as the aggregator. Scoped for per-request
  work. Transient for cheap stateless helpers.
- Never inject a scoped service into a singleton. A `BackgroundService` is a
  singleton: to reach scoped work, inject `IServiceScopeFactory` and create a scope
  per cycle.
- Do not resolve services from `IServiceProvider` inside business code. Constructor
  injection only.
- Guard every injected dependency with `ArgumentNullException.ThrowIfNull`.

## Async and cancellation

- Async all the way. No `.Result`, no `.Wait()`, no `.GetAwaiter().GetResult()`.
- Do not wrap synchronous work in `Task.Run` on the server; it moves work between
  threads without adding throughput.
- Every method that can block accepts a `CancellationToken` and passes it on.
  Public abstractions here default it: `CancellationToken cancellationToken = default`.
- In library-style code use `.ConfigureAwait(false)`.
- In a `BackgroundService` loop, catch `OperationCanceledException` only when
  `stoppingToken.IsCancellationRequested`, and let a failing cycle log and continue
  rather than terminate the loop.
- Use `PeriodicTimer` with the injected `TimeProvider` for periodic work.

## Time

- Inject `TimeProvider` and call `GetUtcNow()`. Never call `DateTime.Now`,
  `DateTime.UtcNow` or `DateTimeOffset.UtcNow` in code that has behaviour worth
  testing.
- Store and expose operational timestamps as `DateTimeOffset` in UTC.

## Configuration and options

- Bind a section to a strongly typed options class, validate it, and fail at
  startup:

  ```csharp
  services.AddOptions<OrderAggregationOptions>()
      .Bind(configuration.GetSection(OrderAggregationOptions.SectionName))
      .Validate(static o => o.DispatchInterval > TimeSpan.Zero, "...must be greater than zero.")
      .ValidateOnStart();
  ```

- Inject `IOptions<T>` for singleton-lifetime values, `IOptionsMonitor<T>` only when
  the value must change at runtime.
- Environment variables override with `Section__Key`. Never read configuration by
  string key from business code.

## Logging

- Use the `[LoggerMessage]` source generator in `Services/Log.cs`. Do not call
  `_logger.LogInformation($"...")` - interpolation destroys the structured fields.
- Log the identifiers a reader needs to correlate the event. Never log request
  bodies, secrets, tokens or connection strings.
- Warning and above for anything that needs action; Information for lifecycle
  events; Debug for per-cycle detail.

## HTTP surface

- Failures under `/api` return RFC 9457 problem details. Validation failures return
  `TypedResults.ValidationProblem` with the offending member as the key.
- Return `TypedResults.*` from handlers and declare the union with
  `Results<TOk, TProblem>` so OpenAPI metadata is derived from the signature.
- Keep endpoint handlers thin: validate, delegate, map the result. Business rules
  belong in `Services/`.
- Health lives at `/health`; the OpenAPI document at `/openapi/v1.json`.
- Middleware order in `Program.cs` is load-bearing. Read the whole pipeline before
  inserting anything.

## Disposal

- Anything holding a timer, a `CancellationTokenSource` or an unmanaged handle
  implements `IDisposable` or `IAsyncDisposable` and is disposed on every path.
- Prefer `using var`. Do not dispose a service the container owns.

## Verify

Run from the repository root, in this order:

```bash
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
dotnet format --verify-no-changes
```

All four must pass. A Release build must produce **zero warnings**. If `dotnet
format` reports changes, fix the code; do not relax `.editorconfig` to match it.

## Antipatterns

- Adding a NuGet package for something the framework already provides
- `async void` outside an event handler
- Catching `Exception` and swallowing it without logging
- Blocking on async code to avoid changing a signature
- Static mutable state as a substitute for dependency injection
- Suppressing an analyzer with `#pragma warning disable` without a comment saying why

Official references verified 2026-08-26: <https://learn.microsoft.com/dotnet/>,
<https://learn.microsoft.com/aspnet/core/>.
