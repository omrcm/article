// Üç ölçüm yapar:
//   1. Tek thread'in bellek maliyeti
//   2. Context switch maliyeti
//   3. 1000 sync thread vs 1000 async task — bellek ve süre karşılaştırması

using System.Diagnostics;

class Program
{
    static async Task Main()
    {
        Console.WriteLine("===========================================");
        Console.WriteLine("  SYNC vs ASYNC — MALİYET BENCHMARK");
        Console.WriteLine("===========================================\n");

        Console.WriteLine($"Sistem: {Environment.OSVersion}");
        Console.WriteLine($"CPU sayısı: {Environment.ProcessorCount}");
        Console.WriteLine($".NET sürümü: {Environment.Version}\n");

        // GC'yi ölçemlerin temiz olması için temizliyoruz
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Measure_ThreadMemoryCost();
        Measure_ContextSwitchCost();
        await Measure_SyncVsAsync_AtScale();

        Console.WriteLine("\n===========================================");
        Console.WriteLine("  BENCHMARK TAMAMLANDI");
        Console.WriteLine("===========================================");
    }

    // ================================================================
    // ÖLÇÜM 1: Tek thread'in bellek maliyeti
    // ================================================================
    static void Measure_ThreadMemoryCost()
    {
        Console.WriteLine("--- Ölçüm 1: Thread'in Bellek Maliyeti ---");

        const int threadCount = 500;
        var memBefore = Process.GetCurrentProcess().WorkingSet64;

        var threads = new Thread[threadCount];
        var startSignal = new ManualResetEventSlim(false);
        var readyCount = 0;

        for (int i = 0; i < threadCount; i++)
        {
            threads[i] = new Thread(() =>
            {
                Interlocked.Increment(ref readyCount);
                startSignal.Wait();
            }) { IsBackground = true };
            threads[i].Start();
        }

        // Tüm thread'ler hazır olana kadar bekle
        while (Volatile.Read(ref readyCount) < threadCount)
            Thread.Sleep(10);

        // Bellek farkını ölç
        Thread.Sleep(100); // memory snapshot stabilize olsun
        var memAfter = Process.GetCurrentProcess().WorkingSet64;
        var diffBytes = memAfter - memBefore;
        var perThreadKB = diffBytes / 1024.0 / threadCount;

        // Cleanup
        startSignal.Set();
        foreach (var t in threads) t.Join();

        Console.WriteLine($"  Thread sayısı:           {threadCount}");
        Console.WriteLine($"  Toplam bellek farkı:     {diffBytes / 1024.0 / 1024.0:F1} MB");
        Console.WriteLine($"  Thread başına ortalama:  {perThreadKB:F0} KB");
        Console.WriteLine($"  Tahmini 10K thread için: {perThreadKB * 10000 / 1024:F0} MB\n");
    }

    // ================================================================
    // ÖLÇÜM 2: Context switch maliyeti ( ping - pong )
    // ================================================================
    static void Measure_ContextSwitchCost()
    {
        Console.WriteLine("--- Ölçüm 2: Context Switch Maliyeti ---");

        const int iterations = 100_000;
        using var pingEvent = new AutoResetEvent(false);
        using var pongEvent = new AutoResetEvent(false);

        // Pong thread: ping bekle, pong sinyalle
        var pong = new Thread(() =>
        {
            for (int i = 0; i < iterations; i++)
            {
                pingEvent.WaitOne();   // ← context switch'e neden olur
                pongEvent.Set();
            }
        }) { IsBackground = true };
        pong.Start();

        for (int i = 0; i < 1000; i++)
        {
            pingEvent.Set();
            pongEvent.WaitOne();
        }

        // ölçüm
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations - 1000; i++)
        {
            pingEvent.Set();
            pongEvent.WaitOne();
        }
        sw.Stop();

        // 1 ping-pong = 2 context switch (main → pong, pong → main)
        var totalSwitches = (iterations - 1000) * 2L;
        var nsPerSwitch = sw.Elapsed.TotalMilliseconds * 1_000_000.0 / totalSwitches;

