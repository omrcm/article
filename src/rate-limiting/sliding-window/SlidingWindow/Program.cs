// -----------------------------------------------------------------------------
// Sınır Sızıntısı (Boundary Leak) Demo
// Rate Limiting Serisi - Sliding Wİndow
//
// Amaç: Fixed Window'un pencere sınırında nasıl sızdırdığını, Sliding Window'un
// bunu nasıl kapattığını ve Log ile Counter arasındaki approximation farkını
// dış bir bağımlılık olmadan, deterministik bir simülasyonla göstermek.
//
// Bu KASITLI olarak saf bir in-memory simülasyondur: HTTP yok, ASP.NET yok,
// Redis yok. Framework'ün SlidingWindowLimiter'ı ve dağıtık senaryo Parça 2'ye,
// gerçek yük ölçümü Parça 3'e aittir.
//
// Çalıştırmak için:
//   dotnet new console -n SinirSizintisi
//   (Program.cs içeriğini bununla değiştir)
//   dotnet run
// -----------------------------------------------------------------------------

const int Limit = 100;        // 60 saniyelik aralıkta en fazla 100 istek
const double Window = 60.0;   // pencere genişliği (saniye)

// Senaryo: 10:00:58'de 100 istek, 10:01:01'de 100 istek.
// Yani iki bitişik pencerenin sınırında, ~3 saniye içinde 200 istek.
double[] BuildBoundaryScenario()
{
    var reqs = new List<double>();
    for (int i = 0; i < 100; i++) reqs.Add(58.0); // batch 1 — pencere sonu
    for (int i = 0; i < 100; i++) reqs.Add(61.0); // batch 2 — sonraki pencere başı
    return reqs.ToArray();
}

var scenario = BuildBoundaryScenario();

IRateLimiter fixedWindow    = new FixedWindowLimiter(Limit, Window);
IRateLimiter slidingLog     = new SlidingWindowLogLimiter(Limit, Window);
IRateLimiter slidingCounter = new SlidingWindowCounterLimiter(Limit, Window);

Console.WriteLine($"Toplam gönderilen istek : {scenario.Length}  (100 + 100, ~3 saniye arayla)");
Console.WriteLine($"Limit                   : {Limit} / {Window:0} saniye");
Console.WriteLine(new string('-', 48));
Console.WriteLine($"Fixed Window            : {Accepted(fixedWindow, scenario)} kabul   <- Sınır Sızıntısı");
Console.WriteLine($"Sliding Window Log      : {Accepted(slidingLog, scenario)} kabul   <- tam, sızıntı kapalı");
Console.WriteLine($"Sliding Window Counter  : {Accepted(slidingCounter, scenario)} kabul   <- yaklaşık");

static int Accepted(IRateLimiter limiter, double[] requests)
{
    int accepted = 0;
    foreach (var t in requests)
        if (limiter.TryAcquire(t)) accepted++;
    return accepted;
}

// -----------------------------------------------------------------------------

interface IRateLimiter
{
    // t: isteğin geldiği an
    bool TryAcquire(double t);
}

// Fixed Window: zamanı sabit dilimlere böler, her dilimde bir sayaç tutar.
// Sızıntı tam da dilim değişiminde, sayaç sıfırlandığı an olur.
sealed class FixedWindowLimiter : IRateLimiter
{
    private readonly int _limit;
    private readonly double _window;
    private readonly Dictionary<long, int> _counts = new();

    public FixedWindowLimiter(int limit, double window) => (_limit, _window) = (limit, window);

    public bool TryAcquire(double t)
    {
        long w = (long)(t / _window);
        int c = _counts.GetValueOrDefault(w);
        if (c < _limit)
        {
            _counts[w] = c + 1;
            return true;
        }
        return false;
    }
}

// Sliding Window Log: her istek için timestamp saklar.
// eski kayıtları temizler, kalanları sayar. Kesin sonuç verir ama memory gelen request hacmiyle büyür.
sealed class SlidingWindowLogLimiter : IRateLimiter
{
    private readonly int _limit;
    private readonly double _window;
    private readonly Queue<double> _log = new();

    public SlidingWindowLogLimiter(int limit, double window) => (_limit, _window) = (limit, window);

    public bool TryAcquire(double t)
    {
        double cutoff = t - _window;
        while (_log.Count > 0 && _log.Peek() <= cutoff)
            _log.Dequeue();

        if (_log.Count < _limit)
        {
            _log.Enqueue(t);
            return true;
        }
        return false;
    }
}

// Sliding Window Counter: yalnızca mevcut ve önceki pencerenin sayaçlarını tutar.
// Sonucu, önceki pencereyi kalan zaman oranıyla ağırlıklandırarak tahmin eder.
// Ucuz ve hızlı — küçük, sınırlı bir approximation hatası pahasına.
sealed class SlidingWindowCounterLimiter : IRateLimiter
{
    private readonly int _limit;
    private readonly double _window;
    private readonly Dictionary<long, int> _counts = new();

    public SlidingWindowCounterLimiter(int limit, double window) => (_limit, _window) = (limit, window);

    public bool TryAcquire(double t)
    {
        long w = (long)(t / _window);
        double elapsedFraction = (t - w * _window) / _window;

        int prev = _counts.GetValueOrDefault(w - 1);
        int cur = _counts.GetValueOrDefault(w);

        double estimate = cur + prev * (1 - elapsedFraction);
        if (estimate < _limit)
        {
            _counts[w] = cur + 1;
            return true;
        }
        return false;
    }
}