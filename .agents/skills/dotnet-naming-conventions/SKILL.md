---
name: dotnet-naming-conventions
description: Naming rules for C# and .NET symbols in this repository - namespaces, assemblies, projects, types, records, interfaces, enums, members, parameters, private fields, constants, async methods, generic type parameters, extension methods, options classes, request and response contracts, endpoint classes, background services, DI extensions, and test names. Use when creating or renaming any C# symbol, file, folder or project, and when reviewing a name for accuracy. Do not use for deciding responsibilities or splitting types; that is object-oriented-design.
---

# .NET naming conventions

## Inspect first

1. `.editorconfig` - the naming rules here are enforced by analyzers and
   `dotnet format`, so a violation breaks the build gate
2. A neighbouring file of the same kind, and follow it
3. The existing namespace of the folder you are adding to

## Core rules

| Element | Convention | Example in this repository |
| --- | --- | --- |
| Namespace | PascalCase, mirrors the folder path | `OrderAggregationService.Services` |
| Assembly / project | PascalCase, matches the root namespace | `OrderAggregationService` |
| Class, record, struct | PascalCase noun | `InMemoryOrderAggregator`, `OrderLine` |
| Interface | PascalCase with `I` prefix | `IOrderAggregator` |
| Enum type / member | PascalCase singular; plural only for `[Flags]` | `HealthStatus.Healthy` |
| Method | PascalCase verb phrase | `AggregateAsync`, `BuildOrderedItems` |
| Property | PascalCase noun | `TotalQuantity` |
| Event | PascalCase verb phrase | `BatchAccepted` |
| Parameter, local | camelCase | `cancellationToken`, `receivedAtUtc` |
| Private instance field | `_camelCase` | `_timeProvider`, `_products` |
| Constant, static readonly | PascalCase | `SectionName`, `SerializerOptions` |
| Generic type parameter | `T` prefix, descriptive when more than one | `TValue`, `TProblem` |
| Extension method class | `<Subject>Extensions` | `OrderAggregationServiceCollectionExtensions` |

## Rules that carry meaning

- **`Async` suffix** on every method returning `Task`, `Task<T>`, `ValueTask` or
  `ValueTask<T>`. Do not add it where a framework contract fixes the name, such as
  `ExecuteAsync` overrides or `CheckHealthAsync` - those already carry it - and
  never add it to a synchronous method.
- **`I` prefix only for interfaces.** Never prefix a class with `I`, and never use
  `Base` or `Abstract` prefixes; make the type `abstract` instead.
- **No Hungarian notation**, no type names in identifiers (`stringName`,
  `listOfOrders`), no `m_` or `s_` prefixes.
- **No invented abbreviations.** `quantity`, not `qty`. `aggregator`, not `agg`.
  Established acronyms stay: `Api`, `Http`, `Json`, `Utc`, `Id`. Two-letter
  acronyms are fully uppercase (`IO`); longer ones are PascalCase (`Http`).
- **Units and kind in the name** when ambiguity is possible: `DispatchInterval`
  (a `TimeSpan`), `ReceivedAtUtc` (a UTC `DateTimeOffset`), `MaxLinesPerRequest`.
- **Booleans** read as an assertion: `IsValid`, `HasPendingOrders`,
  `DispatchEnabled`.

## Role suffixes must be honest

Pick the suffix that matches what the type actually does. A name that overstates
its responsibility is a defect, not a style issue.

| Suffix | Reserved for |
| --- | --- |
| `...Request` / `...Response` | The wire contract of an endpoint |
| `...Options` | A class bound from configuration, with `SectionName` |
| `...Result` | The outcome of an operation, success or failure |
| `...Receipt` / `...Snapshot` | An immutable read model returned to a caller |
| `...Service` | Only when nothing more specific fits. Prefer the real role |
| `...Aggregator`, `...Dispatcher`, `...Validator` | The actual behaviour |
| `...Endpoints` | A static class mapping routes |
| `...BackgroundService` | A `BackgroundService` subclass |
| `...HealthCheck` | An `IHealthCheck` implementation |
| `...Extensions` | A static class of extension methods |

Do not add `Manager`, `Helper`, `Utility`, `Processor` or `Data` - they describe
nothing. If no honest name exists, the type is doing too much; see
`object-oriented-design`.

## Files, folders and namespaces

- One public type per file; the file name matches the type name.
- Folder path equals namespace suffix: `Services/Log.cs` is
  `OrderAggregationService.Services`.
- File-scoped namespace declarations only (`namespace X;`), enforced by
  `.editorconfig`.

## Test naming

- Test class: `<SystemUnderTest>Tests`, for example `InMemoryOrderAggregatorTests`.
- Test method: `Method_Scenario_ExpectedResult`, for example
  `AggregateAsync_SameProductAcrossBatches_SumsQuantities`.
- CA1707 is disabled for `tests/**/*.cs` precisely so these underscores are legal.
  Do not "fix" a test name by removing them.

## Verify

- `dotnet format --verify-no-changes` passes - it enforces the `.editorconfig`
  naming rules, including the `_` prefix and PascalCase constants
- `dotnet build --configuration Release` is warning-free
- Read each new name aloud against what the type does. If the name promises more
  than the type delivers, rename the type or reduce its responsibilities.

Detailed rationale and the full per-element table live in
`references/naming-reference.md` in this skill directory.
