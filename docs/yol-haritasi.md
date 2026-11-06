# Sketch — Mimari ve Yol Haritası

Teknoloji: C# / .NET 10 (LTS) + WPF + OpenGL (Silk.NET). Davranış referansı: `docs/sketchup-analizi/`.

## 1. Çözüm (solution) yapısı

Bağımlılık yönü yukarıdan aşağıya; çekirdek hiçbir arayüz veya çizim kütüphanesine bağlı değildir ve tek başına test edilebilir.

```text
Sketch.sln
├─ src/
│  ├─ Sketch.Geometry    Vektör/nokta/düzlem/dönüşüm matematiği, toleranslar (0,001")
│  ├─ Sketch.Model       Topoloji: Vertex, Edge, Face, Loop; Entities; Group/Component; Tag; Material; undo/redo
│  ├─ Sketch.Operations  Yüz oluşturma, kenar/yüz bölme, Push/Pull, Move (autofold), silme, kesişim
│  ├─ Sketch.Inference   Çıkarım motoru: nokta/doğru çıkarımları, eksen kilitleri, öncelik
│  ├─ Sketch.Tools       Araç durum makineleri (Select, Line, Rectangle, Circle, Push/Pull, Move, Eraser, Orbit/Pan/Zoom)
│  ├─ Sketch.Rendering   OpenGL çizim: yüz üçgenleme, kenar çizimi, seçim vurgusu, çıkarım ipuçları
│  ├─ Sketch.IO          Kendi dosya biçimimiz (.sketch: sürümlü, sıkıştırılmış JSON veya ikili — karar M6'da)
│  └─ Sketch.App         WPF kabuğu: menü, araç çubuğu, durum çubuğu, ölçü kutusu, paneller, komut kataloğu
└─ tests/
   ├─ Sketch.Model.Tests
   ├─ Sketch.Operations.Tests
   └─ Sketch.Inference.Tests
```

## 2. Temel tasarım ilkeleri (araştırmadan çıkanlar)

- **Yapışkan geometri:** Kenarlar ve yüzler bağlı bir topoloji oluşturur; kapalı eş düzlemli kenarlar otomatik yüz oluşturur; yeni kenar mevcut kenarı/yüzü böler; kenar silinince bağlı yüz silinir. (02, 04)
- **Topoloji:** Kenar = 2 köşe; yüz = 1 dış döngü + delik başına 1 iç döngü; bir kenar birden çok yüze ait olabilir. (04)
- **Gruplar/bileşenler:** Tanım (definition) + örnek (instance) + 4×4 dönüşüm. Grup = örneği tek olan, düzenlenince sessizce benzersizleşen bileşen. (02, 04)
- **Araçlar:** Her araç bir durum makinesidir; fare hareketinde önizleme çizer, tıklamada modeli tek bir geri alma işlemiyle değiştirir; ölçü kutusu girişini alır. (04)
- **Çıkarım:** Tüm araçların paylaştığı tek servis; ok tuşlarıyla eksen kilidi, Shift ile yön kilidi. (02)
- **Komut sistemi:** Her komut tek bir katalog kaydı (anahtar, başlık, menü etiketi, ipucu, açıklama, arama etiketleri, kısayol). (05)
- **İç birim:** İnç (SketchUp ile uyumlu); arayüzde kullanıcı birimine çevrilir. (04)

## 3. MVP aşamaları

Durum (2026-10-20): M0–M6 tamamlandı (MVP). Araçlar: Select, Eraser, Line, Rectangle, Circle, Push/Pull, Move (Ctrl kopya, dizi), Orbit, Pan, Zoom, Zoom Extents, standart görünümler, paralel/perspektif, geri al/yinele, `.sketch` kaydet/aç, PNG dışa aktarma (test modunda). 53 birim testi.

