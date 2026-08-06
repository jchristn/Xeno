// The shared Touchstone suites spin real HTTP/WebSocket servers and, when configured for a server
// database, create one isolated catalog per test. They are designed to run sequentially (as the
// Test.Automated console runner does); xUnit parallelizes by default, which causes port contention
// between concurrently hosted servers. Disable parallelization so this runner matches that model.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
