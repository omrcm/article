using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

[MemoryDiagnoser]
public class AsyncBenchmarks
{
    [Params(10, 100, 1000)]
    public int Concurrency;
    
    [Benchmark(Baseline = true)]
    public void SyncWork()
    {
        var threads = new Thread[Concurrency];
        for(var i = 0; i < Concurrency; i++)
        {
            threads[i] = new Thread(() => Thread.Sleep(100));
            threads[i].Start();
        }

        foreach (var t in threads) t.Join();
    }

    [Benchmark]
    public async Task TrueAsync()
    {
        var tasks = new Task[Concurrency];
        for(var i = 0; i < Concurrency; i++)
            tasks[i] = Task.Delay(100);
        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task FakeAsync()
    {
        var tasks = new Task[Concurrency];
        for(var i = 0; i < Concurrency; i++)
            tasks[i] = Task.Run(() => Thread.Sleep(100));
        await Task.WhenAll(tasks);
    }
}

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<AsyncBenchmarks>();
    }
}