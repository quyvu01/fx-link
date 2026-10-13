namespace FxLink.IntegrationTests.Support;

public static class Polling
{
    public static async Task WaitUntilAsync(Func<bool> condition, TimeSpan? timeout = null, int pollMs = 25)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Condition was not met in time.");
            await Task.Delay(pollMs);
        }
    }
}
