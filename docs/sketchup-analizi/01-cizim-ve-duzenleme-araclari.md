# SketchUp 2026 – Çizim, Düzenleme ve Yapı Araçları Analizi

> **Amaç:** Kendi özgün Windows 3B modelleme uygulamamızın (farklı ad, kendi kod ve ikonlar) araç davranışlarını tasarlarken referans almak için SketchUp 2026'nın çizim, düzenleme ve yapı (construction) araçlarının **işlevsel** davranışını özetlemek.
>
> **Yöntem:** Yalnızca herkese açık resmi kaynaklar kullanıldı (help.sketchup.com, sketchup.com / download.sketchup.com resmi Hızlı Başvuru Kartları ve sürüm notları, ruby.sketchup.com, forums.sketchup.com). Yazılım indirilmedi, tersine mühendislik yapılmadı, ikon kopyalanmadı. Metinler özetlenerek/yeniden ifade edilerek yazıldı.
>
> **Araştırma tarihi:** 20 Ekim 2026. İncelenen yardım sayfalarının çoğu "Son güncelleme: 29 Eyl 2026 / 13 Eki 2026" tarihlidir. En güncel masaüstü sürüm notu: **SketchUp Desktop 2026.2 (19 Mayıs 2026)**.
>
> **İşaretler:**
> - **doğrulanamadı** → resmi kaynakta bulunamadı ya da yalnızca topluluk (forum) kaynağından geliyor.
> - **ÇELİŞKİ** → iki resmi kaynak farklı şey söylüyor; uygulamada karar verilmesi gerekiyor.
> - Kısayollar **Windows** içindir. Mac karşılıkları: Ctrl→Option, Alt→Command (yardım sayfalarına göre; araçtan araca değişebilir).

---

## 0. Özet kısayol ve değiştirici tuş tablosu

Kaynak: "Default Keyboard Shortcuts" yardım sayfası + resmi **Quick Reference Card 2026 | Windows** (QRC 2026). "—" = varsayılan kısayol yok (kullanıcı atayabilir).

| Araç (EN) | Varsayılan kısayol | Ctrl | Shift | Alt | Ok tuşları | Ölçü kutusuna (VCB) yazılabilenler |
|---|---|---|---|---|---|---|
| Select | Space | Seçime ekle | Ekle/çıkar (toggle) | — | — | — |
| Lasso Select | Shift+Space | — | Ekle/çıkar | Ekle (QRC) | — | — |
| Line | L | — | Mevcut çıkarım yönünü kilitle (yardım) | Çıkarım modlarını döngüle (yardım) / yön kilidi (QRC) — **ÇELİŞKİ** | →kırmızı, ←yeşil, ↑mavi, ↓paralel/dik | uzunluk, `[x,y,z]`, `<x,y,z>` |
| Freehand | — | Ctrl +/− segment azalt (çizimden hemen sonra) | doğrulanamadı | — | düzlem kilidi | — |
| Rectangle | R | Merkezden çiz (forum) — **ÇELİŞKİ** | Altın oran çıkarımını kilitle | Merkezden çiz (QRC) — **ÇELİŞKİ** | — | `uzunluk,genişlik` |
| Rotated Rectangle | — | — | Yön/düzlemi kilitle | Protractor düzlemini kilitle | çizim düzlemi | `genişlik,açı` |
| Circle | C | — | Mevcut çıkarımı kilitle | — | düzlem | kenar sayısı (çizimden önce), yarıçap; `24s` |
| Polygon | — | İç/dış teğet yarıçap modu (forum) | doğrulanamadı | — | düzlem | kenar sayısı, yarıçap |
| Arc (merkez) | — | Ctrl +/− segment | — | Teğet kilidi (2022+) | düzlem | yarıçap, açı, `Ns` |
| 2 Point Arc | A | Ctrl +/− segment | — | Teğet kilidi | ↑↓←→ eksen kilidi | kiriş uzunluğu, bulge, `Nr`, `Ns`, `Nc` |
| 3 Point Arc | — | Ctrl +/− segment | — | Teğet kilidi | eksen kilidi | açı/değer |
| Pie | — | Ctrl +/− segment | — | — | düzlem | yarıçap, açı |
| Eraser | E | Yumuşat/pürüzsüzleştir (soften/smooth) | Gizle (hide) | Yumuşatmayı geri al (unsoften) | — | — |
| Move | M | Kopya modu (tekrar → Stamp) | Çıkarım yönünü kilitle | Autofold (zorla katla) | eksen/paralel kilidi | mesafe, `[x,y,z]`, `<x,y,z>`, `Nx`/`*N`, `N/` |
| Push/Pull | P | Orijinal yüzü bırakarak kopya yüzle it/çek | — | — | — | mesafe; çift tık = son mesafe |
| Rotate | Q | Kopya döndür (dairesel dizi) | Düzlemi kilitle | Kilitli düzlemde merkezi serbest bırak | düzlem seçimi | açı, eğim `3:12`, `Nx`, `N/` |
| Scale | S | Merkezden ölçekle | Orantılı ölçek (toggle) | — | — | çarpan `1.5`, eksen başına `2,3,4`, uzunluk `10m` |
| Follow Me | — | — | — | Yüz çevresini yol olarak kullan | — | — |
| Offset | F | — | — | Çakışmalara izin ver | — | mesafe; çift tık = son mesafe |
| Flip | — | Kopya (flip + copy) | — | Nesne ↔ üst bağlam eksenleri | ←yeşil, →kırmızı, ↑mavi düzlem | `Nx`, `N/` (dizi) |
| Solid Tools (6 araç) | — | — | — | — | — | — |
| Intersect Faces | (bağlam menüsü) | — | — | — | — | — |
| Tape Measure | T | Mod döngüsü: kılavuz çizgi → kılavuz nokta → ölçüm (2026.1) | — | — | eksen kilidi | ofset; ölçümden sonra yeni değer → modeli yeniden ölçekle |
| Protractor | — | Kılavuz oluşturmayı aç/kapa | Düzlemi kilitle | Kilitli düzlemde protractor'ı serbest taşı | düzlem kilidi | açı, eğim |
| Dimensions | — | — | — | — | — | (metin elle değiştirilebilir) |
| Text | — | — | — | — | — | — |
| 3D Text | — | — | — | — | — | (iletişim kutusu) |
| Axes | — | — | — | Sıradaki ekseni (yeşil/mavi) seç | — | — |
| Section Plane | — | — | Yönü kilitle | — | ↑mavi, →kırmızı, ←yeşil, ↓yüze paralel | — |
| Paint Bucket | B | Bitişik aynı malzemeli yüzleri doldur | Modeldeki aynı malzemeli tüm yüzleri değiştir | Malzeme örnekle (damlalık) | — | — |
| Tag (tool) | — | Bileşenin tüm örneklerini etiketle | Aynı etiketli her şeyin etiketini değiştir | Etiket örnekle | — | — |

Diğer genel kısayollar: Orbit **O**, Pan **H**, Zoom **Z**, Zoom Extents **Shift+Z**, Make Component **G**, Search **Shift+S**, Back Edges **K**, Image Igloo **I**; Geri al **Ctrl+Z** veya **Alt+Backspace**; Tümünü seç **Ctrl+A**; Seçimi kaldır **Ctrl+T**; Esc = işlemi iptal / baştan başla. Fare: tekerlek kaydır = zoom, tekerlek basılı sürükle = orbit, Shift+tekerlek sürükle = pan, tekerlek çift tık = görünümü ortala.

---

## 1. Ortak kavramlar (tüm araçların temeli)

