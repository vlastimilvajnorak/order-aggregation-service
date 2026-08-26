# Requirements

The authoritative specification for this service. When code, tests, the README or a
skill disagree with this document, this document wins and the other is a defect.

## Scope

A web service that accepts order lines over HTTP, accumulates the ordered quantity
per product, and hands the accumulated totals to a downstream system on a bounded
schedule.

## Functional requirements

| Id | Requirement |
| --- | --- |
| FR-1 | Expose a REST endpoint that accepts **one or more** order lines in a single request, as a JSON array of `{ "productId": string, "quantity": number }`. |
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
2. **A quantity is counted exactly once.** A line that is accepted contributes to
   exactly one hand-over. A rejected request contributes nothing.
3. **A rejected request changes no state.** Validation happens before accumulation,
   and a batch is accepted or rejected as a whole.
4. **The API layer does not know how the aggregate is stored.** Swapping the store
   must not change any endpoint, contract or component.

## Acceptance criteria

- Posting two batches that mention the same `productId` yields the summed quantity.
- Different product ids accumulate independently.
- A batch containing any invalid line is rejected with per-line validation errors and
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
| Store selection | configuration key naming the implementation, or a registration extension per store | A `Storage` configuration value bound to a validated enum, resolved in `AddOrderAggregation` |
| Console output shape | the drained aggregate as-is, or a dedicated envelope | A dedicated payload record, so the console format is a stable contract rather than an internal model |
| Backpressure at the endpoint | none, or a bounded queue rejecting with `429` | None while NFR-1 is met by direct accumulation; revisit only with evidence |

## Out of scope

- A real downstream integration, authentication, rate limiting and multi-instance
  coordination. Name them as future improvements rather than implementing them.
- Splitting the solution into `Domain` / `Application` / `Infrastructure` projects.
  The flat layout is deliberate; see `AGENTS.md`.

## Known deltas from the current implementation

Recorded so they are not lost. **None of these are implemented yet.**

| # | Requirement | Current state |
| --- | --- | --- |
| 1 | FR-3: 20-second interval | `DispatchInterval` defaults to `00:00:30` |
| 2 | FR-3, FR-4: hand-over actually runs | `DispatchEnabled` defaults to `false` |
| 3 | FR-4: JSON payload on the console | `LoggingOrderDispatcher` logs a count and a total, not the payload |
| 4 | FR-5: store selectable by configuration | `InMemoryOrderAggregator` is registered unconditionally |
| 5 | NFR-1, NFR-2: stated design target | Not documented anywhere; no load-shaped test |
| 6 | Invariant 1: cadence proven | No test asserts the 20-second floor |
