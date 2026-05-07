# Token Bucket

Rate limiting için en yaygın kullanılan algoritmalardan biri olan Token Bucket'ın .NET ile farklı implementasyon varyantları.

> Konu ile ilgili makaleye buradan [ulaşabilirsiniz](https://medium.com/@omurucum/token-bucket-net-ile-rate-limitingin-temel-algoritmas%C4%B1-cd66db171bde)

## İçerik

| Sınıf | Açıklama |
| --- | --- |
| `NaiveTokenBucket` | Algoritmanın en sade hali, thread-safe değil |
| `LockingTokenBucket` | `lock` ile thread-safe versiyon |
| `LockFreeTokenBucket` | `Interlocked.CompareExchange` tabanlı lock-free versiyon |
| `WaitingTokenBucket` | Token bekleme destekli async versiyon |

## Yapı

```
token-bucket/
    TokenBucket.sln
    src/
        TokenBucket/
```

## Çalıştırma

```bash
dotnet build
dotnet test
```