### 1.1 Geometri modeli
- **Edge (kenar):** Modelin temel yapı taşı; iki köşe (vertex) arasında düz çizgi.
- **Face (yüz):** Aynı düzlemde kapalı bir kenar döngüsü oluştuğunda **otomatik** oluşur. Rectangle, Circle, Polygon, Pie araçları da doğrudan yüz üretir.
- **Kenar bölme (edge splitting):** Bir çizgi başka bir çizgiye değdiğinde/kesiştiğinde mevcut kenar o noktada ikiye bölünür; parçalar ayrı ayrı seçilebilir.
- **Yüz bölme:** Bir yüzün kenarındaki bir noktadan başka bir kenar noktasına çizilen çizgi (veya yay) yüzü iki yüze ayırır. Ayrılan parçalar Push/Pull ile bağımsız itilip çekilebilir.
- **Yüz iyileşme (face healing):** Yüzü bölen çizgi silinirse iki parça tek yüz olarak birleşir. Yanlışlıkla silinen bir yüz, sınır çizgilerinden biri üzerine yeniden çizgi çizilerek geri oluşturulabilir.
- **Kenar silme:** Eraser ile kenar silinince ona bağlı yüz(ler) de silinir. Bağlam menüsünden yalnızca yüz silinirse kenarlar kalır.
- **Eğriler (Curve) / yaylar / daireler / çokgenler:** Tek bir varlık gibi seçilen ama içte **birden çok düz segmentten** oluşan kenar zincirleridir. Bir segmente tıklamak tüm eğriyi seçer; çıkarım motoru yine her segmentin uç/orta noktasını tanır. Bağlam menüsünde **Explode Curve** zinciri ayrı kenarlara böler; **Weld Edges** bağlı kenarları tek eğride birleştirir.
- **Yüzey (surface):** Yumuşatılmış (soft) kenarlarla birleşmiş çok sayıda küçük yüz; görünüşte pürüzsüz. Yay/daire/çokgen içeren yüz Push/Pull edilince oluşur.
- **Varsayılan segment sayıları:** Daire **24** (Ruby API `add_circle` varsayılanı 24; forum ve yardım "Ölçü kutusunda varsayılan görünür" der). Yay **12** (yardım sayfası). Çokgen **6** kenar (yalnızca forum – kısmen doğrulandı). Oturum içinde yazılan yeni değer sonraki çizimlerde varsayılan olur, program yeniden açılınca sıfırlanır (forum – doğrulanamadı resmi).
- **Divide:** Bir kenar/yayın bağlam menüsünden; imleç merkeze yaklaştıkça daha az, uca yaklaştıkça daha çok parça; istenirse parça uzunluğu yazılır. Sonuç: eşit uzunlukta birleşik segmentler.
- **"Yapışkan geometri" (stickiness) ve grup/bileşen izolasyonu:** Bu konuda resmi tool sayfalarında ayrıntı bulunamadı – **doğrulanamadı** (ayrı bir analiz dosyasında ele alınmalı).

### 1.2 Çıkarım motoru (Inference Engine)
- **Nokta çıkarımları:** orijin, bileşen orijini, uç nokta (endpoint), orta nokta (midpoint), yay orta noktası, kesişim, yüz üzerinde, kenar üzerinde, merkez, kılavuz noktası, kılavuz çizgisi üzerinde, kesit ile ilgili noktalar. Grup/bileşen içindeki çıkarımlar **macenta** renkte ve "in group / in component" ipucu ile gösterilir.
- **Doğrusal çıkarımlar:** kırmızı/yeşil/mavi eksen boyunca; bir noktadan; bir noktadan geçen; bir kenara paralel veya kenarın uzantısı; kenara/yüze dik; bir köşede teğet. Genelde geçici noktalı çizgi ile gösterilir.
- **Şekil çıkarımları:** kare, altın oran dikdörtgeni, yarım/çeyrek/üç çeyrek daire; yay ve dairelerde merkez ve kenar çıkarımları.
- İmleci bir noktanın üzerinde **bekletmek** o noktayı öncelikli hizalama referansı yapar.
- Görsel destek: Preferences > Drawing'de "Display crosshairs" imleci eksen renkleriyle gösterir; Styles > Edge Settings'te kenar rengi "By axis" yapılabilir.

### 1.3 Çıkarım kilitleme (genel)
| Tuş | Etki |
|---|---|
| **→ (Sağ ok)** | Kırmızı eksene kilitle |
| **← (Sol ok)** | Yeşil eksene kilitle |
| **↑ (Yukarı ok)** | Mavi eksene kilitle |
| **↓ (Aşağı ok)** | Çıkarılan kenar/düzleme paralel veya dik kilit (macenta). 2025.0'dan itibaren Snaps ile de paralel kilidi. |
| **Shift (basılı)** | O an gösterilen yön/düzlemi kilitle |
| **Shift+Alt** | Çizim düzlemini ve aracı çıkarılan yüzün düzlemine kilitle. Rotate/Protractor'da Alt, düzlem kilitliyken merkezi serbest taşır. |
| **Alt (Line çiziminde, ilk tıktan sonra)** | Üç durum arasında döngü: 1) tüm çıkarımlar açık (varsayılan) 2) tüm çıkarımlar kapalı (nokta çıkarımları yine çalışır) 3) yalnızca paralel/dik |

- **2021.1:** İlk tıktan **önce** ok tuşlarıyla yön "ön-kilitleme" (Line, Freehand, 2/3 Point Arc, Move, Tape Measure). **2022.0:** bu ön-kilit için ipucu (tooltip) eklendi.

