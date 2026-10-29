# FAZ 6-15 — ÖZET VE YAPI HARİTASI

> Bu dosya Faz 6-15'in yapısal özetini içerir. Her faz, ilgili faza gelindiğinde
> Faz 0-5 ile aynı detay seviyesinde yazılacaktır.
> Bu özet, AI'ın genel resmi görmesi ve fazlar arası bağımlılıkları anlaması için hazırlanmıştır.

---

## FAZ 6 — TRANSACTION SERVICE (Ledger / Çift Taraflı Muhasebe)

**PORT:** 5005 | **DB:** transaction_db | **Özel:** Event Sourcing (Marten)

**Ana görevler:**
- [ ] Marten event store kurulumu (NuGet: `Marten`)
- [ ] `LedgerEntry` entity: debit/credit kayıtları
- [ ] Çift taraflı muhasebe: her işlemde DR = CR invariant (toplam borç = toplam alacak)
- [ ] `POST /api/transactions` → yeni işlem kaydı (authorization onayından sonra)
- [ ] `GET /api/transactions/{id}` → işlem detayı
- [ ] `GET /api/transactions/by-account/{accountId}` → hesap ekstre
- [ ] Event'ler: `TransactionCreated`, `TransactionSettled`, `TransactionReversed`
- [ ] Kafka consumer: `AuthorizationApproved` event'ini dinle → otomatik işlem kaydı oluştur

**Kritik kural:** Bu serviste UPDATE/DELETE YOKTUR. Append-only. Hata düzeltme = yeni ters kayıt (reversal entry).

**Dosya yapısı:**
```
src\Services\MiniBank.TransactionService\
├── Domain\
│   ├── Entities\
│   │   ├── Transaction.cs          ← aggregate root
│   │   └── LedgerEntry.cs          ← DR/CR kaydı
│   └── Events\
│       ├── TransactionCreated.cs
│       └── TransactionSettled.cs
├── Infrastructure\
│   ├── Persistence\
│   │   └── TransactionStoreConfig.cs ← Marten konfigürasyonu
│   └── Consumers\
│       └── AuthorizationApprovedConsumer.cs
├── Api\
│   ├── Controllers\
│   │   └── TransactionController.cs
│   └── Dtos\
└── Program.cs
```

---

## FAZ 7 — INSTALLMENT SERVICE (Taksit Hesaplama)

**PORT:** 5006 | **DB:** installment_db

**Ana görevler:**
- [ ] Taksit hesaplama motoru: annuity formula
- [ ] Vergi hesaplama: KKDF (%15) + BSMV (%5)
- [ ] Kampanya tipleri: Standard, Deferred (ertelemeli), ZeroInterest (faizsiz), Extra (ekstra taksit)
- [ ] `POST /api/installments/calculate` → taksit planı hesapla (kaydetmeden)
- [ ] `POST /api/installments` → taksit planı oluştur
- [ ] `GET /api/installments/{planId}` → taksit planı detayı
- [ ] `POST /api/installments/{planId}/pay` → taksit ödemesi

**Formüller:**
```
Aylık taksit = Anapara × (r × (1+r)^n) / ((1+r)^n - 1)
  r = aylık faiz oranı
  n = taksit sayısı

KKDF = Faiz tutarı × %15
BSMV = Faiz tutarı × %5
Toplam maliyet = Anapara + Toplam faiz + KKDF + BSMV
```

**Dosya yapısı:**
```
src\Services\MiniBank.InstallmentService\
├── Domain\
│   ├── Entities\
│   │   ├── InstallmentPlan.cs      ← plan (kaç taksit, toplam tutar)
│   │   └── Installment.cs          ← tek taksit kaydı
│   └── Services\
│       └── InstallmentCalculator.cs ← saf hesaplama (side-effect yok)
├── Api\
│   ├── Controllers\
│   │   └── InstallmentController.cs
│   └── Dtos\
└── Program.cs
```

---

## FAZ 8 — FRAUD SERVICE (Dolandırıcılık Tespit)

