# Rıza Kapsamlı Soru Listesi — Geliştiriciye Sorulacaklar ve Beklenen Cevaplar

*7 Eylül 2026 · Kapsam: yalnızca rıza alanı — rızanın modeli, yaşam döngüsü, sahipliği, değişimi, politika ve denetim izi, müşteri yüzü. Yetkilendirme sunucusu, gateway, Keycloak altyapısı, servisler arası kimlik ve operasyon konuları bilinçli olarak dışarıda bırakıldı; onlar `gelistirici-soru-listesi.md` ve `keycloak-baglaminda-yeni-bulgular-ve-sorular.md` içinde duruyor. Kullanım hedefi: ÖHVPS + servis modeli bankacılığında üçüncü parti API'ler için `consent-core`'un yeniden kullanımı.*

---

## 0 · Elenen sorular ve gerekçesi

| Elenen | Neden rıza kapsamı dışında |
|---|---|
| S-1, S-2, S-8, S-31 | Yetkilendirme sunucusu tasarımı (PAR/PKCE, `/oauth/token`, imza anahtarı, JWT/opak) |
| S-4, S-5, S-23, S-26, S-27, S-39, S-40 | Keycloak rolü, sürümü, servis kimliği, admin kimliği, KYC, kimlik doğrulama UX'i |
| S-11, S-14, S-15, S-16, S-17 | Gateway/PDP/mTLS/log/Redis — istek yolu güvenliği |
| S-18, S-19, S-20, S-32, S-34 | Üretim yolu, webhook, CI, detached JWS, test stratejisi (genel) |
| S-9 | Olay şeması sözleşmesi — rıza olayını taşısa da altyapı sorusu; R-22'ye küçültülerek alındı |
| S-33 | Callback'te vazgeçme tespiti — kimlik tarafı; yalnız "07/13 kodlarının kaynağı" kısmı R-9'a alındı |
| S-38 | Belirteç modeli — yalnız "rıza denetimi nerede, ne sıklıkla" kısmı R-13'e alındı |

Kalanlar (S-3, S-6, S-7, S-10, S-12, S-13, S-21, S-24, S-25, S-28, S-29, S-30, S-35, S-36, S-37, S-41, S-42, S-43, S-44) rıza soruları olarak yeniden yazıldı; eski numara parantezde. Daha önce sorulmamış rıza soruları da eklendi.

---

## A · Rızanın tanımı ve türleri

**R-1 (S-35). "Consent tarafını kullanacağız" derken kastedilen tam olarak ne: yalnız `consent-core` (kayıt + FSM + politika + defter) mi, rızaya bağlı belirteç basan `identity-security` de mi?**
Beklenen cevap: "`consent-core` kural motoru ve defter olarak; belirteç modeli BaaS için ayrıca tasarlanacak." Bu cevap, çekirdeğin belirteçten bağımsız bir rıza kaydı/PDP olarak konumlandığını gösterir. "İkisi birlikte, olduğu gibi" cevabı, rıza = tek işlem varsayımının BaaS'a taşınacağı anlamına gelir ve ilk sorun R-6'da çıkar.

**R-2 (S-36). Servis bankacılığında hangi rıza türleri olacak: ilişki rızası (arayüz geliştirici üzerinden müşteri olma), sözleşme kabulü, KVKK açık rızası, sır niteliğindeki bilgi paylaşım rızası, işlem rızası? Bunlar ayrı kayıtlar mı, tek kaydın alt başlıkları mı?**
Beklenen cevap: Ayrı kayıtlar, çünkü süreleri, iptal etkileri ve dayanakları farklıdır (sözleşme kabulü ilişkiyi kurar, KVKK rızası geri alınınca ilişki bitmeyebilir ama veri paylaşımı durur). `ConsentType` enum'unun ve `TransitionTable`'ın tür başına ayrışacağı kabul edilmeli. "Hepsi tek rıza kaydı" cevabı kısmi iptali (yalnız veri paylaşımını geri alma) imkânsız kılar.