| Aşama | Kapsam | Bitti sayılma ölçütü |
|---|---|---|
| M0 | Ortam ve iskelet: .NET SDK, çözüm yapısı, boş WPF penceresi + OpenGL görünümü, test projeleri | Uygulama açılır, boş 3D görünüm ve eksenler çizilir; testler çalışır |
| M1 | Kamera: Orbit (orta tuş), Pan (Shift+orta), imlece doğru zoom, Zoom Extents, perspektif/paralel, standart görünümler | SketchUp'taki fare davranışıyla karşılaştırmalı kontrol |
| M2 | Model çekirdeği: Vertex/Edge/Face/Loop, geri al/yinele, otomatik yüz oluşturma, kenar/yüz bölme | Birim testleri: kare çizince yüz oluşur, köşegen yüzü ikiye böler, kenar silinince yüz silinir |
| M3 | Çıkarım motoru: uç nokta, orta nokta, kenar üstü, yüz üstü, eksen yönleri, paralel/dik, kilitler | Testler + görsel ipuçları |
| M4 | Çizim araçları: Line, Rectangle, Circle + ölçü kutusu (uzunluk, `a,b`, segment sayısı) | Ölçü girilerek kesin boyutlu çizim |
| M5 | Düzenleme araçları: Select (pencere/kesişim seçimi, çift/üçlü tık), Eraser, Move (Ctrl kopya, dizi, autofold), Push/Pull | Kutu çiz → Push/Pull → kenar taşı senaryosu |
| M6 | Kaydet/aç: `.sketch` biçimi, sürüm alanı | Kaydedilen model aynen geri açılır (gidiş-dönüş testi) |

## 4. MVP sonrası

Durum (2026-10-20) — tamamlananlar:
- Kullanıcının termal modelleme iş akışı (`context/kullanim-senaryosu.md`): termal şablon (etiket listesi, Glass/NonSlidingDoor, sahne sekmeleri), etiketler, malzemeler, gruplar (iç içe, adlandırma, içine girip düzenleme, patlatma), gizle/göster.
- Sağ panel: Varlık Bilgisi (etiket, alan/uzunluk, malzeme, ad, gizli), Malzemeler, Etiketler (görünürlük, renk, etkin etiket, etikete göre renklendirme), Anahat. Üst araç çubuğunda etiket seçici, sahne sekmeleri.
- Görünüm stilleri: tel kafes, gizli çizgi, gölgeli, malzemeli, tek renk, X-ışını, arka kenarlar.
- `.skp` açma ve içe aktarma: SketchUp 2021 ve sonrası dosyalar Sketch'in kendi okuyucusuyla (`SkpNativeReader`, yapı: `docs/skp-bicimi.md`) SketchUp kurulu olmadan okunur; daha eski dosyalar için kurulu SketchUp'ın C API kütüphanesi (`SketchUpAPI.dll`) yedek yoldur. Yüzler, kenarlar, gruplar/bileşenler (grup olarak), resimler, metin notları, kılavuzlar, kesit düzlemleri (etkin kesit dahil), ölçüler, sahneler (ad, kamera, gizli etiketler), etiketler (görünürlük ve renk), malzemeler ve gizlilik okunur. Yazma (`.skp` kaydetme) yok; kayıt `.sketch` olur.
- Araçlar: Boya Kovası, Etiket aracı, Yay, Çokgen, Offset, Döndür (Ctrl kopya, dairesel dizi), Ölçekle (tutamaçlar), Follow Me (yol önce seçilir), Şerit Metre ve kılavuzlar, İletki, Ölçü, Metin, Pencereyle Yakınlaştır, Önceki Görünüm.

- Ekim 2026 ikinci tur: Aynala (Flip, Ctrl = aynalı kopya, düzlem sürükleme, yüz düzlemi), Kement (serbest/çokgen), Serbest Çizim, Döndürülmüş Dikdörtgen, Merkez Yay, 3 Noktalı Yay, Dilim, Eksenler (çizim eksenleri taşınır/döndürülür, dosyaya kaydedilir), Kesit Düzlemi (etkin kesit kırpar, kesit çizgileri; ters çevir/etkin kesit), 3D Metin (dolu/kalınlıklı, grup olarak), Bileşen Yap (şimdilik adlandırılmış grup), Ara (Shift+S). Araç çubukları SketchUp'ın Getting Started + Views (üst) ve Large Tool Set (sol) düzeninde; özgün simge seti (`tools/icons/generate_icons.py`). Yalnızca paralel projeksiyon.

