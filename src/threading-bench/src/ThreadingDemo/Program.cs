using System.Diagnostics;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://0.0.0.0:5000");

// Min thread'i kasten düşük tutuyoruz — starvation davranışı net görünsün
ThreadPool.SetMinThreads(
    workerThreads: Environment.ProcessorCount,
    completionPortThreads: Environment.ProcessorCount);

// OpenTelemetry ile .NET runtime metrics'i Prometheus'a expose ediyoruz
// Bu, dotnet-counters'ın gösterdiği aynı metrikler — ThreadPool size, queue length, GC, vs.
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddRuntimeInstrumentation()  // ThreadPool, GC, JIT metrikleri
            .AddPrometheusExporter();
    });

var app = builder.Build();

// /metrics endpoint — Prometheus buraya scrape eder
app.MapPrometheusScrapingEndpoint();

// === Sağlık kontrolü ===
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    cpuCount = Environment.ProcessorCount,
    machine = Environment.MachineName
}));

// === ThreadPool durumu — anlık snapshot ===
app.MapGet("/threadpool", () =>
{
    ThreadPool.GetAvailableThreads(out int availWorker, out int availIocp);
    ThreadPool.GetMinThreads(out int minWorker, out int minIocp);
    ThreadPool.GetMaxThreads(out int maxWorker, out int maxIocp);
    return Results.Ok(new
    {
        worker = new { available = availWorker, min = minWorker, max = maxWorker },
        iocp = new { available = availIocp, min = minIocp, max = maxIocp },
        currentThreadCount = ThreadPool.ThreadCount,
        processThreadCount = Process.GetCurrentProcess().Threads.Count
    });
});

// === SENARYO A: Saf Sync — 100ms thread'i bloklar ===
app.MapGet("/sync", () =>
{
    Thread.Sleep(100);
    return Results.Ok(new { mode = "sync", threadId = Environment.CurrentManagedThreadId });
});

// === SENARYO B: True Async — timer-tabanlı bekleme, thread serbest ===
app.MapGet("/true-async", async () =>
{
    await Task.Delay(100);
    return Results.Ok(new { mode = "true-async", threadId = Environment.CurrentManagedThreadId });
});

// === SENARYO C: Fake Async — async görünüm, içeride sync iş ===
app.MapGet("/fake-async", async () =>
{
    await Task.Run(() => Thread.Sleep(100));
    return Results.Ok(new { mode = "fake-async", threadId = Environment.CurrentManagedThreadId });
});

Console.WriteLine($"API başlıyor: {Environment.MachineName}, {Environment.ProcessorCount} CPU");
Console.WriteLine($"Metrics: http://localhost:5000/metrics");
app.Run();
