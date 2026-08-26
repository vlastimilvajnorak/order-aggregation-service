# Naming reference

Supporting detail for the `dotnet-naming-conventions` skill. Read the skill first;
come here only when a case is not covered there.

## Authoritative sources

Verified 2026-08-26:

- C# identifier naming rules and conventions -
  <https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/identifier-names>
- C# coding conventions -
  <https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions>
- Framework design guidelines, naming -
  <https://learn.microsoft.com/dotnet/standard/design-guidelines/naming-guidelines>
- .NET code-style rules enforced by analyzers -
  <https://learn.microsoft.com/dotnet/fundamentals/code-analysis/code-style-rule-options>
- ASP.NET Core Razor components, naming and namespaces -
  <https://learn.microsoft.com/aspnet/core/blazor/components/>
- ASP.NET Core Blazor data binding, the `@bind-{PROPERTY}` contract -
  <https://learn.microsoft.com/aspnet/core/blazor/components/data-binding>

Where this repository's `.editorconfig` is stricter than the guidelines, the
`.editorconfig` wins, because it is enforced by the build.

## Casing by element

| Element | Casing | Notes |
| --- | --- | --- |
| Namespace | PascalCase | Dot-separated, mirrors folders |
| Assembly | PascalCase | Equals the root namespace |
| Project directory | PascalCase | Equals the project name |
| Class | PascalCase | Noun or noun phrase |
| Record (class) | PascalCase | Noun; used for immutable data here |
| Record struct / struct | PascalCase | Keep small and immutable |
| Interface | PascalCase, `I` prefix | Capability or role |
| Delegate | PascalCase | Verb phrase, or `...Handler` |
| Enum | PascalCase | Singular unless `[Flags]` |
| Enum member | PascalCase | No type-name prefix |
| Method | PascalCase | Verb phrase |
| Local function | PascalCase | Same as a method |
| Property | PascalCase | Noun; not a verb |
| Indexer | `this[...]` | Named `Item` in metadata |
| Event | PascalCase | Verb phrase, present or past tense |
| Field, private instance | `_camelCase` | Enforced by `.editorconfig` |
| Field, private static | PascalCase when `static readonly`; otherwise `_camelCase` | The naming rule matches `static, readonly` first |
| Field, const | PascalCase | Any accessibility |
| Parameter | camelCase | No prefix |
| Local variable | camelCase | No prefix |
| Generic type parameter | `T` or `T<Descriptive>` | Single parameter may be plain `T` |
| Type parameter constraint | n/a | Prefer descriptive names over `T1`, `T2` |
| Extension method container | `<Subject>Extensions` | Static, non-generic |
| Attribute | `...Attribute` | Used without the suffix |
| Exception | `...Exception` | Derive from `Exception` |

## Acronyms

- Two letters: both uppercase - `IO`, `ID` is written `Id` because it is a word,
  not an acronym.
- Three or more letters: PascalCase - `Http`, `Json`, `Xml`, `Api`, `Utc`.
- camelCase positions lowercase the whole acronym - `httpClient`, `apiBaseUrl`.

## Async naming detail

Add `Async` when the method returns an awaitable and the name is yours to choose.

Do not add `Async` when:

- overriding or implementing a framework member whose name is fixed
  (`ExecuteAsync`, `CheckHealthAsync`, `DisposeAsync`, `OnInitializedAsync`)
- the member is a property or field returning a `Task`
- the method is synchronous, even if it returns a completed `Task`

Do not create sync/async pairs (`Aggregate` and `AggregateAsync`) unless both are
genuinely needed. This repository is async-only below the endpoint layer.

## Distinguishing similar roles

| Concept | Meaning | Example |
| --- | --- | --- |
| Entity | Has identity and lifecycle | not yet present here |
| Value object | Defined entirely by its values, immutable | `OrderLine` |
| Request contract | What a client sends | `OrderItemRequest` |
| Response contract | What the API returns | `OrderBatchReceipt` |
| Read model / snapshot | Point-in-time projection | `AggregationSnapshot`, `AggregatedOrderItem` |
| Command | An instruction to change state | not yet present here |
| Result | Outcome including failure | `OrderBatchValidationResult` |
| Options | Bound configuration | `OrderAggregationOptions` |
| Service abstraction | A role another type depends on | `IOrderAggregator`, `IOrderDispatcher` |

When a new type does not fit one of these, that is a signal to reconsider the
design before inventing a new suffix.

## Razor component naming detail

Framework requirements, enforced by the Razor compiler:

- The component type name is the `.razor` file name. Renaming the file renames the
  type, and every `<Tag />` usage must change with it.
- The first character of the file name must be uppercase. `productDetail.razor` does
  not compile as a component.
- The namespace comes from the folder path under the project root namespace, unless
  an `@namespace` directive overrides it. Moving a component between folders changes
  its namespace and can break a `@using` elsewhere.

The two-way binding contract, from the data-binding documentation:

- `@bind-Year="year"` on a child component requires the child to declare a
  `[Parameter] public int Year { get; set; }` and a
  `[Parameter] public EventCallback<int> YearChanged { get; set; }`.
- `@bind-{PROPERTY}` is equivalent to writing
  `@bind-{PROPERTY}:event="{PROPERTY}Changed"`, so the `Changed` suffix is the
  default the framework looks for, not a style choice.
- Name a callback that is not part of a binding pair `On<Event>`
  (`OnRefreshRequested`), so it reads as an action rather than a value change.

Conventions worth holding to in this repository:

- Routable components use the kebab-case form of their name in `@page`, so
  `Dashboard.razor` routes at `/dashboard`.
- Fields inside `@code` follow the same `_camelCase` rule as any other private
  field. `Dashboard.razor` already does this; keep it that way.
- Keep a component single-file while its logic stays small. Move to
  `<Component>.razor.cs` with a `partial class` when the `@code` block outgrows the
  markup, not as a default.

## Names to reject in review

- `Manager`, `Helper`, `Utility`, `Utils`, `Common`, `Shared`, `Misc`, `Data`,
  `Info`, `Processor`, `Handler` when nothing is being handled
- `DoWork`, `Process`, `Execute` on a public API without an object
- `tmp`, `res`, `val`, `obj`, `x` outside a two-line lambda
- `data`, `item`, `thing` where the domain has a real word
- Any name that contains the type: `orderList`, `stringProductId`, `intQuantity`
- Any name that survives from a copy-paste and no longer describes the code