- Arayüz tamamen İngilizce (SketchUp'taki komut/menü/panel/ipucu adları); eksen çizgisi bir kenarı kestiğinde "On Edge" yapışması.

- DEAP Evidence Tool - Design Stage (üst araç çubuğunda buton): EDC'nin SketchUp eklentisinin (v2.0) hesabı ve CSV'si (`src/Sketch.Operations/Deap/DeapEvidence.cs`); Glass = pencere, Door (eklentide wood) = kapı. Doğruluk: elle hesaplanan testler + `tests/Fixtures/deap-golden.txt` ile eklentinin CSV'leriyle karşılaştırma. 2026-10-21: Kinsale Rd Block 4 — 62/62 daire ve 199 açıklık satırı eklentiyle birebir aynı. Açıklık etiketleri (kimlik, yön, alan) modele grup olarak eklenmez; ekran kaplaması olarak gösterilir (View > DEAP Opening Labels), eklentinin bıraktığı QA grupları DEAP çalışınca silinir.

- 2026-10-21: Büyük modellerde performans (grup bazlı önbellekler; Block 4'te kare ~2–6 ms). SKP içindeki resimler (kat planı PNG'leri) okunur. Grup düzenlenirken modelin geri kalanı gizlenir (View > Component Edit > Hide Rest of Model). Outliner'da kök başlık yok; her grup ayrı satır. Dikdörtgen ve yayların düzlemi köşe/kenardan başlayınca imleci izler; model yazıları yüzlerin arkasında gizlenir.

- 2026-10-21 (ikinci tur, bu bilgisayar): performans (çıkarım ızgarası, grup kutusu ön elemesi, vsync kapalı, Intel'de 4x MSAA), resimli grup vurgulama/seçim, Outliner Ctrl/Shift, DEAP etiketleri ekran kaplaması, geri al kapsamı (etiket/malzeme/sahne/eksen), Open Recent, PNG dışa aktarma, Window > Default Tray, araç geri çağrılarında hata güvenliği, `5x` dizi güvenliği, betik testleri `tests/scripts/`.

- 2026-10-22: Sürükleme akıcılığı (Block 4): araç önizlemesinde geri yüklenen kapsayıcıdaki değişmemiş gruplar yeniden kurulmuyor; Outliner ve etiket listeleri yalnız içerikleri değişince yenileniyor; çıkarım ekran dizininin grup kısmı kamera ve gruplar aynıyken korunuyor. Kökte Push/Pull ~500 → ~17 ms/adım, Move ~390 → ~19 ms, grup içinde ~115 → ~24 ms (ölçüm: `dragbench`).

- 2026-11-06: Explode artık grubun her yüzünü (delikleri, etiketi, malzemesi ve yönüyle) aynen çıkarıyor; önceden yalnız kenarlardan kendiliğinden oluşan yüzler kalıyor, yan yana döşemeler ve cam yüzleri kayboluyordu (Block 4'te 62 dairenin 62'si birebir). `.skp`'den gelen Thermal Elements sahneleri etikete göre renklendiriyor.

Kalanlar (sıra kullanıcıyla belirlenecek):

`.skp` okuyucuda kalan bilinmeyen kayıtlar (`docs/skp-bicimi.md` sonu) ve `.skp` yazma; autofold'da delikli yüzlerin delikleri; OpenGL hata denetimi; Flip aracında model eksenleri; bileşenler (paylaşılan tanım, örnekler arası bağ), gölgeler, Solid Tools, Follow Me'nin sürükleme modu, Kesişim (Intersect Faces), içe/dışa aktarma (OBJ/STL/DAE, .skp kaydetme), Sandbox araçları, alan raporu (etikete göre).

## 5. Açık noktalar

- Çıkarım öncelik sırası ve renkleri gerçek SketchUp'ta gözlemlenerek ayarlanacak (`sketchup-analizi/README.md`).
- Dosya biçimi (JSON mu ikili mi) M6'da karara bağlanacak.
- `.skp` okuma: 2021+ dosyalar SketchUp olmadan açılır (2026-10-21); 2020 ve eski dosyalar yalnızca kurulu SketchUp ile. Okuyucunun henüz bilmediği kayıtlar `docs/skp-bicimi.md` sonunda listelidir.
