# Test Komutları — Sliding Window / Azure

Her test için çalıştırılacak olan script ilgili başlık altındadır.

1) replika ayarı
2) warm-up
3) test

Kabul/red sayıları terminaldeki `uniq -c` çıktısından; latency ve custom metrikler Azure portal (Container App Metrics / Application Insights).

## Ortak değişkenler

```bash
APP_FQDN=$(az containerapp show -n $APP -g $RG --query properties.configuration.ingress.fqdn -o tsv)

FIXED="https://$APP_FQDN/api/transfers/fixed"
SLIDING="https://$APP_FQDN/api/transfers/in-memory"
TOKEN="https://$APP_FQDN/api/transfers/token-bucket"
REDIS="https://$APP_FQDN/api/transfers"
```

> Kural: Her test öncesinde yeni CID oluşturup temiz bir ölçüm hedefleniyor. Bu kısım önemlidir.
> Warm-up her zaman ayrı bir `warmup-*` müşterisiyle yapılır, sonuçları sayılmaz. Azure sonuçlarında `dN-` şeklinde bir ayrım yapacağız. KQL kodunda yazıda bahsettiğim kısım.
> Ortak değişkenler çalışılan terminal açık olduğu sürece geçerlidir. Terminal kapatıldığında silinir. 

---

# Test 1 — Fixed vs Sliding: Sınır Sızıntısı · 1 replika

```bash
az containerapp update -n $APP -g $RG --min-replicas 1 --max-replicas 1
```

## Fixed

```bash
# warm-up
for i in $(seq 1 200); do curl -s -o /dev/null -X POST "$FIXED" -H "X-Customer-Id: warmup-fixed"; done

CID="d1-fixed-$(date +%s)-$RANDOM"
echo "--- batch 1 ---"
for i in $(seq 1 10); do curl -s -o /dev/null -w "%{http_code}\n" -X POST "$FIXED" -H "X-Customer-Id: $CID"; done | sort | uniq -c
sleep 60
echo "--- batch 2 ---"
for i in $(seq 1 10); do curl -s -o /dev/null -w "%{http_code}\n" -X POST "$FIXED" -H "X-Customer-Id: $CID"; done | sort | uniq -c
```

## Sliding

```bash
# warm-up  (DİKKAT: $SLIDING)
for i in $(seq 1 200); do curl -s -o /dev/null -X POST "$SLIDING" -H "X-Customer-Id: warmup-sliding"; done

# test
CID="d1-sliding-$(date +%s)-$RANDOM"
echo "--- batch 1 ---"
for i in $(seq 1 10); do curl -s -o /dev/null -w "%{http_code}\n" -X POST "$SLIDING" -H "X-Customer-Id: $CID"; done | sort | uniq -c
sleep 30
echo "--- batch 2 ---"
for i in $(seq 1 10); do curl -s -o /dev/null -w "%{http_code}\n" -X POST "$SLIDING" -H "X-Customer-Id: $CID"; done | sort | uniq -c
```

# Test 2 — Sürekli trafik · 1 replika

60 sn boyunca ~2 istek/sn (120 istek). Üç endpoint ayrı ayrı.

## Sliding

```bash
for i in $(seq 1 200); do curl -s -o /dev/null -X POST "$SLIDING" -H "X-Customer-Id: warmup-sliding"; done

CID="d2-sliding-$(date +%s)-$RANDOM"
for i in $(seq 1 120); do
  curl -s -o /dev/null -w "%{http_code}\n" -X POST "$SLIDING" -H "X-Customer-Id: $CID"
  sleep 0.5
done | sort | uniq -c
```

## Fixed

```bash
for i in $(seq 1 200); do curl -s -o /dev/null -X POST "$FIXED" -H "X-Customer-Id: warmup-fixed"; done

CID="d2-fixed-$(date +%s)-$RANDOM"
for i in $(seq 1 120); do
  curl -s -o /dev/null -w "%{http_code}\n" -X POST "$FIXED" -H "X-Customer-Id: $CID"
  sleep 0.5
done | sort | uniq -c
```

## Token Bucket

```bash
for i in $(seq 1 200); do curl -s -o /dev/null -X POST "$TOKEN" -H "X-Customer-Id: warmup-token"; done

CID="d2-token-$(date +%s)-$RANDOM"
for i in $(seq 1 120); do
  curl -s -o /dev/null -w "%{http_code}\n" -X POST "$TOKEN" -H "X-Customer-Id: $CID"
  sleep 0.5
done | sort | uniq -c
```

# Test 3 — Burst · 1 replika

## Fixed

```bash
for i in $(seq 1 200); do curl -s -o /dev/null -X POST "$FIXED" -H "X-Customer-Id: warmup-fixed"; done

CID="d3-fixed-$(date +%s)-$RANDOM"
seq 1 100 | xargs -P 100 -I{} curl -s -o /dev/null -w "%{http_code}\n" -X POST "$FIXED" -H "X-Customer-Id: $CID" | sort | uniq -c
```

## Sliding

```bash
for i in $(seq 1 200); do curl -s -o /dev/null -X POST "$SLIDING" -H "X-Customer-Id: warmup-sliding"; done

CID="d3-sliding-$(date +%s)-$RANDOM"
seq 1 100 | xargs -P 100 -I{} curl -s -o /dev/null -w "%{http_code}\n" -X POST "$SLIDING" -H "X-Customer-Id: $CID" | sort | uniq -c
```

## Token Bucket

```bash
for i in $(seq 1 200); do curl -s -o /dev/null -X POST "$TOKEN" -H "X-Customer-Id: warmup-token"; done

CID="d3-token-$(date +%s)-$RANDOM"
seq 1 100 | xargs -P 100 -I{} curl -s -o /dev/null -w "%{http_code}\n" -X POST "$TOKEN" -H "X-Customer-Id: $CID" | sort | uniq -c
```

# Test 4 — Multi-replica in-memory: Sahte Global

```bash
az containerapp update -n $APP -g $RG --min-replicas 3 --max-replicas 3
sleep 30   # 3 replika ayağa kalksın
```

## Sliding (asıl kanıt)

```bash
for i in $(seq 1 200); do curl -s -o /dev/null -X POST "$SLIDING" -H "X-Customer-Id: warmup-sliding"; done

CID="d4-sliding-$(date +%s)-$RANDOM"
seq 1 40 | xargs -P 40 -I{} curl -s -o /dev/null -w "%{http_code}\n" -X POST "$SLIDING" -H "X-Customer-Id: $CID" | sort | uniq -c
```

## Fixed (opsiyonel, aynı etkiyi Fixed'de de göstermek için)

```bash
for i in $(seq 1 200); do curl -s -o /dev/null -X POST "$FIXED" -H "X-Customer-Id: warmup-fixed"; done

CID="d4-fixed-$(date +%s)-$RANDOM"
seq 1 40 | xargs -P 40 -I{} curl -s -o /dev/null -w "%{http_code}\n" -X POST "$FIXED" -H "X-Customer-Id: $CID" | sort | uniq -c
```


# Test 5 — Redis ile global limit 

```bash
for i in $(seq 1 200); do curl -s -o /dev/null -X POST "$REDIS" -H "X-Customer-Id: warmup-redis"; done

CID="d5-$(date +%s)-$RANDOM"
seq 1 40 | xargs -P 40 -I{} curl -s -o /dev/null -w "%{http_code}\n" -X POST "$REDIS" -H "X-Customer-Id: $CID" | sort | uniq -c
```