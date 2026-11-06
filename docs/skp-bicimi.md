# SketchUp 2021+ `.skp` dosya yapısı (temiz oda notları)

Bu belge, Sketch'in kendi `.skp` okuyucusunun (`src/Sketch.IO/Skp/SkpNativeReader.cs`, `SkpTlv.cs`) dayandığı yapıyı kaydeder. Yapı yalnızca kullanıcının kendi dosyalarının (ör. Kinsale Rd Block 4, SketchUp 2026) bayt içeriğine bakılarak çıkarılmıştır; Trimble'ın programı, kütüphaneleri veya şifreli dosyaları çözülmemiştir (karar: `context/kararlar.md`, 2026-10-21). Kayıt adları bizim verdiğimiz adlardır.

## 1. Dosya kabı

| Konum | İçerik |
|---|---|
| 0 | `FF FE FF` + uzunluk + UTF-16LE `"SketchUp Model"` |
| ~0x20 | `FF FE FF` + uzunluk + UTF-16LE `"{26.1.256}"` (yazan sürüm; 21 ve üstü bu yapıdadır) |
| ~0x38 | `VFF` + 8 bayt |
| ~0x45 | Standart **ZIP arşivi** (`PK 03 04`); ofsetler ZIP başlangıcına görelidir |

ZIP girdileri: `model.dat` (model), `meta/meta.dat`, `materials/<ad>/material.xml` (+ doku resmi, `thumbnail.jpg`), `styles/<ad>/style.xml`, `scene_thumbnails/*.png`, `thumbnails/*.png`. Katman renkleri `materials/Layer_<katman adı>/material.xml` içindedir.

`material.xml`: `<mat:material name colorRed colorGreen colorBlue trans useTrans hasTexture>`; dokulu malzemede `<mat:texture><mat:images><mat:image path="./dosya.png">`. Opaklık = `useTrans=1` ise `1 - trans`.

## 2. `model.dat`: etiket-uzunluk-değer ağacı

Her kayıt: 2 bayt etiket (LE) + 4 bayt uzunluk (LE) + içerik. Kapsayıcı kayıtlar ardışık alt kayıtlar tutar; yapraklar sayı, metin (UTF-8), kimlik veya çift dizisi tutar. **Kimlikler** en az baytla yazılmış işaretsiz LE tamsayılardır (1–8 bayt). Noktalar 3 çift (inç), düzlemler 4 çift (a b c d).

Kök `01f4` altındaki bölümler:

| Etiket | İçerik |
|---|---|
| `0200` | Seçenekler: `61a8 > 61a9 > 61aa` (`61ab` ad, `61ac` değerler: `61ad` ad + `38a4 > 38a7/38a9/38aa/38ad` int/double/bool/metin). `UnitsOptions/LengthUnit`: 0 inç, 1 ft, 2 mm, 3 cm, 4 m |
| `01f7` | Malzeme listesi: `30d4 > 30d5 > 32c8*` |
| `01f8` | Katman listesi: `3a98 > 3a99 > 3c8c*` |
| `01f9` | Tanımlar (grup/bileşen/resim): `1770 > 1771 > 157c*` |
| `01f6` | Kök modelin varlık kapsayıcısı: `1388` |
| `0207` | Sahneler: `6d60 > 6d61 > 7148*`: `7153` ad, `714a > 34bc` kamera (varsa kamera kaydedilir), `7150` gizli etiket kimlikleri (her giriş 1 bayt uzunluk + kimlik; varsa etiketler kaydedilir), `714c` stil kimliği (varsa stil kaydedilir), `7149` bayraklar |
| `01fa` | Kamera (`34bc`: göz `34bd`, hedef `34be`, yukarı `34bf`, `34c4` FOV, `34c6` perspektif) |

## 3. Varlıklar

Ortak başlık `07d0`: `05dc > 05de` kimlik; `07d1` malzeme kimliği (yüzlerde ön malzeme, metinlerde renk); `07d2` katman kimliği (yoksa Layer0); `07d3` bayraklar (bit 6 = gizli; bit 1–2 gölge seçenekleri; kenarlarda bit 0 = yumuşak).

Varlık kapsayıcısı `1388`:

