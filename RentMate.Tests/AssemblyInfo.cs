// The integration-test factory keeps a single shared SQLite :memory:
// connection per WebApplicationFactory, and some controller actions fire
// background work (Task.Run scoring) that writes on that same connection.
// SQLite in-memory is not safe for concurrent access, so running test
// collections in parallel produces order-dependent, flaky failures.
// Serialize the whole assembly for deterministic runs (the suite is fast).
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
