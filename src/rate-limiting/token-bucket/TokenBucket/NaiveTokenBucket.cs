public class NaiveTokenBucket
{
    private readonly int _capacity;
    private readonly double _refillRatePerSecond;
    private double _tokens;
    private DateTime _lastRefill;

    public NaiveTokenBucket(int capacity, double refillRatePerSecond)
    {
        _capacity = capacity;
        _refillRatePerSecond = refillRatePerSecond;
        _tokens = capacity;
        _lastRefill = DateTime.UtcNow;
    }

    public bool TryConsume(int tokens = 1)
    {
        Refill();
        if (_tokens >= tokens)
        {
            _tokens -= tokens;
            return true;
        }
        return false;
    }

    private void Refill()
    {
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastRefill).TotalSeconds;
        _tokens = Math.Min(_capacity, _tokens + elapsed * _refillRatePerSecond);
        _lastRefill = now;
    }
}