# Token Bucket

Rate limiting için en yaygın kullanılan algoritmalardan biri olan Token Bucket'ın .NET ile farklı implementasyon varyantları.

> Eşlik eden makale: _eklenecek_

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
├── TokenBucket.sln
├── src/
│   └── TokenBucket/           # Implementasyon
└── tests/
    └── TokenBucket.Tests/     # Unit testler
```

## Çalıştırma

```bash
dotnet build
dotnet test
```