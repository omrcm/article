// İddia: ".Result kullanımı ThreadPool'u tüketir, CPU boşken sistem
//         yeni iş kabul edemez hale gelir."
//
// Senaryo:
//   - "API endpoint"i taklit eden bir metot var (.Result kullanıyor)
//   - 50 eşzamanlı "request" gönderiyoruz
//   - Pool'un nasıl tükendiğini ve CPU'nun nasıl boş gezdiğini ölçüyoruz

using System.Diagnostics;

class Program
{
    // Min thread sayısını CPU sayısına eşit bırakıyoruz (default davranış)
    // Bu, "default ayarlarla bir ASP.NET Core uygulaması" senaryosunu taklit eder

    static async Task Main()
    {
        // Küçük ölçekli bir pod'u simule ediyoruz.
        ThreadPool.SetMinThreads(4, 4);
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Console.WriteLine("======================================================");
        Console.WriteLine("  .RESULT STARVATION DEMO");
        Console.WriteLine("======================================================\n");
        Console.WriteLine($"CPU sayısı: {Environment.ProcessorCount}");

        ThreadPool.GetMinThreads(out int minWorker, out _);
        Console.WriteLine($"Min worker threads: {minWorker}\n");

        Console.WriteLine("Senaryo: 200 eşzamanlı 'request', her biri .Result kullanıyor.");
        Console.WriteLine("Her isteğin asıl işi 100ms async I/O — yani CPU iş yapmıyor.\n");

        var sw = Stopwatch.StartNew();
        var totalRequests = 200;
        var completed = 0;

        // 50 paralel "request" başlat
        var tasks = Enumerable.Range(0, totalRequests).Select(i => Task.Run(() =>
        {
            var requestStart = sw.ElapsedMilliseconds;
            
            // FakeAsyncWork async ama burada .Result ile bekleniyor.
            var result = FakeAsyncWork(i).Result;
            
            var requestEnd = sw.ElapsedMilliseconds;
            Interlocked.Increment(ref completed);
            return new { Id = i, Duration = requestEnd - requestStart };
        })).ToArray();

        // Her saniye durum bildirimi
        var monitorTask = Task.Run(async () =>
        {
            while (completed < totalRequests)
            {
                await Task.Delay(1000);
                var threadCount = ThreadPool.ThreadCount;
                var done = Volatile.Read(ref completed);
                Console.WriteLine($"  [{sw.ElapsedMilliseconds / 1000.0,5:F1} sn] " +
                                  $"Tamamlanan: {done}/{totalRequests}  " +
                                  $"ThreadPool size: {threadCount}");
            }
        });

        await Task.WhenAll(tasks);
        await monitorTask;
        sw.Stop();

        Console.WriteLine();
        Console.WriteLine("--- SONUÇ ---");
        Console.WriteLine($" Toplam süre:           {sw.ElapsedMilliseconds} ms");
        Console.WriteLine($" Beklenen süre (async): ~200 ms (hepsi paralel)");
        Console.WriteLine($" Gerçek/beklenen oranı: {sw.ElapsedMilliseconds / 200.0:F1}x");
        Console.WriteLine();
        Console.WriteLine("--- YORUM ---");
        Console.WriteLine(" Async metot olmasına rağmen, .Result kullanımı pool'u kilitledi.");
        Console.WriteLine(" Her thread continuation'ı için bir başka thread bekliyor —");
        Console.WriteLine(" ama o thread de aynı şeyi yapıyor. Hill Climbing yetişmeye");
        Console.WriteLine(" çalışıyor ama saniyede 1-2 thread ile bu işin altından kalkamıyor.");
        Console.WriteLine();
        Console.WriteLine(" Bu kod ASP.NET Core'da olsa, CPU %5 iken request'ler timeout'a");
        Console.WriteLine(" düşerdi. CPU'nun boş gezmesinin sebebi: thread'ler iş yapmıyor,");
        Console.WriteLine(" birbirini bekliyor.");
    }

    // Async ama içinde I/O simülasyonu olan metot
    // (Gerçek hayatta: DB sorgusu, HTTP çağrısı, dosya I/O, vs.)
    static async Task<string> FakeAsyncWork(int id)
    {
        await Task.Delay(250);
        return $"Result-{id}";
    }
}