| Etiket | Kayıt | Alanlar |
|---|---|---|
| `1389 > 09c4` | Köşe | `05dc > 05de` kimlik, `09c5` konum |
| `138a > 0bb8` | Kenar | `07d0`, `0bb9` başlangıç köşesi, `0bba` bitiş köşesi, `0bbb` eğri kimliği (varsa) |
| `138b > 0dac` | Yüz | `07d0`, `0dad` düzlem, `0daf` ön malzeme, `0dae > 1194*` döngüler (ilki dış, diğerleri delik); `1194 > 1195 > 0fa0*` kenar kullanımı: `0fa1` kenar kimliği, `0fa2` ters (1 ise bitişten başlangıca) |
| `138d > 1d4c > 1964` | Grup/bileşen örneği | `07d0`, `1965` ad, `1966` 13 çift: 3×3 matris **satır satır** (p'ᵢ = Σⱼ mᵢⱼ pⱼ + oᵢ), orijin, ölçek böleni; `1967` tanım kimliği, `1968` GUID. Satır sırası Block 4 ile doğrulandı: yalnız bu yorumda kat planı resimleri daireleri kapsıyor ve DEAP yönleri dosyadaki eklenti etiketleriyle eşleşiyor |
| `1390 > 1f40 > 1964` | Resim örneği | aynı; tanım türü 2 |
| `138c > 1964` | Bileşen örneği (tanım türü 0) | grup örneğiyle aynı alanlar, sarmalayıcı yok |
| `1392 > 426c` | Kılavuz | `4268 > 07d0` başlık, `426d` ilk nokta, `426e` ikinci nokta, `426f` 1 = çizgi |
| `1393 > 445c` | Kesit düzlemi | `07d0`, `445d` düzlem (a b c d; normal görünür kalan tarafı gösterir), `445e` ad, `445f` simge; `1394` = etkin kesitin kimliği |
| `1399 > 5bcc` | Ölçü | `59d8 > 07d0` başlık, `5bcd`/`5bce > 5208` uçlar (bağlantı), `5bcf` düzlem normali, `5bd2` ölçü çizgisinin işaretli uzaklığı |
| `5208` | Bağlantı (metin ve ölçü uçları) | `5209` tür: 1 = `520a` nokta, 2 = `520b > 53fc > 53fd` aynı kapsayıcıdaki köşe kimliği (+ `520a` kayma) |
| `1396 > 4a38` | Eğri (yay/çoklu çizgi) | `4a39` kenar sayısı, `4a3b` ilk, `4a3c` son kenar |
| `1398 > 55f0` | Metin | `07d0`, `55f1` metin, `55f4 > 5208 > 520a` bağlantı noktası, `55f7` normal |
| `13a0` | Sınır kutusu | min xyz, max xyz |

Tanım `157c`: `1388` varlıklar (kimliği `1388 > 07d0 > 05dc > 05de`), `157e` ad, `1583` tür (0 bileşen, 1 grup, 2 resim), `157d` GUID. Resim tanımının varlıkları piksel boyutunda bir dikdörtgen yüz içerir; yüzün malzemesi (`07d1`) resim dosyasını taşır; örnek dönüşümü dikdörtgeni modele yerleştirir.

Katman `3c8c`: `05dc > 05de` kimlik, `3c8d` ad, `3c8e` gizli (1), `3c8f > 32c8` katman malzemesi (`Layer_<ad>`). Malzeme `32c8`: `05dc > 05de` kimlik, `32cc` ad (klasör adıyla aynı), `32cd` opaklık yüzdesi (varsa).

## 4. Doğrulama

Kinsale Rd Block 4 (SketchUp 2026): 132 tanım, kökte 128 örnek, 109.663 köşe, 110.272 kenar, 4.792 yüz, 5.897 döngü (kenar kullanım yönü kuralı tamamında tutarlı), 199 Glass_Basic_01 yüzü = eklentinin 199 açıklık satırı, 3 kat planı resmi. Test: `tests/Sketch.Model.Tests/SkpNativeReaderTests.cs`.

Kılavuz, kesit düzlemi, ölçü, bileşen örnekleri ve sahneler `ornek.skp` (SketchUp 2026, kullanıcının deneme dosyası) ile çözüldü; test: `tests/Sketch.Model.Tests/SkpSampleFileTests.cs`. Bilinmeyenler: `138e` (çift dizisi), `139b/639f`, `139f`, yüz arka malzemesi, `7159` (sahnede ikinci kimlik listesi), ölçü/metin yazı stili (`59da`).
