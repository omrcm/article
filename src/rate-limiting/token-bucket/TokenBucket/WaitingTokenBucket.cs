public class WaitingTokenBucket
{
    private readonly LockingTokenBucket _bucket;
    private readonly double _refillRatePerSecond;

    public WaitingTokenBucket(int capacity, double refillRatePerSecond)
    {
        _bucket = new LockingTokenBucket(capacity, refillRatePerSecond);
        _refillRatePerSecond = refillRatePerSecond;
    }

    public async Task<bool> WaitAndConsumeAsync(
        int tokens,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (_bucket.TryConsume(tokens))
                return true;

            // Yeterli token birikene kadar yaklaşık ne kadar beklemeli?
            var waitMs = (int)Math.Ceiling(tokens / _refillRatePerSecond * 1000);

            // 100ms üst sınırı: bir şey yanlışsa erken uyan
            await Task.Delay(Math.Min(waitMs, 100), ct);
        }
        return false;
    }
}