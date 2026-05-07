public class LockFreeTokenBucket
{
    private readonly long _capacityScaled;       // capacity * 1000
    private readonly double _refillRatePerSecond;
    private long _tokensScaled;                  // tokens * 1000
    private long _lastRefillTicks;

    public LockFreeTokenBucket(int capacity, double refillRatePerSecond)
    {
        _capacityScaled = capacity * 1000L;
        _refillRatePerSecond = refillRatePerSecond;
        _tokensScaled = _capacityScaled;
        _lastRefillTicks = DateTime.UtcNow.Ticks;
    }

    public bool TryConsume(int tokens = 1)
    {
        var costScaled = tokens * 1000L;
        var spinner = new SpinWait();

        while (true)
        {
            var nowTicks = DateTime.UtcNow.Ticks;
            var lastTicks = Volatile.Read(ref _lastRefillTicks);
            var currentTokens = Volatile.Read(ref _tokensScaled);

            var elapsedSec = (nowTicks - lastTicks) / (double)TimeSpan.TicksPerSecond;
            var refillAmount = (long)(elapsedSec * _refillRatePerSecond * 1000);
            var refilledTokens = Math.Min(_capacityScaled, currentTokens + refillAmount);

            if (refilledTokens < costScaled)
                return false;

            var afterConsume = refilledTokens - costScaled;

            if (Interlocked.CompareExchange(ref _tokensScaled, afterConsume, currentTokens) == currentTokens)
            {
                Interlocked.Exchange(ref _lastRefillTicks, nowTicks);
                return true;
            }
            spinner.SpinOnce();
        }
    }
}