**R-3. Her rıza türünün hukuki dayanağı ve düzenleyici referansı kayıtta tutulacak mı?**
Beklenen cevap: Evet; `Channel.LegalBasis` bunu kanal düzeyinde tutuyor, rıza düzeyine inmeli. Denetçi "bu paylaşım hangi dayanakla yapıldı" sorusunu rıza kaydından okumalı. Kamu talebi gibi rızaya dayanmayan erişimlerin de (PUBLIC_SECTOR) ayrı bir kayıt türü olması gerekir.

**R-4 (S-24). Rızanın öznesi hangi anahtarla tutulacak: TCKN (`kmlkVrs`) mi, müşteri numarası mı? Defterdeki `actor("CUSTOMER", customerId)` satırlarında ham TCKN yazılması kabul mü?**
Beklenen cevap: Müşteri numarası ya da takma anahtar; TCKN yalnızca eşleme tablosunda, deftere girmez. GAP-11 belirteçten TCKN'yi çıkardı, aynı ilke rıza kaydı ve defter için de geçerli olmalı. Tüzel kişi müşteride özne şirket, yetkili temsilci ayrı alan olarak tutulmalı.

**R-5. Rıza kapsamı hangi granülerlikte: hesap listesi (`permittedAccounts`), veri kategorisi (`dataTypes`), amaç, tutar/adet sınırı? Sır paylaşımı için amaç ve kategori zorunlu mu olacak?**
Beklenen cevap: Amaç ve kategori zorunlu; `InformationGrant.dataTypes` genişletilir, amaç alanı eklenir. "Scope string yeter" cevabı, sır paylaşım yönetmeliğinin "gerekli olanla sınırlı" beklentisini karşılamaz.

---

## B · Yaşam döngüsü: yetkilendirme, belirteç bağı, süre, iptal

**R-6. İlişki rızası altında aynı müşteri birden fazla cihaz/oturumdan işlem yapacak. Bugünkü `AUTHORIZED → TOKEN_USED` tek geçişi ve tek refresh zinciri bu modelde nasıl değişecek?**
Beklenen cevap: İşlem rızası için tek kullanım korunur; ilişki rızası için `ACTIVE` benzeri, çok belirteç/oturuma izin veren ayrı bir durum ve geçiş tablosu tanımlanır. "Her oturum için yeni rıza" cevabı müşteriyi her girişte onaya götürür.

**R-7 (S-28, S-29). Rızayı `AUTHORIZED`'a taşıyan `/authorize` çağrısı bugün dış tarafın gönderdiği `customerId` ile çalışıyor ve üç public yoldan erişilebiliyor; yetki kodu hiç doğrulanmıyor. Hedef tasarımda rızanın yetkilendirildiğine dair kanıt ne olacak ve kimden gelecek?**
Beklenen cevap: `/authorize` yalnızca bankanın kimlik doğrulama sonucunu taşıyan iç bileşenden çağrılır; kanıt olarak `acr/amr`, oturum kimliği ve zaman damgası `scaReference` yerine yapılandırılmış alan olarak deftere yazılır (S-3). Yetki kodu rıza + taraf + tek kullanım + süreye bağlanır ve belirteç öncesi tüketilir. "Adaptör zaten kontrol ediyor" cevabı doğru değil; hiçbiri etmiyor.

**R-8 (S-25). SCA politikası rıza türüne göre değişecek mi: işlem rızasında her seferinde yeniden doğrulama, ilişki rızasında oturum kabulü, tutar eşiğinde step-up?**
Beklenen cevap: Evet ve kural `consent-core` politika kancasında (`sca-exemption` gibi) yaşar, kimlik katmanında değil. Hangi durumda hangi `acr` seviyesinin yeterli sayıldığı yazılı olmalı.

**R-9 (S-33 kısmi). İptal detay kodları tablosu ÖHVPS 2.0.0'dan alınmış. BaaS rızaları için kod tablosu ne olacak; 07/13 gibi GKD'ye özgü kodların kaynağı kimlik katmanı değiştiğinde nereden gelecek?**
Beklenen cevap: Kod tablosu standarda göre ayrışır (çekirdek kanonik sebep tutar, adaptör tel koduna çevirir); BaaS için bankanın kendi sebep listesi tanımlanır. `abandon-sca?duplicateCall` gibi parametreyle sebep seçtirmek yerine sebep, tespit eden bileşenden yapılandırılmış olarak gelir.

