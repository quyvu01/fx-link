namespace FxLink.IntegrationTests.Fixtures;

// Self-request/response fixture: a single process is both the requester and the consumer that
// answers it (round-trips through the real broker's exchange/subject and its own reply queue/inbox),
// used by the RabbitMq/NATS lifecycle and outage tests below. Nested inside the test classes that
// use it would force a distinct queue/subject per test class even though they're logically the same
// message; kept here instead so every transport test round-trips the identical wire shape.
public sealed class EchoRequest
{
    public string Value { get; init; }
}

public sealed class EchoResponse
{
    public string Value { get; init; }
}
