# Requirements

The authoritative specification for this service. When code, tests, the README or a
skill disagree with this document, this document wins and the other is a defect.

## Scope

A web service that accepts orders over HTTP, accumulates the ordered quantity
per product, and hands the accumulated totals to a downstream system on a bounded
schedule.

## Functional requirements

| Id | Requirement |
| --- | --- |
| FR-1 | Expose a REST endpoint that accepts **one or more** orders in a single request, as a JSON array of `{ "productId": string, "quantity": number }`. |
| FR-2 | Accumulate the ordered quantity per `productId` across every accepted request. |
| FR-3 | Hand the accumulated totals to the downstream system **no more often than once every 20 seconds**. |
| FR-4 | Until a real downstream integration exists, the hand-over serializes the aggregated payload as **JSON to the console**. |
| FR-5 | The persistence mechanism must be **extensible and selectable through configuration**. An in-memory implementation satisfies the current scope. |
| FR-6 | Reject invalid input: an empty request, a missing or empty `productId`, or a `quantity` that is not greater than zero. |

## Non-functional requirements

| Id | Requirement |
| --- | --- |
| NFR-1 | Sustain **hundreds of small orders per second**. |
| NFR-2 | Handle a **bounded product catalogue, on the order of hundreds of distinct product ids**. The aggregate fits comfortably in memory; optimise for write throughput, not for cardinality. |
| NFR-3 | Production-quality code: tests, structured logging, health reporting, clean Release build with no warnings. |
| NFR-4 | Accumulation must be correct under concurrent writers. No lost updates. |

## Invariants

These are the rules that must never be violated, whatever the implementation:

1. **The 20-second floor is a minimum interval, not a target.** A burst of orders
   must never trigger an earlier hand-over. Two consecutive hand-overs are at least
   20 seconds apart.
2. **A quantity is counted exactly once.** An order that is accepted contributes to
   exactly one hand-over. A rejected request contributes nothing.
3. **A rejected request changes no state.** Validation happens before accumulation,
   and a batch is accepted or rejected as a whole.
4. **The API layer does not know how the aggregate is stored.** Swapping the store
   must not change any endpoint, contract or component.

## Acceptance criteria

- Posting two batches that mention the same `productId` yields the summed quantity.
- Different product ids accumulate independently.
- A request containing any invalid order is rejected with per-order validation errors and
  leaves the aggregate untouched.
- Concurrent submissions from many writers produce exact totals.
- With a controlled clock, no hand-over occurs before 20 seconds have elapsed, and a
  burst inside that window produces exactly one hand-over.
- The hand-over emits the aggregated payload as JSON.
- Selecting a different store through configuration changes no endpoint behaviour.

## Design decisions still open

| Decision | Options | Recommendation |
| --- | --- | --- |
| Delivery guarantee on hand-over failure | drain-then-send loses the batch if the send throws; peek-send-commit keeps it | Peek, send, then commit. The current drain-then-send is the known weakness. |
| Console output shape | the drained aggregate as-is, or a dedicated envelope | A dedicated payload record, so the console format is a stable contract rather than an internal model |
| Backpressure at the endpoint | none, or a bounded queue rejecting with `429` | None while NFR-1 is met by direct accumulation; revisit only with evidence |

## Decisions taken

| Decision | Options considered | Resolution |
| --- | --- | --- |
| Store selection | configuration key naming the implementation, or a registration extension per store | An `OrderPersistence` section with its own options class, `Provider` bound to a validated enum, resolved in `AddOrderAggregation` |
| Sharing the accumulation algorithm between stores | composition behind a persistence interface, or a template method | `OrderAggregatorBase` owns the algorithm and exposes three hooks. The algorithm is fixed; only *where* state lives varies, so a store cannot change how quantities aggregate |
| Shape of the persistence hooks | hand the store a full snapshot after every change, or hand it the delta | Deltas. The provider this seam exists for is a database, where an accepted request is an upsert (`quantity = quantity + @delta`) and a hand-over is a delete; a full rewrite per request would not survive the design load |
| How many providers ship | in-memory only, or a second concrete provider as proof | In-memory only. FR-5 says in-memory is sufficient, so the deliverable is the abstraction, its configuration section and a skeleton showing where an implementation plugs in - not a second working store, and no data-access dependency |

### Error contract

| Decision | Options considered | Resolution |
| --- | --- | --- |
| A body the server cannot parse | let it surface as 500, or map it to 400 | 400. `ThrowOnBadRequest` is pinned to `false` so the behaviour is the same in every environment, and problem details explain what was expected |
| Statuses the framework produces itself (404, 405, 415) | leave the empty body, or fill it in | `UseStatusCodePages` on the machine-endpoint branch, so an API that promises problem details keeps that promise for responses it did not write |
| Which paths get problem details | `/api` only, or every machine-read path | `/api`, `/health` and `/openapi`. Answering an orchestrator with the Blazor error page would hand a machine HTML where it expects a document |
| Unbounded product identifiers | accept any string, or bound the length | Bounded (`MaxProductIdLength`, 64). Every distinct identifier costs an entry until the next hand-over, so an unbounded one lets a caller grow the aggregate by the size of what it sends rather than by the orders it places |

## Out of scope

- A real downstream integration, authentication, rate limiting and multi-instance
  coordination. Name them as future improvements rather than implementing them.
- Splitting the solution into `Domain` / `Application` / `Infrastructure` projects.
  The flat layout is deliberate for the current scope; see `AGENTS.md`.

## Known deltas from the current implementation

None. Every functional requirement and invariant above is implemented and covered by
tests. When a future change opens a gap, record it here rather than leaving it only in
a commit message.

The one design decision still carrying risk is the delivery guarantee: the cycle
drains and then dispatches, so a hand-over that throws loses that batch. See the
decision table above.