        Console.WriteLine($"  Toplam ping-pong:        {iterations - 1000:N0}");
        Console.WriteLine($"  Toplam süre:             {sw.ElapsedMilliseconds:N0} ms");
        Console.WriteLine($"  Context switch başına:   ~{nsPerSwitch:F0} ns ({nsPerSwitch / 1000:F2} µs)");
        Console.WriteLine($"  Tahmini 1M switch için:  ~{nsPerSwitch / 1000:F0} ms\n");
    }

    // ================================================================
    // ÖLÇÜM 3: 1000 sync thread vs 1000 async task
    // ================================================================
    static async Task Measure_SyncVsAsync_AtScale()
    {
        Console.WriteLine("--- Ölçüm 3: 1000 Concurrent İş — Sync vs Async ---\n");

        const int concurrency = 1000;
        const int waitMs = 100;

        // GC reset
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var memBaseline = GC.GetTotalMemory(true);

        // ----- SYNC: her iş için ayrı thread -----
        Console.WriteLine($"  [SYNC] {concurrency} thread, her biri Thread.Sleep({waitMs})...");
        var swSync = Stopwatch.StartNew();

        var syncThreads = new Thread[concurrency];
        for (int i = 0; i < concurrency; i++)
        {
            syncThreads[i] = new Thread(() => Thread.Sleep(waitMs)) { IsBackground = true };
            syncThreads[i].Start();
        }
        foreach (var t in syncThreads) t.Join();

        swSync.Stop();
        var memSync = GC.GetTotalMemory(false) - memBaseline;
        var syncMemMB = (Process.GetCurrentProcess().PeakWorkingSet64 - memBaseline) / 1024.0 / 1024.0;

        Console.WriteLine($" Süre:              {swSync.ElapsedMilliseconds} ms");
        Console.WriteLine($" GC heap kullanımı: {memSync / 1024.0:F0} KB");
        Console.WriteLine($" Açılan thread:     {concurrency}\n");

        // GC reset
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        memBaseline = GC.GetTotalMemory(true);
        var threadsBefore = Process.GetCurrentProcess().Threads.Count;

        // ----- ASYNC: 1000 Task, ThreadPool kullanır -----
        Console.WriteLine($"  [ASYNC] {concurrency} Task, her biri await Task.Delay({waitMs})...");
        var swAsync = Stopwatch.StartNew();

        var asyncTasks = new Task[concurrency];
        for (int i = 0; i < concurrency; i++)
            asyncTasks[i] = Task.Delay(waitMs);
        await Task.WhenAll(asyncTasks);

        swAsync.Stop();
        var memAsync = GC.GetTotalMemory(false) - memBaseline;
        var threadsAfter = Process.GetCurrentProcess().Threads.Count;

        Console.WriteLine($" Süre:              {swAsync.ElapsedMilliseconds} ms");
        Console.WriteLine($" GC heap kullanımı: {memAsync / 1024.0:F0} KB");
        Console.WriteLine($" Process thread:    {threadsBefore} → {threadsAfter} (fark: {threadsAfter - threadsBefore})\n");

        // ----- Karşılaştırma -----
        Console.WriteLine("--------ÖZET------");
        Console.WriteLine($" Süre oranı:    SYNC {swSync.ElapsedMilliseconds}ms vs ASYNC {swAsync.ElapsedMilliseconds}ms" +
                          $" -> {(double)swSync.ElapsedMilliseconds / swAsync.ElapsedMilliseconds:F1}x");
        Console.WriteLine($" Bellek oranı:  SYNC {memSync / 1024.0:F0}KB vs ASYNC {memAsync / 1024.0:F0}KB" +
                          $" -> {(memSync == 0 ? 0 : (double)memSync / Math.Max(memAsync, 1)):F1}x");
        Console.WriteLine($"    Thread oranı:  SYNC {concurrency} adet OS thread vs ASYNC ~{threadsAfter - threadsBefore} adet ThreadPool worker");
    }
}