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

Müşteri kimliği `X-Customer-Id` header'ından okunur.

## Farkı gözlemleme

**In-memory :** aynı müşteriyle 40 istek atın; 10'dan fazlası geçer,
çünkü her replika kendi sayacını tutar.

```bash
for i in $(seq 1 40); do
  curl -s -o /dev/null -w "%{http_code}\n" -X POST \
    http://localhost:8080/api/transfers/in-memory -H "X-Customer-Id: musteri-42"
done | sort | uniq -c
```

**Redis :** aynı test, ama bu kez ~10 tane `200`, gerisi `429`:

```bash
for i in $(seq 1 40); do
  curl -s -o /dev/null -w "%{http_code}\n" -X POST \
    http://localhost:8080/api/transfers -H "X-Customer-Id: musteri-42"
done | sort | uniq -c
```