**R-10. Varsayılan geçerlilik süreleri (AIS 6 ay, ödeme 24 saat) ve süpürme eşikleri (bekleyen 3 dk, yetkilendirilmiş 10 dk, kullanılmamış belirteç 30 dk) hangi dayanakla seçildi; üretim değerleri ne?**
Beklenen cevap: Her sürenin düzenleyici ya da ürün dayanağı tek satırla belirtilir; demo kısaltmaları (`pending-timeout: PT3M`) yapılandırmadan üretim değerine çekilir. "Demoda böyleydi" cevabı yalnız S6 için kabul edilebilir.

**R-11. Süresiz rıza yapısal olarak yasak (`validUntil` null olamaz). İlişki rızası altı ayda dolamayacağına göre yenileme kadansı ne: süre uzatma mı, periyodik yeniden onay mı?**
Beklenen cevap: Periyodik yeniden onay ya da "sözleşme yaşadıkça geçerli, yıllık teyit"; kural rıza türüne bağlanır. Yasağın kaldırılması değil, türe göre uzun üst sınır beklenir.

**R-12 (S-7). Rıza iptal edilince belirteç çağlayanı olay üzerinden çalışıyor ve Kafka kesintisinde 15 dakikaya kadar pencere var. Rıza tarafında bu pencere kabul mü; "iptal anında erişim kesilir" garantisi nerede verilecek?**
Beklenen cevap: Ya rıza denetimi her çağrıda çekirdeğe sorulur (R-13) ya da outbox ile olay garantisi verilir; pencere yazılı kabul olarak dokümana girer. "Milisaniyelik" ifadesi Kafka ayaktayken doğru, düşerken değil.

**R-13 (S-38 kısmi). Rıza denetimi nerede ve ne sıklıkla yapılacak: belirteç üretiminde bir kez mi (bugünkü), her API çağrısında çekirdeğe sorarak mı?**
Beklenen cevap: İlişki rızası için her çağrıda (ya da kısa TTL önbellekle) çekirdek PDP olarak sorulur; işlem rızası için üretimde bir kez yeterlidir. Bu karar iptal penceresini, kapasite planını ve `consent-core`'un gecikme bütçesini belirler; verilmiş olmalı.

**R-14 (S-12). Tek kullanımlık rıza `TERMINATED` olduktan sonra durum sorgularının sertifika-only çalışması hangi spesifikasyon maddesine dayanıyor? BaaS'ta kapanmış rızanın ne kadar süre sorgulanabilir kalacağı tanımlı mı?**
Beklenen cevap: ÖHVPS için madde referansı; BaaS için "kapanıştan sonra N gün sorgulanabilir, sonra yalnız defterden" gibi bir kural.

**R-15 (S-13). `requireOperable` kontrol sırası (durum → süre → kapsam → hesap → tutar → kalan kullanım) ve hata kodları spesifikasyondan mı türetildi? Her ret deftere yazılıyor mu ve hangi kodla?**
Beklenen cevap: Sıra gerekçeli ve sabit; her ret `DENY` olarak deftere düşer; ÖHVPS `hataYaniti` eşlemesi test edilir. BaaS için aynı sıra, farklı hata zarfı.

---

## C · Sahiplik, izolasyon, bütünlük

**R-16 (S-30). Çağıran tarafın rızanın sahibi olduğu hiçbir çekirdek işlemde doğrulanmıyor (`require()` yalnız varlık + standart sınırı). Bilinçli bir "adaptör sorumluluğu" kararı mı? BaaS'ta bu, arayüz geliştiriciler arası izolasyon demek.**
Beklenen cevap: "Atlanmış; `require(reference, party)` çekirdeğe eklenecek, standart sınırıyla aynı 404 davranışı." Adaptöre bırakmak kabul edilemez; adaptörler de yapmıyor.

