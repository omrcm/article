using System.Diagnostics;
 
class Program
{
    static async Task Main()
    {
        Console.WriteLine("=================================================");
        Console.WriteLine("  ThreadPool Gözlemleri — Part 2'nin Kanıtları");
        Console.WriteLine("=================================================\n");
        Console.WriteLine($"Sistem: {Environment.OSVersion}");
        Console.WriteLine($"CPU sayısı: {Environment.ProcessorCount}");
        Console.WriteLine($".NET sürümü: {Environment.Version}\n");

        DefaultMinThreads();
        Console.WriteLine();
        await ResultCausesDeadlock();

        Console.WriteLine("\n=================================================");
        Console.WriteLine("  GÖZLEMLER TAMAMLANDI");
        Console.WriteLine("=================================================");
    }

    // ================================================================
    // GÖZLEM 1: Default min thread sayısı CPU sayısına eşittir
    // ================================================================
    static void DefaultMinThreads()
    {
        Console.WriteLine("--- Gözlem 1: ThreadPool Başlangıç Durumu ---");

        ThreadPool.GetMinThreads(out int minWorker, out int minIocp);
        ThreadPool.GetMaxThreads(out int maxWorker, out int maxIocp);
        ThreadPool.GetAvailableThreads(out int availWorker, out int availIocp);

        Console.WriteLine($" CPU sayısı:                    {Environment.ProcessorCount}");
        Console.WriteLine($" Min worker threads:            {minWorker}");
        Console.WriteLine($" Min IOCP threads:              {minIocp}");
        Console.WriteLine($" Max worker threads:            {maxWorker}");
        Console.WriteLine($" Max IOCP threads:              {maxIocp}");
        Console.WriteLine($" Şu an boştaki worker threads:  {availWorker}");
        Console.WriteLine();
        Console.WriteLine($" Min = CPU sayısı. Üstüne çıkmak için Hill Climbing devreye girer.");
    }

    // ================================================================
    // GÖZLEM 2: SyncContext varlığında .Result deadlock üretir
    // ================================================================
    static async Task ResultCausesDeadlock()
    {
        Console.WriteLine("--- Gözlem 3: SyncContext + .Result = Deadlock ---");
        Console.WriteLine(" WPF/Legacy ASP.NET'i taklit eden tek-thread'lik SyncContext'te");
        Console.WriteLine(" .Result çağrısı deadlock üretir mi? Test ediyoruz...\n");

        using var ctx = new SingleThreadSyncContext();

        var task = ctx.Run(() =>
        {
            Console.WriteLine($" SyncContext içinde Thread: {Environment.CurrentManagedThreadId}");
            Console.WriteLine(" .Result ile bekliyorum (3 sn timeout)...");

            try
            {
                // Bu çağrı deadlock üretir:
                // 1. SimulateAsyncWork başlar, Task.Delay yield eder
                // 2. Continuation context'e dönmek ister (Thread 15'e)
                // 3. Ama context bloklu (.Result bekliyor)
                // 4. Deadlock.
                bool completed = SimulateAsyncWork().Wait(TimeSpan.FromSeconds(3));

                if (completed)
                    Console.WriteLine(" Beklenmedik: deadlock olmadı!");
                else
                    Console.WriteLine(" Beklendiği gibi: 3 sn'de tamamlanmadı — DEADLOCK!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Exception: {ex.GetType().Name}");
            }
        });

        await task;

        Console.WriteLine();
        Console.WriteLine(" ASP.NET Core'da bu deadlock olmaz (SyncContext yok),");
        Console.WriteLine(" ama starvation üretir — Part 3'te ölçeceğiz.");
    }

    static async Task SimulateAsyncWork()
    {
        await Task.Delay(100);
    }
}

// Tek thread'li mini SynchronizationContext — WPF UI thread'i taklidi
class SingleThreadSyncContext : SynchronizationContext, IDisposable
{
    private readonly System.Collections.Concurrent.BlockingCollection<(SendOrPostCallback cb, object? state)> _queue = new();
    private readonly Thread _thread;

    public SingleThreadSyncContext()
    {
        _thread = new Thread(() =>
        {
            SetSynchronizationContext(this);
            foreach (var (cb, state) in _queue.GetConsumingEnumerable())
                cb(state);
        })
        { IsBackground = true, Name = "SingleThreadSyncCtx" };
        _thread.Start();
    }

    public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));

    public Task Run(Action action)
    {
        var tcs = new TaskCompletionSource();
        Post(_ =>
        {
            try { action(); tcs.SetResult(); }
            catch (Exception ex) { tcs.SetException(ex); }
        }, null);
        return tcs.Task;
    }

    public void Dispose() => _queue.CompleteAdding();
}