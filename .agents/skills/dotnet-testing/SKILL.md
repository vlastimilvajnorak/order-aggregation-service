---
name: dotnet-testing
description: Test strategy and authoring rules for this repository - the unit, integration, architecture and end-to-end boundary, xUnit conventions, Arrange-Act-Assert, deterministic concurrency tests, fake time through TimeProvider, WebApplicationFactory HTTP tests, asserting ProblemDetails, covering cancellation and failure paths, and when mocking is justified. Use when adding, changing, reviewing or debugging tests, when a test is flaky, or when deciding what level a new behaviour should be tested at. Do not use for production code design decisions.
---

# .NET testing

## Test projects

```text
tests/OrderAggregationService.Tests/
  InMemoryOrderAggregatorTests.cs   unit tests over the aggregator
  OrderBatchValidatorTests.cs       unit tests over validation
  OrderApiIntegrationTests.cs       HTTP tests through WebApplicationFactory
  OrderApiFactory.cs                the test host
  FixedTimeProvider.cs              deterministic clock
```

One test project today, holding both unit and integration tests, distinguished by
file and class name. Do not split it into separate projects unless the user asks.
If the suite is ever split, keep the same boundaries described below.

## Inspect first

0. `docs/requirements.md` - the acceptance criteria a test is meant to prove
1. The existing test file for the type you are changing
2. `OrderApiFactory.cs` before writing any HTTP test
3. `FixedTimeProvider.cs` before writing anything time-dependent
4. `Directory.Packages.props` - test package versions are pinned there

## The pyramid

| Level | Scope | Substitutes | Where |
| --- | --- | --- | --- |
| Unit | One type, in process, no host | Injected fakes only | `*Tests.cs` |
| Integration | The real request pipeline through `WebApplicationFactory` | Only external systems | `OrderApiIntegrationTests.cs` |
| Architecture | Dependency direction between namespaces or projects | none | not present yet |
| End-to-end | A deployed process over the network | nothing | not present; do not add without asking |

Test a behaviour at the lowest level that can actually observe it. Validation rules
belong in unit tests; the HTTP status code and the `ProblemDetails` shape belong in
an integration test.

## xUnit conventions

- xUnit v2 is in use. `TestContext.Current` is a v3 API and does not compile here.
- `[Fact]` for a single case, `[Theory]` with `[InlineData]` for the same assertion
  over several inputs.
- Class name `<SystemUnderTest>Tests`; method name
  `Method_Scenario_ExpectedResult`.
- Keep Arrange, Act and Assert visually separated by blank lines.
- One reason to fail per test. If a failure message would not tell you what broke,
  split the test.
- Prefer `Assert.Single`, `Assert.Contains`, `Assert.Equal` over a chain of boolean
  assertions.

## Isolation

- Every test constructs its own subject. No shared mutable static state, no fixture
  holding data that another test mutates.
- Integration tests create and dispose their own `OrderApiFactory` and `HttpClient`
  so no aggregation state crosses a test boundary.
- Tests must pass in any order and when run in parallel. If a test only passes
  after another one, it is broken.

## Time

- Never call `DateTime.UtcNow` or `Task.Delay` to let something happen.
- Inject `FixedTimeProvider` and call `Advance(...)` to move time.
- A test must never wait on the wall clock. If a test sleeps, redesign it.

## The hand-over cadence

The 20-second minimum interval is a specified invariant, so it needs a test that
would fail if someone added an early-flush path:

- Drive the background service with `FixedTimeProvider`, never with a real delay.
- Assert that no hand-over happens before the interval elapses, that a burst of
  submissions inside one window produces exactly **one** hand-over, and that the
  hand-over carries the summed quantities.
- Assert the payload shape the console output is contracted to produce, not merely
  that the dispatcher was called.
- A test that waits 20 real seconds is a defect. So is one that asserts the timer
  type instead of the observable cadence.

## Load-shaped tests

The service is specified for hundreds of orders per second over hundreds of product
ids. A test that mirrors that shape belongs in the suite:

- Many concurrent writers, a bounded set of product ids, exact expected totals.
- Assert correctness under contention, not elapsed time. A throughput assertion on
  a shared CI runner is flaky by construction and must not gate the build.

## Concurrency

- Drive concurrency deterministically: start N tasks, `await Task.WhenAll`, then
  assert exact totals.
- Assert on an exact expected value, not on "no exception was thrown".
- Never treat a passing timing-dependent test as proof of thread safety. Randomness
  proves nothing; exact totals under contention do.
- Do not add retries or `Thread.Sleep` to stabilise a concurrency test.

## HTTP tests

- Use `WebApplicationFactory<Program>` through `OrderApiFactory`, which pins the
  environment and disables periodic dispatch so a background drain cannot race the
  assertions.
- Assert the status code, then the deserialized body.
- For a rejected request, deserialize into `HttpValidationProblemDetails` and assert
  the specific error key, for example `[0].quantity`, not merely that the request
  failed.
- Cover the failure paths: empty batch, missing `productId`, non-positive quantity,
  oversized batch, and that a rejected batch changed no state.
- Cover cancellation where a method accepts a `CancellationToken` and the
  cancellation is observable.

## Mocking

- Substitute only at a real boundary: a clock, an outbound integration, the network.
- Do not mock the type under test, and do not mock a value object or a `record`.
- Prefer a small hand-written fake, as `FixedTimeProvider` does, over a mocking
  framework. Do not add a mocking package without a concrete need.
- A test needing more than two substitutes is usually testing the wrong level.
- An in-memory implementation is not a substitute for an integration test against a
  real database. If a database is ever added, it needs its own integration tests.

## Coverage

Coverage is a diagnostic for finding untested paths, not a target. Do not add
assertions purely to raise a number, and do not exclude code to raise it either.

## Flaky tests

Fix the cause. Do not re-run, add a retry attribute, add a delay, or disable the
test. If the cause is genuinely external, say so and ask before skipping.

## Verify

```bash
dotnet test --configuration Release
```

Then confirm by reading:

- the new test fails when the behaviour it describes is broken
- it asserts observable behaviour, not a private implementation detail
- it introduces no sleep, no wall-clock dependency and no shared state
- it runs in CI, which executes `dotnet test` on every push and pull request to
  `develop` and `master`

Official references verified 2026-08-26:
<https://learn.microsoft.com/dotnet/core/testing/>,
<https://learn.microsoft.com/aspnet/core/test/integration-tests>,
<https://xunit.net/docs/getting-started/v2/getting-started>.