**R-17. Rıza numarası `CNS-<epoch ms mod 10^8>-<4 hane>` formatında; tahmin edilebilir ve yük altında çakışabilir. Üretim formatı ne olacak?**
Beklenen cevap: 128 bit rastgele ya da UUIDv7; ÖHVPS `rizaNo` uzunluk kısıtına uyan bir kodlama. Numaranın webhook ve defterde dolaştığı hatırlatılmalı.

**R-18 (S-37). Bir müşteri arayüz geliştirici A üzerinden bankaya geldiyse, B aynı müşteri için rıza açabilir mi? AIS'teki "üçlü başına tek aktif rıza" kuralı BaaS'ta nasıl uyarlanacak?**
Beklenen cevap: Ürün kararı yazılı olmalı; teknik olarak tekillik indeksi (taraf, müşteri, tür) BaaS türlerine de uygulanır. "Müşteri karar verir" cevabı geçerli ama o zaman müşterinin rızalarını gördüğü bir ekran (R-27) şarttır.

**R-19. Geçersiz ek claim isteğinin rızayı harcadığı hata bir kez yaşanmış. Rızayı tüketen işlemlerin listesi ve her birinin idempotent olduğu nasıl garanti ediliyor?**
Beklenen cevap: Tüketen işlemler tek yerde listelenir (token-grant, execution cycle); `token-grant` idempotency anahtarı taşır; "doğrulama önce, tüketim sonra" kuralı test ile korunur.

**R-20 (S-6). Çekirdekte kalan paralel belirteç yolu (`issueToken`/`refreshToken`, `/tokens/refresh`) ve `Token` tablosu ne olacak? Rıza kaydı hangi belirteç tablosunu "kendi bağlı belirteçleri" olarak görecek?**
Beklenen cevap: Çekirdekteki üretim kodu silinir, `revokeBoundTokens` yalnız çağlayan olayını yayınlar; belirteç tek yerde yaşar. İki tablonun ayrışması BaaS'ta çoklu oturumla birlikte görünür hata üretir.

---

## D · Değişiklik, versiyon, toplu işlemler

**R-21 (S-10). "Rıza değiştirme" bilinçli olarak yok; 01 ve 15 kodları tanımlı ama akış yok. Hem ÖHVPS hesap rızası güncellemesi hem BaaS için atomik "yeni rıza aç + eskisini 01/15 ile kapat + iki kaydı bağla" ucu planlanıyor mu?**
Beklenen cevap: Evet, tek işlem içinde; tekillik kuralıyla sıralama çatışmasını platform çözer, çağıran değil. Defterde `supersededBy` bağı görünür.

**R-22 (S-42). Sözleşme ya da rıza metni değiştiğinde mevcut rızalar ne olur: yeniden onay, versiyon bağı, belirli sürede otomatik kapanış? Rıza kaydı metnin versiyonunu/hash'ini tutuyor mu?**
Beklenen cevap: Rıza, onaylanan metnin versiyon kimliğini ve hash'ini taşır; metin değişince yeni versiyon rıza R-21 akışıyla açılır. "Metin rızada tutulmuyor" cevabı ispat zincirini kırar.

**R-23 (S-43). Bir arayüz geliştiricinin sözleşmesi feshedildiğinde on binlerce rızanın toplu iptali nasıl yapılacak? Bugünkü iptal tek rıza, tek olay; defter zincir ucunda sıralanıyor.**
Beklenen cevap: Taraf bazlı toplu iptal ucu, tek sebep kodu, parti parti işleme ve müşteri bildirimi; defter yazımının bu yükü kaldıracağı ölçülmüş (R-25).

**R-24. Rıza kaydı kapanıştan sonra ne kadar saklanacak ve KVKK silme talebi ile değiştirilemez defter nasıl bağdaştırılacak?**
Beklenen cevap: Rıza kaydı için saklama süresi (düzenleyici asgari + ürün kararı); defterde kişisel veri yerine takma anahtar (R-4) tutulduğu için silme talebi eşleme tablosunda karşılanır, zincir bozulmaz. "Defter silinemez, o yüzden KVKK talebi reddedilir" cevabı kabul edilemez.