**PORT:** 5007 | **DB:** fraud_db

**Ana görevler:**
- [ ] Kural motoru: velocity check (X dakikada Y'den fazla işlem)
- [ ] Redis sorted set ile sliding window implementasyonu
- [ ] Kara liste kontrolü (merchant, ülke, BIN)
- [ ] Tutar anomali tespiti (ortalamadan 3x sapma)
- [ ] `POST /api/fraud/check` → işlem için fraud skoru hesapla (0-100)
- [ ] Skor > 80 → otomatik red, 50-80 → 3D Secure yönlendirmesi
- [ ] Kural CRUD: `POST /api/fraud/rules`

**Redis velocity check akışı:**
```
Key: "velocity:{cardId}"
Sorted set: member = txn_id, score = unix_timestamp
ZADD → yeni işlem ekle
ZRANGEBYSCORE → son X dakikadaki işlemleri say
ZREMRANGEBYSCORE → eski kayıtları temizle
```

---

## FAZ 9 — EOD SERVICE (Gün Sonu Batch İşlemleri)

**PORT:** 5008 | **DB:** eod_db

**Ana görevler:**
- [ ] Quartz.NET scheduler kurulumu
- [ ] 11 adımlı EOD pipeline (sıralı çalışmalı):
  1. Cut-off: Yeni işlem kabul etme
  2. Pending → Settled: Onaylı provizyonları kesinleştir
  3. Takas hesaplama: Bankalar arası net pozisyon
  4. Limit sıfırlama: Günlük/haftalık limitler
  5. Faiz hesaplama: Kredi kartı borç faizi
  6. Minimum ödeme hesaplama: Ekstre tutarı
  7. Kart yenileme: Süresi dolan kartları işaretle
  8. Dormant hesap kontrolü: 1 yıl hareketsiz
  9. Ekstre oluşturma: PDF/JSON ekstre
  10. MASAK raporlama: Şüpheli işlem raporu
  11. Cut-off kaldır: Yeni işlem kabul etmeye başla

- [ ] `POST /api/eod/trigger` → manuel tetikleme (test için)
- [ ] `GET /api/eod/status` → son EOD çalışmasının durumu
- [ ] Hata durumunda step-by-step retry (bir adım fail ederse kaldığı yerden devam)

---

## FAZ 10 — RECONCILIATION SERVICE (Mutabakat)

**PORT:** 5009 | **DB:** reconcile_db

**Ana görevler:**
- [ ] Dahili mutabakat: Authorization kaydı ↔ Transaction kaydı eşleşmesi
- [ ] Harici mutabakat: Kendi kayıtlarımız ↔ BKM/Visa/MC dosyaları
- [ ] Eşleşmeyen kayıtlar → alarm + manuel inceleme kuyruğu
- [ ] `POST /api/reconciliation/run` → mutabakat çalıştır
- [ ] `GET /api/reconciliation/mismatches` → eşleşmeyen kayıtlar
- [ ] Stub: BKM dosya formatı (TC50/TC56) simülatörü

---

## FAZ 11 — NOTIFICATION SERVICE (Bildirim)

**PORT:** 5010 | **DB:** yok (stateless)

**Ana görevler:**
- [ ] Kafka consumer: event'leri dinle → bildirim gönder
- [ ] SMS stub (ISmsSender interface)
- [ ] Email stub (IEmailSender interface)
- [ ] Push notification stub (IPushSender interface)
- [ ] Template engine: "{cardLast4} ile {merchant}'ta {amount} TRY harcama yapıldı"
- [ ] Bildirim tercihleri: müşteri hangi kanaldan bildirim istiyor

**Dinlenecek event'ler:**
- `AuthorizationApproved` → SMS: "Kartınızla X TRY harcama yapıldı"
- `AuthorizationDeclined` → Push: "Kartınızla denenen X TRY işlem reddedildi"
- `AccountFrozen` → Email: "Hesabınız donduruldu"
- `CardBlocked` → SMS + Email: "Kartınız bloke edildi"

---

## FAZ 12 — API GATEWAY & UÇTAN UCA TEST

**PORT:** 5000

**Ana görevler:**
- [ ] YARP reverse proxy konfigürasyonu
- [ ] Route tanımları: `/iam/**` → 5001, `/accounts/**` → 5002, vb.
- [ ] Rate limiting middleware (IP bazlı, endpoint bazlı)
- [ ] Request/response logging
- [ ] Uçtan uca test senaryosu:
  1. Register → Login → Token al
  2. Müşteri oluştur → KYC onay
  3. Hesap aç (TRY vadesiz)
  4. Kart oluştur → Aktive et
  5. Günlük limit tanımla (5000 TRY)
  6. Ödeme isteği gönder (500 TRY) → onay
  7. Tekrar ödeme (5000 TRY) → limit aşıldı
  8. Provizyon iptali
  9. Bakiye kontrolü

---

## FAZ 13 — OBSERVABILITY & RESILIENCE

**Ana görevler:**
- [ ] OpenTelemetry entegrasyonu (tüm servisler)
- [ ] Jaeger'da distributed tracing görüntüleme
- [ ] Prometheus metrikleri: request count, latency, error rate
- [ ] Grafana dashboard: her servisin durumu
- [ ] Serilog → Elasticsearch → Kibana (log arama)
- [ ] Polly resilience: circuit breaker, retry, timeout (servisler arası çağrılarda)
- [ ] Health check aggregation: tüm servislerin durumu tek endpoint'ten

---

## FAZ 14 — GÜVENLİK SERTLEŞTİRME

**Ana görevler:**
- [ ] PCI-DSS kontrol listesi doğrulama
- [ ] KVKK: kişisel veri maskeleme loglarda
- [ ] API güvenliği: rate limit, CORS, HTTPS redirect
- [ ] JWT token'da ek güvenlik: jti claim, token blacklist
- [ ] Input validation: her endpoint'te model validation
- [ ] SQL injection koruması: parametreli sorgular (EF Core zaten yapıyor)
- [ ] HSM stub: kart verisi şifreleme interface'i
- [ ] Penetrasyon testi senaryoları

---

## FAZ 15 — YÜK TESTİ & PRODUCTION READINESS

**Ana görevler:**
- [ ] k6 veya NBomber ile yük testi
- [ ] Hedef: Authorization endpoint'i 1000 req/sec, p99 < 200ms
- [ ] Docker Compose ile tüm sistemin tek komutla ayağa kalkması
- [ ] README.md: kurulum, çalıştırma, mimari özet
- [ ] ADR (Architecture Decision Records) dokümanları
- [ ] Son kontrol: tüm testler geçiyor, tüm servisler sağlıklı

---

## FAZLAR ARASI BAĞIMLILIK GRAFİĞİ

```
Faz 0 (Altyapı)
  └── Faz 1 (Shared Kernel)
        ├── Faz 2 (IAM) ─────────────────────────┐
        ├── Faz 3 (Account) ──────────────────┐   │
        │     └── Faz 4 (Card) ───────────┐   │   │
        │           └── Faz 5 (Authorization)─┤   │
        │                 └── Faz 6 (Transaction)  │
        │                       └── Faz 7 (Installment)
        │                 └── Faz 8 (Fraud)        │
        │                 └── Faz 9 (EOD) ─────────┤
        │                       └── Faz 10 (Reconciliation)
        │                                          │
        └── Faz 11 (Notification) ← tüm event'leri dinler
              └── Faz 12 (API Gateway) ← tüm servisleri birleştirir
                    └── Faz 13 (Observability)
                          └── Faz 14 (Security)
                                └── Faz 15 (Load Test)
```

**KURAL:** Bir faza başlamadan önce bağımlılıklarının TAMAMLANMIŞ olması gerekir.
Örneğin Faz 5 (Authorization) için Faz 4 (Card) tamamlanmış olmalı.

---

*Her faz, ilgili sıraya gelindiğinde Faz 0-5 ile aynı detay seviyesinde yazılacaktır.*
