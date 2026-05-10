using System.Collections.Concurrent;

class Program
{
    private static readonly HttpClient _httpClient = new HttpClient();

    static async Task Main()
    {
        Console.WriteLine("-----------------");
        Console.WriteLine("Async Thread Demo");
        Console.WriteLine("-----------------");

        await ThreadChanges();
        await NoThreadChange();
        await ReturnsToSameThread();
        
        Console.WriteLine("-----------------");
        Console.WriteLine("Demo Tamamlandı");
        Console.WriteLine("-----------------");
    }
    
    // 1. Senaryo: await sonrası ThreadPool'dan farklı thread alması bekleniyor
    static async Task ThreadChanges()
    {
        Console.WriteLine($"SyncContext.Current: {SynchronizationContext.Current?.ToString() ?? "Null"}\n");
        await FetchDataAsync();
        Console.WriteLine();
    }

    public static async Task<String> FetchDataAsync()
    {
        Console.WriteLine($" Await Öncesi Thread: {Environment.CurrentManagedThreadId, 3} " + $"(IsThreadPool: {Thread.CurrentThread.IsThreadPoolThread})");   
        var data = await _httpClient.GetStringAsync("https://www.httpbin.org/uuid");
        Console.WriteLine($" Await Sonrası Thread: {Environment.CurrentManagedThreadId, 3} " + $"(IsThreadPool: {Thread.CurrentThread.IsThreadPoolThread})");   
        return data;
    }

    // 2. Senaryo: await task tamamlanmış thread'in değişmedi beklenir
    static async Task NoThreadChange()
    {
        Console.WriteLine("----Tamamlanmış Task----");
        Console.WriteLine($" 2. Senaryo Thread: {Environment.CurrentManagedThreadId,3}");

        var data = await Task.FromResult("instant");
        
        Console.WriteLine($" Await sonrası Thread: {Environment.CurrentManagedThreadId,3}  (aynı kalmalı)\n");
    }

    // 3. Senaryo: await sonrası tekrar context'in threadine dönmesini bekliyoruz
    // Amaç WPF/WinForm UI thread davranışını taklit etmek. Tartışma konusuna sebep olan kısım
    static async Task ReturnsToSameThread()
    {
        Console.WriteLine("---WPF/WinForm UI Taklidi---");
        using var ctx = new SingleThreadSyncContext();
        var prevContext = SynchronizationContext.Current;
        try
        {
            await ctx.Run(async () =>
            {
                Console.WriteLine($" Başlangıç Thread: {Environment.CurrentManagedThreadId,3}  (SyncContext: {SynchronizationContext.Current?.GetType().Name})");
 
                await Task.Delay(100);
 
                Console.WriteLine($" Await sonrası Thread: {Environment.CurrentManagedThreadId,3}  (aynı thread'e dönmeli)");
                
                await Task.Delay(100).ConfigureAwait(false);
 
                Console.WriteLine($" Thread: {Environment.CurrentManagedThreadId,3}  (ThreadPool'da)");
            });
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(prevContext);
        }
        
        Console.WriteLine();
    }
}

class SingleThreadSyncContext : SynchronizationContext, IDisposable
{
    private readonly BlockingCollection<(SendOrPostCallback cb, object? state)> _queue = new();
    private readonly Thread _thread;
 
    public SingleThreadSyncContext()
    {
        _thread = new Thread(() =>
        {
            SetSynchronizationContext(this);
            foreach (var (cb, state) in _queue.GetConsumingEnumerable())
                cb(state);
        }) { IsBackground = true, Name = "SingleThreadSyncCtx" };
        _thread.Start();
    }
 
    public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));
 
    public Task Run(Func<Task> action)
    {
        var tcs = new TaskCompletionSource();
        Post(async _ =>
        {
            try { await action(); tcs.SetResult(); }
            catch (Exception ex) { tcs.SetException(ex); }
        }, null);
        return tcs.Task;
    }
 
    public void Dispose() => _queue.CompleteAdding();
}