### 1.4 Ölçü kutusu (Measurements box / VCB)
- Sağ alt köşede; anlamı aktif araca göre değişir. Değer **yazıp Enter** ile girilir; çoğu araçta işlem bittikten sonra, başka araca geçilene/başka işlem yapılana kadar değer **tekrar yazılarak düzeltilebilir**.
- Birim son ekleri: `10"` inç, `10'` fit, `10mm`, `10cm`, `10m`. Birimsiz sayı belge birimini kullanır; yazılan birim şablon birimini geçersiz kılar (ör. metrik modelde `3'6"`).
- Koordinat: `[x,y,z]` mutlak (aktif eksenlere göre), `<x,y,z>` göreli (başlangıç noktasına göre).
- Negatif değer ters yöne uygular (Rectangle, Move, Rotate'de saat yönü tersine vb.).
- Son ekler: `r` yarıçap, `s` segment/kenar sayısı, `c` daire tabanlı segment, `x`/`*` dış dizi, `/` iç dizi.

---

## 2. Çizim araçları (Drawing)

### 2.1 Line — Kısayol: **L**
**Amaç:** Kenar (edge) çizmek; kapalı düzlemsel döngüde yüz oluşturmak; yüzleri bölmek.

**Etkileşim adımları**
1. L'ye bas (imleç kalem olur). İsteğe bağlı: ilk tıktan önce ok tuşuyla eksen ön-kilidi.
2. Başlangıç noktasına tıkla. (Esc → iptal.)
3. İmleci hareket ettir: önizleme çizgisi takip eder, Ölçü kutusunda uzunluk görünür; çizgi rengi hizalandığı ekseni gösterir (kırmızı/yeşil/mavi; paralel/dik için macenta).
4. Bitiş noktasına tıkla **veya** uzunluk yazıp Enter. Bitiş noktası yeni segmentin başlangıcı olur; araç başka araca geçilene ya da Esc'ye kadar zincir çizmeye devam eder.
5. Koordinat girişi: `[3m,5m,0]` (mutlak) veya `<1.5m,4m,2.75m>` (göreli).

**Değiştiriciler:** Ok tuşları eksen kilidi; Shift basılı = mevcut yönü kilitle; Alt = çıkarım modu döngüsü (yardım sayfası). **ÇELİŞKİ:** QRC 2026, Line için Alt'ı "mevcut çıkarım yönünü kilitle" olarak tanımlar. Uygulamamız için öneri: Shift=kilit, Alt=çıkarım modu döngüsü (yardımdaki ayrıntılı tanım).

**Oluşan geometri ve kenar durumları**
- Her segment bir edge; kesiştiği edge'leri böler.
- Kapalı ve düzlemsel döngü tamamlanınca yüz otomatik oluşur; mevcut yüz üzerinde kenardan kenara çizgi yüzü böler.
- Yüze sınır olmayan çizgiler Move ile uç noktası sürüklenerek ya da Entity Info'daki Length ile uzatılıp kısaltılabilir.
- Orbit ile kontrol önerilir: bir açıdan eksende görünen çizgi aslında olmayabilir.

### 2.2 Freehand — Kısayol: **yok**
**Amaç:** Serbest el eğrisi (Curve varlığı) çizmek.

**Adımlar**
1. Aracı seç (imleç eğrili kalem). İsteğe bağlı: ok tuşlarıyla çizim düzlemi kilidi (← yeşil, ↑ mavi, → kırmızı) – 2022.0'dan beri.
2. Basılı tutarak başla, sürükle, bırak.
3. Çizimden hemen sonra **Ctrl + / Ctrl −** ile segment sayısını kademeli azalt (ilk vuruşta oluşandan fazla segment eklenemez).
4. Başlangıç noktasında bitirilirse kapalı şekil olur.

**Geometri:** Çok segmentli tek Curve varlığı; seçim bütün olarak, çıkarım segment bazında. Kapalı eğri yüzü tanımlar/böler. Push/Pull edilince çokyüzlü yüzey (polyface) oluşur. 2022.0'dan beri bitişik farklı düzlemli yüzler üzerinden çizim yapılabilir; kenarları keserken yalnızca gerektiği kadar eğri oluşur.

**doğrulanamadı:** Shift ile "3B polyline / çıkarımsız çizim" davranışı resmi kaynakta bulunamadı.

### 2.3 Rectangle — Kısayol: **R**
**Amaç:** Eksenlere hizalı dikdörtgen yüz (4 kenar + 1 yüz).

**Adımlar**
1. R'ye bas; ilk köşeye tıkla.
2. Çapraz yönde hareket et, karşı köşeye tıkla.
3. Hassas ölçü: `uzunluk,genişlik` + Enter (ör. `8m,20m`). Yalnızca birini değiştirmek: `5m,` veya `,5m`. Negatif değer ters yöne çizer.

**Çıkarımlar:** **Square** (kare) ve **Golden Section** (altın oran) — mavi noktalar ve ipucu; altın oran çıkarımı Shift ile kilitlenir. 2024.0: yüz üzerinde çizerken o yüzün düzlemindeki kenarlara çıkarım yapılabilir. 2026.0: "kare" ipucu gösterildiğinde sonucun gerçekten kare olması düzeltildi.

**Merkezden çizim — ÇELİŞKİ:** QRC 2026 "Alt = merkezden başla" der; topluluk forumu (2018'den beri) "Ctrl ile merkez/köşe arasında geçiş" der. Hangisinin 2026'da geçerli olduğu **doğrulanamadı**.

**Sınır:** Başka işleme geçildikten sonra genişlik/uzunluk parametrik düzenlenemez (yeniden çiz veya Scale kullan).

### 2.4 Rotated Rectangle — Kısayol: **yok** (Draw > Shapes > Rotated Rectangle)
**Amaç:** Herhangi bir düzlemde/açıda dikdörtgen.

**Adımlar**
1. Aracı seç; isteğe bağlı ok tuşuyla çizim düzlemi (ör. ← yeşil düzlem).
2. İlk köşeye tıkla.
3. İlk kenarı tanımla: ikinci köşeye tıkla ya da uzunluk yaz + Enter.
4. Açı ve genişlik: protractor ile açı, merkezden uzaklaşarak genişlik belirlenir; ya da `genişlik,açı` yaz (ör. `90,20`) + Enter.
5. Tıklayarak bitir.

**Değiştiriciler:** Shift = ilk kenarın yönünü/düzlemi kilitle (açı aşamasında açıyı kısıtlar). Alt = protractor düzlemini kilitle ve imlecin konumunda yeni taban çizgisi ata (kesikli çizgi). Ok tuşları ilk kenarı eksene hizalar.

### 2.5 Circle — Kısayol: **C**
**Adımlar**
1. C'ye bas; Ölçü kutusu varsayılan kenar sayısını gösterir (24). İsteğe bağlı: sayı yazıp Enter → kenar sayısı.
2. Merkeze tıkla (yüz üzerindeyse yüz düzlemini alır; ok tuşları ile normal ekseni seçilebilir).
3. Dışarı hareket et, yarıçap canlı görünür; tıkla.
4. Başka araca geçmeden yarıçap yazılabilir (ör. `34cm`) veya `48s` ile segment sayısı.

**Değiştirici:** Shift = mevcut çıkarımı (düzlemi) kilitle.

**Geometri:** Birbirine bağlı segmentlerden oluşan, **yarıçap bilgisi taşıyan** tek daire varlığı + dairesel yüz. Entity Info'dan Radius ve Segments sonradan değiştirilebilir (kenara sağ tık – yüze değil). Push/Pull edildiğinde yan yüzler yumuşatılmış görünür (silindir yüzeyi). Elips: daire çiz → Scale → orta (kenar) tutamağı sürükle.

### 2.6 Polygon — Kısayol: **yok**
**Adımlar:** Aracı seç → kenar sayısını yaz (en az 3) → merkeze tıkla → yarıçap için hareket et veya yarıçap yaz → tıkla. Araç değiştirilmeden yarıçap/kenar sayısı yeniden yazılabilir.

**İç/dış teğet (inscribed/circumscribed):** Ctrl'e bas-bırak ile mod değişir; Ölçü kutusu etiketi "Inscribed Radius / Circumscribed Radius" olur (yalnızca **forum** kaynaklı – resmi yardımda yok → kısmen doğrulandı). Entity Info her iki modda köşe yarıçapını saklar.

**Geometri:** Tek çokgen varlığı + yüz. Push/Pull sonrası daireden farkı: yan kenarlar yumuşatılmaz, kenarlar belirgin görünür.

### 2.7 Arc (merkez + açı) ve Pie — Kısayol: **yok**
**Adımlar**
1. Aracı seç; yarım daire protractor çizim düzlemini gösterir.
2. Merkeze tıkla (tam protractor düzlemi kilitler).
3. Başlangıç noktasına tıkla veya yarıçap yaz + Enter.
4. Bitiş noktasına tıkla veya açı yaz + Enter.

**Sonuç:** Arc → açık yay varlığı (segmentli). Pie → kapalı dilim şeklinde **yüz**.

### 2.8 2 Point Arc — Kısayol: **A**
**Adımlar**
1. Başlangıç noktasına tıkla (Ölçü kutusu: kiriş Length).
2. Bitiş noktasına tıkla veya kiriş uzunluğu yaz → düz bir çizgi oluşur, kutu **Bulge** (şişkinlik) bekler.
3. Çizgiye dik hareket et, tıkla veya bulge yaz (ör. `5mm`). Yarım dairede "Half Circle" çıkarımı.
4. Yazılabilir: `24r` (yarıçap), `20s` (segment), `20c` (20 kenarlı daireye göre segment).

**Teğet / köşe yuvarlatma (fillet):**
- **Alt** (Windows) teğet çıkarım kilidi (2022.0'dan beri): mevcut kenardan teğet kilitlenir, sonraki tık teğet yay üretir. Birden çok kenar kesişiyorsa, ilk tıktan önce bir kenarın üzerine gelip teğet referansı seçilebilir.
- **Çift tık tekrar:** Bir köşede yay oluşturduktan sonra diğer köşelere çift tıklamak aynı parametreli yayı uygular (dikdörtgen köşelerini yuvarlatmak için).
- **2025.0:** Teğet yay oluşturulunca aynı yüzdeki diğer kenar kesişimlerinde önizleme gösterilir, çift tıkla tekrarlanır. Yayın dış bükey tarafına tıklayarak köşe "yuvarlatma geri alma" (unround) yapılabilir. Arc aracı aktifken çıkarım noktaları görünür.

**Segment değiştirme:** Ctrl + / Ctrl − (yardım). **ÇELİŞKİ:** QRC 2026 bu işlemi "Alt +" başlığı altında, Mac için Option +/− olarak listeler; Windows için kesin tuş yardım sayfasına göre Ctrl'dir.

**Düzenleme:** Move ile yay orta noktası sürüklenerek şişkinlik; uç noktası ile uzunluk/yarıçap değişir. Entity Info'dan yarıçap/segment. Düzgün olmayan ölçek yayın yarıçap tanımını bozar (serbest eğriye dönüşür). Yay içeren yüz Push/Pull edilince yarıçapı hâlâ düzenlenebilen yüzey oluşur.

### 2.9 3 Point Arc — Kısayol: **yok**
**Adımlar:** Başlangıç noktasına tıkla → ikinci noktaya tıkla (küçük pivot noktası oluşur) → hareket et, yay önizlenir → tıkla. 2022.0'dan beri ikinci nokta değeri yazılabilir. Ok tuşları eksen kilidi (↓ paralel/dik), Alt teğet kilidi, Esc baştan başlatır.

---

## 3. Düzenleme araçları (Modification)

### 3.1 Select — Kısayol: **Space** (Lasso: **Shift+Space**)
**Tıklama davranışı**
| Eylem | Sonuç |
|---|---|
| Tek tık | Tek varlık (kenar, yüz, grup/bileşen) |
| Yüze çift tık | Yüz + sınır kenarları |
| Kenara çift tık | Kenar + bağlı yüzler |
| Üçlü tık | Bağlı tüm geometri (ör. küpün tamamı) |
| Boş alana tık | Seçimi kaldır |
| Grup/bileşene çift tık | Düzenleme bağlamına gir (Esc ile çık – 2026.0'da güvenilir hâle getirildi) |

**Pencere vs. kesişen seçim (sürükleme)**
- **Soldan sağa** sürükleme = **Window**: yalnızca tamamen kutu içindekiler.
- **Sağdan sola** sürükleme = **Crossing**: tamamen veya kısmen içindekiler.

**Değiştiriciler (Windows)**
| Tuş | Etki | İmleç |
|---|---|---|
| Ctrl | Seçime ekle | + |
| Shift | Ekle/çıkar (toggle) | +/− |
| Ctrl+Shift | Seçimden çıkar | − |

**Lasso (2022.0'dan beri):** Serbest el modu ve "segments" modu (tıklayarak köşe koy, çift tıkla bitir). **ÇELİŞKİ (yön):** 2022.0 sürüm notu "saat yönü = window (yalnız tam içindekiler), saat yönü tersi = crossing" der ve yönün Preferences > Drawing'den tersine çevrilebildiğini belirtir; güncel yardım sayfası ise saat yönünü "dokunan her şey", ters yönü "yalnız tam içindekiler" olarak anlatır. **ÇELİŞKİ (değiştirici):** 2022.0 notu Lasso'nun Select ile aynı değiştiricileri kullandığını söyler; QRC 2026 ise Lasso için Shift=ekle/çıkar, Alt=ekle, Shift+Alt=çıkar listeler.

**Bağlam menüsü > Select:** Connected Faces, All Connected, All with Same Tag, All with Same Material, Bounding Edges, All Instances (2022.0), Connected by Snaps (2025.0), Invert Selection, Deselect Edges/Faces. 2026.1: boş alana sağ tıkta "Select All".
**Menü:** Ctrl+A tümünü seç, Ctrl+T seçimi kaldır.

### 3.2 Eraser — Kısayol: **E**
**Adımlar:** Kenara tıkla → kenar ve ona bağlı yüzler silinir. Basılı tutup birden çok kenar üzerinden sürükle → kenarlar mavi işaretlenir, fare bırakılınca silinir. Eraser yüzü doğrudan silemez; yüz, sınır kenarı silinince kaybolur. Alternatif: Select ile seç + sağ tık > Erase (veya Delete tuşu – yardımda açıkça yok, standart davranış) → yüzler de silinir.

**Değiştiriciler (QRC 2026 + yardım)**
| Tuş | Etki |
|---|---|
| Ctrl | Silmek yerine **yumuşat + pürüzsüzleştir** (soft + smooth): kenar gizlenir, komşu yüzler tek yüzey olur ve gölgelendirme yumuşar |
| Shift | Silmek yerine **gizle** (hide); yüzey oluşturmaz |
| Alt | Yumuşatmayı/pürüzsüzleştirmeyi geri al (unsoften/unsmooth) |

Kılavuzlar da Eraser ile tıklanarak silinir.

### 3.3 Move — Kısayol: **M**
**Temel adımlar**
1. Nesneyi Select/Lasso ile seç (önseçim) → M.
2. Referans (tutma) noktasına tıkla (uç nokta, köşe vb.).
3. Taşı; çıkarım ve Ölçü kutusu mesafeyi gösterir.
4. Hedefe tıkla **veya** mesafe yaz + Enter (işaretli değer; birim yazılabilir). `[x,y,z]` mutlak, `<x,y,z>` göreli koordinat.

**Önseçimsiz (hover) taşıma:** Seçim yokken imlecin altındaki varlığın tıklanıp taşınması — resmi yardımda bu iş akışı tarif edilmemiş → **doğrulanamadı** (QRC/yardım yalnızca önseçimli akışı anlatır).

**Değiştiriciler**
| Tuş | Etki |
|---|---|
| Ctrl | Kopya modu (imleçte +). Ardışık birden çok kopya. Tekrar Ctrl → **Stamp** modu: önceden seçili geometri her tıkta yeni yere damgalanır (Stamp yalnızca Move işlemi başlamadan etkinleşir). 2022.0: Stamp modunda da dizi ve mesafe yazılabilir. |
| Shift (basılı) | Mevcut çıkarım yönünü kilitle |
| Alt | **Autofold**: işlem bir yüzü düzlemsellikten çıkaracaksa SketchUp normalde işlemi sınırlar; Alt ile serbest hareket edip ek katlama kenarları/yüzler oluşturulur |
| Ok tuşları | → kırmızı, ← yeşil, ↑ mavi, ↓ paralel/dik |

**Dizi (array)**
- **Dış dizi:** Ctrl ile ilk kopyayı yerleştir, sonra `5x` (veya `x5`, `*5`) + Enter → aynı aralıkla toplam 5 kopya bir sıra hâlinde.
- **İç dizi:** İlk kopyayı yerleştir, sonra `5/` + Enter → orijinal ile ilk kopya arasındaki mesafe 5 eşit parçaya bölünür (arada eşit aralıklı kopyalar).
- Başka işlem yapılana kadar sayı/mesafe tekrar yazılarak dizi güncellenebilir.

**Bağlı geometriyi germe (stretching)**
| Tutulan | Sonuç |
|---|---|
| Yüz | Yüz korunur, komşu yüzler gerilir |
| Kenar | Kenara bitişik tüm yüzler gerilir |
| Uç nokta | Noktaya bağlı tüm kenar ve yüzler gerilir |

- Autofold: gerdirme yüzü bükecekse otomatik kat çizgisi eklenir (tüm yüzler düzlemsel kalır).
- Ekstrüde edilmiş yay/daire/çokgen yüzeylerinde "kontrol kenarı" sürüklenerek yüzey bozulmadan yeniden boyutlandırılır (serbest el eğrilerinden çıkan yüzeylerde yoktur).

**Grup/bileşen tutamakları**
- Seçili veya üzerine gelinen grup/bileşenin sınır kutusu köşelerinde tutamaklar görünür. Alt ile köşe → orta nokta → yan merkez → merkez tutamakları arasında döngü.
- **Döndürme tutamakları** (kırmızı artılar) — 2024.0'da Drawing tercihlerinden açılıp kapatılabilir hâle geldi ve kısayol atanabilir.
- 2026.0: grup/bileşen **profil kenarından** konumlandırılabilir (nesne yarı saydam olur).
- **Snaps:** Bileşenlere eklenen bağlantı noktaları; Move ile bir Snap'i başka bir Snap/yüz/kenara takınca nesne konum ve yönelimi tek adımda alır. Alt = "tractor beam" ile en yakın uygun Snap'i bul ve önizle. 2024.0 iyileştirmeleri; 2025.0'da ↓ ile Snaps sırasında paralel kilidi. Snaps'in ilk çıktığı sürüm **doğrulanamadı**.

### 3.4 Push/Pull — Kısayol: **P**
**Amaç:** Düzlemsel bir yüzü 3B'ye ekstrüde etmek veya modelden hacim oymak.

**Adımlar**
1. P → yüze tıkla (zor seçiliyorsa önce Select ile seç). Esc → baştan.
2. Hareket et; Ölçü kutusu derinliği gösterir.
3. Tıkla veya mesafe yaz + Enter.

**Çıkarım:** Çekmeden önce imleç başka bir yüzün üzerine getirilirse o yüzle aynı seviyeye hizalanır (paralel yüz çıkarımı).

**Değiştiriciler ve tekrar**
- **Çift tık:** Başka bir yüze çift tık → son Push/Pull mesafesini o yüze uygular.
- **Ctrl:** Orijinal yüzü yerinde bırakarak yeni bir yüz kopyasıyla it/çek (yeni başlangıç yüzü; aradaki kenar halkası korunur). Ctrl + çift tık → mevcut ekstrüzyonun üzerine aynı yükseklikte yeni bir kat "istifle".

**Oyma / delik açma**
- Yüzü içe it; kısmen it = malzeme kaldır.
- Karşı yüze kadar itilirse "Offset limited to…" mesajı çıkar; tam kaldırma için orada bırak. Delik açabilmek için itilen yüz karşı yüze **paralel** olmalı ve karşı yüzü bölen çizgiler önce silinmeli.

**Kenar durumları**
- **Eğri/yumuşatılmış yüzeyler:** Yerleşik Push/Pull yalnızca tek **düzlemsel** yüzde çalışır; eğri veya yumuşatılmış yüzeylerde çalışmaz (topluluk forumu; SketchUp'ın "Cannot push curved or smooth surface" uyarısı). Kalınlık vermek için uzantılar veya Solid/Intersect iş akışları kullanılır.
- Çok ince (~2,5 cm altı) ekstrüzyonlarda kenarlar yüzün içinden görünebilir (görüntüleme sınırı).
- Yay/daire içeren yüzlerden pürüzsüz yüzey (yumuşatılmış kenarlı küçük yüzler) oluşur.

**Oluşan geometri:** Yan yüzler + kenarlar oluşturulur; yeni kapak yüzü orijinal yüzün yerine geçer (Ctrl'siz) ya da kopya olarak eklenir (Ctrl'li).

### 3.5 Rotate — Kısayol: **Q**
**Adımlar**
1. Geometriyi seç → Q (protractor imleci).
2. İmleci döndürme düzlemine getir; Shift ile düzlemi kilitle (imleç eksene dikse o eksenin renginde). Ok tuşlarıyla düzlem seçilebilir.
3. Shift basılıyken Alt → protractor'ı çıkarılan düzlemden ayırıp başka noktaya taşı (düzlem korunur).
4. Tık 1: dönme merkezi (açının tepe noktası). Tık 2: başlangıç kolu. Hareket + tık 3: bitiş.
5. İsteğe bağlı: derece (`34.1`) veya eğim (`8:12`) yaz + Enter. Negatif değer saat yönü tersine.
6. Tekerleği basılı tutarak geçici Orbit.

**Ctrl – kopyalı döndürme / dairesel dizi:** Ctrl ile kopya modu; ilk kopya döndürülüp yerleştirildikten sonra `Nx`, `*N` veya `N/` yazılarak merkez etrafında dairesel dizi oluşturulur (yardımın "Copying" makalesi – arama özeti üzerinden; sayfa tam açılamadı, kısmen doğrulandı).

**Katlama:** Seçimi döndürürken katlama ekseninin iki ucuna tıklanır, sonra açı verilir (bağlı geometri bükülür).

**Grup/bileşen döndürme tutamakları:** Sınır kutusu köşelerinde gri noktalar; sürükleyerek döndür. Gizli tutamak mavi olur ve nesne saydamlaşır. Alt → Object Center / Context Edge Center / Context Edge Corner döngüsü.

**Sürüm notları:** 2024.0 "Leaning Ladder" çıkarımı (döndürülen geometri yaslandığı yüzle kesiştiğinde "X" gösterir). 2025.0 döndürme tutamakları sınır kutularında, protractor bileşen kutularına ve orta nokta tutamaklarına hizalanır. 2026.0 düzlem kilitliyken döndürme tutamakları gizlenir.

### 3.6 Scale — Kısayol: **S**
**Adımlar**
1. Seç → S. Sarı sınır kutusu ve yeşil tutamaklar görünür.
2. Bir tutamağa tıkla (seçilen ve karşıtı kırmızı olur; Ölçü kutusu 100% / 1.00).
3. Hareket et; tıkla veya değer yaz + Enter.

**Tutamak türleri**
| Tutamak | Davranış |
|---|---|
| Köşe | Orantılı (uniform) ölçek |
| Kenar | İki eksende germe/sıkıştırma (ör. "Red, Green Scale") |
| Yüz | Tek eksende (ör. "Blue Scale") |

**Değiştiriciler:** Ctrl = merkezden ölçekle (karşı tutamak yerine). Shift = orantılı ölçek (toggle; köşe dışı tutamaklarda deformasyonu önler).

**Yazılabilir değerler:** Çarpan `1.5` (=150%), eksen başına `2,3,4`, uzunluk + birim `10m`. Negatif çarpan aynalama yapar (QRC/yardımda örnek yok → **doğrulanamadı**, ama Flip öncesi yaygın yöntem).

**Bileşenler:** Tek örnek Scale ile ölçeklenirse yalnızca o örnek değişir; bileşenin içine girip ölçeklemek tüm örnekleri değiştirir. Live Component'lerde de çalışır.

**2D yüz ölçeği:** Düz bir yüzde tutamaklar o düzlemde görünür (resmi sayfada ayrı anlatılmıyor – **doğrulanamadı**).

**Sürüm notları:** 2026.0 Live Component'leri çıkarımla yerleştir/boyutlandır; tutamaklar geometri arkasından görünür. 2026.1.2: orantılı ölçekte yazılan uzunluk seçilen eksen tutamağı boyunca uygulanır (arama özeti). 2026.2: tutamakların arka kenarlardan görünme hatası düzeltildi.

**Tüm modeli ölçekleme:** Tape Measure ile bilinen bir mesafeyi ölç → yeni değeri yaz + Enter → "modeli yeniden boyutlandır?" onayı. Yalnız bir parçayı bu şekilde ölçeklemek için önce grup yap, gruba gir, ölç ve değer yaz.

### 3.7 Follow Me — Kısayol: **yok**
**Amaç:** Bir profili (yüz) bir yol boyunca süpürerek 3B şekil (korniş, oluk, torna/lathe formlar).

**Otomatik yöntem (önerilen):** Yola yaklaşık dik bir profil çiz (yola değmesi gerekmez) → Select ile yolu oluşturan sürekli kenarları seç → Follow Me → profile tıkla.

**Elle yöntem:** Follow Me → profile tıkla → imleçle yol boyunca sürükle (yol kırmızı vurgulanır) → yolun sonunda tıkla. Başlangıç kenarı profile değmiyorsa ekstrüzyon o kenardan başlar. Esc → baştan.

**Değiştirici:** **Alt** = tıklanan yüzün **çevresini yol** olarak kullan (QRC 2026).

**Lathe (döndürerek):** Daire çiz → daireye dik, alt ucu dairenin merkezine hizalı yarım kesit yüzü çiz → daireyi seç → Follow Me → kesite tıkla. Küre/kase/vazo için.

**Kurallar ve sonuç:** Yol ve profil aynı grup bağlamında olmalı. Yolda boşluk varsa işlem başarısız olur (yakınlaştırıp boşlukları Line ile kapat). Yol olarak eğri (Curve) kullanılırsa sonuçta gizli/yumuşatılmış kenarlı pürüzsüz yüzey oluşur.

### 3.8 Offset — Kısayol: **F**
**Adımlar**
- **Yüz:** F → yüze tıkla → içe/dışa hareket et → tıkla veya mesafe yaz + Enter.
- **Kenarlar:** Aynı düzlemde iki veya daha fazla **bağlı** kenarı önseç → F → seçili segmentlerden birine tıkla → hareket/tık veya mesafe.
- Yön imlecin hareket ettirildiği tarafa göre belirlenir.
- Başka araç/çizim yapılana kadar mesafe tekrar yazılabilir.
- **Çift tık:** Bir sonraki yüze çift tıklayınca aynı mesafeyle offset.

**Değiştirici:** Varsayılan olarak offset'in oluşturacağı **çakışmalar temizlenir**; **Alt** basılıysa çakışmalar korunur.

**Geometri:** Yeni kenarlar orijinale paralel; yüz offsetinde iç yüz ayrı bir yüz olarak bölünür. Yay offset edildiğinde ortaya çıkan eğri genelde düzenlenemez; ancak 3+ segmentli dairesel yaylar algılanıp yay olarak korunur.

### 3.9 Flip — Kısayol: **yok** (aranabilir/atanabilir) — **2023.0'da eklendi**
2023.0 sürüm notlarına göre eski "Flip Along" bağlam menüsü komutlarının yerini aldı (İngilizce 2023.0 not sayfası açılamadı; Lehçe sayfanın arama özeti ve forum başlıklarıyla doğrulandı → kısmen doğrulandı).

**Adımlar**
1. Geometriyi önseç **veya** aracı etkinleştirip üzerine gel; bir kez tıkla.
2. Kırmızı/yeşil/mavi eksene dik **üç yarı saydam düzlem** görünür.
3. Bir düzlemi eksen boyunca sürükleyip (çıkarımlarla) konumlandır.
4. Düzleme tıkla → o düzleme göre aynala.

**Değiştiriciler**
| Tuş | Etki |
|---|---|
| ← / → / ↑ | Yeşil / kırmızı / mavi düzlemi seç |
| Ctrl | Flip ↔ kopya arasında geçiş (düzlemi sürükleyip bırak → aynalanmış kopya) |
| Alt | Seçili nesnenin eksenleri ↔ üst (bağlam) eksenleri |
| Yüz üzerine gelme | Macenta özel düzlem; tıkla = aynala, sürükle = konumla |

Diziler: Ölçü kutusu `3x`, `3*`, `3/` söz dizimi Flip için de geçerli (yardımın Measurements box sayfası).

### 3.10 Solid Tools — Kısayol: **yok** (Solids araç çubuğu, Tools menüsü)
**Katı (solid) tanımı:** Sonlu, kapalı hacimli; "sızıntı" içermeyen (eksik yüz yok, her kenar düzgün bağlı) **grup veya bileşen**. Entity Info sol üstünde "Solid" ibaresi görünür. 2025.0: tüm seçim katıysa Entity Info toplam hacmi gösterir.

| Araç | İşlev | Lisans |
|---|---|---|
| **Outer Shell** | Çakışan katıların yalnızca dış kabuğunu tutar (iç yüzler atılır) | Ücretsiz sürümde de var |
| **Union** | Katıları tek katıda birleştirir (iç geometri kalabilir) | Ücretli |
| **Intersect** | Yalnızca ortak (kesişen) hacmi tutar | Ücretli |
| **Subtract** | Birinci katı ile ikinciyi keser; **kesici silinir** | Ücretli |
| **Trim** | Birinci katı ile ikinciyi keser; **kesici kalır** | Ücretli |
| **Split** | Çakışan katıları kesişim kenarlarından ayırır (ör. 2 grup → 3 grup) | Ücretli |

**Etkileşim:** Aracı seç → 1. katıya tıkla (imleçte "1") → 2. katıya tıkla ("2"). Outer Shell/Union/Intersect'te tıklamaya devam ederek ek katı eklenebilir. Alternatif: katıları önseç → sağ tık > Solid Tools > araç. **Sıra önemli (Subtract/Trim):** önce tıklanan = kesici.
Değiştirici tuş: resmi kaynakta yok. Sonucun grup mu bileşen mi olarak kaldığı (Outer Shell/Union/Intersect için) yardımda açık değil → **doğrulanamadı**.

### 3.11 Intersect Faces — Bağlam menüsü / Edit menüsü (kısayol yok)
**Amaç:** Seçili yüzlerin diğer yüzlerle kesiştiği yerlere yeni kenar (ve yüz bölünmesi) üretmek; Solid Tools'un ücretsiz alternatifi (kesiştir → istenmeyen parçaları Eraser ile sil).

| Seçenek | Neyle kesiştirir | Ne zaman kullanılabilir |
|---|---|---|
| **With Model** | Modeldeki tüm yüzler (iç içe grup/bileşenler dahil) | Seçimde yüz varken; büyük modelde yavaş |
| **With Selection** | Yalnızca seçili yüzler birbirleriyle | Birden fazla yüz seçiliyken |
| **With Context** | Yalnızca açık düzenleme bağlamındaki yüzler | Yalnızca bir grup/bileşen düzenlenirken |

**Kritik kural:** Hangi seçenek olursa olsun yeni kenarlar **aktif düzenleme bağlamına** eklenir (bu ayrıntı forum uzmanlarından; resmi yardım yalnızca üç seçeneği tanımlar). Küçük kenarlar temizlenebilir; çözüm olarak modeli geçici büyütme önerilir (forum).

---

## 4. Yapı (Construction) araçları

### 4.1 Tape Measure — Kısayol: **T**
**Modlar (2026.1 itibarıyla):** **Create Guide Lines** (varsayılan) → **Create Guide Points** → **Measure**. **Ctrl** her basışta modları döngüler (2026.1 öncesi QRC tanımı: "kılavuz oluştur / yalnız ölç" geçişi).

**Ölçme:** Ctrl ile Measure moduna geç → başlangıca tıkla → hareket → bitişe tıkla → mesafe Ölçü kutusunda. Bir kenarın üzerine gelmek tüm uzunluğu gösterir. 2022.0'dan beri ölçülen değer kutuda kalır (araç kısayoluna yeniden basınca sıfırlanır).

**Kılavuz çizgisi:** Hedefe paralel bir varlıktaki noktaya (ör. kenar) tıkla → dik yönde hareket et → tıkla veya ofset yaz + Enter → sonsuz, kesikli kılavuz çizgisi (kenara paralel).
**Kılavuz noktası:** Guide Point modunda tıkla (bir noktadan belirli mesafede); **çift tık** tek kılavuz noktası oluşturur (2026.1).
**Ok tuşları:** eksen kilidi (↓ paralel/dik).
**Yeniden ölçekleme:** Ölçümden sonra yeni değer yaz + Enter → "modeli yeniden boyutlandır?" iletişim kutusu.

**Kılavuz yönetimi:** Kılavuzlar geometriyi değiştirmez, çıkarım referansıdır. Move/Rotate ile taşınıp döndürülebilir (sonsuz olduklarından boyutlandırılamaz). Gizleme: Edit > Hide; Silme: Eraser ile tık, sağ tık > Erase, Edit > Delete Guides (tümü). Çok sayıda kılavuz performansı düşürür. Oluşturulurken kılavuz görünürlüğü kapalıysa otomatik açılır (2022.0). 2024.0: kılavuzlara çıkarım iyileşti.

### 4.2 Protractor — Kısayol: **yok**
**Adımlar**
1. Aracı seç; protractor rengi başladığı düzlemi gösterir. Düzlem: üzerine gelerek veya ok tuşlarıyla; **Shift** ile kilitle.
2. Açının tepe noktasına tıkla. İsteğe bağlı **Alt**: protractor'ı çıkarılan düzlemden ayırıp başka geometriye taşı (açı orijinal düzlemde kalır).
3. Açının başladığı noktaya tıkla (tepe noktasından sürükleyerek özel dönme ekseni de verilebilir).
4. Hareket et → tıkla → **açılı kılavuz çizgisi** oluşur.

**Hassasiyet:** 0,1° çözünürlük; 15° işaretler, merkeze yakınken bu işaretlere yapışır.
**Değiştiriciler (QRC):** Ctrl = kılavuz oluşturmayı aç/kapa (yalnız ölçüm); ok tuşları = düzlem kilidi.
**Yazılabilir değer:** açı ve eğim (`rise:run`) — yardım sayfasında açık değil, Rotate ile aynı söz dizimi olduğu varsayılır → **doğrulanamadı**.

### 4.3 Dimensions — Kısayol: **yok**
**Doğrusal ölçü:** Aracı seç → başlangıç noktası (uç, orta, kenar üstü, kesişim, yay/daire merkezi) → bitiş noktası → ölçüyü dik yönde dışarı çek → tıkla. Tek bir kenar için kenara tıkla ve çek.
**Yarıçap/çap:** Yay veya daireye uygulanır; sağ tık > Type > Radius/Diameter.
**Düzlemler:** Kırmızı-yeşil, kırmızı-mavi, mavi-yeşil düzlemler veya ölçülen kenarın düzlemi (orbit ile seçilir); yerleştikten sonra yalnızca kendi düzleminde hareket eder.
**İlişkilendirme:** Ölçüler geometriye bağlıdır ve model değişince güncellenir; metin elle değiştirilirse bağ kopar (Model Info'da kırmızı vurgulanabilir).
**Biçim:** Metin konumu, renk, yazı tipi, uç stili (eğik çizgi, nokta, kapalı/açık ok, yok), metni ölçüye veya ekrana hizalama — Entity Info (tek) veya Model Info > Dimensions (toplu).
**Sürüm notları:** 2025.0 grup içi ölçüler grup düzenlenince kaymaz; ölçekli gruplarda bozulmaz. 2026.0 ölçeklenen bileşen içindeki ölçüler güncellenir; Color by Tag ölçülere uygulanır.

### 4.4 Text — Kısayol: **yok**
- **Ekran metni (screen text):** Boş alana tıkla → yaz → kutu dışına tıkla veya iki kez Enter. Ekrana sabittir, orbit'ten etkilenmez.
- **Lider metni (leader text):** Varlığa tıkla → metni konumlandır → tıkla → varsayılan metni (bileşen adı, yüz alanı vb.) düzenle.
- İpucu: Text aracıyla bir yüze **çift tık** → alanını metin olarak yazar.
- Düzenleme: Metne çift tık veya sağ tık > Edit Text. Lider stilleri: Pushpin (varsayılan), View Based, Hidden. Ok stilleri: None, Dot, Closed (varsayılan), Open.
- **2025.0:** Ölçüm içeren metin, başvurduğu varlık değişince otomatik güncellenir; `<>` yer tutucusu dinamik değeri temsil eder, öncesine/sonrasına metin eklenebilir.

### 4.5 3D Text — Kısayol: **yok**
1. Aracı seç → "Place 3D Text" iletişim kutusu.
2. Metni yaz; yazı tipi, normal/kalın, hizalama, yükseklik (değer+birim).
3. **Filled** = harf içleri yüz; kapalıysa yalnız 2D dış çizgi. **Extruded** + değer = ekstrüzyon derinliği; kapalıysa düz metin.
4. Place → model içinde Move ile konumlandırıp tıkla.
- Sonuç gerçek geometridir, **grup** olarak yerleşir (yardım). İtalik seçeneği yardımda yok, Ruby API `add_3d_text` italik parametresi içerir.
- Gravür ipucu: Negatif ekstrüzyon → grubu patlat → gereksiz çizgileri sil.

### 4.6 Axes — Kısayol: **yok**
- **Hızlı:** Araç aktifken bir yere **çift tık** → eksenleri oraya taşı.
- **Adım adım:** Orijine tıkla → kırmızı eksen varsayılan olarak imleci izler (**Alt** ile yeşil/mavi seçilir) → ilk ekseni hizala, tıkla → kalan ekseni hizala, tıkla.
- **Bağlam menüsü:** Yüzde **Align Axes** (yüz düşeyse eksenler 90° döner; yatay yüzde zemin düzlemi o yüze hizalanır). Eksen çizgisinde **Move** (sayısal ofset/dönme iletişim kutusu), **Reset**, **Hide**.
- Bileşenlerde: sağ tık > **Change Axes** (bileşen orijini).
- 2022.0'dan beri standart görünümler (Top/Front…) özel eksenlere uyar.

### 4.7 Section Plane — Kısayol: **yok**
1. (Doğru bağlamda olduğundan ve başka seçim olmadığından emin ol) aracı seç.
2. Bir yüzün üzerine gel; **Shift** ile yönü kilitle. Ok tuşları: ↑ mavi, → kırmızı, ← yeşil, ↓ yüze paralel.
3. Tıkla → ad ve sembol/numara iletişim kutusu → OK.

**Düzenleme:** Move/Rotate ile taşı/döndür. Sağ tık: **Reverse** (yönü çevir), **Align View**, **Active Cut**, **Create Group from Slice**, **Hide**, **Troubleshoot Section Fill**. Düzleme çift tık = aktif kesiti aç/kapa. Her grup/bileşen bağlamında **tek aktif kesit**.
**Section Fill:** Styles > Edit > Modeling Settings > Section Fill (renk); kapalı kesit döngüsü gerektirir.
**Görünürlük:** Display Section Planes / Display Section Cuts araçları. 2022.0: görünür kesit düzlemlerinin arkasındaki varlıklar seçilebilir/boyanabilir.
**Dışa aktarma:** File > Export > Section Slice (varsayılan .dwg).

---

## 5. Paint Bucket — Kısayol: **B**
**Temel:** İsteğe bağlı önseçim → B (Materials paneli açılır) → malzeme seç → önseçim varsa ona uygulanır, yoksa yüz/kenara tıkla. Grup/bileşene tıklamak nesnenin (varsayılan malzemesi boş yüzlerini) boyar; içerideki geometriye ulaşmak için gruba girilir.

| Değiştirici | Etki (QRC 2026) |
|---|---|
| Tık | Tek yüz/varlık |
| **Ctrl** | **Fill:** Tıklanan yüzle aynı malzemeye sahip **bitişik** tüm yüzleri boya |
| **Shift** | **Replace:** Modelde (aktif bağlamda) aynı malzemeli **tüm** yüzleri değiştir |
| **Ctrl+Shift** | **Replace on object:** Aynı nesnedeki aynı malzemeli yüzleri değiştir |
| **Alt** (basılı) | **Sample:** Modeldeki malzemeyi örnekle (damlalık) |

Ctrl/Shift/Ctrl+Shift davranışları güncel yardım sayfasında yer almıyor; yalnızca resmi QRC'de var. Stil ayarları yüzlerin/arka yüzlerin malzeme göstermesini engelleyebilir.
**Sürüm notları:** 2025.0 Photoreal (PBR) malzemeler (Metalness, Roughness, Normal, Ambient Occlusion haritaları), varsayılan kütüphanenin tamamı Photoreal, USD/GLB malzeme alışverişi, AI ile "Generate Textures". 2026.0 Live Component'lere serbest malzeme, Invert Roughness, Windows'ta malzeme küçük resmi türü (Auto/Cube/Flat).

## 6. Tag (tool) — Kısayol: **yok** — **2022.0'da eklendi**
Tags panelinde tek bir etiket seç → Tag aracı → varlığa tıkla (mor sınır yanıp söner = uygulandı). Birden çok etiket veya klasör seçiliyse uygulamaz. Önseçili varlıklarla da çalışır.
| Değiştirici | Etki |
|---|---|
| Alt | **Sample:** tıklanan varlığın etiketini seçili etiket yap |
| Shift | **Replace All:** tıklanan varlıkla aynı etiketi taşıyan her şeyin etiketini değiştir |
| Ctrl | **Tag All Instances:** bileşenin tüm örneklerini etiketle |
İlgili seçim komutları (2022.0): Select > All with Tag, Select > All Instances, Tags panelinde Select All Tagged.

---

## 7. Sürüm bazında yeni/ değişen özellikler (2022 → 2026.2)

| Sürüm | Çizim/düzenleme ile ilgili yenilikler | Doğrulama |
|---|---|---|
| 2021.1 | İlk tıktan önce ok tuşlarıyla yön ön-kilidi (Line, Freehand, 2/3P Arc, Move, Tape) | 2022.0 notunda geçmiş referans |
| **2022.0** (25 Oca 2022) | **Search** (Shift+S), **Lasso** (Shift+Space), **Tag tool**, Freehand iyileştirmesi (segment azaltma, düzlem kilidi, yüzler arası çizim), 2P/3P Arc **teğet kilidi (Alt)**, arc ikinci nokta değer girişi, Stamp modunda dizi | Resmi sürüm notu PDF |
| **2023.0** | **Flip tool** (Flip Along'un yerine; Ctrl kopya, Alt eksen bağlamı), Overlays (uzantı API'si) | Kısmen (arama özeti + forum) |
| **2024.0** | **Yeni grafik motoru** (yaygın yapılandırmada ~2,4×, üst düzeyde ~8,4× hız; klasik motor Preferences > Graphics'te kalır, uygun değilse otomatik geri düşer; GPU seçimi), **Ambient Occlusion** stili (View > Face Style), Rotate "Leaning Ladder" çıkarımı, Move döndürme tutamaklarını aç/kapa (+kısayol), Undo devam eden işlemi iptal eder, Rectangle yüz düzlemindeki kenarlara çıkarım, kılavuz çıkarımı, Snaps iyileştirmeleri (Reorient/Reverse Snap) | Resmi sürüm notu |
| **2025.0** | **Photoreal Materials** (PBR), **Environments** (HDRI/EXR, güneş konumu, skydome, yansıma), AO bir PBR haritası olarak, 2P/3P Arc fillet tekrarı ve "unround", Text'te dinamik `<>`, Rotate tutamakları sınır kutusunda, Snaps'ta ↓ paralel kilidi, Select > Connected by Snaps, Entity Info katı hacmi, Style Builder kaldırıldı | Resmi sürüm notu |
| **2026.0** | AO Distance Multiplier + renk, Scale/Live Component iyileştirmeleri, düzlem kilidinde döndürme tutamakları gizli, profil kenarından konumlandırma, Paint Bucket ↔ Live Components, **Comment tool**, sahne işlemleri geri alınabilir, Color by Tag metin/ölçülere, Windows panel/tepsi yeniden düzeni, LayOut'a Trim/Extend/Fillet/Chamfer (Shift+T/E/F/C – yalnız LayOut) | Resmi sürüm notu |
| **2026.1** | Tape Measure **Create Guide Point** modu + Ctrl ile 3 mod döngüsü, çift tıkla kılavuz noktası, **AI Render**, Windows'ta tek sayfa PDF'i görüntü olarak içe aktarma | Resmi sürüm notu |
| 2026.1.2 | Orantılı ölçekte yazılan uzunluk seçilen eksen tutamağı boyunca | Arama özeti (sayfa açılmadı) |
| **2026.2** (19 May 2026) | **Hızlı gölge sistemi** (kenar gölgesi yok, kesit/X-ray sınırlamaları; Windows'ta kapatılabilir), Scale tutamak çizimi düzeltmesi, Move/Rotate tutamak döngüsü düzeltmesi, Windows'ta Ctrl+Q artık kaldırılabilir kısayol | Resmi sürüm notu |

**Not:** "Tag tool" ve "Flip tool" 2024/2025/2026 yeniliği **değildir** (sırasıyla 2022.0 ve 2023.0). "Snaps" özelliğinin ilk sürümü **doğrulanamadı** (2024.0 notunda iyileştirme olarak geçiyor).

---

## 8. Çelişkiler ve doğrulanamayan noktalar (karar listesi)

1. **Line + Alt:** Yardım = çıkarım modu döngüsü; QRC = yön kilidi. (Shift = yön kilidi her iki kaynakta tutarlı.)
2. **Rectangle merkezden çizim:** QRC = Alt; forum = Ctrl (toggle).
3. **Lasso yönü ve değiştiricileri:** 2022.0 notu ↔ güncel yardım ↔ QRC farklı.
4. **Arc segment +/− tuşu (Windows):** Yardım = Ctrl; QRC tablosu "Alt +" başlığı.
5. **Push/Pull + Ctrl:** QRC = orijinal yüzü bırak; yardım = Ctrl+çift tık ile istifleme. (Muhtemelen ikisi de doğru; aynı mod.)
6. **Polygon iç/dış teğet (Ctrl)** ve **varsayılan 6 kenar:** yalnızca forum.
7. **Move önseçimsiz hover-taşıma:** resmi kaynakta tarif yok.
8. **Freehand + Shift:** bulunamadı.
9. **Scale negatif değerle aynalama, 2D yüz ölçeği:** resmi kaynakta yok.
10. **Protractor'a açı/eğim yazma:** açıkça belgelenmemiş.
11. **Solid Tools sonucunun grup/bileşen olarak korunması:** belgelenmemiş.
12. **Intersect Faces sonucunun aktif bağlama yazılması:** yalnızca forum uzmanları.
13. **Rotate dairesel dizi söz dizimi:** yardım makalesi tam açılamadı; arama özetiyle doğrulandı.
14. **Flip tool 2023.0:** İngilizce not sayfası açılamadı.
15. **Eraser + Ctrl+Shift (klasik unsoften):** güncel kaynaklarda Alt olarak geçiyor; Ctrl+Shift doğrulanamadı.

---

## Kaynaklar

**Resmi yardım (help.sketchup.com)**
- https://help.sketchup.com/en/sketchup/introducing-drawing-basics-and-concepts
- https://help.sketchup.com/en/sketchup/drawing-basic-shapes
- https://help.sketchup.com/en/sketchup/drawing-arcs
- https://help.sketchup.com/en/sketchup/drawing-freehand-shapes
- https://help.sketchup.com/en/default-keyboard-shortcuts
- https://help.sketchup.com/en/sketchup/selecting-geometry
- https://help.sketchup.com/en/sketchup/moving-entities-around
- https://help.sketchup.com/en/sketchup/stretching-geometry
- https://help.sketchup.com/en/sketchup/snaps
- https://help.sketchup.com/en/sketchup/flipping-mirroring-rotating-and-arrays
- https://help.sketchup.com/en/article/3000092 (Copying What You've Already Drawn – arama özeti üzerinden)
- https://help.sketchup.com/en/sketchup/pushing-and-pulling-shapes-3d
- https://help.sketchup.com/en/sketchup/scaling-your-model-or-parts-your-model
- https://help.sketchup.com/en/sketchup/extruding-follow-me
- https://help.sketchup.com/en/sketchup/offsetting-line-existing-geometry
- https://help.sketchup.com/en/sketchup/erasing-and-undoing
- https://help.sketchup.com/en/sketchup/softening-smoothing-and-hiding-geometry (arama özeti üzerinden)
- https://help.sketchup.com/en/sketchup/modeling-complex-3d-shapes-solid-tools
- https://help.sketchup.com/en/sketchup/dividing-splitting-and-exploding-lines-and-faces
- https://help.sketchup.com/en/context-menu-items-entities
- https://help.sketchup.com/en/sketchup/measuring-angles-and-distances-model-precisely
- https://help.sketchup.com/en/measuring-distance
- https://help.sketchup.com/en/measuring-angles
- https://help.sketchup.com/en/using-guides
- https://help.sketchup.com/en/using-measurements-box
- https://help.sketchup.com/en/sketchup/adding-text-labels-and-dimensions-model
- https://help.sketchup.com/en/sketchup/adjusting-drawing-axes
- https://help.sketchup.com/en/sketchup/slicing-model-peer-inside
- https://help.sketchup.com/en/sketchup/applying-materials
- https://help.sketchup.com/en/sketchup/controlling-visibility-tags (arama özeti üzerinden)

**Resmi sürüm notları**
- https://help.sketchup.com/en/release-notes/sketchup-desktop-20240
- https://help.sketchup.com/en/release-notes/sketchup-desktop-20250
- https://help.sketchup.com/en/release-notes/sketchup-desktop-20260
- https://help.sketchup.com/en/sketchup-desktop-20261
- https://help.sketchup.com/en/sketchup-desktop-20262
- https://help.sketchup.com/en/release-notes/sketchup-desktop-202612 (arama özeti üzerinden)
- https://help.sketchup.com/pl/release-notes/sketchup-desktop-20230 (arama özeti üzerinden; doğrudan açılamadı)
- https://download.sketchup.com/legacydocs/SketchUp%20Desktop%202022.0.pdf

**Resmi Hızlı Başvuru Kartları (QRC)**
- https://download.sketchup.com/QRC2026/HC-QRC2026-en-SU-windows.pdf
- https://download.sketchup.com/QRC2023-SU-win_en-US.pdf

**Resmi API belgesi**
- https://ruby.sketchup.com/Sketchup/Entities.html

**Topluluk forumu (resmi olmayan kullanıcı içerikleri; yalnız destekleyici)**
- https://forums.sketchup.com/t/directions-to-use-intersect-faces-with-model-and-selection-and-context/43096
- https://forums.sketchup.com/t/how-do-i-change-whether-im-placing-a-corner-of-a-polygon-circle-or-a-side/28120 (arama özeti üzerinden)
- https://forums.sketchup.com/t/sides-on-a-circle-default/17299 (arama özeti üzerinden)
- https://forums.sketchup.com/t/cannot-push-pull-curved-or-smooth-surface/68707 (arama özeti üzerinden)
- https://forums.sketchup.com/t/issue-rectangle-tool-switches-between-corner-and-center-focus/79239 (arama özeti üzerinden)
- https://forums.sketchup.com/t/sketchup-2023-flip-along-not-in-context-menu/228250 (arama özeti üzerinden)
