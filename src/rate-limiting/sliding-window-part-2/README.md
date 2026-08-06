# SlidingWindow.Api — Production'da Sliding Window

Rate Limiting serisi Parça 2'nin çalışır örnek kodu. İki limiter'ı bir arada gösterir:

- **In-memory (yerleşik):** .NET'in `SlidingWindowLimiter`'ı (segment tabanlı,
  `SegmentsPerWindow`), `CustomerId`'ye göre partition'lı. Çok replikada **Sahte Global** oluşur.
- **Redis global:** Sorted Set + atomik Lua ile paylaşılan tek sayaç. Tüm replikalar
  aynı Redis'e baktığından gerçek global limit uygulanır.

Kurulum: **60 saniyede en fazla 10 transfer / müşteri.**

## Çalıştırma

Tek gereksinim Docker'dır. `docker compose`, LB (nginx) → 3 API replikası → Redis'i ayağa kaldırır:

```bash
docker compose up --build
```

API `http://localhost:8080` üzerinden erişilir.

## Uç noktalar

| Method | Path | Limiter | Beklenen |
|--------|------|---------|----------|
| POST | `/api/transfers/in-memory` | Yerleşik, replika başına | ~30 kabul (3 × 10) — Sahte Global |
| POST | `/api/transfers` | Redis, global | ~10 kabul — gerçek global limit |
| GET | `/health` | — | `ok` |

Müşteri kimliği `X-Customer-Id` header'ından okunur (production'da JWT claim'i olmalı).

## Farkı gözlemleme

**In-memory (Sahte Global):** aynı müşteriyle 40 istek atın; 10'dan fazlası geçer,
çünkü her replika kendi sayacını tutar.

```bash
for i in $(seq 1 40); do
  curl -s -o /dev/null -w "%{http_code}\n" -X POST \
    http://localhost:8080/api/transfers/in-memory -H "X-Customer-Id: musteri-42"
done | sort | uniq -c
```

**Redis (gerçek global):** aynı test, ama bu kez ~10 tane `200`, gerisi `429`:

```bash
for i in $(seq 1 40); do
  curl -s -o /dev/null -w "%{http_code}\n" -X POST \
    http://localhost:8080/api/transfers -H "X-Customer-Id: musteri-42"
done | sort | uniq -c
```

Yanıt gövdesindeki `instance` alanı, isteğe hangi replikanın baktığını gösterir —
in-memory testte farklı instance'ların ayrı ayrı 10'a kadar saydığını görürsünüz.

> Not: in-memory sayısı tam 30 çıkmayabilir; nginx dağıtımı ve zamanlamaya göre
> değişir. Önemli olan 10'u belirgin biçimde aşması — Sahte Global'in kanıtı budur.

## Yapılandırma

- Redis bağlantısı: `ConnectionStrings__Redis` ortam değişkeni (compose'da `redis:6379`).
- Limit ve pencere: `Program.cs` içinde `PermitLimit` / `Window` (ikisi de 10 / 60 sn).
- Replika sayısı: compose'daki `api1/api2/api3` ve `nginx.conf` upstream'i.

## Production notları

- **Kimlik:** `X-Customer-Id` header'ı yalnızca test içindir; header spoof edilebilir.
  Production'da `ResolveCustomer` değerini doğrulanmış bir JWT claim'inden okuyun.
- **Clock drift:** Örnekte `now` uygulamadan geçiriliyor. Replika saatleri kayabileceğinden,
  sağlam kurulumda `now`'ı Redis'in `TIME` komutundan alın (tek zaman kaynağı).
- **Script önbelleği:** `ScriptEvaluateAsync`, EVALSHA'yı otomatik yönetir; script her
  çağrıda tekrar gönderilmez.
- **Redis latency / memory / hot key:** Yazının "Production'da tökezleten yerler"
  bölümündeki edge case'ler burada da geçerlidir.

## Doğrulama

Bu ortamda .NET SDK bulunmadığından kod burada derlenmedi; `docker compose` senin
makinende NuGet restore + build yapıp çalıştırır. Redis Lua limiter'ının **mantığı**
ayrıca birebir portlanıp test edildi (tek anda 15 → 10 kabul; pencere kayınca yeniden
10; 3 replika tek Redis → global 10). Sonuçlar beklendiği gibi.

## Repo yerleşimi (öneri)

Kendi düzenine göre, örneğin:
`article/src/rate-limiting/sliding-window/code/SlidingWindow.Api`
Bu klasörde `.gitignore` hazır — `bin/`, `obj/` bu kez takip edilmez.
