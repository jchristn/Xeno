// The shared Touchstone suites spin real HTTP/WebSocket servers and, when configured for a server
// database, create one isolated catalog per test. They are designed to run sequentially (as the
// Test.Automated console runner does). Force this runner to execute them one at a time so concurrently
// hosted servers do not contend for the same ports.
[assembly: NUnit.Framework.NonParallelizable]
[assembly: NUnit.Framework.LevelOfParallelism(1)]
