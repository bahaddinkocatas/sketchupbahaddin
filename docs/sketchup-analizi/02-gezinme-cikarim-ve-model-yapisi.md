# 02 — Gezinme, Çıkarım (Inference) Motoru ve Model Yapısı

> **Amaç:** Kendi adı, kodu ve ikonları olan özgün bir Windows 3B modelleme uygulaması geliştirirken, SketchUp 2026'nın (Trimble) kamera/gezinme, çıkarım, Ölçüler kutusu, geometri modeli ve model organizasyonu davranışlarını **yalnızca herkese açık resmî belgelerden** analiz etmek.
>
> **Yöntem:** Yalnızca help.sketchup.com, sketchup.com, forums.sketchup.com ve ruby.sketchup.com kullanıldı. Yazılım indirilmedi, tersine mühendislik yapılmadı, ikon/görsel kopyalanmadı. Metinler özetlenerek Türkçeleştirildi.
>
> **Etiketler:**
> - **[Resmî]**: help.sketchup.com veya ruby.sketchup.com metninde doğrudan görüldü.
> - **[Forum]**: Yalnızca forums.sketchup.com kullanıcı yanıtlarında görüldü; resmî değil.
> - **doğrulanamadı**: Resmî kaynakta bulunamadı veya kaynaklar arasında net değil.
> - **[Öneri]**: Bizim uygulamamız için tasarım önerisi; SketchUp davranışı iddiası değildir.
>
> **Not:** Yardım sayfalarının büyük bölümü 2026 tarihli güncellemeler taşıyor; ancak bazı sayfaların hangi sürüme ait olduğu açıkça belirtilmiyor. Windows/macOS farkları belirtilen yerlerde yazıldı (bu proje Windows hedefli olduğundan Windows tuşları öne alındı).

---

## 1. Kamera ve gezinme

### 1.1 Temel kamera araçları

| Araç | Varsayılan kısayol | Davranış (özet) | Kaynak durumu |
|---|---|---|---|
| **Orbit (Yörünge)** | `O` | Tıklayıp sürükleyince kamera, çizim alanının merkezi etrafında döner. Çizim alanında çift tıklama modeli ortalar. | [Resmî] |
| Orbit + `Ctrl` (Win) / `Option` (Mac) | — | Kamerayı yana yatırır (roll). Bu sırada dikey kenarları dik tutan yerleşik "yerçekimi" askıya alınır. | [Resmî] |
| **Pan (Kaydır)** | `H` | Tıklayıp sürükleyerek kamera yatay/dikey kayar. | [Resmî] |
| **Zoom (Yakınlaş)** | `Z` | Yukarı sürükle = yakınlaş, aşağı = uzaklaş. Sürükleyerek zoom ekran merkezinden yapılır. | [Resmî] |
| Zoom + `Shift` sürükle | — | Görüş alanını (FOV) elle değiştirir. | [Resmî] |
| **Zoom Window (Pencereyle zoom)** | Resmî listede kısayol yok | Kutu çizilir; bırakınca kutudaki alan çizim alanını doldurur. | [Resmî]; kısayol **doğrulanamadı** |
| **Zoom Extents (Tümünü göster)** | `Shift+Z` | Tüm modeli görünür ve ortalanmış hâle getirir. 2026.0'da yaklaşık %90 hızlandığı belirtiliyor. | [Resmî] |
| **Previous / Next** | Resmî listede kısayol yok | Camera > Previous bir önceki görünüme döner; Camera > Next geri gidildikten sonra ileri gider. | [Resmî]; kısayol **doğrulanamadı** |

### 1.2 Fare eşlemeleri (geçici araç değişimi)

Geçici kısayollar, etkin aracı değiştirmeden gezinmeye izin verir; bırakılınca önceki araca dönülür.

| Hareket | Sonuç | Kaynak durumu |
|---|---|---|
| Orta tuş (tekerlek) basılı tut + sürükle | Orbit | [Resmî] |
| Orta tuş + sol tuş birlikte basılı | Pan | [Resmî] |
| Orbit sırasında `Shift` basılı tut | Pan'a geçer | [Resmî] |
| Varsayılan kısayol listesinde "Shift + tıkla + sürükle" | Pan | [Resmî] (orta tuşla mı kastedildiği sayfada açık değil; yaygın kullanım `Shift`+orta tuştur → **doğrulanamadı** düzeyinde kabul) |
| Tekerlek kaydırma (her araçta) | Zoom; **imleç zoom merkezidir** (imleç yönüne zoom) | [Resmî] |
| Zoom hızı | İmleç altındaki geometri uzaklaştıkça zoom daha hızlı | [Resmî] |
| Tek tuşlu fare | Orbit: `Ctrl + Windows tuşu` (Win) / `Ctrl + Cmd` (Mac) + tıkla. Pan: tek tuşlu fare/izleme yüzeyinde `Control + Command + Shift` (Mac) + basılı tıklama; Windows karşılığı **doğrulanamadı** | [Resmî] |
| Orta tuşla çift tıklama | Tıklanan noktayı merkeze alma | **doğrulanamadı** (resmî sayfada yalnızca "Orbit ile çift tıklama modeli ortalar" ifadesi var) |

### 1.3 Projeksiyon ve görüş alanı

- **Perspective (varsayılan):** Çizgiler ufka doğru yakınsar; ölçek korunmaz. [Resmî]
- **Parallel Projection (ortografik):** Paralel çizgiler paralel kalır, uzunluklar ölçeklidir. DWG/DXF dışa aktarımı için standart görünüm + paralel projeksiyon önerilir. [Resmî]
- **Two-Point Perspective:** Görünümü iki kaçış noktası olacak şekilde hizalar; mimari/konsept çizimde kullanılır. [Resmî]
  - Kamera tam yatay bakacak şekilde ayarlanır, dikey kenarlar dik kalır; Orbit yapılınca normal perspektife döner, Pan ile korunur. [Forum] — güncel sürümde **doğrulanamadı**.
  - Ruby API'de kameranın iki noktalı perspektif veya Match Photo modunda olup olmadığını bildiren ayrı bir durum (`is_2d?`) ile 2B ofset/ölçek değerleri (`center_2d`, `scale_2d`) vardır. Bu, iki noktalı perspektifin kamera üzerine eklenen bir **2B kaydırma/ölçek katmanı** olarak modellendiğini düşündürür. [Resmî — API]
- **Field of View (Görüş alanı):** Camera > Field of View veya Zoom aracı etkinken Ölçüler kutusuna derece **ya da** milimetre (odak uzaklığı) yazılır. [Resmî]
  - API'ye göre FOV aralığı 1°–120°, odak uzaklığı 1–3000 mm; odak hesabında varsayılan film genişliği 36 mm. FOV'un yatay mı dikey mi ölçüldüğü ayrı bir bayrakla tutulur. [Resmî — API]
  - Varsayılan FOV değeri (genelde 35° bilinir): **doğrulanamadı**.
- **Paralel projeksiyonda** "FOV" yerine görünüm yüksekliği (inç) saklanır. [Resmî — API]
- **Kamera geçişi:** API'de kamera atanırken bir geçiş süresi verilebilir; mevcut kameradan yenisine animasyonla geçilir. [Resmî — API]

### 1.4 Standart görünümler

