// Amaç: Hill Climbing algoritmasının yavaşlığını canlı göstermek.
//   - Pool'a aniden çok iş atılır
//   - Her saniye thread sayısı kaydedilir
//   - Yeni thread'lerin "saniyede kaç tane" eklendiği görülür

using System.Diagnostics;
using System.Globalization;

class Program
{
    static async Task Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Console.WriteLine("=========================================================");
        Console.WriteLine(" HILL CLIMBING DEMO — ThreadPool'un Tırmanış Hızı");
        Console.WriteLine("=========================================================\n");

        Console.WriteLine($"Sistem:       {Environment.OSVersion}");
        Console.WriteLine($"CPU sayısı:   {Environment.ProcessorCount}");
        Console.WriteLine($".NET sürümü:  {Environment.Version}\n");

        // Başlangıç durumu
        ThreadPool.GetMinThreads(out int minWorker, out int minIocp);
        var startThreadCount = ThreadPool.ThreadCount;
        Console.WriteLine($"Min worker:   {minWorker}");
        Console.WriteLine($"Min IOCP:     {minIocp}");
        Console.WriteLine($"Başlangıç ThreadPool.ThreadCount: {startThreadCount}\n");

        Console.WriteLine("Senaryo: 500 adet iş kuyruğa atılacak, her biri 5 saniye thread'i blokluyor.");
        Console.WriteLine("Pool, min thread sayısının ÇOK üstüne çıkmak zorunda kalacak.\n");

        Console.WriteLine("İş kuyruğa atılıyor...\n");

        // 500 adet sync iş kuyruğa at — her biri 5 sn blok
        // Pool'un Hill Climbing devreye girecek çünkü min thread sayısından fazla iş var
        for (int i = 0; i < 500; i++)
        {
            Task.Run(() => Thread.Sleep(5000));
        }

        // Her saniye thread sayısı
        Console.WriteLine($"{"Saniye",-8}{"Thread Count",-15}{"Bu sn eklenen",-18}{"Toplam fark",-12}");
        Console.WriteLine(new string('-', 53));

        var samples = new List<(int sec, int count)>();
        int prevCount = startThreadCount;

        var sw = Stopwatch.StartNew();
        for (int sec = 0; sec <= 15; sec++)
        {
            var current = ThreadPool.ThreadCount;
            var addedThisSec = current - prevCount;
            var totalDiff = current - startThreadCount;
            
            // Hill Climbing'in yavaşlığını net göstermek için
            string indicator = addedThisSec switch
            {
                0 => "  ░",
                1 or 2 => "  ▒",
                _ => "  █"
            };

            Console.WriteLine(
                $"{sec,3} sn   {current,-15}{addedThisSec,-18}+{totalDiff,-10}{indicator}");
            
            samples.Add((sec, current));
            prevCount = current;

            if (sec < 15)
                Thread.Sleep(1000);
        }

        sw.Stop();
        Console.WriteLine();

        // İstatistikler
        var totalAdded = samples.Last().count - samples.First().count;
        var avgPerSec = totalAdded / 15.0;
        var maxPerSec = samples.Zip(samples.Skip(1), (a, b) => b.count - a.count).DefaultIfEmpty(0).Max();

        Console.WriteLine("--- İstatistikler ---");
        Console.WriteLine($" 15 saniyede eklenen toplam thread: {totalAdded}");
        Console.WriteLine($" Saniye başına ortalama: {avgPerSec.ToString("F1", CultureInfo.InvariantCulture)}");
        Console.WriteLine($" Saniye başına maksimum: {maxPerSec}");
        Console.WriteLine($" Hâlâ kuyrukta bekleyen iş: {500 - totalAdded - startThreadCount} (yaklaşık)");
        Console.WriteLine();
        Console.WriteLine("--- Yorum ---");
        Console.WriteLine(" 500 iş atıldı. Pool aynı anda çalıştırabileceği thread sayısını");
        Console.WriteLine(" saniyede ~1-2 artırıyor. 500 iş için 'ideal' pool boyutu 500.");
        Console.WriteLine(" Şu hızla 500'e çıkması yüzlerce saniye sürer.");
        Console.WriteLine();
        Console.WriteLine(" Production'da bu, request'lerin timeout'a düşmesi demektir.");
        Console.WriteLine(" CPU %5'te çöken API'lerin teknik ispatı");

        // Sleep'lerin bitmesini beklemiyoruz
        Environment.Exit(0);
    }
}