---

## E · Politika kancaları ve denetim defteri

**R-25 (S-21). Defter yazımları zincir ucunda sıralanıyor. BaaS ölçeğinde (yüz binlerce müşteri, R-13'te her çağrıda denetim) bu darboğaz ölçüldü mü; bölümleme (gün/taraf bazlı zincir) düşünüldü mü?**
Beklenen cevap: Hedef TPS ve ölçüm sonucu; gerekirse taraf ya da gün bazlı zincirler ve dönemsel kök hash. "Ölçmedik" cevabı BaaS kararından önce kapatılmalı.

**R-26 (S-44). Deftere cihaz, IP, oturum kimliği, kanal ve `acr` yazılacak mı? Bugünkü alanlar ÖHVPS için tasarlandı; e-bankacılık iz beklentisi daha geniş.**
Beklenen cevap: Evet; alanlar `details` içine değil, sorgulanabilir sütunlara eklenir ve hash girdisine dahil edilir.

**R-27. Politika kancaları (`amount-limit`, `participation-product`, `sca-exemption`, `risk-fraud`) rıza oluşturma ile yürütme arasında hangi noktada çalışıyor ve BaaS için hangi yeni kancalar gerekecek (arayüz geliştirici bazlı ürün/limit kuralları)? Anahtar değişiklikleri deftere yazılıyor mu?**
Beklenen cevap: Her kancanın çalıştığı nokta tablo olarak; taraf bazlı kural için `PolicyContext`'e parti alanı eklenir; `toggle` çağrısı deftere `POLICY.CHANGED` olarak düşer ve kim değiştirdi bilgisi taşır.

**R-28. Rıza reddi (politika ya da tekillik) deftere yazılıyor ama rıza kaydı oluşmuyor. Denetçi "reddedilen rıza taleplerini" nasıl listeleyecek?**
Beklenen cevap: `/audit/denials` benzeri bir görünüm taraf/müşteri/sebep filtreli olarak; ret satırı talebin parmak izini taşır.

---

## F · Müşteri yüzü

**R-29 (S-41). Müşteri, verdiği rızaları bankanın kendi kanalından görebilecek ve iptal edebilecek mi? O kanal `consent-core`'a hangi yoldan bağlanacak?**
Beklenen cevap: Evet; müşteri bazlı listeleme ve iptal ucu (`byCustomer`, müşteri aktörlü `cancel`), banka mobil/şube kanalından iç çağrı ile. Bugün iptal yalnız taraf ya da süpürme kaynaklı; müşteri aktörü yok.

**R-30. Müşteriye gösterilecek rıza metni ve özet (kim, ne için, hangi hesaplar, ne zamana kadar) rıza kaydından üretilebiliyor mu, yoksa adaptörün tel gövdesinde mi kalıyor?**
Beklenen cevap: Kanonik kayıttan üretilir; `BAAS_EMBEDDED` kanalında "müşteri bankayı görmeyebilir" notunun karşılığı olarak metin kanal bazlı şablonlanır. Enum yorumundaki bu fark bugün kodda uygulanmıyor.

**R-31. Rıza olaylarının müşteriye bildirimi (verildi, kullanıldı, iptal edildi) planlanıyor mu? Bugün bildirim yalnız dış tarafa webhook.**
Beklenen cevap: `consent.state.changed` olayının ikinci tüketicisi bankanın müşteri bildirim kanalı; hangi geçişlerin müşteriye gideceği tabloda.

---

### Görüşme notu

Öncelik sırası: **R-1, R-6, R-7, R-16, R-13.** Bu beşin cevabı, `consent-core`'un BaaS'ta "işlem rızası motoru" mu yoksa "ilişki rızası kaydı + PDP" mi olacağını belirler; geri kalan sorular bu karara göre şekillenir. R-7 ve R-16 hangi karar verilirse verilsin bugün açık ve Keycloak beklemeden kapatılmalı.