- Camera menüsünde: **Top, Bottom, Front, Back, Left, Right, Iso** (Viewer'da "Camera > Standard Views" alt menüsü). [Resmî]
- Bu görünümler için varsayılan klavye kısayolu: resmî varsayılan listede yok → **doğrulanamadı** (kullanıcı Preferences > Shortcuts ile atayabilir). [Resmî]

### 1.5 Yürüme araçları

| Araç | Davranış | Kaynak durumu |
|---|---|---|
| **Position Camera** | Tek tıklama: kamerayı tıklanan noktanın 5'6" (~1,68 m) üstüne koyar. Basılı tut + sürükle: önce konum, sonra bakış yönü belirlenir. Göz yüksekliği Ölçüler kutusuna yazılarak değiştirilir. İşlemden sonra otomatik olarak Look Around'a geçilir. | [Resmî] |
| **Look Around** | Sabit noktadan "baş çevirme": sürükleyerek sağ/sol/yukarı/aşağı bakılır. Göz yüksekliği her an Ölçüler kutusundan değiştirilebilir. | [Resmî] |
| **Walk** | Tıklayıp sürükle; tıklanan yerde küçük bir artı işareti çıkar, ondan uzaklaştıkça hız artar. Yukarı/aşağı = ileri/geri, sağ/sol = dönme. Eğim ve merdivenlerde göz yüksekliği korunur. Çarpışma algılama varsayılan açıktır. | [Resmî] |
| Walk + `Shift` | İleri/geri yerine yukarı/aşağı hareket | [Resmî] |
| Walk + `Ctrl` (Win) / `Option` (Mac) | Koşma | [Resmî] |
| Walk + `Alt` | Duvarlardan geçme (çarpışmayı kapatma) | [Resmî] |
| Walk ok tuşları, basamak yüksekliği, hızlar | — | **doğrulanamadı** |

### 1.6 Kamera ile ilgili diğer varsayılan kısayollar (resmî liste)

`Space` Select, `Shift+Space` Lasso Select, `L` Line, `R` Rectangle, `C` Circle, `A` 2 Point Arc, `M` Move, `P` Push/Pull, `Q` Rotate, `S` Scale, `F` Offset, `T` Tape Measure, `B` Paint Bucket, `E` Eraser, `G` Make Component, `K` Back Edges, `I` Image Igloo, `Shift+S` Search; kamera: `O`, `H`, `Z`, `Shift+Z`. [Resmî]
Kısayollar Preferences > Shortcuts'tan değiştirilebilir; Windows'ta içe/dışa aktarılabilir. [Resmî]

### 1.7 [Öneri] Uygulamamız için

- Kamera modeli: `eye/target/up + fov + projeksiyon tipi + (opsiyonel) 2B ofset/ölçek`. İki noktalı perspektifi ayrı bir "dikey kenarları koru" kısıtı olarak ele alıp Orbit'te kapatın.
- Tekerlek zoom'u imleç altındaki 3B noktaya doğru yapılmalı ve adım boyu o noktaya olan mesafeyle orantılı olmalı.
- Previous/Next için kamera geçmişi yığını; geçişlerde süre parametreli yumuşak animasyon.

---

## 2. Çıkarım (inference) motoru

Çıkarım, imlecin mevcut geometriye veya eksen yönlerine "yapışmasıdır". Her çıkarımın kendine ait bir **rengi** ve adını gösteren bir **ipucu (ScreenTip)** vardır. İmleç bir grup/bileşen içindeki geometriyi işaret ediyorsa ipucuna "in group / in component" eklenir. [Resmî]

### 2.1 Nokta çıkarımları

| Çıkarım | Anlamı | Renk/şekil |
|---|---|---|
| Origin | Çizim eksenlerinin kesiştiği nokta | **doğrulanamadı** |
| Component Origin Point | Grup/bileşen içindeki eksen orijini; varsayılan yerleştirme noktası | **doğrulanamadı** |
| **Endpoint** | Çizgi, yay veya yay segmentinin ucu | Yeşil daire [Forum] |
| **Midpoint** | Çizgi/kenar/yay segmentinin ortası | Açık mavi (cyan) nokta [Resmî] |
| Arc Midpoint | Yayın orta noktası | **doğrulanamadı** |
| **Intersection** | Çizginin başka çizgi veya yüzeyle kesiştiği nokta | **doğrulanamadı** |
| **On Edge** | Kenar üzerindeki herhangi bir nokta | Kırmızı kare [Forum] |
| **On Face** | Yüzey üzerindeki nokta | Mavi elmas (eşkenar dörtgen) [Forum] |
| **Center** | Daire, yay veya çokgen merkezi | **doğrulanamadı** |
| Guide Point / On Line | Kılavuz noktası veya kılavuz çizgi üzerindeki nokta | **doğrulanamadı** |
| On Section | Çizim aracının kesit düzlemi üzerinde kenar oluşturduğu nokta | **doğrulanamadı** |
| Intersection with Hidden Section | Gizli kesit düzleminin kenarı ile kesişim | **doğrulanamadı** |

- **Grup/bileşen içi kural:** Tüm nokta çıkarımları, geometri bir grup veya bileşen içindeyse **aynı şekilde ama macenta (mor-pembe)** renkte gösterilir. [Resmî]
- Yay ve daire kenarı üzerinde gezinince kenar ve merkez çıkarımları gösterilir. [Resmî]
- Daire tek bir varlıktır ama çıkarımlar yine tek tek segmentlerine yapışır. [Resmî]

### 2.2 Doğrusal (linear) çıkarımlar

| Çıkarım | Anlamı | Renk |
|---|---|---|
| **On Red / Green / Blue Axis** | Çizim yönü kırmızı (X), yeşil (Y) veya mavi (Z) eksene paralel | Eksen rengiyle: kırmızı / yeşil / mavi [Resmî] |
| **From Point** | Bir noktadan eksen yönünde hizalanma; noktalı (dotted) çizgi gösterilir | Noktalı çizginin rengi eksen yönüyle aynı [Resmî] |
| **Through Point** | Bir noktadan başlayıp başka bir noktanın üzerinde durup `Shift` ile o iki noktadan geçen yöne kilitleme | **doğrulanamadı** |
| **Parallel** | Bir kenara paralel | Macenta [Resmî] |
| **Perpendicular** | Bir kenara dik | Macenta [Resmî] |
| Perpendicular to Face | Yüzey normaline hizalı | **doğrulanamadı** |
| Extend Edge | Mevcut kenarın uzantısı | **doğrulanamadı** |
| Tangent at Vertex | Önceki yayın ucuna teğet yeni yay | **doğrulanamadı** (2 Point Arc ile ilişkili) |

- Paralel/dik çıkarımda hem çizim yönü hem de referans alınan kenar macenta olur. [Resmî]
- Hizalanma, çizilen çizginin (ve çizim düzleminin) kırmızı/yeşil/mavi/macenta renge dönmesiyle gösterilir. [Resmî]
- **Şekil çıkarımları:** Kare, Altın Oran (Golden Section), Yarım/Çeyrek/Üç Çeyrek Daire, Yay Kenarı ve Merkezi, Daire/Çokgen Merkezi. Dikdörtgen aracında kare ve altın oran için mavi noktalar ve ipucu görünür. [Resmî]
- Ayar: Preferences > Drawing > "Display cross hairs" ile imleçte eksen renklerinde artı çizgileri; Styles > Edge Settings > Color "By axis" ile kenarlar eksen hizasına göre renklenir. [Resmî]

### 2.3 Kilitleme (inference locking)

| Tuş | Etki | Kaynak durumu |
|---|---|---|
| `Shift` (basılı tut) | O anda etkin çıkarım yönüne veya düzlemine kilitler. Kilitliyken imleç başka geometriye referans alabilir (ör. yükseklik eşitlemek). | [Resmî] |
| `↑` Yukarı ok | Mavi eksene (Z) kilitle | [Resmî] |
| `→` Sağ ok | Kırmızı eksene (X) kilitle ("Right locks Red") | [Resmî] |
| `←` Sol ok | Yeşil eksene (Y) kilitle | [Resmî] |
| `↓` Aşağı ok | Çıkarım yapılan kenara/düzleme paralel–dik (macenta) kilidini aç/kapa | [Resmî] |
| `Shift+Alt` (Win) / `Shift+Command` (Mac) | Çizim düzlemini ve aracı, çıkarım yapılan yüzeyin düzlemine kilitler | [Resmî] |
| Rotate / Protractor'da `Alt` (Win) / `Command` (Mac) | Merkezi serbest bırakıp taşınabilir yapar | [Resmî] |
| Circle / Rotate | Ok tuşlarıyla normal yönü seçilerek bir **düzleme** kilitlenir (ör. sol ok = yeşil düzlem) | [Resmî] |

- LayOut belgesinde kilitlenince kesikli renkli çizginin **düz (kalın)** çizgiye dönüştüğü yazıyor; SketchUp için aynı görsel ayrım **doğrulanamadı**.
- Kesit düzlemi yerleştirirken de aynı ok tuşu eşlemesi kullanılır (Yukarı=mavi, Sağ=kırmızı, Sol=yeşil, Aşağı=yüzeye paralel). [Resmî]
- Ruby API'de kilitleme üç biçimde: argümansız = tüm kilitleri kaldır; tek giriş noktası = o noktanın varlığına (kenarın doğrusu veya yüzeyin düzlemi) kilitle; iki giriş noktası = iki nokta arasındaki eksene kilitle. [Resmî — API]
- Forumda, eksen dışı (pembe/macenta) çıkarımlarda özel araçlarda kilitlemenin beklendiği gibi çalışmadığına dair geliştirici raporu var. [Forum]

### 2.4 Çıkarımı açma/kapama

Line aracında ilk tıklamadan sonra `Alt` (Win) / `Command` (Mac) basıldıkça üç durum arasında döner: [Resmî]
1. Tüm çıkarımlar açık (varsayılan)
2. Tüm doğrusal çıkarımlar kapalı (nokta çıkarımları — On Face, Guide Point, Midpoint vb. — yine çalışır)
3. Yalnızca paralel ve dik çıkarımlar

### 2.5 Önceliklendirme (snap priority)

- **Resmî olarak belgelenen tek kural:** İmleci bir noktanın üzerinde kısa süre **bekletmek**, SketchUp'ın o noktayı referans almasını önceliklendirir ("çıkarımı teşvik etme"); aksi hâlde yanlış geometriye hizalanabilir. [Resmî]
- Ruby API, imleç altındaki noktanın **serbestlik derecesini** sınıflandırır: 0 = uç/kesişim noktası (tam sabit), 1 = kenar veya eksen üzerinde, 2 = yüzey üzerinde, 3 = boşlukta. Bu, öncelik sırasının nokta > kenar > yüzey > boşluk şeklinde olduğunu **düşündürür**, ancak açık bir öncelik tablosu yayımlanmamıştır → **doğrulanamadı**. [Resmî — API kısmı]
- 2026.0 sürüm notu: küçük hedeflere çıkarım yapma hızlandırıldı; grup/bileşen yerleştirirken nesne yarı saydam bir profil kenarıyla gösterilerek hedef seçimi kolaylaştırıldı. [Resmî]

### 2.6 [Öneri] Uygulamamız için

- Snap adaylarını tek bir yapı ile üretin: `{tip, konum, serbestlik, kaynak varlık, bağlam yolu, ekran mesafesi}`; skor = tip önceliği (vertex > midpoint/intersection > center > on-edge > on-face > eksen yönü) + ekran mesafesi + "bekleme" ile kazanılmış referans noktaları.
- Referans noktası belleği: üzerinde ~0,5 sn beklenen noktaları kısa süreli listeye alıp onlardan eksen yönlü "from point" çizgileri üretin.
- Renk/şekil kodlaması özgün olmalı; SketchUp ikonları/işaretleri kopyalanmamalı. Eksen renklerinin RGB = XYZ eşlemesi sektör genelinde yaygın bir kuraldır.

---

## 3. Ölçüler kutusu (Measurements box / VCB)

### 3.1 Genel davranış

- Arayüzün **sağ alt köşesinde** bulunur; anlamı etkin araca göre değişir (çizimde boyut, Move/Rotate'te konum/açı, Scale'de ölçek). [Resmî]
- Değer yazılıp `Enter` ile onaylanır. Kutuya önce tıklamak gerekmez; doğrudan klavyeden yazılır → resmî sayfada açıkça yazmıyor, **doğrulanamadı** (yaygın bilinen davranış).
- Birimsiz yazılan sayı, belgenin (şablonun) birim ayarını kullanır. [Resmî]

### 3.2 Birim son ekleri

| Birim | Son ek | Örnek |
|---|---|---|
| İnç | `"` | `10"` |
| Fit | `'` | `10'` |
| Milimetre | `mm` | `10mm` |
| Santimetre | `cm` | `10cm` |
| Metre | `m` | `10m` |

Herhangi bir araçta son ekli değer, yalnızca o giriş için birimi değiştirir. [Resmî]

### 3.3 Koordinat girişi

- **Mutlak (global):** köşeli parantez, ör. `[3', 5', 7']` — etkin eksenlere göre. [Resmî]
- **Göreli:** açılı parantez, ör. `<1.5m, 4m, 2.75m>` — çizginin (veya taşımanın) başlangıç noktasına göre. [Resmî]
- Koordinatla **yalnızca bitiş noktası** belirlenebilir, başlangıç noktası belirlenemez; Move ile nesne mutlak/göreli koordinata taşınabilir. [Forum]

### 3.4 Çok boyutlu girişler

- Dikdörtgen: `uzunluk,genişlik` (ör. `8',20'`); yalnız biri için `3',` veya `,3'`. Negatif değer dikdörtgeni ters yönde oluşturur. [Resmî]
- Liste ayırıcısı ondalık ayırıcısı virgül olan yerel ayarlarda farklı (noktalı virgül) olabilir → **doğrulanamadı**.

### 3.5 Segment ve yarıçap

- **Circle:** Kutu varsayılan segment sayısını gösterir; daire yerleştirilmeden önce sayı yazılarak değiştirilir. Merkezden sonra yarıçap yazılır. [Resmî]
- **Polygon:** En az 3 kenar; kenar sayısı ve yarıçap aynı mantıkla. [Resmî]
- Çizim bittikten sonra, başka araç seçilene veya yeni şekil çizilene kadar yarıçap/segment sayısı tekrar yazılarak değiştirilebilir. Sonradan Entity Info panelinden de değiştirilebilir. [Resmî]
- **2 Point Arc:** 1. ve 2. tıklama uçları (kiriş), 3. tıklama şişkinliği belirler; ilk tıklamadan sonra uzunluk yazılabilir. Çizimden hemen sonra `20c` gibi "sayı + c" yazmak, yayı 20 kenarlı bir daireye göre segmentlere ayırır. 12'den az segment basit ama köşeli görünür. [Resmî — arama özetinden]
- Segment sayısı için `s` son eki (ör. `12s`) yaygın bilinir ama bu araştırmada resmî metinde görülmedi → **doğrulanamadı**.
- Ruby API'de daire ve çokgen için varsayılan segment/kenar sayısı **24**. [Resmî — API] (Arayüz aracının varsayılanı da 24 olarak bilinir → arayüz için **doğrulanamadı**.)

### 3.6 Dizi (array) kopyalama

Move (Ctrl ile kopya modunda) veya Flip ile bir kopya yapıldıktan sonra: [Resmî]

| Yazım | Anlamı |
|---|---|
| `3x` veya `3*` | Dış dizi: ilk kopya aralığıyla toplam 3 kopya (orijinalin ötesine) |
| `3/` | İç dizi: orijinal ile ilk kopya arasındaki mesafeyi eşit bölerek kopyalar |

- Kopya sayısı yalnızca işlem kopya olarak başladıysa çalışır; `x`/`*` olmadan yazılan sayı yeni mesafe olarak yorumlanır. Mesafe, tıklama noktaları arasıdır. [Forum]
- Move ile kopyalama: `M` → `Ctrl` (Win) / `Option` (Mac); imleçte artı işareti çıkar; mesafe yazılıp `Enter`. [Resmî — arama özetinden]

### 3.7 Araç tamamlandıktan sonra

- Line: çizgi çizildikten sonra yeni uzunluk yazıp `Enter` ile **istenildiği kadar** düzeltilebilir; yeni çizgi çizilene veya başka araç seçilene kadar geçerlidir. [Resmî]
- Circle/Polygon/Arc ve dizi sayıları için de aynı "son işlemi düzelt" mantığı geçerlidir. [Resmî]
- Yanlış ilk tıklamada `Esc` ile baştan başlanır. [Resmî]

### 3.8 Model Info > Units (birim ayarları)

Window > Model Info > Units: [Resmî]
- **Format:** Architectural, Decimal, Engineering, Fractional.
- Decimal'de uzunluk, alan ve hacim için ayrı birim seçilebilir (ör. uzunluk cm, alan m²).
- **Precision:** biçime göre (Architectural en fazla 1/64 inç; Engineering 6 ondalık).
- **Enable Length Snapping:** çizim aracı, belirtilen hassasiyetteki en yakın birime yapışır.
- **Display Units Format:** (Decimal/Fractional) birim simgesini ipuçlarında gösterir/gizler.
- **Force Display of 0"** (Architectural): `3' 0"` gibi sıfırları gösterir.
- **Angle:** açı hassasiyeti (en fazla 3 ondalık) ve **Enable Angle Snapping**. Varsayılan açı yakalama adımı (genelde 15° bilinir) → **doğrulanamadı**.
- Başlangıç şablonu temel birimi belirler. [Resmî]

### 3.9 [Öneri] Uygulamamız için

- VCB ayrıştırıcısı için küçük bir dilbilgisi: `uzunluk[birim]`, `a,b[,c]`, `[x,y,z]`, `<x,y,z>`, `Nx|N*|N/`, `Ns`, `Nc`, açı/eğim. Ayrıştırma her aracın "son işlemi yeniden parametrele" kancasına bağlanmalı.
- Ondalık/liste ayırıcısını işletim sistemi yerel ayarından almayı (Türkçe'de ondalık virgül) baştan tasarlayın.

---

## 4. Geometri modeli ("yapışkan geometri")

### 4.1 Kenarlar ve yüzeyler

- Tüm modeller **kenar (edge)** ve **yüzey (face)** varlıklarından oluşur. Kenarlar Line aracıyla çizilir. [Resmî]
- Çizgiler kapalı bir şekil oluşturunca bir **yüzey** oluşur; Rectangle/Circle/Polygon doğrudan yüzey üretir. [Resmî]
- Yüzey **düzlemsel (planar)**, en az 3 kenarlı, kenarlarla sınırlı, **iki taraflı** bir çokgendir. [Resmî — API]
- Yüzeyin bir **dış döngüsü (outer loop)** ve sıfır veya daha çok **iç döngüsü** vardır; iç döngüler **delikleri** tanımlar. [Resmî — API]
- "Yüzey yalnızca kenarlar eş düzlemliyse oluşur" ifadesi yardım sayfalarında açıkça yazmıyor, ancak yüzeyin düzlemsel olması zorunluluğundan çıkar; forumda da yüzey oluşmamasının yaygın nedeni olarak eş düzlemli olmayan kenarlar gösteriliyor. [Resmî — API + Forum]
- API'de, kapalı bir döngü oluşturan kenarlar eklendiğinde yüzey otomatik oluşmaz; `find_faces` ile kenara bağlı tüm olası yüzeyler oluşturulur. Arayüzdeki çizim araçları ise bunu otomatik yapar. [Resmî — API]

### 4.2 Bölme (splitting) ve birleşme

- Yüzeyin üzerine çizilen ve iki kenarına dayanan çizgi/eğri, yüzeyi **ikiye böler**; parçalar Push/Pull ile ayrı ayrı itilebilir. [Resmî]
- Yeni bir çizgi var olan bir kenarı/yayı kestiğinde o kenar kesişim noktasında **bölünür**. [Resmî]
- Kenarlar ve yüzeyler birbirine "yapışır": bağlı geometri taşınınca model **gerilir** (stretch): [Resmî]
  - yüzey sürüklenirse yüzey korunur, komşular gerilir;
  - kenar sürüklenirse komşu tüm yüzeyler gerilir;
  - uç nokta sürüklenirse o noktaya bağlı tüm kenar ve yüzeyler gerilir.
- **Autofold:** Yüzeyler her zaman düzlemsel kalmalıdır; bir gerilme yüzeyi bükecekse SketchUp otomatik kırım kenarı ekler. Move sırasında `Alt` (Win) / `Command` (Mac) Autofold'u açıp kapatır. Bazen düzlemselliği korumak için hareket kısıtlanır. [Resmî]
- **Divide:** Kenar/yay bağlam menüsünden eşit parçalara bölünür. [Resmî — arama özetinden]
- **Explode/Weld:** Daire ve çokgen tek tek segmentlere patlatılabilir; bağlı segmentler tekrar tek bir **Curve** varlığına kaynaklanabilir. Eğriler kesişemez; birden çok eğrinin buluştuğu yerde bölünür. [Resmî + API]
- Gruplar/bileşenler **yapışmaz**: içindeki geometri dışarıdakine bağlanmaz; yapışkanlığı yalıtmanın temel yolu budur. [Resmî]

### 4.3 Silme davranışı

- Eraser ile kenar silinirse ona bağlı yüzey(ler) de silinir. Yüzey bağlam menüsünden Erase edilirse yalnızca yüzey silinir. [Resmî]
- Silinen yüzey, onu tanımlayan kenarlardan biri yeniden çizilerek geri getirilebilir. [Resmî]
- Bir yüzeyi sınırlayan çizginin uzunluğu Entity Info'dan değiştirilemez. [Resmî]

### 4.4 Ön ve arka yüzler

- Her yüzeyin bir **ön** (varsayılan beyaz) ve **arka** (varsayılan mavi-gri) yüzü vardır; normal vektörü ön yönü gösterir; ön ve arka için ayrı malzeme atanabilir. [Resmî + API]
- Önerilen kural: tüm ön yüzler dışa baksın; tutarsızlık renk boşluklarına ve render sorunlarına yol açar. [Resmî]
- **Reverse Faces:** seçili yüzeyi çevirir. **Orient Faces:** bitişik yüzeyleri seçilen yüzeyle tutarlı hâle getirir. [Resmî]
- Zemin düzleminde oluşturulan yüzeyler için API'de köşe sırası dikkate alınmaz (zemin yüzeylerinin yönü özel ele alınır). [Resmî — API] Arayüzde zemine çizilen yüzeyin arka yüzünün yukarı baktığı yaygın bilinir → **doğrulanamadı**.

### 4.5 Yumuşak (soft) ve pürüzsüz (smooth) kenarlar, eğriler ve yüzeyler (surface)

- **Soft:** Kenar gizlenir ve sınırladığı yüzeyler tek bir **surface** varlığı gibi davranır (birine tıklayınca hepsi seçilir). [Resmî]
- **Smooth:** Yüzeyler arasında gölgelendirme yumuşatılır; tek başına uygulanırsa kenar görünür kalır. Genellikle ikisi birlikte uygulanır. [Resmî]
- **Hide:** Kenarı gizler ama surface oluşturmaz. [Resmî]
- Yay, daire ve eğrilerin Push/Pull ile ekstrüzyonu varsayılan olarak soft+smooth kenarlar üretir. [Resmî]
- Eraser + `Ctrl` (Win) / `Option` (Mac): tıklanan kenarları soft+smooth yapar; Eraser + `Alt` (Win) / `Command` (Mac): yumuşatmayı geri alır. [Resmî] Eraser + `Shift` kenarı gizler. [Resmî]
- **Soften Edges** paneli (Windows: Window > Default Tray): açı sınırı kaydırıcısı (bu açıdan küçük kenarlar yumuşatılır), "Smooth normals" (varsayılan açık), "Soften coplanar" (eş düzlemli yüzeyler arasındaki kenarları yumuşatır). Entity Info'da tek kenar için Soft/Smooth onay kutuları vardır. [Resmî]
- **Kontrol kenarı:** Yay/daire/çokgen ekstrüzyonuyla oluşan yüzeylerin bir kontrol kenarı vardır; Move ile sürüklenince surface bozulmadan yeniden boyutlanır. Serbest el eğrisinden oluşanlarda yoktur. [Resmî]
- Daire ve çokgen **tek varlık** gibi seçilir; Push/Pull'da daire kenarları pürüzsüz, çokgen kenarları belirgin görünür. [Resmî]

### 4.6 Delikler

- İç döngüler delik oluşturur. [Resmî — API]
- Yüzeyin içine çizilen kapalı şekil yeni bir iç yüzey oluşturur; bu yüzey silinirse veya karşı yüzeye kadar itilirse delik oluşur. Push/Pull ile delmek için başlangıç ve bitiş yüzleri **paralel** olmalı ve arada kenar bulunmamalı; grup içindeyse önce grubun düzenleme bağlamına girilmelidir. [Forum]
- Solid Inspector'ın otomatik düzelttiği hatalar arasında "face holes" bulunur. [Resmî]

### 4.7 [Öneri] Uygulamamız için

- Çekirdek veri yapısı olarak yarı-kenar (half-edge) veya radial-edge tabanlı bir **manifold olmayan** B-rep; her bağlam (model kökü, her grup/bileşen tanımı) kendi topolojisine sahip.
- Her ekleme sonrası: kesişim/bölme → kapalı eş düzlemli döngü arama → dış/iç döngü sınıflandırma (delik) → yüzey oluşturma. Bu işlem yerel (dokunulan kenarlar çevresinde) yapılmalı.
- Düzlemsellik toleransı ve köşe birleştirme (vertex welding) toleransı tek yerde tanımlanmalı.

---

## 5. Gruplar, bileşenler ve model organizasyonu

### 5.1 Grup ve bileşen ayrımı

- Gruplanmış geometriye genel olarak **Object** denir; iki tür vardır: **Group** ve **Component**. [Resmî]
- Nesneler iç içe olabilir (ev > oda > mobilya). Nesneler başka varlıklara yapışmaz; üst üste olsalar da bağımsız düzenlenir. [Resmî]
- **Grup oluşturma:** Seçim → Edit > Make Group veya sağ tık > Make Group; seçim yokken de boş grup başlatılıp içine çizilebilir. Grup için varsayılan kısayol resmî listede yok → **doğrulanamadı**. [Resmî]
- API düzeyinde her grubun da gizli bir **tanımı (definition)** vardır; kopyalanan gruplar aynı tanımı paylaşır ama arayüzden düzenlendiğinde **sessizce benzersizleştirilir (make unique)**. Yani grup = "her zaman tekil davranan bileşen". [Resmî — API]

### 5.2 Bileşenler: tanım ve örnek

- **Tanım (definition):** paylaşılan geometri ve özellikler. **Örnek (instance):** modeldeki yerleşimi (dönüşüm matrisi ile). [Resmî]
- Tanım düzenlenirse modeldeki **tüm örnekler** değişir. [Resmî]
- **Make Unique:** Bir örnek seçilip sağ tık > Make Unique yapılırsa o örnek için yeni bir tanım oluşturulur; tanım adına `#1` gibi bir son ek eklenir. [Resmî]
- Outliner'da bir bileşen yeniden adlandırılırsa tanım adı değişir, dolayısıyla tüm örneklerin adı değişir. [Resmî]
- **Make Component** (`G`) iletişim kutusu alanları: [Resmî]
  - Definition (ad), Description
  - **Glue To:** Any / Horizontal / Vertical / Sloped
  - **Cut Opening:** yerleştirildiği yüzeyde açıklık keser
  - **Always Face Camera:** 2B "billboard" davranışı
  - **Shadows Face Sun:** gölge güneşe bakıyormuş gibi (ağaç gibi dar tabanlılar için)
  - **Set Component Axes:** bileşenin orijin/eksenlerini belirler
  - **Replace selection with component:** varsayılan açık; kapalıysa seçim yerinde kalır, tanım yalnızca "In Model" listesine eklenir
  - Gelişmiş öznitelikler: Price, Size, URL, Type (IFC sınıfı); sonradan Status/Owner eklenebilir
- **Glue / Cut Opening kuralları:**
  - API'ye göre açıklık kesme, yapıştırılabilirlik (2B davranış) açık olmadan etkinleştirilemez; yapıştırma düzlem türü ayrı bir özelliktir. Ölçek tutamaklarının hangilerinin gizleneceği de bir bit maskesiyle tanımlanır. [Resmî — API]
  - Gruplar yüzeye yapışabilir ama **açıklık kesemez**; bileşen ancak yüzeyle **aynı bağlamdaysa** keser; bileşenin mavi (Z) ekseni yapışacağı yüzeye dik olmalıdır; yapışık olmayan bir örneğin kopyası kesme/yapışma davranışını kazanmaz. [Forum]

### 5.3 Düzenleme bağlamı (edit context)

- Nesneye Select aracıyla **çift tıklanınca** düzenleme bağlamı açılır; sınırlayıcı kutu kesikli görünür. Bağlamdan çıkmak için boş alana tıklanır veya Edit > Close Group/Component seçilir. [Resmî]
- Düzenleme sırasında modelin geri kalanının nasıl gösterileceği Model Info > Components ayarıyla kontrol edilir (ör. "Hide Rest of Model", "Hide Similar Components"; bunlar şablona kaydedilebilir). [Resmî (iPad/Display ve şablon sayfaları) + Forum]
- Modelin geri kalanının **soluk (fade)** gösterilmesi ve bunun yüzdesi masaüstü için **doğrulanamadı** (yaygın bilinen davranış).
- `Esc` ile bağlamdan çıkılması **doğrulanamadı**.

### 5.4 Explode ve Lock

- **Explode:** Edit > Group > Explode veya Edit > Component > Explode; nesne bileşen varlıklarına ayrılır ve bir üst bağlamla birleşir (yapışkan geometri yeniden etkileşir). [Resmî + API]
- **Lock:** Sağ tık > Lock / Unlock. Kilitli nesne taşınamaz ve düzenlenemez, ama çıkarım için referans/sınır olarak kullanılabilir. Kilitli nesnenin kırmızı sınırlayıcı kutuyla gösterilmesi **doğrulanamadı**. [Resmî]

### 5.5 Outliner

- Window > Default Tray > Outliner. Grupları, bileşenleri ve **kesit düzlemlerini** hiyerarşik ağaç olarak gösterir. [Resmî]
- Çocuğu olan öğede ok simgesi; Details > Expand All; Sort By Name (kapalıyken oluşturma/ekleme sırası). [Resmî]
- Outliner'da seçim = modelde seçim; gizli nesne seçilirse modelde taralı gösterilir. Üstte arama/filtre alanı. [Resmî]
- Sürükle-bırak ile **yeniden ebeveynleme** (hiyerarşiyi değiştirme). [Resmî]
- Durum simgeleri: açık/kapalı, kilitli, gizli/görünür; kesit düzlemlerinde ayrıca "inactive". Görünürlük simgesine tıklamak nesneyi ve içindekileri gizler. [Resmî]
- Gruplar yeniden adlandırılabilir (sağ tık veya üçlü tıklama); kesit düzlemlerinin adı ve sembolü değiştirilebilir. [Resmî]

### 5.6 Katı (solid) varlıklar

- **Solid:** Sonlu, kapalı hacmi olan grup veya bileşen; eksik yüzey veya bir kenarda buluşmayan yüzey ("sızıntı") olmamalı. Entity Info panelinin sol üstünde katı olup olmadığı gösterilir. [Resmî]
- Katı araçları: [Resmî]

| Araç | İşlev | Sürüm |
|---|---|---|
| Outer Shell | Üst üste binen katıların yalnızca dış yüzeylerini bırakır | Ücretsiz sürümde de var |
| Union | Katıları tek forma birleştirir (iç geometri kalabilir) | Ücretli |
| Subtract | Kesici katı diğerinden çıkarılır ve silinir | Ücretli |
| Trim | Kesici katı keser ama modelde kalır (ilk seçilen kesici) | Ücretli |
| Intersect | Yalnızca kesişim hacmini bırakır | Ücretli |
| Split | Kesişim kenarlarından böler (2 nesne → 3 nesne) | Ücretli |

- API'de hacim yalnızca manifold nesnelerde anlamlıdır (pozitif = manifold); Boolean işlemleri manifold olmayan girdide başarısız olur; Split sonucu "fark2, fark1, kesişim" sırasıyla döner. [Resmî — API]
- Solid Inspector'ın otomatik düzelttikleri: ters yüzler, başıboş kenarlar, iç/dış başıboş yüzeyler, yüzey delikleri. Elle düzeltilenler: kenar delikleri, iç içe gruplar, görüntü varlıkları, kısa kenarlar. [Resmî]

### 5.7 Dinamik bileşenler (kısaca)

- Öznitelikler (attributes) bileşenlere davranış kazandırır; önceden tanımlı öznitelikler (konum, boyut, döndürme, görünürlük, kopya sayısı vb.) ve özel öznitelikler vardır. Öznitelik yazma **Pro** özelliğidir. [Resmî — arama özetinden]
- **Interact** aracı (Tools > Interact): ör. dinamik kapıya tıklayınca kapı açılır. **Component Options** penceresi: kullanıcının değiştirebileceği önceden tanımlı seçenekler. [Resmî — arama özetinden]

### 5.8 Live Components

- 3D Warehouse'ta "Live" rozetli, **yapılandırılabilir** bileşenler; Warehouse'ta veya SketchUp içinde (bağlam menüsü > Configure Live Component ya da çift tıklama) kaydırıcı, alan ve açılır listelerle yapılandırılır; model gerçek zamanlı güncellenir. [Resmî]
- Değişiklikler **tanım düzeyinde** uygulanır (tüm örnekleri etkiler); tek örnek için Make Unique gerekir. Scale aracıyla boyutlanabilir ve Paint Bucket ile özel malzeme alabilir. [Resmî]
- Çevrimdışı davranış ve dinamik bileşenlerle teknik farkı **doğrulanamadı**.

### 5.9 [Öneri] Uygulamamız için

- `Definition { entities, behavior, attributes }` + `Instance { definition_ref, transform, tag, locked, hidden, glued_to }`. Grup = tek örnekli, düzenlemede kopyala-yaz (copy-on-write) yapan tanım.
- Düzenleme bağlamı = örnek yolu (instance path) yığını; tüm çıkarımlar ve yeni geometri bu yolun dönüşümünde hesaplanır.
- Açıklık kesme, yapıştırılan yüzeyle aynı bağlamda bir "kesme döngüsü" olarak render edilir; gerçek topoloji değişikliği yapılmadan görsel/boolean olarak çözülmesi değerlendirilebilir.
- Dinamik/Live bileşen benzeri özellikler ilk sürüm kapsamına alınmadan önce ayrıca karar verilmelidir.

---

## 6. Etiketler, sahneler, gizleme, kesitler ve Model Info

### 6.1 Etiketler (Tags; eski adıyla Layers)

- SketchUp 2020'den itibaren arayüzde "Layer" yerine **Tag** denir; API'de hâlâ "Layer". Katman 0 arayüzde **Untagged** olarak görünür. [Resmî + API]
- Etiketler nesneleri düzenler ve **görünürlüğü** kontrol eder; tek tıkla etiketli tüm nesneler gizlenebilir. [Resmî]
- Yeni ham geometri varsayılan olarak **Untagged** olur. **Önerilen uygulama:** Etiketi kenar/yüzeye değil grup ve bileşenlere verin; etkin etiketi (kalem simgesi) değiştirmeyin (yalnızca deneyimli kullanıcılar için). [Resmî]
- **Tags paneli:** Ekle (+), klasör ekle, filtre, Tag aracı simgesi, Color by Tag, her etiket için görünürlük (göz), renk örneği, **Dashes** (kesik çizgi deseni) sütunu; Ad veya Görünürlük başlığına göre sıralama. Details menüsü: Expand/Collapse All, Select All, Delete Tag/Folder, **Purge** (kullanılmayan etiketleri sil), Select All Tagged. [Resmî]
- **Tag aracı:** Tek etiket seçilir, araçla varlığa tıklanır (varlık mor yanıp söner). `Alt` = örnekle (tıklanan varlığın etiketini al), `Shift` = aynı etiketi taşıyan tüm varlıkların etiketini değiştir, `Ctrl` = bileşenin tüm örneklerini etiketle. [Resmî]
- **Etiket klasörleri:** Klasör görünürlüğü içindeki tüm etiketleri değiştirir; klasörler iç içe olabilir; seçili etiketler yeni klasöre taşınır; adsız klasör otomatik numaralanır. [Resmî]
- **Dashes:** Etiket başına kesik çizgi deseni atanır; o etiketteki geometrinin **kenarları** kesik çizilir. Styles > Edge Settings > Dashes ile kesikler kenarları gizlemeden kapatılabilir. DWG/DXF'den çizgi tipleri yaklaşık eşlenerek içe alınır; eşleşme yoksa düz çizilir. Dışa aktarım: PDF, EPS, raster formatlar, DWG/DXF. LayOut'ta kesik ölçeği varsayılan 0,5 pt. [Resmî]
- API: Etiketin rengi, çizgi stili, üst klasörü ve **yeni/mevcut sahnelerdeki varsayılan görünürlük davranışı** (bayrak) tutulur. [Resmî — API]
- 2026.0: Color by Tag artık metin ve ölçülendirmelere de uygulanıyor; kullanılmayan etiketleri temizleme hızlandı. [Resmî]

### 6.2 Sahneler (Scenes)

- Sahne, model görünümünü ve seçili özellikleri kaydeder; modelleme penceresinin üstünde **sekme** olarak görünür. [Resmî]
- Sahne eklemeden önce görünüm, stil, sis, gölge, kesitler, gizli geometri ve etiket görünürlüğü ayarlanır, sonra **Add Scene**. **Show Details** ile ad, açıklama ve kaydedilecek özellikler seçilir. [Resmî]
- **Kaydedilebilen özellikler** (onay kutuları): [Resmî]

| Özellik | İçerik |
|---|---|
| Camera Location | Bakış noktası, zoom mesafesi, görüş alanı |
| Hidden Geometry / Hidden Objects (üst düzey) | Gizli geometri ve gizli nesneler |
| Visible Tags | Etiket görünürlükleri |
| Active Section Planes | Yalnızca etkin kesit düzlemleri |
| Style and Fog | Tüm stil ve sis ayarları |
| Shadow Settings | Gölge, tarih ve saat |
| Environment Settings | Güneş konumu ve gökyüzü kutusu (skybox) |
| Axes Location | Eksen konumu ve görünürlüğü |
| (Scan Essentials) | Nokta bulutu görünürlüğü |
| Include in Animation | Sahnenin animasyona dahil olması |

- Yönetim: Update Scene, Remove Scene, Move Up/Down, sekmede Move Left/Right, sahne küçük resmi, sekmelerin solundaki arama ile sahne bulma. [Resmî]
- **Animasyon:** View > Animation > Play; ayarlar View > Animation > Settings veya Model Info > Animation: **Scene Transitions** (aç/kapa + saniye) ve **Scene Delay** (sahnede bekleme süresi). Video (.mp4 vb.) veya görüntü dizisi olarak dışa aktarılabilir. [Resmî — arama özetinden]
- API'de sahne başına geçiş ve bekleme süresi ayrıca tanımlanabilir (−1 = varsayılanı kullan). [Resmî — API]
- 2026.0: Sahne ekleme/düzenleme/yeniden adlandırma/silme geri alınabilir; geçişler daha akıcı; aynı adla iki sahneye API'de izin verilmiyor. [Resmî]

### 6.3 Gizleme / gösterme

- Seçim → sağ tık > Hide veya Edit > Hide. Gizli geometri modelde kalır ama görünmez ve seçilemez. Kenar, yüzey ve nesnelere uygulanır. [Resmî]
- View > Hidden Geometry / View > Hidden Objects: gizli öğeleri **hayalet (ghosted)** gösterir; seçilip sağ tık > Unhide ile gösterilir. [Resmî]
- Unhide seçenekleri: **All**, **Selected**, **Last** (iPad Display paneli belgesinde). Masaüstü menü yolu (Edit > Unhide > ...) **doğrulanamadı**. [Resmî — iPad]
- Görünürlük katmanları birbirinden ayrıdır: varlığın kendi gizli bayrağı, etiket görünürlüğü, Outliner görünürlüğü (nesne gizleme), sahne kayıtları. [Resmî — sentez]

### 6.4 Kesit düzlemleri ve kesitler

- Tools > Section Plane; varsayılan kısayol resmî listede yok → **doğrulanamadı**. Yerleştirmeden önce seçimi kaldırın ve düzlemin ait olacağı bağlam dışındaki bağlamları kapatın. [Resmî]
- Bir yüzeye tıklanarak yerleştirilir; yön için yüzey üzerinde durup `Shift` ile kilitleme veya ok tuşları (Yukarı=mavi, Sağ=kırmızı, Sol=yeşil, Aşağı=yüzeye paralel). Yerleştirince **ad ve sembol** istenir. [Resmî]
- Bir modelde birden çok kesit düzlemi olabilir ama **her bağlamda (model kökü, her grup/bileşen) yalnızca bir etkin kesit** olur. Etkinleştirme: sağ tık > Active Cut veya düzleme çift tıklama. [Resmî]
- Sağ tık menüsü: Reverse (kesme yönünü çevir), Align View (kamerayı düzleme hizala), Active Cut, Hide, **Create Group from Slice** (kesitten grup geometrisi), **Troubleshoot Section Fill** (açık döngüdeki köşeleri kırmızı dairelerle gösterir). [Resmî]
- Görünüm düğmeleri: Display Section Planes (tüm düzlemler), Display Section Cuts (tüm kesimler; kapalıyken model bütün görünür). Tek tek gizlenen düzlem bu genel düğmeyle yeniden görünmez. [Resmî]
- **Section Fill:** Styles > Edit > Modeling Settings > Section Fill (renk seçilebilir) veya Section Fill aracı. Dolgu yalnızca grup/bileşen hiyerarşisi içinde **kapalı döngü** oluşturan kesitlerde çalışır. [Resmî]
- Dışa aktarım: File > Export > Section Slice (varsayılan .dwg; True Section veya Screen Projection). [Resmî]
- Kesit düzlemi yalnızca **görsel** bir etkidir; katıyı gerçekten bölmez. Gerçek kesim için Intersect Faces önerilir. [Forum]
- Sahneler etkin kesit düzlemlerini kaydedebilir (bkz. 6.2). 2026.0: 3B DWG dışa aktarımı kesit düzlemlerini koruyor. [Resmî]

### 6.5 Model Info

Window > Model Info bölümleri: **Animation, Classifications, Components, Credits, Dimensions, File, Geo-location, Rendering, Statistics, Text, Units.** [Resmî — şablon sayfası]

- Animation: sahne geçiş süresi ve sahne gecikmesi.
- Components: grup/bileşen içinde düzenlerken modelin geri kalanı ve benzer bileşenlerin nasıl gösterileceği.
- Credits: modeli kim oluşturdu; lisans sahibi adını ekleme.
- Geo-location: konum bilgisi (modelleme penceresinin sol altındaki simgeden de açılır).
- Statistics: varlık sayıları; kullanılmayanları temizleme (purge) ve bazı yaygın sorunları düzeltme.
- Dimensions / Text: ölçülendirme ve metin varsayılanları.
- Units: bkz. 3.8.
- Classifications, File, Rendering bölümlerinin içerikleri bu araştırmada ayrıntılı incelenmedi → **doğrulanamadı**.
- 2026.0 sürüm notlarında Model Info veya Units için değişiklik görülmedi. [Resmî]

### 6.6 [Öneri] Uygulamamız için

- Görünürlük hesabı tek bir fonksiyonda toplanmalı: `görünür = !varlık.gizli && etiket.görünür && tüm üst klasörler görünür && tüm üst örnekler görünür && (sahne geçersiz kılmaları)`.
- Sahne = isteğe bağlı özellik kümelerinin anlık görüntüsü (her özellik için "kaydet/kaydetme" bayrağı); geçiş için kamera enterpolasyonu.
- Kesit düzlemi: bağlam başına tek etkin düzlem; render'da kırpma düzlemi + kapalı döngüden dolgu poligonu üretimi.

---

## 7. Doğrulanamayan noktaların özeti

- Nokta çıkarımlarının çoğunun kesin renk/şekli (yalnızca Midpoint açık mavi resmî; Endpoint yeşil, On Edge kırmızı, On Face mavi forumdan; Intersection, Center, Guide vb. belirsiz).
- Çıkarım öncelik sırasının resmî tablosu.
- SketchUp'ta kilitli çıkarımın kalın/düz çizgiyle gösterilmesi (yalnızca LayOut'ta belgelenmiş).
- Zoom Window, Previous/Next, standart görünümler, Make Group ve Section Plane için varsayılan kısayollar.
- Varsayılan FOV (35°), varsayılan açı yakalama (15°), arayüzde varsayılan daire segment sayısı (API'de 24).
- Segment sayısı için `s` son eki; Türkçe gibi ondalık virgüllü yerel ayarlarda liste ayırıcısı.
- Orta tuş çift tıklama ile merkezleme; `Esc` ile düzenleme bağlamından çıkma; düzenleme bağlamında modelin soluk gösterilme oranı; kilitli nesnenin kırmızı kutuyla gösterimi.
- Masaüstünde Edit > Unhide alt menü seçenekleri (iPad belgesinde All/Selected/Last var).
- İki noktalı perspektifin Orbit'te bozulması (yalnızca eski forum yanıtları).
- Model Info'nun Classifications, File, Rendering bölümlerinin ayrıntıları; Live Components'ın çevrimdışı davranışı.

---

## Kaynaklar

Resmî yardım (help.sketchup.com):
- https://help.sketchup.com/en/sketchup/viewing-model
- https://help.sketchup.com/en/sketchup/walking-through-model
- https://help.sketchup.com/en/sketchup/introducing-drawing-basics-and-concepts
- https://help.sketchup.com/en/using-measurements-box
- https://help.sketchup.com/en/sketchup/drawing-basic-shapes
- https://help.sketchup.com/en/sketchup/drawing-arcs (arama özeti üzerinden)
- https://help.sketchup.com/en/sketchup/dividing-splitting-and-exploding-lines-and-faces (arama özeti üzerinden)
- https://help.sketchup.com/en/sketchup/softening-smoothing-and-hiding-geometry
- https://help.sketchup.com/en/sketchup/stretching-geometry
- https://help.sketchup.com/en/3d-warehouse/orienting-faces (arama özeti üzerinden)
- https://help.sketchup.com/en/sketchup/grouping-geometry
- https://help.sketchup.com/en/sketchup/creating-basic-component
- https://help.sketchup.com/en/sketchup/editing-components
- https://help.sketchup.com/en/sketchup-live-components
- https://help.sketchup.com/en/sketchup/interacting-dynamic-components (arama özeti üzerinden)
- https://help.sketchup.com/en/sketchup/adding-attributes (arama özeti üzerinden)
- https://help.sketchup.com/en/sketchup/working-hierarchies-outliner
- https://help.sketchup.com/en/sketchup/modeling-complex-3d-shapes-solid-tools
- https://help.sketchup.com/en/sketchup/controlling-visibility-tags
- https://help.sketchup.com/en/sketchup/applying-dashed-lines-layers
- https://help.sketchup.com/en/sketchup/creating-scenes
- https://help.sketchup.com/en/sketchup/animating-scenes (arama özeti üzerinden)
- https://help.sketchup.com/en/sketchup/slicing-model-peer-inside
- https://help.sketchup.com/en/managing-units-measurement
- https://help.sketchup.com/en/sketchup-ipad/model-info-panel (arama özeti üzerinden)
- https://help.sketchup.com/en/sketchup-ipad/display-panel (arama özeti üzerinden)
- https://prod-aws-help.sketchup.com/article/3000141 (Setting Templates / Model Info bölümleri; arama özeti üzerinden)
- https://prod-aws-help.sketchup.com/article/3000092 (Copying What You've Already Drawn; arama özeti üzerinden)
- https://help.sketchup.com/en/sketchup/shortcuts
- https://help.sketchup.com/en/default-keyboard-shortcuts
- https://help.sketchup.com/en/release-notes/sketchup-desktop-20260

Ruby API (ruby.sketchup.com):
- https://ruby.sketchup.com/Sketchup/Camera.html
- https://ruby.sketchup.com/Sketchup/View.html
- https://ruby.sketchup.com/Sketchup/InputPoint.html
- https://ruby.sketchup.com/Sketchup/Face.html
- https://ruby.sketchup.com/Sketchup/Edge.html
- https://ruby.sketchup.com/Sketchup/Entities.html
- https://ruby.sketchup.com/Sketchup/Group.html
- https://ruby.sketchup.com/Sketchup/Behavior.html
- https://ruby.sketchup.com/Sketchup/Page.html
- https://ruby.sketchup.com/Sketchup/Layer.html

Forum (forums.sketchup.com; resmî değil):
- https://forums.sketchup.com/t/tape-measure-red-green-beginning-points/250835
- https://forums.sketchup.com/t/how-to-lock-inference-when-the-inference-line-is-pink/335604 (arama özeti üzerinden)
- https://forums.sketchup.com/t/camera-refuses-to-stay-on-2-point-perspective/83370 (arama özeti üzerinden)
- https://forums.sketchup.com/t/i-would-like-to-start-a-line-at-a-fixed-point-such-as-x-5-and-y-5-is-there-any-way-to-use-coordinates-in-sketchup/238501 (arama özeti üzerinden)
- https://forums.sketchup.com/t/need-help-with-move-array-divide/22986 (arama özeti üzerinden)
- https://forums.sketchup.com/t/array-copies-limit-to-2-everytime-every/90210 (arama özeti üzerinden)
- https://forums.sketchup.com/t/cutting-opening-window-component/31933 (arama özeti üzerinden)
- https://forums.sketchup.com/t/how-to-correct-cut-opening/47588 (arama özeti üzerinden)
- https://forums.sketchup.com/t/how-to-uncheck-as-default-in-component-group-editing-setting/200371 (arama özeti üzerinden)
- https://forums.sketchup.com/t/how-to-create-a-hole-using-the-inner-loop/74239 (arama özeti üzerinden)
- https://forums.sketchup.com/t/issue-with-push-pull-tool/64289 (arama özeti üzerinden)
- https://forums.sketchup.com/t/how-to-really-close-faces-of-a-plane-section-cut-object/137700 (arama özeti üzerinden)
