---
name: rest-api-design
description: HTTP contract rules for the REST API in this repository, following the Microsoft RESTful web API design guidance - resource URI naming, verb semantics and idempotency, which status code each outcome returns, RFC 9457 problem details, validation error shape, request and response contracts, pagination and filtering, versioning, caching and concurrency, and OpenAPI documentation. Use when adding or changing an endpoint, a route, a request or response contract, a status code, an error response, or the OpenAPI document. Do not use for framework wiring such as dependency injection lifetimes or hosted services, and do not use for Razor components.
---

# REST API design

## Current API surface

| Method | Route | Success | Notes |
| --- | --- | --- | --- |
| `POST` | `/api/orders` | `202 Accepted` + `Location` | Submits a batch, returns `OrderBatchReceipt` |
| `GET` | `/api/orders/aggregates` | `200 OK` | Returns `AggregationSnapshot` |
| `GET` | `/health` | `200 OK` | JSON health report, outside `/api` |
| `GET` | `/openapi/v1.json` | `200 OK` | Generated OpenAPI document |

## Inspect first

0. `docs/requirements.md` - the authoritative specification. The submission endpoint
   must accept **one or more** order lines in a single request, and a rejected
   request must change no state.
1. `src/OrderAggregationService/Endpoints/OrderEndpoints.cs` - route constants and
   handler signatures
2. `src/OrderAggregationService/Endpoints/OrderBatchValidator.cs` - the error key
   shape every 400 must follow
3. `src/OrderAggregationService/Models/` - the existing contracts
4. `src/OrderAggregationService/Program.cs` - `AddProblemDetails`, `AddOpenApi` and
   the pipeline split between `/api` and browser traffic
5. `README.md`, the API section - it documents the contract and must stay true

## Resource URIs

- Name resources with **nouns**, never verbs. The method already carries the action:
  `POST /api/orders`, not `/api/create-order`.
- Use **plural nouns for collections**: `/api/orders`, and `/api/orders/{id}` for a
  single item.
- Keep the hierarchy shallow. Prefer `/api/orders/{id}` over
  `/api/customers/{customerId}/orders/{orderId}/lines`. Two levels is usually the
  limit before the URI becomes hard to evolve.
- Lowercase, hyphen-separated segments. No file extensions, no trailing slash.
- Every route lives under `/api`, because the pipeline routes `/api` failures to
  problem details and everything else to the Blazor error page.
- Declare each route as a `const` on the endpoints class and reuse it, so the route
  and the `Location` header cannot drift apart.

## Verbs, idempotency and status codes

| Verb | Meaning | Idempotent | Typical success | Typical failure |
| --- | --- | --- | --- | --- |
| `GET` | Read, no side effects | yes | `200`, `204` when genuinely empty | `404` |
| `POST` | Create in a collection, or submit for processing | **no** | `201` + `Location` when a resource is created, `202` when accepted for later processing, `200` when it processed and created nothing | `400`, `405` |
| `PUT` | Replace an item with a full representation | **yes** | `200`, `201`, `204` | `400`, `404`, `409` |
| `PATCH` | Partial update | no | `200`, `204` | `400`, `404`, `409` |
| `DELETE` | Remove | yes | `204` | `404` |

Rules that follow from this:

- A client never invents the URI of a new resource. Submit to the collection and let
  the server assign it.
- `PUT` must be safe to repeat: the same request twice leaves the same state.
  If your handler cannot promise that, it is a `POST`.
- Never return `200` with an error payload inside. The status code is the outcome.
- Never return `500` for something the caller did wrong.
- `204` means there is genuinely nothing to send, not "an empty list". A collection
  with no items is `200` with an empty array.

`POST /api/orders` answers `202 Accepted` because the batch is accumulated and
handed on later rather than becoming an addressable resource. Keep it that way
unless the dispatch model changes.

## Errors

- Every failure under `/api` is an RFC 9457 problem details document. This is wired
  up by `AddProblemDetails()` plus the `/api` branch of `UseExceptionHandler`.
- Validation failures use `TypedResults.ValidationProblem(errors, detail, title)`.
- The error dictionary key identifies the **offending member**, using the shape the
  validator already produces: `request` for a whole-batch problem, `[0].productId`
  and `[0].quantity` for a specific line. A caller must be able to map the key back
  to what they sent.
- Never put an exception message, stack trace, connection string or internal type
  name into a response.
- Validate the whole request and report every error at once. Do not stop at the
  first bad line.

## Contracts

- Request and response types are `sealed record` types in `Models/`, immutable, with
  no behaviour.
- The wire format is camelCase JSON; that is the ASP.NET Core default and no
  handler should override it.
- Keep the request contract separate from the domain type. `OrderItemRequest` is
  nullable and unvalidated on purpose; `OrderLine` is the validated domain value.
- Return a purpose-built read model, never an internal entity.
- Adding an optional property is backwards compatible. Removing or renaming one, or
  making an optional property required, is a breaking change and needs the
  `BREAKING CHANGE` footer on the commit.

## Handlers

- Return `TypedResults.*` and declare the union in the signature, for example
  `Results<Accepted<OrderBatchReceipt>, ValidationProblem>`, so the OpenAPI
  metadata comes from the type rather than from hand-written attributes.
- Keep the handler thin: validate, delegate to a service, map the outcome.
- Accept a `CancellationToken` and pass it down.
- Bind the body to a concrete type; do not read the raw stream.

## Collections

The current collections are small enough to return whole. Before returning a
collection that can grow without bound:

- Page it with `limit` and `offset` query parameters, and return the page plus the
  total count.
- Filter and sort through query parameters, never through new routes.
- Give every list endpoint a stable, documented default order. `GetSnapshotAsync`
  already orders by product identifier; keep that guarantee.

## Versioning

The API is unversioned today, which is fine while it has no external consumers.
When one appears, add a version rather than breaking the shape. Microsoft describes
URI, query-string, header and media-type versioning; pick one and apply it to the
whole API. Do not version individual endpoints differently, and do not change the
meaning of an existing field in place.

## Caching and concurrency

- `GET` responses that are expensive and cacheable get `Cache-Control` and an `ETag`.
- Anything that mutates state and can be raced needs `If-Match` with the `ETag` and
  answers `409 Conflict` or `412 Precondition Failed`.
- Do not add either until there is a real need; adding an `ETag` you never validate
  is worse than none.

## OpenAPI

- The document is generated from the handler signatures. Keep `WithName`,
  `WithSummary` and `WithDescription` accurate when you change behaviour.
- `WithName` values must be unique and stable; they become client method names.
- Verify the document still describes reality with the existing integration test
  that fetches `/openapi/v1.json`.

## Antipatterns

- A verb in a URI (`/api/orders/create`, `/api/getOrders`)
- `200 OK` carrying `{"success": false}`
- A generic `500` for a validation failure
- Returning a domain type directly, so an internal rename becomes a wire break
- An error body that is a bare string instead of problem details
- A new route added because a query parameter felt awkward
- Documenting behaviour in the README that the handler does not implement

## Verify

```bash
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
```

Then confirm:

- an integration test covers the new status code and, for a 400, the exact error key
- `/openapi/v1.json` still loads and contains the route
- the README API section matches the implemented contract
- no existing response shape changed without a `BREAKING CHANGE` note

Official references verified 2026-08-26:
<https://learn.microsoft.com/azure/architecture/best-practices/api-design>,
<https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis>,
<https://learn.microsoft.com/aspnet/core/web-api/handle-errors>.
