# Threading Bench

Mac M-serisi makinende Docker Compose ile **API + k6 + Prometheus + Grafana** stack'i. Local ortamda yapılan bu sistemin GCP tarafına transferi ve ayağa kaldırılması burada anlatılmamıştır.

## Önkoşullar

- Docker Desktop for Mac (ARM64 sürüm)
- 8 GB RAM (önerilen: 16 GB)
- 5 GB disk alanı

## Hızlı Başlangıç

### 1. Stack'i Ayağa Kaldır

```bash
docker compose up -d
```

Bu komut:
- API'yi build eder (ilk seferde ~1-2 dakika)
- Prometheus ve Grafana'yı başlatır
- Network'ü kurar

İlk build sonrası kontrol:

```bash
# API çalışıyor mu?
curl http://localhost:5000/health

# ThreadPool durumu
curl http://localhost:5000/threadpool

# Metrics endpoint'i Prometheus için
curl http://localhost:5000/metrics | head -20
```

### 2. Grafana'yı Aç

Browser: **http://localhost:3000**

- Username: `admin`
- Password: `admin`
- Dashboard "ThreadPool & Async Behavior" otomatik yüklenir

Sol menü → Dashboards → ThreadPool & Async Behavior

### 3. Load Test Çalıştır

Grafana açık dururken başka terminalde:

```bash
# Senaryo A: Sync (kötü)
docker compose run --rm k6 run /scripts/stress-test.js -e ENDPOINT=sync

# 1 dakika bekle (pool soğusun), sonra:
docker compose run --rm k6 run /scripts/stress-test.js -e ENDPOINT=true-async

# 1 dakika bekle, sonra:
docker compose run --rm k6 run /scripts/stress-test.js -e ENDPOINT=fake-async
```

Her test sırasında Grafana'da:
- **Sync:** ThreadPool patlar, queue dolar, latency tavanı görür
- **True async:** ThreadPool sabit kalır, latency düz çizgi
- **Fake async:** Sync'e benzer kötü davranış

## Görüntülenecek Metrikler

Dashboard'da 7 panel var:

| Panel | Ne gösterir | Sync vs Async farkı |
|---|---|---|
| ThreadPool — Workers ve Queue | Aktif thread sayısı + bekleyen iş | Sync'te dramatik artış |
| Request Rate (RPS) | Saniyedeki request | True-async ~10x daha yüksek |
| Request Latency | p50/p95/p99 | Sync'te p95 patlar |
| GC Heap Size | Memory baskısı | Async daha az allocation |
| Aktif Request Sayısı | Eş zamanlı işlenen | Sync'te birikme |
| Mevcut ThreadPool Boyutu | O an havuzda | Sync: yüksek, Async: düşük |
| Kuyrukta Bekleyen İş | Backlog | Sync'te tehlikeli artış |

## Sabit Yük Testi (Davranış Analizi İçin)

Ramping yerine sabit VU ile ThreadPool davranışını izole görmek için:

```bash
# 500 VU, 2 dakika — sync senaryosu
docker compose run --rm k6 run /scripts/constant-load.js \
  -e ENDPOINT=sync -e VUS=500 -e DURATION=2m

# Aynı yük, true async
docker compose run --rm k6 run /scripts/constant-load.js \
  -e ENDPOINT=true-async -e VUS=500 -e DURATION=2m
```

Grafana'da iki test sırasındaki **ThreadPool grafikleri** çok farklı olacak.

## Logları İzleme

```bash
# API logları
docker compose logs -f api

# Tüm servisler
docker compose logs -f

# Sadece son 50 satır
docker compose logs --tail=50 api
```

## Senaryolar Arası Bekleme (Önemli)

Her test arasında **en az 1 dakika** bekle:
- ThreadPool eski thread'ler "expire" olur (genelde 20 saniye)
- Hill Climbing scale-up sıfırlanır
- Bir önceki testin "tortusu" kalmaz

## Sonuçları İndir

JSON çıktıları `./results/` klasörüne yazılır:

```bash
ls -lh results/
# summary-sync.json
# summary-true-async.json
# summary-fake-async.json
```

## Stack'i Durdur

```bash
# Stack'i durdur ama veriyi tut
docker compose stop

# Tamamen temizle (veri dahil)
docker compose down -v
```

## Mac M-Series Özel Notlar

### Docker Desktop Ayarları

System Settings → Resources:
- **CPUs:** En az 4 (8 önerilir)
- **Memory:** En az 6 GB (8 GB önerilir)

Eksik kaynak verilirse:
- API container yeterli thread alamaz
- k6 yük üretmekte zorlanır
- Test sonuçları gerçekçi olmaz

### ARM64 Native Çalışma

Tüm imajlar (.NET 8, k6, Prometheus, Grafana) ARM64 native destekler.

### Performans Beklentisi

M-serisi makinelerde:
- True async endpoint: **~5000-10000 RPS** (CPU sınırına yakın)
- Sync endpoint: **~80-200 RPS** (ThreadPool sınırlı)
- Fark oranı: **50-100x**