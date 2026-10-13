using Xunit;

// Every test class here talks to the same real local brokers (see README.md). xunit parallelizes
// different test classes by default, and contention between them (e.g. one test's StartAsync
// racing another's on the same broker connection count/CPU) produces exactly the kind of flaky
// AMQP/SQS timing failures this suite is meant to avoid. Sequential execution trades some wall-clock
// time for determinism, which matters more for a small local-only suite like this one.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
