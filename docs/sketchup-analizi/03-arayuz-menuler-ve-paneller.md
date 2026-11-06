# SketchUp 2026 Analizi — 03: Kullanıcı Arayüzü, Menüler, Paneller ve Kısayollar (Windows)

> **Amaç:** Kendi adı, kendi kodu ve kendi simgeleri olan; işlevselliği SketchUp 2026 (Trimble) Windows masaüstü sürümünü örnek alan özgün bir 3B modelleme uygulaması için arayüz referans notu.
> **Yöntem:** Yalnızca herkese açık resmi belgeler incelendi (help.sketchup.com yardım makaleleri ve sürüm notları, download.sketchup.com üzerindeki resmi Hızlı Başvuru Kartı ve eski sürüm notu PDF'leri, sketchup.trimble.com blogu, forums.sketchup.com). SketchUp yazılımı indirilmedi, çalıştırılmadı, tersine mühendislik yapılmadı ve simgeler kopyalanmadı. Tüm metinler özetlenerek, yeniden ifade edilerek aktarıldı.
> **Araştırma tarihi:** 2026-10-20 (en güncel sürüm notu: 2026.2, 19 Mayıs 2026).
> **İşaretleme:**
> - İşaretsiz ifadeler resmi bir kaynakta görülen bilgilere dayanır.
> - **"doğrulanamadı"**: Resmi belgelerde açıkça bulunamayan; genel bilgi, eski sürüm bilgisi veya yalnızca topluluk forumu ile desteklenen noktalar.
> - **(forum)**: Bilgi yalnızca forums.sketchup.com üzerindeki kullanıcı/personel mesajına dayanır.
> - Menü ve araç adları, eşleştirme kolaylığı için İngilizce özgün adlarıyla verilmiştir. Bizim uygulamada **farklı adlar ve kendi simgelerimiz** kullanılmalıdır.

---

## İçindekiler

0. [Sürüm bağlamı (2022–2026.2)](#0-sürüm-bağlamı-20222026)
1. [Pencere yerleşimi](#1-pencere-yerleşimi)
2. [Menüler ve Windows kısayolları](#2-menüler-ve-windows-kısayolları)
3. [Tepsi (tray) panelleri](#3-tepsi-tray-panelleri)
4. [Bağlam menüleri, Tercihler (Preferences), Model Bilgisi (Model Info)](#4-bağlam-menüleri-tercihler-preferences-model-bilgisi-model-info)
5. [Stil sistemi, gölgeler, sis, ortamlar](#5-stil-sistemi-gölgeler-sis-ortamlar)
6. [Varsayılan klavye kısayolları ve araç değiştirici tuşları (Windows)](#6-varsayılan-klavye-kısayolları-ve-araç-değiştirici-tuşları-windows)
7. [Sandbox (arazi) araçları](#7-sandbox-arazi-araçları)
8. [Doğrulanamayan noktalar ve kaynak çelişkileri](#8-doğrulanamayan-noktalar-ve-kaynak-çelişkileri)
9. [Uygulamamız için arayüz çıkarımları](#9-uygulamamız-için-arayüz-çıkarımları)
10. [Kaynaklar](#kaynaklar)

---

## 0. Sürüm bağlamı (2022–2026)

Arayüzün bugünkü hali, son beş yılda eklenen özelliklerle şekillendi. Aşağıdaki tablo yalnızca arayüzü etkileyen başlıca değişiklikleri özetler.

| Sürüm | Tarih | Arayüze etkisi olan başlıca yenilikler |
|---|---|---|
| 2022.0 | 25 Ocak 2022 | **Search SketchUp** (komut arama; Getting Started araç çubuğunun en solunda, Help menüsünde, kısayol Shift+S). **Lasso** seçim aracı (Shift+Space). **Tag** aracı (tıklayarak etiket atama, örnekleme değiştiricisi). Bağlam menüsünde *Select > All with Tag*, *Select > All Instances*; Tags panelinde *Select All Tagged*. Sahne sekmelerinin yanında **sahne arama** kutusu. Yay araçlarında teğet kilidi (Alt). Advanced Camera Tools artık birlikte gelmiyor. |
| 2023.0 | Mart 2023 | Birleşik **Flip** aracı (özel yansıtma düzlemi dahil), **Overlays** (eklentilerin araç etkin değilken de ekrana çizim yapabilmesi; Overlays paneli), **Polygonal Lasso**, 11 yıl sonra ilk büyük **simge yenilemesi**. Ayrıntılı 2023 sürüm notu metni okunamadı; bazı ayrıntılar doğrulanamadı. |
| 2024.0 | 2024 | **Yeni grafik motoru** (Preferences > Graphics'ten klasik motora dönülebilir). **Ambient Occlusion** yüz stili (View > Face Style, Search, Styles paneli). Trimble Connect yerleşik araç çubuğu ve File menüsü seçenekleri. Yenilenen **Add Location** akışı. Move aracı döndürme tutamaçları (Preferences > Drawing). İşlem ortasında geri al. Başlık çubuğunda kaydedilmemiş değişiklik işareti (`*`). Bağlam menüsünde *Reorient Snap* / *Reverse Snap*. |
| 2025.0 | 2025 | **Environments** paneli (HDRI/OpenEXR, Select/Edit sekmeleri). **Photoreal Materials** yüz stili ve PBR haritaları (Metalness, Roughness, Normal, AO). Materials panelinde **Generate Textures** (yapay zekâ). **Save Out / Reload** komutları. Kaydederken *Purge* hatırlatması. Tags ve Scenes panellerinde *Apply Tags to Scenes*. *Select > Connected by Snaps*. Style Builder kaldırıldı. Entity Info katı nesnelerde toplam hacim gösteriyor. |
| 2026.0 | 7 Ekim 2025 | **Collaboration Bar** (Window menüsü), **Comment** aracı ve **Comments** paneli. AO için *Distance Multiplier* ve renk. Malzemelerde *Invert Roughness*. Windows'ta: menü öğelerinin yanında araç simgeleri; panel başlığına sağ tıklayıp panel gösterme; panelleri tepsiler arasında sürükleme; yüzen panelden **Make Tray**. **Purge Unused** Edit menüsünde. Sahne işlemleri geri alınabilir. Preferences'ta özel şablon klasörü, Environment paneli kısayolu (Windows), Reset Workspace'in varsayılan yerleşime dönmesi. |
| 2026.1 | 2026 | **AI Assistant** (Extensions > AI Assistant; Help ve Generate Object araçları) ve **AI Render**. Tape Measure'da Ctrl ile üç kip arasında geçiş (Measure / Create Guide Lines / Create Guide Point). **File > Migrate Preferences**. Boş alana sağ tıklamada *Invert Selection* yerine *Select All*. Windows'ta PDF içe aktarma (tek sayfa, görüntü olarak). Styles paneli arayüzü güncellendi; Tags bölümü yeniden boyutlandırılabilir. Araç çubuğundan düğme kaldırmak artık Toolbars menüsünde sağ tık gerektiriyor. |
| 2026.1.3 | 16 Nisan 2026 | Arayüz değişikliği yok (hata düzeltmeleri). |
| 2026.2 | 19 Mayıs 2026 | **Analysis Hub** (Extensions menüsü veya Shadows panelindeki düğme; Advanced Shadows ve Labs analizleri). **Hızlı gölge sistemi** (Preferences > Graphics'ten kapatılabilir; şimdilik Windows). Components paneli araması artık modeldeki bileşenleri süzüyor; 3D Warehouse ayrı düğmede. Extension Manager yeni Qt tabanlı arayüzde. Edit menüsündeki hatalı kısayol gösterimleri düzeltildi. |

> Not: 2026.1 sürüm notu sayfası `help.sketchup.com/en/sketchup-desktop-20261` adresindedir; `release-notes/` önekli adres 404 vermektedir.

---

## 1. Pencere yerleşimi

### 1.1 Genel düzen (Windows)

Resmi "Navigating the SketchUp Interface" sayfasına göre ana pencere şu bölgelerden oluşur:

| Bölge | Konum | İşlev |
|---|---|---|
| **Başlık çubuğu** | En üst | Standart pencere düğmeleri, açık modelin adı ve SketchUp sürümü. 2024'ten beri kaydedilmemiş değişiklik varsa ada `*` eklenir. |
| **Menü çubuğu** | Başlığın altı | File, Edit, View, Camera, Draw, Tools, Window, Extensions, Help. 2026.0'dan beri Windows'ta birçok menü öğesinin yanında araç simgesi görünür. |
| **Araç çubukları** | Menünün altında (yerleştirilebilir) | Yeni modelde varsayılan olarak **Getting Started** araç çubuğu açıktır. |
| **Çizim alanı (drawing area)** | Ortada | Modelin oluşturulduğu 3B alan. Çizim eksenleri (kırmızı/yeşil/mavi) yönelimi gösterir. İlk açılışta ölçek fikri vermek için basit bir insan figürü bulunabilir. |
| **Sahne sekmeleri** | Çizim alanının üstü | Her sahne bir sekme olarak görünür (View > Scene Tabs ile açılıp kapanır; menü yolu doğrulanamadı). İki veya daha fazla sahne varsa sekmelerin solunda **sahne arama** düğmesi bulunur (2022). |
| **Varsayılan tepsi (Default Tray)** | Sağ kenar | Önceden seçilmiş panelleri içeren, yerleştirilebilir panel kabı. |
| **Durum çubuğu** | En alt | Dört bölüm: coğrafi konum, Model Info/kredi, araç ipuçları (talimatlar), Measurements kutusu. |

### 1.2 Hoş geldiniz ekranı (Welcome Window) ve şablonlar

- Yeni model oluştururken **Home** bölümünden varsayılan şablon seçilir. Listede görünmeyen şablonlar için **More templates** bağlantısı tüm listeyi açar. Tüm şablonlar ayrıca **Preferences > Template** bölümünde listelenir; varsayılan şablon oradan değiştirilir.
- Varsayılan şablonlar mimari, inşaat, kentsel planlama, peyzaj gibi yaygın kullanımlara göre hazırlanmıştır. **Şablonların tam ad listesi ve birim varyantları** (örn. Feet/Inches, Millimeters, Meters) resmi sayfada sayılmamaktadır — *doğrulanamadı*.
- Hoş geldiniz ekranının diğer bölümleri (son dosyalar, öğrenme kaynakları, lisans/hesap bilgisi vb.) resmi sayfada ayrıntılandırılmamıştır — *doğrulanamadı*.
- Her şablon Model Info ayarlarını, stili, gölge ayarlarını ve varsa temel geometriyi belirler. Özel şablon: varsayılan şablondan yeni dosya aç → ayarla → **File > Save As Template** → konum ve ad ver.
- 2026.0: Preferences'ta **özel şablon klasörü** belirlenebilir.
- Hoş geldiniz ekranına Help menüsünden tekrar erişildiği genel bilgidir — *doğrulanamadı*.

### 1.3 Durum çubuğu ve Measurements kutusu

Durum çubuğu (soldan sağa):

1. **Geo-location (raptiye simgesi):** Üzerine gelinince konum verisi görünür (yalnızca model coğrafi konumlandırılmışsa); tıklanınca coğrafi konum seçenekleri açılır.
2. **Model Info / Credits:** Model Info penceresine kısayol. Üzerine gelinince seçili öğeyi kimin oluşturduğu (kredi) görünür.
3. **Araç ipuçları / talimatlar (orta alan):** Etkin aracın kullanım adımları ve değiştirici tuşlar (Ctrl/Shift/Alt/oklar) burada yazar. Araç değiştiğinde güncellenir.
4. **Measurements kutusu (sağ):** Çizim sırasında boyutları gösterir; kullanıcı buraya değer yazarak seçili öğeyi değiştirebilir veya eşit aralıklı kopyalar oluşturabilir. Ayrıca **Measurements** adlı bir araç çubuğu olarak da gösterilebilir.

Measurements kutusuna yazılabilen başlıca girdi biçimleri (Windows Hızlı Başvuru Kartı 2026):

| Araç | Girdi | Anlamı |
|---|---|---|
| Line | `sayı` Enter | Uzunluk |
| Rectangle | `uzunluk,genişlik` Enter (ör. 20,40) | Boyutlar |
| Rotated Rectangle | iki köşe tıklandıktan sonra `genişlik,açı` | Genişlik ve açı |
| Circle | `sayı` Enter / `sayı`S Enter | Yarıçap / segment sayısı |
| 2 Point Arc | `sayı` Enter / `sayı`R Enter / `sayı`S Enter | Bombe / yarıçap / segment |
| Push/Pull | `sayı` Enter | İtme/çekme mesafesi |
| Move | `sayı` Enter; ilk kopyadan sonra `sayı`X (dış dizi) veya `sayı`/ (iç dizi) | Mesafe ve dizi kopyalama |
| Rotate | `açı` Enter; `yükselme:koşu` (ör. 3:12) | Açı veya eğim olarak açı |
| Scale | `çarpan` (ör. 1.5) veya `uzunluk+birim` (ör. 10m) | Ölçek |
| Offset | `sayı` Enter | Öteleme mesafesi |
| Tape Measure | ölçtükten sonra yeni boyut + Enter | Tüm modeli yeniden ölçekleme |
| Look Around | `sayı` Enter | Göz yüksekliği |
| Field of View | değer | Görüş alanı |

### 1.4 Fare (Windows Hızlı Başvuru Kartı)

| Hareket | Sonuç |
|---|---|
| Orta tuş (tekerlek) kaydırma | Zoom |
| Orta tuş basılı sürükleme | Orbit |
| Shift + orta tuş sürükleme | Pan |
| Orta tuş çift tıklama | Görünümü tıklanan noktaya ortalama |

### 1.5 Araç çubukları

**Erişim:** View > **Toolbars…** penceresinden veya herhangi bir araç çubuğuna sağ tıklayarak listeden açılıp kapatılır. Toolbars penceresinin **Options** sekmesinde **büyük simgeler** ve **ekran ipuçları (screen tips)** seçenekleri bulunur. Preferences > Workspace'te büyük araç simgelerinden vazgeçilebilir.

**2026.1 davranış değişikliği:** Bir aracı başka araç çubuğuna sürüklemek onu eski yerinden kaldırmaz; bir düğmeyi kaldırmak için Toolbars menüsünde sağ tık gerekir. **Yeni araç çubuğu oluşturma** akışı resmi sayfada anlatılmamıştır — *doğrulanamadı* (eski sürümlerde Toolbars penceresinde "New…" ile özel araç çubuğu oluşturulabildiği bilinmektedir).

Resmi listede yer alan araç çubukları ve içerikleri:

| Araç çubuğu | Amaç (resmi) | İçerik (kaynak) |
|---|---|---|
| **Getting Started** | Varsayılan, öğrenmeye yönelik temel set | En solda **Search**. Ayrıca Flip (yardım sayfası) ve 2026'dan itibaren Collaboration/Comment. Tam düğme listesi *doğrulanamadı* (genel bilgi: Select, Make Component, Eraser, Tag, Line, Arc, Rectangle, Push/Pull, Offset, Move, Rotate, Scale, Tape Measure, Paint Bucket, Orbit, Pan, Zoom, Zoom Extents, 3D Warehouse, Extension Warehouse, Send to LayOut vb.). |
| **Large Tool Set** | Çok amaçlı büyük set | Hızlı Başvuru Kartı: Select, Lasso, Paint Bucket, Eraser, Make Component, Tag, Line, Freehand, Rectangle, Rotated Rectangle, Circle, Polygon, Arc, 2 Point Arc, 3 Point Arc, Pie, Move, Push/Pull, Rotate, Follow Me, Scale, Offset, Tape Measure, Dimensions, Protractor, Text, Axes, 3D Text, Comment, Flip, Orbit, Pan, Zoom, Zoom Window, Zoom Extents, Previous, Position Camera, Walk, Look Around, Field of View, Section Plane. |
| **Standard** | Dosya ve pano komutları | Düğme listesi *doğrulanamadı* (genel bilgi: New, Open, Save, Cut, Copy, Paste, Erase, Undo, Redo, Print, Model Info). |
| **Principal** | Çizim alanını büyük tutan temel set | *doğrulanamadı* (genel bilgi: Select, Make Component, Paint Bucket, Eraser). |
| **Drawing** | Çizim | *doğrulanamadı* (Line, Freehand, Rectangle/Rotated Rectangle, Circle, Polygon, yay araçları). |
| **Edit** | Var olan modeli düzenleme | *doğrulanamadı* (Move, Push/Pull, Rotate, Follow Me, Scale, Offset). |
| **Construction** | Hassas modelleme | *doğrulanamadı* (Tape Measure, Dimensions, Protractor, Text, Axes, 3D Text). |
| **Camera** | Kamera açıları | Kart: Orbit, Pan, Zoom, Zoom Window, Zoom Extents, Previous, Position Camera, Walk, Look Around (Field of View ayrı düğme olabilir — *doğrulanamadı*). |
| **Views** | Standart görünümler | Kurulu 2026 ayarına göre 7 düğme: Iso, Top, Front, Right, Left, Back, Bottom (aşağıdaki not). |
| **Styles** | Stil/yüz görünümü | Kart: X-Ray, Back Edges, Wireframe, Hidden Line, Shaded, Shaded with Textures, Monochrome, Photoreal Materials. |
| **Section** | Kesit düzlemleri | Section Plane (kart). Diğer düğmeler (Display Section Planes, Display Section Cuts, Display Section Fill) *doğrulanamadı*. |
| **Solid Tools** | Karmaşık şekiller (Boolean) | Kart: Outer Shell, Intersect (Pro), Union (Pro), Subtract (Pro), Trim (Pro), Split (Pro). |
| **Sandbox** | TIN arazi ve yumuşak şekiller | Kart: From Contours, From Scratch, Smoove, Stamp, Drape, Add Detail, Flip Edge. |
| **Warehouse** | 3D Warehouse ve eklentiler | Kart: 3D Warehouse, Share Model, Share Component, Extension Warehouse. |
| **Add Location / Location** | Coğrafi konum | Kart: Add Location, Toggle Terrain. |
| **Shadows** | Gölge ayarı ve çalışması | Gölge aç/kapa, tarih/saat kaydırıcıları, panel kısayolu *doğrulanamadı*. 2026.2: **Analysis Hub** simgesi (ilk açılışta görünmezse araç çubuğunu sıfırlamak gerekebilir). |
| **Tags** | Etiket görünürlüğü | Etkin etiket açılır listesi ve Tags paneli kısayolu *doğrulanamadı*. |
| **Measurements** | Hassas giriş | Measurements kutusunun araç çubuğu biçimi. |
| **Dynamic Components** | Dinamik bileşen geliştirme | Kart: Interact, Configurator Tool (Component Options), Component Attributes. |
| **Classifier** | BIM/IFC sınıflandırma | Classifier (Pro). |
| **Trimble Connect** | Bulut içerik | 2024'ten beri yerleşik; düğmeler *doğrulanamadı*. |
| **LayOut** (Send to LayOut) | LayOut'a gönderme | Send to LayOut (Pro). Araç çubuğu adı *doğrulanamadı*. |
| **Collaboration Bar** (2026) | Paylaşım/işbirliği | Paylaşım, Copy Link, Hide Cursor, kullanıcı avatarları (görünümü eşitleme/izleme), Comment. Window menüsünden açılır. |

**Kurulu SketchUp 2026'dan doğrulanan düğme sıraları (2026-10-20).** Kaynak: kullanıcının `%LOCALAPPDATA%\SketchUp\SketchUp 2026\SketchUp\PrivatePreferences.json` dosyasındaki `ToolbarSettings/ToolbarState` (onaltılık kodlu araç çubuğu durumu; düğme kimlikleri `Resources\en-US\commands.json` içindeki `skoreId` değerleriyle eşleşir). Bu, kullanıcının kayıtlı durumudur; fabrika varsayılanı olduğu doğrulanamadı. `|` = ayraç.
- **Getting Started:** Search | Select▾ | Eraser, Line▾, Arc▾, Rectangle▾ | Push/Pull, Offset, Move, Rotate, Scale, Flip | Tape Measure, Paint Bucket | Orbit, Pan, Zoom, Zoom Extents | 3D Warehouse, Extension Warehouse, (LayOut?) | Add Comment | (hesap?). Açılır menü içerikleri çıkarımdır: Select▾ = Select/Lasso; Line▾ = Line/Freehand; Arc▾ = Arc/2 Point/3 Point/Pie; Rectangle▾ = Rectangle/Rotated Rectangle/Circle/Polygon.
- **Large Tool Set (2 sütun):** Select, Lasso / Paint Bucket, Eraser / Make Component, Tag | Line, Freehand / Rectangle, Rotated Rectangle / Circle, Polygon / Arc, 2 Point Arc / 3 Point Arc, Pie | Move, Push/Pull / Rotate, Follow Me / Scale, Offset / Axes, Flip | Tape Measure, Dimensions / Protractor, Text / Section Plane, 3D Text | Orbit, Pan / Zoom, Zoom Window / Zoom Extents, Previous | Position Camera, Walk / Look Around, Field of View | 3D Warehouse, Extension Warehouse / (LayOut?), Add Comment.
- 2023.0 sürüm notu: "Large Toolset re-organized, adding the Flip tool and changing the location of the Section Plane tool" (https://download.sketchup.com/legacydocs/SketchUp%20Desktop%202023.0.pdf).
- Not: Kement'in çokgen (tıklamalı) modu 2023.1'de geldi.

### 1.6 Varsayılan tepsi ve panel yönetimi (Windows)

- Windows'ta paneller **tepsi (tray)** içinde durur; varsayılan tepsi sağ kenardadır ve önceden seçilmiş paneller içerir. Varsayılan tepsideki **panellerin tam listesi** resmi sayfada sayılmamaktadır — *doğrulanamadı* (yardım makaleleri Entity Info, Styles, Tags, Outliner, Soften Edges, Materials gibi panellerin Windows'ta varsayılan tepside bulunduğunu ayrı ayrı belirtir).
- Panelin başlık çubuğuna tıklamak paneli açar/daraltır; başlık sürüklenerek tepsi içindeki sıra değiştirilir.
- Tepsinin başlık çubuğu sürüklenince tepsi ayrı **yüzen pencereye** dönüşür (ekran dışına da taşınabilir); yüzen tepsinin başlığına **çift tıklamak** yeniden sabitler.
- Birden fazla tepsi varsa diğer tepsiler çizim alanının sağında **sekme** olarak görünür; daraltılmış tepsi de sekme olarak durur.
- **Window menüsü:** *Default Tray* (tepsiyi gizle, yeniden adlandır, içeriğini değiştir; alt menüde paneller), *New Tray* (yeni tepsi oluşturma penceresi), *Manage Trays* (tepsileri listele, oluştur, yeniden adlandır, sil).
- Tepsiyi **otomatik gizleme (auto-hide)** ve iğneleme topluluk mesajlarında geçer (forum); resmi Windows sayfasında anlatılmaz — *doğrulanamadı*.
- **2026.0 yenilikleri (Windows):** tepsi/panel başlığına sağ tıklayıp panel gösterme; panelleri tepsiler arasında veya dışarıya (yüzen tepsiye) sürükleme; yüzen panel için bağlam menüsünde **Make Tray**; **Reset Workspace** artık varsayılan yerleşime döner.
- Panel konumları ve açık/kapalı durumları `%LOCALAPPDATA%\SketchUp\SketchUp 2026\...\PrivatePreferences.json` dosyasında saklanır (forum) — resmi olarak *doğrulanamadı*.
- 2026.2: Migrate Extensions ile önceki sürümden geçişte Windows tepsi ve çalışma alanı özelleştirmeleri taşınır.
- macOS'ta paneller ayrı diyalog pencereleri olarak açılır (karşılaştırma için).

### 1.7 Search (komut arama)

- **Açma:** Getting Started araç çubuğunun en solundaki simge, **Help** menüsü veya **Shift+S**.
- Yazdıkça eşleşen sonuçlar listelenir; listeden komut doğrudan çalıştırılır.
- **Concept Search:** Kullanıcı amacını yazar (ör. "transparent", "elevation", "boolean", "chamfer"), ilgili araçlar önerilir.
- Kurulu eklentilerin komutları, eklenti adı veya komut adıyla bulunur (2022 notuna göre Pro sürümde).
- İlk olarak 2022.0'da masaüstüne geldi (önce web sürümündeydi).
- Panellerin aranıp aranamadığı resmi sayfada belirtilmemiştir — *doğrulanamadı*.

---

## 2. Menüler ve Windows kısayolları

> **Önemli uyarı:** help.sketchup.com'da her menünün tüm öğelerini listeleyen tek bir resmi sayfa bulunamadı. Aşağıdaki listeler; resmi yardım makalelerinde, sürüm notlarında ve Hızlı Başvuru Kartı'nda adı geçen öğeleri **(D)** ile işaretler. İşaretsiz satırlar, SketchUp'ın uzun süredir bilinen menü yapısına dayanan genel bilgidir ve **doğrulanamadı** kabul edilmelidir. Kısayol sütunundaki işaretsiz değerler de *doğrulanamadı* durumundadır (çoğu standart Windows kısayoludur). Menüde kısayolun öğe adının yanında gösterildiği forumda belirtilmiştir.

### 2.1 File

| Öğe | Kısayol | Not |
|---|---|---|
| New | Ctrl+N | |
| New from Template… | — | |
| Open… | Ctrl+O | |
| Save | Ctrl+S | (D) "Save" menüde. |
| Save As… | — | (D) "Saving a Model" sayfası Save As'i File menüsünde sayar. Bkz. §8 çelişki notu. |
| Save a Copy As… | — | |
| Save As Template… | — | (D) şablon sayfası. |
| Revert | — | |
| Trimble Connect ▸ (Open, Save, Save a Copy, Import, Reload, revizyonlar, görüntüleme bağlantısı paylaşma) | — | (D) 2024: Trimble Connect seçenekleri File menüsünde; 2025: Import/Reload. Alt öğe adları kısmen doğrulanamadı. |
| Send to LayOut | — | Pro. |
| Geo-location ▸ Add Location…, Clear Location, Show Terrain | — | Alt öğeler doğrulanamadı; Add Location (D). |
| 3D Warehouse ▸ Get Models…, Share Model…, Share Component… | — | Kart Share Model/Share Component'ı doğrular. |
| Import… | — | (D) 2025: HDRI/EXR ortamları da buradan; 2026.1: tek sayfalı PDF (Windows). |
| Export ▸ 3D Model…, 2D Graphic…, Section Slice…, Animation… | — | |
| Print Setup…, Print Preview…, Print… | Ctrl+P (Print) | |
| Generate Report… | — | Pro. |
| Migrate Preferences | — | (D) 2026.1. Önce Extensions > Migrate Extensions çalıştırılır. |
| Son açılan dosyalar listesi | — | |
| Exit | — | |

### 2.2 Edit

| Öğe | Kısayol | Not |
|---|---|---|
| Undo | Ctrl+Z (ek: Alt+Backspace) | 2024: işlem ortasında Undo yalnızca devam eden işlemi iptal eder (D). |
| Redo | Ctrl+Y | |
| Cut | Ctrl+X (ek: Shift+Delete) | |
| Copy | Ctrl+C (ek: Ctrl+Insert) | |
| Paste | Ctrl+V (ek: Shift+Insert) | |
| Paste In Place | — | |
| Delete | Delete | |
| Delete Guides | — | (forum) Edit menüsünde; varsayılan kısayolu yok. |
| Select All | **Ctrl+A** | (D) |
| Select None / Deselect All | **Ctrl+T** | (D) Resmi sayfa "Deselect All" adını kullanır. |
| Invert Selection | Ctrl+Shift+I | Kısayol doğrulanamadı; resmi seçim sayfası bunu bağlam menüsü öğesi olarak anlatır. 2026.1'de boş alana sağ tık menüsünde yerini Select All'a bıraktı (D). |
| Hide | — | (D) |
| Unhide ▸ Selected / Last / All | — | Alt öğeler iPad belgesinde doğrulandı; masaüstü için doğrulanamadı. |
| Lock / Unlock ▸ Selected / All | — | |
| Make Component… | **G** | (D) |
| Make Group | — | |
| Close Group / Component | — | |
| Intersect Faces ▸ With Model / With Selection / With Context | — | (D) bağlam menüsü sayfası üç kapsamı doğrular. |
| Flip | — | (D) Flip aracı Edit menüsünde. |
| Purge Unused | — (atanabilir) | (D) 2026.0'da Edit menüsüne eklendi; kısayol atanabilir. |
| Seçime özgü alt menü (ör. "Face", "Group", "N Entities") | — | Seçime göre bağlam menüsü öğelerini içerir — doğrulanamadı. |

2026.2 sürüm notu, Edit menüsündeki **yanlış gösterilen kısayolların düzeltildiğini** belirtir (hangileri olduğu belirtilmemiş).

### 2.3 View

| Öğe | Kısayol | Not |
|---|---|---|
| Toolbars… | — | (D) |
| Scene Tabs | — | |
| Hidden Geometry | — | (D) gizli geometriyi soluk gösterir. |
| Hidden Objects | — | (D) |
| Section Planes / Section Cuts / Section Fill | — | |
| Axes | — | |
| Guides | — | (D) 2022 notu "View Guides" anahtarına değinir. |
| Shadows | — | |
| Fog | — | Sis panelindeki "Display Fog" ile eşdeğer — doğrulanamadı. |
| Edge Style ▸ Edges, Back Edges, Profiles, Depth Cue, Extension | **K** (Back Edges) | (D) Back Edges = K. |
| Face Style ▸ X-ray, Wireframe, Hidden Line, Shaded, Shaded With Textures, Monochrome, Photoreal Materials, Ambient Occlusion | — | (D) AO'nun View > Face Style'da olduğu 2024 notunda; diğerleri Styles araç çubuğunda (D). Photoreal'in bu menüde yeri doğrulanamadı. |
| Component Edit ▸ Hide Rest of Model, Hide Similar Components | — | Adlar iPad Display panelinde ve 2022 notunda geçer; masaüstü menü yolu doğrulanamadı. |
| Animation ▸ Add Scene, Update Scene, Delete Scene, Previous Scene, Next Scene, Play, Settings | — | |

### 2.4 Camera

| Öğe | Kısayol | Not |
|---|---|---|
| Previous / Next | — | Previous kartta (D). |
| Standard Views ▸ Top, Bottom, Front, Back, Left, Right, Iso | — | (D) 2022'den beri özel model eksenlerine hizalanır. |
| Parallel Projection | — | (D) ortografik |
| Perspective | — | (D) varsayılan |
| Two-Point Perspective | — | (D) |
| Match New Photo… | — | (D) |
| Edit Matched Photo | — | (D) "Camera > Match Photo" olarak geçer. |
| Orbit | **O** | (D) |
| Pan | **H** | (D) |
| Zoom | **Z** | (D) Shift basılı sürükleme görüş alanını değiştirir. |
| Field of View | — | (D) |
| Zoom Window | — | (D) |
| Zoom Extents | **Shift+Z** | (D) |
| Zoom to Photo | — | (D) kısayol filtresi örneğinde geçer. |
| Position Camera | — | (D) |
| Walk | — | (D) |
| Look Around | — | (D) |
| "Image Igloo" | **I** | Resmi varsayılan kısayol tablosunda geçer; işlevi ve menü yeri **doğrulanamadı**. |

### 2.5 Draw

| Öğe | Kısayol | Not |
|---|---|---|
| Lines ▸ Line, Freehand | **L** (Line) | Line (D) |
| Arcs ▸ Arc, 2 Point Arc, 3 Point Arc, Pie | **A** (2 Point Arc) | (D) |
| Shapes ▸ Rectangle, Rotated Rectangle, Circle, Polygon | **R**, —, **C**, — | (D) |
| Sandbox ▸ From Contours, From Scratch | — | (D) |

### 2.6 Tools

| Öğe | Kısayol | Not |
|---|---|---|
| Select | **Space** | (D) Tools menüsünde. |
| Lasso | **Shift+Space** | (D) Tools menüsünde. |
| Eraser | **E** | (D) |
| Tag | — | (D) Tag aracı Tools menüsünde. |
| Paint Bucket | **B** | (D) |
| Move | **M** | (D) |
| Rotate | **Q** | (D) |
| Scale | **S** | (D) |
| Push/Pull | **P** | (D) |
| Follow Me | — | (D) |
| Offset | **F** | (D) |
| Outer Shell | — | (D) |
| Solid Tools ▸ Intersect, Union, Subtract, Trim, Split | — | (D) Pro |
| Tape Measure | **T** | (D) |
| Protractor | — | (D) |
| Axes | — | (D) |
| Dimensions | — | (D) |
| Text / 3D Text | — | (D) |
| Section Plane | — | (D) |
| Comment | — | (D) 2026.0 aracı; menü yeri doğrulanamadı. |
| Interact | — | (D) Dinamik bileşen aracı; menü yeri doğrulanamadı. |
| Sandbox ▸ Smoove, Stamp, Drape, Add Detail, Flip Edge | — | (forum, moderatör yanıtı) |

### 2.7 Window

| Öğe | Not |
|---|---|
| Model Info | (D) |
| Preferences | (D) Windows'ta Window menüsünde (macOS'ta SketchUp > Settings). |
| Default Tray ▸ (panel listesi) | (D) Entity Info, Materials, Components, Styles, Tags, Scenes, Shadows, Fog, Match Photo, Soften Edges, Overlays, Instructor, Outliner, Environments (2025+) vb. |
| New Tray… | (D) |
| Manage Trays… | (D) |
| Collaboration Bar | (D) 2026.0 |
| Component Options / Component Attributes | Dinamik bileşen pencereleri — menü yeri doğrulanamadı. |
| Diğer (Photo Textures, Ruby Console vb.) | doğrulanamadı |

### 2.8 Extensions

| Öğe | Not |
|---|---|
| Extension Warehouse | (D) |
| Extension Manager | (D) etkinleştirme/devre dışı bırakma, kurma/kaldırma, imza durumu, yükleme politikası; 2026.2'de Qt tabanlı yeni arayüz. |
| Developer ▸ Ruby Console | doğrulanamadı |
| Migrate Extensions | (D) 2025'te Labs eklentisi olarak; 2026.2'de yeniden başlatma gerektirmiyor. |
| AI Assistant | (D) 2026.1 |
| Analysis Hub | (D) 2026.2 |
| Kurulu eklentilerin kendi menüleri | genel bilgi |

### 2.9 Help

| Öğe | Kısayol | Not |
|---|---|---|
| Search | **Shift+S** | (D) |
| Welcome to SketchUp… | — | doğrulanamadı |
| Knowledge Center / Help Center, Contact Us, License / Account, Check for Update, About SketchUp | — | doğrulanamadı (sürüm/derleme numarası About'ta görülür — 2026.2 notu Help > About'a yönlendirir) |

---

## 3. Tepsi (tray) panelleri

Resmi listede (Dialog Boxes and Trays) şu paneller geçer: **Entity Info, Materials, Components, Styles, Tags, Scenes, Shadows, Fog, Match Photo, Soften Edges, Overlays, Instructor, Outliner.** Buna 2025'te **Environments**, 2026'da **Comments** eklenmiştir.

### 3.1 Entity Info

- **Açma (Windows):** Varsayılan tepside "Entity Info" başlığının yanındaki oka tıklanır; tepsi gizliyse Window > Default Tray > Entity Info. Bağlam menüsünde de *Entity Info* bulunur.
- Seçili öğenin türünü (kenar, yüz, grup, bileşen) ve türe uygun ölçüleri gösterir; çoklu seçimde yalnızca seçili öğe sayısı görünür.
- Simgeler: **görünürlük (gizle), kilit, gölge alma, gölge düşürme** anahtarları.
- Bileşenlerde örnek (instance) bilgisi, katı olup olmadığı ve IFC türü gibi ek öznitelikler bulunabilir. 2025'ten beri katı nesneler için **toplam hacim** gösterilir.
- Alan-alan ayrıntılar (yüz için ön/arka malzeme ve alan; kenar için uzunluk, yumuşatma; grup/bileşen için örnek adı, tanım adı, etiket) resmi masaüstü sayfasında metin olarak verilmemiştir — *doğrulanamadı* (iPad belgesi tür, ölçü, malzeme, etiket ve gölge anahtarlarını doğrular).

### 3.2 Materials

- Windows'ta varsayılan tepside; Paint Bucket etkinleştirildiğinde de açılır.
- **Üst bölüm:** etkin malzemenin önizlemesi ve adı; **Copy** (benzersiz kopya); **Generate Textures** (2025, yapay zekâ ile PBR doku üretimi; yükseltilen malzemelerde altın parıltı simgesi; internet gerekir).
- **Select sekmesi:** modeldeki malzemeler, açılır listeden varsayılan kütüphaneler, 3D Warehouse'tan malzeme. Düğmeler: **Create Material**, **3D Warehouse**, **In Model** (yalnızca modeldekiler), **Sample Paint** (yüzden malzeme örnekleme), **ikincil seçim bölmesi** (iki koleksiyonla aynı anda çalışma), **Details** menüsü (koleksiyon yönetimi ve görünüm).
- **Edit sekmesi:** renk, **Opacity** (0 tam saydam – 100 opak), doku görüntüsü ve boyutu (zincir simgesi en-boy oranını kilitler/çözer), harici görüntü düzenleyicide düzenleme (Preferences > Applications'ta tanımlı), PBR seçenekleri: **Metalness, Roughness (Invert Roughness – 2026.0), Normal, Ambient Occlusion** haritası. Photoreal seçenekleri yalnızca PBR haritası olan malzemelerde ve etkin stil Photoreal Materials yüz stilini kullanıyorsa etkilidir.
- Renk seçici kipleri (Color Wheel/HLS/HSB/RGB) ve **Colorize** seçeneği resmi aramalarda bulunamadı — *doğrulanamadı*.
- 2025: tüm varsayılan malzemeler Photoreal; USD/GLB'de PBR içe/dışa aktarım. 2026.0 (Windows): önizlemeye sağ tıklayarak küçük resim tipi **Auto / Cube / Flat**.
- Paint Bucket değiştiricileri için bkz. §6.2.

### 3.3 Components

- **Select sekmesi:** hazır koleksiyonlar (Architecture, Landscape, People, Transportation vb.) açılır listesi, arama kutusu, **In Model** koleksiyonu (modelde kayıtlı tüm bileşenler), coğrafi konumlu modellerde **Nearby Models**, Details menüsünden yerel koleksiyon açma/oluşturma.
- **2026.2 (Windows):** arama kutusu artık modeldeki bileşenleri süzer; 3D Warehouse araması ayrı **3D Warehouse** düğmesiyle yapılır.
- **Edit** ve **Statistics** sekmeleri (bileşen hizalama/yapıştırma seçenekleri ve geometri sayıları) yalnızca eski/üçüncü taraf kaynaklarda geçer — *doğrulanamadı*.
- Bileşen oluştururken (Create Component diyaloğu) **Glue To**: Any / Horizontal / Vertical / Sloped; **Set Component Axes** ile yerleştirme ekseni.

### 3.4 Styles

Ayrıntı için bkz. §5.1. Panel **Select / Edit / Mix** sekmelerinden oluşur; **Create New Style** ve **Update Style with Changes** düğmeleri vardır. 2026.1'de Windows Styles paneli arayüzü güncellenmiştir.

### 3.5 Tags

- Windows'ta varsayılan tepside. Tags, eski sürümlerdeki **Layers**'ın yerini almıştır.
- **Satır öğeleri:** görünürlük göz simgesi, ad (çift tıkla yeniden adlandır), renk örneği, **çizgi deseni (dashes)**. Ad veya Visible başlığına tıklayarak sıralama.
- **Üst çubuk:** **Add Tag (+)** (2024'te Windows'ta büyütüldü), **Add Tag Folder** (seçili etiketleri yeni klasöre taşır; klasör görünürlüğü tüm alt etiketleri etkiler), **Filter** kutusu, **Tag aracı** düğmesi, **Color by Tag** düğmesi.
- **Details menüsü:** Expand All, Collapse All, Select All, Delete Tag / Delete Tag Folder, **Purge** (kullanılmayan etiketleri sil), **Select All Tagged**.
- **Untagged:** ham geometrinin varsayılan etiketi; kenar/yüzlere etiket vermemesi önerilir.
- Ctrl ile çoklu seçim; Shift ile aralık seçimi.
- 2025: sağ tıkta **Apply Tags to Scenes** (seçili etiket görünürlüğünü seçili sahnelere uygular).
- 2026.1: Model Inspector (Windows) içindeki Tags bölümü yeniden boyutlandırılabilir.
- **Tag aracı:** Tek bir etiket seçiliyken varlığa tıklanır; varlık mor renkte yanıp söner. Değiştiriciler: **Alt = örnekle (sample)**, **Shift = tümünü değiştir**, **Ctrl = tüm örnekleri etiketle**. Birden fazla etiket veya klasör seçiliyse uygulamaz. Konumları: Tags paneli, Large Tool Set, Tools menüsü.

### 3.6 Scenes

- Her sahne çizim alanının üstünde bir sekme olarak görünür.
- **Düğmeler:** Add Scene, Show Details, Update Scene, Remove Scene, Move Scene Up/Down; sahne sekmelerinin solunda **Search** (sahne arama, 2022).
- **Ayrıntılar bölümü:** ad, açıklama, **Include in Animation** ve kaydedilecek özellikler: Camera Location, Hidden Geometry / Hidden Objects, Visible Tags, Active Section Planes, Style and Fog, Shadow Settings, **Environment Settings** (güneş konumu ve gökyüzü kubbesi), Axes Location, **Point Clouds** (yalnızca Scan Essentials ile; 2026.2 Windows).
- **Sahne sekmesi sağ tık:** Use Scene Thumbnail, Update Scene Thumbnail, Move Left/Right (panelde de küçük resim seçenekleri).
- Details menüsünde **Add Scene with Matched Photo**.
- 2026.0: sahne oluşturma/yeniden adlandırma/silme/güncelleme **Undo/Redo** ile geri alınabilir. 2024: Windows'ta Tab ile ad ve açıklama alanları arasında geçiş.
- Animasyon zamanlaması Model Info > Animation'da ayarlanır.

### 3.7 Shadows

Ayrıntı için bkz. §5.4.

### 3.8 Fog

Ayrıntı için bkz. §5.5.

### 3.9 Match Photo

- **Açma:** Camera > **Match New Photo** → görüntü seç → kamera Match Photo görünümüne geçer, görünüm bir sahne sekmesi olarak kaydedilir ve panel açılır. İçe aktarılan görüntüden *New Match Photo* ile de başlatılabilir.
- **Panel denetimleri:** stil (ızgara tipi), düzlemler ve ızgara aralığı (adlar resmi sayfada açıklanmıyor — Inside/Above/Outside seçenekleri *doğrulanamadı*); **Model** onay kutusu (modeli gizler); **Project Textures From Photo**; **Done**.
- **Çizim alanı tutamaçları:** eksen orijini (fotoğrafta belirgin noktaya sürüklenir), iki kırmızı ve iki yeşil **kaçış noktası çubuğu** (uçlarında tutamaç), otomatik oturan ufuk çizgisi, mavi eksen boyunca sürükleyerek ölçekleme.

### 3.10 Soften Edges

- **Açma (Windows):** Window > Default Tray > Soften Edges; ya da kenarları seçip sağ tık > **Soften/Smooth Edges**.
- **Angle between normals** kaydırıcısı (yumuşatılacak en büyük açı; değer arttıkça daha çok kenar etkilenir), **Smooth normals** (varsayılan açık; gölgelemeyi yumuşatır), **Soften coplanar** (eş düzlemli yüzler arasındaki kenarları yumuşatır).
- Eraser ile: Ctrl = yumuşat/düzgünleştir, Alt = geri al (yumuşatmayı kaldır), Shift = gizle.

### 3.11 Instructor

- Seçili araç veya özellik hakkında kısa kullanım rehberi gösterir (SketchUp for Schools belgesi; masaüstünde panel listesinde resmi olarak yer alır).
- Açma: **Window > Default Tray > Instructor** (forum). Ayrı bir "Help" tepsisine konup yüzdürülebileceği/otomatik gizlenebileceği önerilmiştir (forum).
- İçeriğin animasyonlu adımlar içerdiği yalnızca eski üçüncü taraf kaynakta geçer — *doğrulanamadı*.

### 3.12 Outliner

- **Açma:** Window > Default Tray > Outliner.
- Grupları, bileşenleri ve kesit düzlemlerini **hiyerarşik ağaç** olarak gösterir; iç içe öğelerin yanında ok bulunur.
- Üstte **filtre (arama) kutusu**.
- **Details menüsü:** Expand All, **Sort By Name** (kapalıyken oluşturma/ekleme sırası). Collapse All vb. *doğrulanamadı*.
- **Durum simgeleri:** Grup, Bileşen ve Kesit düzlemi için ayrı Kapalı/Açık/Kilitli simgeleri; tüm türler için ortak Gizli/Görünür simgesi; yalnızca kesit düzlemleri için Inactive.
- **Görünürlük simgesi** ile öğe ve içindekiler gizlenir/gösterilir; gizli öğe seçilince modelde taralı görünür.
- Sürükle-bırak ile hiyerarşi yeniden düzenlenir; Outliner'da seçim modelde de seçim yapar.
- Sağ tık/üçlü tık: **Rename**; kesit düzlemi için **Rename Section Plane / Rename Symbol**. Bileşen adı tanımdan gelir; değiştirmek tüm örnekleri etkiler.

### 3.13 Overlays

- 2023.0 ile geldi. Eklentilerin, kendi araçları etkin değilken de görünüm alanına bilgi çizmesine izin verir (resmi personel duyurusu, forum). Örnekler: kompozisyon kılavuzları, hata vurgulayan katı denetleyici, ekran üzerine not alma.
- Overlays panelindeki her kaplamanın bir **etkinleştirme onay kutusu** vardır; kaplamalar varsayılan olarak kapalıdır (geliştirici forumu; Ruby API `Overlay#enabled=` ile eşdeğer).
- Kaplama çıktısı seçilemez ve dışa aktarılamaz (Ruby API belgesi, ikinci el aktarım).
- 2024: C API'ye de kaplama desteği eklendi.
- Panelin ayrıntılı arayüzünü anlatan resmi help.sketchup.com sayfası bulunamadı — *doğrulanamadı*.

### 3.14 Environments (2025+)

Ayrıntı için bkz. §5.6. Panel Window menüsünde ve varsayılan tepside yer alır (forum). 2026.0'da Windows için Environment panelini gösterme kısayolu eklendi.

### 3.15 Comments ve Collaboration Bar (2026.0+)

- **Collaboration Bar** (Window > Collaboration Bar; ayrıca varsayılan araç çubukları ve Large Tool Set): modeli e-postayla özel davetle veya herkese açık bağlantıyla paylaşma; **Copy Link**, **Hide Cursor**; tıklanabilir kullanıcı avatarları (görünümü eşitleme veya izleme).
- **Erişim düzeyleri:** davetliler için *View Scenes* veya *Comment*; genel bağlantı için *No Access / View Scenes / View Model*.
- **Comment aracı:** 3B yorum işaretleyici yerleştirir; **Comments paneli** konu dizileri, yanıtlar ve işaretleyici görünürlüğünü yönetir. Yorumlara görüntü, bağlantı, kamera konumu eklenebilir ve sahneye bağlanabilir. İzleyiciler değişiklikleri, araç kullanımını, imleçleri gerçek zamanlı görür.

### 3.16 Diğer yeni yüzeyler (2024–2026)

| Yüzey | Sürüm | Not |
|---|---|---|
| Yeni grafik motoru | 2024.0 | Preferences > Graphics'ten klasik motora dönülebilir. |
| Ambient Occlusion yüz stili | 2024.0 (2026.0'da Distance Multiplier ve renk) | View > Face Style, Styles paneli, Search. |
| Add Location yenilemesi | 2024.0 | Konum raptiyesi, Gerçek Kuzey oku, saydam model önizleme, ağ yoğunluğu kaydırıcısı, etiket yönetimi. |
| Extension Error diyaloğu | 2024.0 | Başlangıçta yüklenemeyen eklenti için ayrıntı/güncelle/devre dışı bırak/kaldır. |
| Photoreal Materials yüz stili | 2025.0 | |
| Purge hatırlatması | 2025.0 (2026.0'da varsayılan kapalı) | Preferences > General'dan kapatılabilir. |
| AI Assistant / AI Render | 2026.1 | Extensions > AI Assistant veya "SketchUp AI" simgesi; ücretli planlarda aylık AI kredisi. |
| Analysis Hub | 2026.2 | Extensions menüsü veya Shadows panelindeki düğme; Advanced Shadows (gündönümü/ekinoks), Analysis Viewer, Labs analizleri (Annual Illuminance, Daylight Factor, sDA/ASE, Direct Sun, Date & Time). |

---

## 4. Bağlam menüleri, Tercihler (Preferences), Model Bilgisi (Model Info)

### 4.1 Bağlam (sağ tık) menüleri

Resmi "Context menu items: Entities" sayfasına göre öğeler varlık türüne göre değişir.

**Hemen her varlıkta ortak:** Entity Info, Erase, Make Component / Make Group (ham geometri seçiliyken; 2025'ten beri tek nesnede de görünür), Hide/Unhide, **Save Out** (cihaz / Trimble Connect / 3D Warehouse), **Reload**, **Zoom Selection**.

| Varlık | Bağlam menüsü öğeleri |
|---|---|
| **Yüz (Face)** | Align View, Align Axes, Area (seçim/etiket/malzeme için alan), Combine Textures, Intersect Faces (With Model / With Selected / With Context), Make Unique Texture, **Modify Face** (canlı bileşen efekti: Chamfer, Fillet, Inflate, Twist, Wave — sürümü doğrulanamadı), Orient Faces, Reverse Faces. |
| **Kenar (Line)** | Divide, Select ▸ (Bounding Edges, Connected Faces, All Connected, All with Same Tag, All with Same Material, Invert Selection, Deselect Edges/Faces; 2025: Connected by Snaps; 2022: All with Tag, All Instances), Soften. |
| **Eğri / Daire** | Convert to Polygon, Divide, Explode Curve, Find Center, Select ▸, Soften. |
| **3D Polyline / Polygon** | Divide, Explode, Explode Curve, Find Center, Select ▸, Soften. |
| **Grup** | Edit Group / Close Group, Explode, Intersect Faces, Lock/Unlock, Make Component, Reset Scale, Reset Skew, Soften/Smooth Edges, Unglue. |
| **Bileşen** | Change Axes, Dynamic Components ▸ (Component Options, Component Attributes, Swap Component, Redraw), Edit Component, Explode, Intersect Faces, Lock/Unlock, Make Unique, Reload, Reset Scale, Reset Skew, Save As, Scale Definition, Unglue, 3D Warehouse ▸ (yükle/yenile/ayrıntılar). |
| **Boyut (Dimension)** | Edit Text, Invert Selection, Text Position (Start / Center / End). |
| **Kılavuz metin (Leader)** | Arrow (None / Dot / Closed Arrow / Open Arrow), Edit Text, Leader (View Based / Pushpin / Hidden). |
| **Görüntü (Image)** | Export, Explode, Invert Selection, Reload, Shadows (gölge düşürme/alma), Unglue, Use As Material. |
| **Kesit düzlemi** | Active Cut, Align View, Create Group from Slice, Reverse. |
| **Boş alan** | 2026.1'den beri **Select All** (önceden Invert Selection). |
| **Snap** | 2024: Reorient Snap, Reverse Snap. |
| **Sahne sekmesi** | Use/Update Scene Thumbnail, Move Left/Right. |
| **Tags/Scenes paneli** | Apply Tags to Scenes (2025); Tags ağacında Select All Tagged. |
| **Tepsi/panel başlığı (Windows, 2026)** | Panel gösterme, yüzen panelde Make Tray. |

Flip: 2023 öncesi bağlam menüsündeki dokuz "Flip Along" komutu 2023'te kaldırılıp yerine üç genel komut (Flip Red/Green/Blue) ve Flip aracı geldi (forum) — resmi sürüm notuyla *doğrulanamadı*.

### 4.2 Preferences (Window > Preferences)

| Bölüm | İçerik |
|---|---|
| **Accessibility** | Çizim eksenleri ve çıkarım (inference) renklerini özelleştirme. |
| **Applications** | Doku düzenlemek için harici görüntü düzenleyici seçme (Choose). |
| **Compatibility** | Varsayılan kapalı. *Component/Group Highlighting > Bounding Box Only*; *Mouse Wheel Style* (tekerlek yönünü ters çevirme). |
| **Drawing** | **Click Style:** Click-drag-release / **Auto Detect (varsayılan)** / Click-move-click; **Continue line drawing** (yalnızca Auto Detect ve Click-move-click'te; varsayılan açık). **Lasso Direction** (saat yönü/ters yön davranışını tersine çevirme). **Miscellaneous:** Display Crosshairs (eksen renginde artı imleç), Disable Pre-Pick on Push/Pull Tool, Show Move Tool Rotation Grips (2024; kısayol atanabilir). |
| **Files** | SketchUp ile ilgili dosyaların varsayılan konumları. 2026.0: özel şablon klasörü (bu bölümde olup olmadığı doğrulanamadı). |
| **General** | Otomatik kaydetme (varsayılan **5 dakika**), yedek oluşturma, açılır uyarılar, Purge hatırlatması (2025). Diğer seçenekler (model sorun denetimi, güncelleme denetimi, başlangıçta Welcome penceresi) *doğrulanamadı*. |
| **Graphics** | **Multisample Anti-Aliasing** 0x/2x/4x/8x; **Use Fast Feedback**; **Use Maximum Texture Size**; **Capabilities:** Graphics Engine Details (motor adı, API, cihaz, sürücü), Show Log File; **Graphics Engine:** yeni motor veya Classic (uyumsuz cihazda Classic varsayılan); **GPU Selection** (birden çok GPU varsa); **Automatically Enhance Materials** (PBR'siz malzemelere Metalness/Roughness atama, kaydırıcılı); 2026.2: **hızlı gölge sistemi** anahtarı (şimdilik Windows). |
| **Shortcuts** | Tüm komutların tablosu; arama/filtre kutusu (ör. "zoom" → Camera/Zoom, Zoom Extents, Zoom to Photo, Zoom Window); **Assigned** sütunu (Windows); **Add Shortcut** kutusu; ekle/kaldır; **Import/Export** (yalnızca Windows; tercih dosyası olarak). Kurallar: kısayol rakamla başlayamaz; Shift/Alt/Ctrl birleştirilebilir; Windows'un ayırdığı kombinasyonlar engellenir; varsayılanlar yeniden atanabilir. "Reset All" düğmesi ve dosya uzantısı *doğrulanamadı*. Bazı komutlar listede yalnızca ilgili seçim varken görünebilir (forum). |
| **Template** | Varsayılan şablon seçimi. |
| **Workspace** | Büyük araç simgelerinden vazgeçme; **Reset Workspace** (2026.0'dan beri varsayılan yerleşime döner). 2026.0: Environment panelini gösterme kısayolu (Windows; bu bölümde olduğu doğrulanamadı). |

**Extensions sekmesi:** Güncel Preferences belgesinde yer almaz; eklenti yönetimi **Extensions > Extension Manager**'dadır. Manuel `.rbz` kurulumu için belgeler Preferences'a atıf yapsa da güncel akış Extension Manager üzerindendir — ayrıntı *doğrulanamadı*.

### 4.3 Model Info (Window > Model Info veya durum çubuğundaki simge)

Model genelinde geçerli, şablona bağlı ayarlar:

| Sekme | İçerik |
|---|---|
| **Animation** | Sahne geçiş süresi ve sahneler arası bekleme. |
| **Classifications** | BIM sınıflandırma sistemlerini içe aktarma. |
| **Components** | Grup/bileşen içinde düzenlerken modelin geri kalanının görünümü (soluklaştırma/gizleme) ve bileşen eksenlerinin görünürlüğü. |
| **Credits** | Modeli oluşturanlar; **Claim Credit** ile lisans sahibinin adını ekleme. |
| **Dimensions** | Varsayılan boyut metni; **Expert Dimension Settings** düğmesi (2022 notunda geçer). |
| **File** | Dosyanın temel bilgileri. |
| **Geo-location** | Modelin coğrafi konum durumu. |
| **Rendering** | Kenar yumuşatmalı (anti-aliased) dokular; performansı etkileyebilir. |
| **Statistics** | Varlık sayıları; kullanılmayanları temizleme (Purge Unused) ve sorunları düzeltme (Fix Problems). |
| **Text** | Ekran metni ve kılavuz (leader) metni varsayılanları. |
| **Units** | Varsayılan ölçü birimi. Biçim seçenekleri (Architectural, Decimal, Engineering, Fractional), hassasiyet, uzunluk/açı yakalama ayrıntıları masaüstü sayfasında verilmemiştir — *doğrulanamadı* (iPad belgesi uzunluk/alan/hacim/açı birimleri, hassasiyet ve uzunluk yakalamayı doğrular). |

---

## 5. Stil sistemi, gölgeler, sis, ortamlar

### 5.1 Styles paneli

- **Stil** = kenar, yüz, arka plan (ve filigran, modelleme) ayarlarının bir bütünü. Stiller ve stil değişiklikleri modelle birlikte kaydedilir (**In Model** stilleri).
- **Select sekmesi:** açılır listeden stil kategorisi; küçük resme gelince ad görünür, tıklayınca uygulanır. **Create New Style** düğmesi bu sekmededir. Hazır kategoriler: **Ambient Occlusion** (2024), **Assorted Styles**, **Color Sets**, **Default Styles**, **Photo Modeling**, **Sketchy Edges**, **Straight Lines**, **Style Builder Competition Winners**.
- **Edit sekmesi** beş bölümden oluşur: **Edge Settings, Face Settings, Background Settings, Watermark Settings, Modeling Settings**.
- **Update Style with Changes:** düzenlemeleri seçili stile işler.
- **Mix sekmesi:** başka stillerin ayarlarını (kenar/yüz/arka plan/filigran/modelleme) sürükleyip birleştirme — resmi güncel sayfada anlatılmıyor, *doğrulanamadı*.
- **Style Builder** (el çizimi kenar stilleri üreten ayrı uygulama) 2025.0'da kaldırıldı; mevcut "Sketchy Edges" stilleri kullanılmaya devam eder.
- 2026.1: Windows Styles paneli arayüzü güncellendi.

### 5.2 Kenar ayarları (Edge Settings)

| Seçenek | Davranış |
|---|---|
| **Edges** | Kenarları göster/gizle. |
| **Back Edges** | Arkada kalan kenarları kesikli çizgiyle gösterir; açılınca X-Ray devre dışı kalır. Kısayol **K**. |
| **Profiles** | Ana şekillerin dış hatlarını kalınlaştırır; kalınlık piksel cinsinden. |
| **Depth Cue** | Ön plandaki çizgileri arkadakilere göre kalın gösterir; ön plan kalınlığı piksel. |
| **Extension** | Çizgileri uç noktalarının biraz ötesine uzatır (el çizimi görünümü); uzunluk piksel; çıkarımı (inference) etkilemez. |
| **Endpoints** | Çizgi uçlarında ek kalınlık; vurgulanan uzunluk piksel. |
| **Jitter** | Her çizgiyi hafif kaydırarak birkaç kez çizer (eskiz görünümü); çıkarımı etkilemez. |
| **Color: All Same** | Tüm kenarlar tek renk (renk örneği); kenarlara atanmış renkler korunur. |
| **Color: By Material** | Kenarlar atanmış malzeme rengiyle. |
| **Color: By Axis** | Eksene paralel kenarlar eksen rengiyle; diğerleri All Same rengiyle; gizli kenarlarda eksen rengi görünmez. |

**Sketchy (el çizimi) kenarlar:** Güncel kenar ayarları sayfasında ayrı bir seçenek olarak geçmez; "Sketchy Edges" hazır stil kategorisi olarak sunulur. Fırça darbesi (stroke) tabanlı kenar seçimi ve **Halo / Level of Detail** gibi ek ayarlar resmi sayfada bulunamadı — *doğrulanamadı*.

### 5.3 Yüz ayarları (Face Settings)

| Seçenek | Davranış |
|---|---|
| **Front / Back Color** | Tüm yüzlerin ön ve arka varsayılan rengi; atanan malzemeler bunu geçersiz kılar. |
| **Wireframe** | Yalnızca çizgiler; yüzler görünmez, Push/Pull gibi yüz araçları çalışmaz. |
| **Hidden Line** | Gölgelemesiz, dokusuz yüzler; siyah-beyaz sunum için. |
| **Shaded** | Bir ışık kaynağını kabaca taklit eden temel gölgeleme (malzeme renkleri). |
| **Shaded Using Textures** | Temel gölgeleme + dokular. |
| **Shaded Using All Same (Monochrome)** | Her iki yüz tarafı da seçili ön yüz rengiyle. Araç çubuğunda/kartta "Monochrome" adıyla geçer. |
| **Photoreal Materials** (2025) | Malzemelerin PBR haritalarını kullanır; tam etki için bir Environment seçili olmalı. |
| **X-Ray** | Tüm yüzleri saydam yapar (gizli kenar/nokta seçimi ve yakalama kolaylaşır); bu kipte yüzler gölge düşürmez; diğer stillerle birlikte açılabilir. |
| **Material Transparency** | Malzeme saydamlığını genel olarak aç/kapa. |
| **Transparency Quality** | Faster (hızlı, düşük kalite) / Nicer (güzel, yavaş). |
| **Ambient Occlusion** (2024) | Ayrıca etkinleştirilir. **Color Controls** (model rengi veya özel renk — 2026.0), **Distance** (etkinin kenarlardan uzaklığı), **Intensity** (gölgelemenin şiddeti), **Distance Multiplier** (2026.0; uzaklaşınca etkinin görünür kaldığı mesafe). |

### 5.4 Gölgeler (Shadows paneli)

- **Önkoşul:** Model coğrafi konumlandırılmalıdır. Hesap; enlem/boylam, kardinal yön (çizim eksenleriyle belirlenir) ve seçili saat dilimine dayanır.
- **Denetimler:** Show/Hide Shadows; **Time** ve **Date** kaydırıcıları ve açılır listeler; **Light** ve **Dark** kaydırıcıları (yoğunluk); **Use Sun for Shading** (gölgeler kapalıyken de güneş gölgelemesi); **Display: On Faces** (kapatmak performansı artırabilir) ve **On Ground**.
- **UTC/saat dilimi seçimi** ve **From Edges** seçeneği güncel resmi sayfada açıkça geçmez — *doğrulanamadı*.
- **Environments ile ilişki:** Environment'taki *Set Sun Location* gölge ayarlarındaki konum/saat değerlerini geçersiz kılar.
- **2026.2:** hızlı gölge sistemi (Preferences > Graphics'ten kapatılabilir; bilinen kısıtlar: kenar gölgeleri eksik, kesit düzlemi ve X-Ray ile sorunlar); panelde **Analysis Hub** düğmesi.
- Sahneler gölge ayarlarını (saat ve tarih dahil) kaydedebilir.

### 5.5 Sis (Fog paneli)

- **Display Fog** ile açılır.
- **Distance kaydırıcıları** mevcut görünüme göredir: sol kaydırıcı sisin başladığı yeri (sıfırda kameranın hemen önü), sağ kaydırıcı sisin %100 (sıfır görünürlük) olduğu yeri belirler; sağ kaydırıcı tamamen sola çekilirse hiçbir şey görünmez. Yörüngede gezinip yakınlaştırdıkça kaydırıcılar kayar.
- **Use Background Color** ve renk seçimi: Resmi sayfa, bu seçeneği seçip renk belirlemeyi ve boş bırakılırsa varsayılan gri kullanılacağını söyler; onay kutusu ile renk örneğinin tam etkileşimi açık değildir — *doğrulanamadı*.
- Sahne ayrıntılarındaki **Style and Fog** özelliği sis ayarlarını sahneye kaydeder.

### 5.6 Ortamlar (Environments paneli, 2025+)

- 360° panoramik arka planlar ve görüntü tabanlı aydınlatma (HDRI veya OpenEXR).
- **Select sekmesi:** varsayılan kütüphane (dış mekân ışığı ve tepe ışığı kısılmış iç mekân seçenekleri); küçük resme tıklayınca ortam modele eklenir ve **In-Model** kategorisinde görünür; ekli ortamları inceleme, devre dışı bırakma, silme, özel koleksiyonlar, küçük resim boyutu/liste görünümü, yenileme. **Add Environment** simgesiyle cihazdan HDRI/EXR ekleme (File > Import de olur). Ek içerik: 3D Warehouse ve SketchUp Content Library.
- **Edit sekmesi:** **Set Sun Location** (gölgeleri açar; küçük resimde sürüklenebilir güneş simgesi ve kaydırıcılar; Shadows panelindeki konum/saati geçersiz kılar), **Use Environment as Skydome** (360° arka plan; kapatılınca ışık etkisi sürer), **Use Environment for Reflections**, **Rotation Angle**, **Skydome-Exposure**, **Reflections-Exposure**.
- Ortamın etkili olması için etkin stilin **Photoreal Materials** yüz stilini kullanması gerekir.
- Sahneler **Environment Settings** (güneş konumu, gökyüzü kubbesi) kaydedebilir.

### 5.7 Arka plan ve filigran

- **Background Settings:** Background rengi; **Sky** (onay kutusu + renk); **Horizon** rengi (gökyüzüyle harmanlanır); **Ground** (onay kutusu + renk); **Show ground from below**. Zemin saydamlığı kaydırıcısı güncel sayfada geçmez — *doğrulanamadı*.
- **Watermark Settings:** *Add Watermark* sihirbazı: görüntü seç ve adlandır → **Background** veya ön plan → isteğe bağlı **Create Mask** → saydamlık kaydırıcısı → yerleşim: **Stretched to fit the screen** (Lock Aspect Ratio), **Tiled across the screen** (Scale kaydırıcısı), **Positioned in the screen** (konum şeması + boyut kaydırıcısı) → Finish.
- **Modeling Settings:** kılavuzlar ve eksenler gibi modelleme işaretlerinin renkleri. Tam seçenek listesi (Selected, Locked, Guides, Inactive/Active Section, Section Cuts rengi/genişliği, Hidden Geometry, Color by Tag, Section Planes, Model Axes, Photo-matched görünürlüğü vb.) resmi sayfada verilmemiştir — *doğrulanamadı*.

---

## 6. Varsayılan klavye kısayolları ve araç değiştirici tuşları (Windows)

### 6.1 Varsayılan kısayollar

**A. Resmi kaynaklarla doğrulananlar** ("Default Keyboard Shortcuts" sayfası ve 2026 Windows Hızlı Başvuru Kartı):

| Komut | Kısayol |
|---|---|
| Select | Space |
| Lasso | Shift+Space |
| Paint Bucket | B |
| Eraser | E |
| Line | L |
| Rectangle | R |
| Circle | C |
| 2 Point Arc | A |
| Move | M |
| Push/Pull | P |
| Rotate | Q |
| Scale | S |
| Offset | F |
| Tape Measure | T |
| Orbit | O (veya orta tuş sürükleme) |
| Pan | H (veya Shift + orta tuş sürükleme) |
| Zoom | Z (veya tekerlek) |
| Zoom Extents | Shift+Z |
| Make Component | G |
| Back Edges | K |
| Search | Shift+S |
| "Image Igloo" | I (işlevi doğrulanamadı) |
| Select All | Ctrl+A |
| Deselect All / Select None | Ctrl+T |

**B. Standart Windows kısayolları — doğrulanamadı** (resmi tabloda yok; genel bilgi):

| Komut | Kısayol |
|---|---|
| New / Open / Save / Print | Ctrl+N / Ctrl+O / Ctrl+S / Ctrl+P |
| Undo / Redo | Ctrl+Z (ek: Alt+Backspace) / Ctrl+Y |
| Cut / Copy / Paste | Ctrl+X / Ctrl+C / Ctrl+V (ekler: Shift+Delete, Ctrl+Insert, Shift+Insert) |
| Delete | Delete |
| Invert Selection | Ctrl+Shift+I |

**Not:** Standart görünümler (Top, Front vb.), Hidden Geometry, X-Ray, Section Plane gibi pek çok komutun **varsayılan kısayolu yoktur**; kullanıcı Preferences > Shortcuts'tan atar (genel bilgi — *doğrulanamadı*). Resmi tablo yalnızca yukarıdaki A grubunu listeler.

### 6.2 Araç değiştirici tuşları (Windows Hızlı Başvuru Kartı 2026 ve yardım sayfaları)

| Araç | Tuş | Etki |
|---|---|---|
| **Select** | Ctrl / Shift / Ctrl+Shift | Ekle / ekle-çıkar (değiştir) / çıkar. Sağa sürükleme yalnızca tamamen içindekileri, sola sürükleme kısmen içindekileri de seçer. Çift tık: yüz + sınır kenarları; üçlü tık: tüm bağlılar. |
| **Lasso** | Shift / Alt / Shift+Alt | Ekle-çıkar / ekle / çıkar. Saat yönü ve tersi farklı seçim davranışı (Preferences'tan ters çevrilebilir); tıklayarak çokgen kement. |
| **Eraser** | Ctrl / Shift / Alt | Yumuşat-düzgünleştir / gizle / yumuşatmayı kaldır. |
| **Line** | Alt; oklar | Çıkarım yönünü kilitle; ↑ mavi, → kırmızı, ← yeşil, ↓ paralel/dik. |
| **3 Point Arc** | Alt + "+"/"−"; oklar | Segment sayısı (kart Option ifadesi kullanıyor); yön kilidi. 2022'den beri 2/3 noktalı yayda Alt teğet kilidi. |
| **Circle** | Shift | Mevcut çıkarımları kilitle. |
| **Rectangle** | Alt | Merkezden çiz. |
| **Rotated Rectangle** | Shift / Alt | Yön/düzlem kilidi / ilk kenar için çizim düzlemi kilidi. |
| **Move** | Ctrl / Shift / Alt; oklar | Kopya kipi (art arda) / çıkarım yönü kilidi / otomatik katlama (auto-fold). |
| **Push/Pull** | Ctrl; çift tık | Yüzün kopyasını iterek çek / son mesafeyi bu yüze uygula. |
| **Rotate** | Ctrl | Kopyayı döndür. 2026.1 bilinen sorun: ok tuşları dönme eksenini kilitlemiyor (forum). |
| **Scale** | Ctrl / Shift | Merkez etrafında / orantılı ölçekle. |
| **Offset** | Alt | Sonuçların üst üste binmesine izin ver. |
| **Orbit** | Alt / Shift | "Yerçekimi ağırlıklı" yörüngeyi kapat / geçici Pan. |
| **Zoom** | Shift + sürükle | Görüş alanını (FOV) değiştir. |
| **Paint Bucket** | Ctrl / Shift / Ctrl+Shift / Alt | Bitişik eşleşen yüzleri doldur / modeldeki tüm eşleşenleri değiştir / aynı nesnedeki eşleşenleri değiştir / malzeme örnekle. |
| **Follow Me** | Alt | Yüz çevresini yol olarak kullan. |
| **Flip** | Ctrl / Alt; oklar | Flip ↔ kopya / nesne eksenleri ↔ üst bağlam eksenleri; ← yeşil, → kırmızı, ↑ mavi; yüz üzerine gelince macenta özel düzlem. |
| **Protractor** | Ctrl; oklar | Kılavuz oluşturmayı aç/kapa; dönme düzlemi kilidi. |
| **Tape Measure** | Ctrl; oklar | 2026.1'den beri üç kip arasında döngü: Measure / Create Guide Lines (varsayılan) / Create Guide Point; çift tık tek kılavuz noktası. |
| **Tag** | Alt / Shift / Ctrl | Örnekle / tümünü değiştir / tüm bileşen örneklerini etiketle. |
| **Add Detail** (Sandbox) | Shift | Yatay taşı. |

---

## 7. Sandbox (arazi) araçları

**Konum:** **Sandbox** araç çubuğu (View > Toolbars). Menüde iki yere bölünmüştür: **Draw > Sandbox** (From Contours, From Scratch) ve **Tools > Sandbox** (Smoove, Stamp, Drape, Add Detail, Flip Edge). Üretilen yüzey bir **TIN** (Üçgenlenmiş Düzensiz Ağ) ve bir **grup**tur.

| Araç | Ne yapar | Kullanım adımları / girdiler |
|---|---|---|
| **From Contours** | Seçili eş yükselti çizgilerinden 3B arazi (TIN) üretir. | Her eğriyi Move + Measurements kutusuyla doğru yüksekliğe taşı → tümünü (ör. Lasso ile) seç → aracı çalıştır. Gerekirse önce eğrilerin segment sayısını azalt. |
| **From Scratch** | Düz, dikdörtgen bir TIN ızgarası oluşturur. | Aracı seç → başlangıç noktasına tıkla → uzunluk ve genişliği fareyle veya Measurements kutusuyla ver. Izgara aralığı için TIN seçiliyken değer girilir (girdi biçimi doğrulanamadı). |
| **Smoove** | Seçili bölgeyi yumuşak geçişle yükseltir/alçaltır (tepe/vadi). | TIN'de alanı seç → aracı çalıştır (köşeler vurgulanır) → Measurements'ta yarıçap → fareyi yukarı/aşağı hareket ettir veya kesin yükseklik farkı yaz (negatif = çukur). |
| **Stamp** | Bir nesnenin tabanına uyan düz bir platformu araziye "basar" ve çevreyle harmanlar. | Grup/bileşen düzenlemesinden çık → nesneyi TIN'in hemen üstüne yerleştir → aracı çalıştır → nesneye tıkla → Measurements'ta **offset** (geçiş bandı) → araziye tıkla → yüksekliği fareyle ayarla → tekrar tıkla. |
| **Drape** | Düz bir yüzeydeki kenarları kavisli TIN yüzeyine aktarır (yol, patika izleri). | Kenarları TIN'in üstüne yerleştir → kenarları seç → aracı çalıştır → TIN'e tıkla. İpucu: X-Ray kipinde çizmek. |
| **Add Detail** | Kenar veya yüz üzerine yeni köşe ekleyerek ayrıntı artırır. | Aracı çalıştır → kenar/yüze tıkla (birden çok kenar için önceden seç) → fareyle yükselt/alçalt veya Measurements'a yükseklik yaz → tıkla. Shift = yatay taşıma. Sonra Smoove ile inceltme önerilir. |
| **Flip Edge** | İki üçgenin ortak köşegenini çevirir; dönüştürme sonrası istenmeyen düz alanları giderir. | Gizli geometriyi görünür yap → aracı çalıştır → çevrilecek kenarlara tıkla. Measurements girdisi/değiştirici yok. |

Ayrıca arazi için DEM içe aktarma, Add Location ile arazi getirme ve **Toggle Terrain** mevcuttur. Scan Essentials (abonelik) **Ground Mesh** (2024) ve **Surface Mesh** (2026) araçlarıyla nokta bulutundan arazi/yüzey üretir.

---

## 8. Doğrulanamayan noktalar ve kaynak çelişkileri

**Çelişkiler (kullanıcıya bildirilmesi gereken):**

1. **Save Out ve File menüsü:** 2025.0 sürüm notlarının otomatik özeti "Save Out'un File menüsünde Save As'in yerini aldığını" ifade ederken, güncel "Saving a Model" sayfası File menüsünde Save/Save As bulunduğunu ve Save Out'un yalnızca seçime sağ tıklayınca geldiğini söyler; bağlam menüsü sayfası ise bileşen için hem "Save As" hem "Save Out" listeler. En olası yorum: Save Out, bileşen bağlam menüsündeki eski "Save As"in yerini almıştır. Kesin menü yeri **doğrulanamadı**.
2. **Flip aracının yeri:** Yardım sayfası Flip'i Getting Started, Large Tool Set ve **Edit** menüsünde gösterir; Tools menüsünde de bulunup bulunmadığı doğrulanamadı.
3. **Overlays paneli:** Resmi panel listesinde var, ancak arayüzünü açıklayan help.sketchup.com sayfası bulunamadı; bilgiler personel forum duyurusu ve API belgesinden.
4. **"Image Igloo" (I):** Resmi varsayılan kısayol tablosunda geçiyor; işlevi hiçbir resmi kaynakta açıklanmıyor.

**Doğrulanamayan başlıca alanlar:**

- Menülerin tam ve sıralı öğe listeleri (özellikle File, View, Window, Help) ve standart Windows kısayolları (Ctrl+Z/Y/X/C/V vb.).
- Getting Started, Standard, Principal, Drawing, Edit, Construction, Section, Tags, Shadows araç çubuklarının düğme listeleri.
- Hoş geldiniz ekranının bölümleri ve varsayılan şablon adları.
- Varsayılan tepsideki panellerin tam listesi ve sırası.
- Components panelinin Edit/Statistics sekmeleri; Materials Edit sekmesinin renk kipleri ve Colorize.
- Styles Mix sekmesi, Modeling Settings ayrıntıları, Halo/Level of Detail benzeri kenar ayarları.
- Shadows panelinde UTC/saat dilimi denetimi ve "From Edges".
- Preferences > General ve Files ayrıntıları; Shortcuts'taki "Reset All" ve dışa aktarma dosya biçimi.
- Model Info > Units ayrıntıları (masaüstü).
- 2023.0 sürüm notlarının ayrıntılı içeriği (PDF metni okunamadı; yalnızca bölüm başlıkları görüldü).

---

## 9. Uygulamamız için arayüz çıkarımları

Bu bölüm bir öneri listesidir; kesinleşmiş proje kararı değildir.

- **Yerleşim:** Menü çubuğu + yerleştirilebilir araç çubukları + merkezde 3B görünüm + sağda sekmeli/katlanabilir panel tepsisi + altta dört bölümlü durum çubuğu (konum, model bilgisi, araç talimatı, sayısal giriş kutusu) kullanıcıların alıştığı bir modeldir. Sayısal giriş kutusu, araç bağlamına göre farklı sözdizimleri (ör. `20,40`, `5x`, `3:12`, `10m`) kabul etmelidir.
- **Araç durum makinesi:** Her aracın değiştirici tuşları (Ctrl/Shift/Alt/oklar) ve durum çubuğu talimat metni araç tanımının parçası olmalıdır; ok tuşlarıyla eksen kilidi tüm çizim/taşıma araçlarında tutarlı olmalıdır.
- **Komut kaydı (command registry):** Menüler, araç çubukları, bağlam menüleri, komut arama (Search) ve kısayol düzenleyicisi tek bir komut kaydından beslenmelidir; böylece eklenti komutları da aranabilir ve kısayol atanabilir olur.
- **Panel sistemi:** Panellerin tepsiler arasında taşınabildiği, yüzdürülebildiği, yerleşimin JSON'da saklandığı ve "çalışma alanını sıfırla" komutunun olduğu bir yerleştirme (docking) sistemi gerekir.
- **Görünüm ayarları:** Kenar/yüz/arka plan/filigran/modelleme ayarlarını tek bir "stil" nesnesinde toplamak; sahnelerin bu nesneyi ve kamera, etiket görünürlüğü, kesit, gölge, ortam ayarlarını seçmeli olarak kaydetmesi.
- **Adlandırma ve simgeler:** Tüm araç, panel ve menü adları ile simgeler özgün olmalı; SketchUp'ın ad ve simgeleri yalnızca işlev eşleştirmesi için referanstır.

---

## Kaynaklar

Aşağıdaki URL'ler bu belgenin hazırlanmasında fiilen kullanıldı (erişim: 2026-10-20). "(arama özeti)" işaretli olanlar doğrudan açılmadı, arama motoru sonuç özetinden yararlanıldı.

**Sürüm notları (resmi)**
- https://help.sketchup.com/en/release-notes
- https://help.sketchup.com/en/release-notes/sketchup-desktop-20260
- https://help.sketchup.com/en/sketchup-desktop-20261
- https://help.sketchup.com/en/sketchup-desktop-20262
- https://help.sketchup.com/en/release-notes/sketchup-desktop-202613
- https://help.sketchup.com/en/release-notes/sketchup-desktop-20250
- https://help.sketchup.com/en/release-notes/sketchup-desktop-202503
- https://help.sketchup.com/en/release-notes/sketchup-desktop-20240
- https://help.sketchup.com/en/accounts-and-administration/release-notes-unsupported-versions-sketchup
- https://download.sketchup.com/legacydocs/SketchUp%20Desktop%202022.0.pdf
- https://download.sketchup.com/legacydocs/SketchUp%20Desktop%202023.0.pdf (yalnızca bölüm başlıkları okunabildi)

**Arayüz, araç çubukları, paneller (resmi yardım)**
- https://help.sketchup.com/en/sketchup/getting-started-sketchup
- https://help.sketchup.com/en/sketchup/user-interface
- https://help.sketchup.com/en/sketchup/toolbars
- https://help.sketchup.com/en/sketchup/dialog-boxes-and-trays
- https://help.sketchup.com/en/sketchup/managing-dialog-boxes-windows
- https://help.sketchup.com/en/sketchup/setting-templates
- https://help.sketchup.com/en/sketchup/search-tools-commands-and-extensions
- https://help.sketchup.com/en/sketchup/inspecting-entity
- https://help.sketchup.com/en/navigating-materials-panel
- https://help.sketchup.com/en/sketchup/adding-colors-and-textures-materials
- https://help.sketchup.com/en/sketchup/editing-materials (arama özeti)
- https://help.sketchup.com/en/sketchup/components-3d-warehouse (arama özeti)
- https://help.sketchup.com/en/sketchup/creating-basic-component (arama özeti)
- https://help.sketchup.com/en/sketchup/controlling-visibility-tags
- https://help.sketchup.com/en/sketchup/creating-scenes
- https://help.sketchup.com/en/sketchup/working-hierarchies-outliner
- https://help.sketchup.com/en/sketchup/matching-photo-model-or-model-photo
- https://help.sketchup.com/en/sketchup/softening-smoothing-and-hiding-geometry
- https://help.sketchup.com/en/sketchup/flipping-mirroring-rotating-and-arrays
- https://help.sketchup.com/en/sketchup/selecting-geometry
- https://help.sketchup.com/en/context-menu-items-entities
- https://help.sketchup.com/en/sketchup/saving-model (arama özeti)
- https://help.sketchup.com/en/sketchup/viewing-model (arama özeti)
- https://help.sketchup.com/en/extension-warehouse/getting-started-extension-warehouse (arama özeti)
- https://help.sketchup.com/en/sketchup-ipad/entity-info-panel (arama özeti; iPad karşılaştırması)
- https://help.sketchup.com/en/sketchup-ipad/display-panel (arama özeti; iPad karşılaştırması)
- https://help.sketchup.com/en/sketchup-ipad/soften-smooth-panel (arama özeti; iPad karşılaştırması)
- https://help.sketchup.com/en/sketchup-ipad/model-info-panel (arama özeti; iPad karşılaştırması)
- https://help.sketchup.com/en/sketchup-schools/navigating-sketchup-schools-interface (arama özeti; Instructor)

**Tercihler ve kısayollar (resmi)**
- https://help.sketchup.com/en/sketchup/preferences
- https://help.sketchup.com/en/sketchup/graphics
- https://help.sketchup.com/en/sketchup/drawing
- https://help.sketchup.com/en/sketchup/shortcuts
- https://help.sketchup.com/en/default-keyboard-shortcuts
- https://help.sketchup.com/en/creating-and-editing-shortcuts (arama özeti)
- https://help.sketchup.com/en/quick-reference-cards
- https://download.sketchup.com/QRC2026/HC-QRC2026-en-SU-windows.pdf

**Stiller, gölge, sis, ortam (resmi)**
- https://help.sketchup.com/en/sketchup/choosing-style
- https://help.sketchup.com/en/sketchup/creating-and-editing-style
- https://help.sketchup.com/en/sketchup/edge-styles
- https://help.sketchup.com/en/face-style
- https://help.sketchup.com/en/sketchup/customizing-models-background-styles
- https://help.sketchup.com/en/sketchup/casting-real-world-shadows
- https://help.sketchup.com/en/sketchup/letting-fog-roll-your-model
- https://help.sketchup.com/en/sketchup/adding-environment-to-models
- https://help.sketchup.com/en/layout/sketchup-model-effects (arama özeti)

**Sandbox (resmi)**
- https://help.sketchup.com/en/sketchup/creating-terrain-scratch
- https://help.sketchup.com/en/sketchup/sculpting-and-fine-tuning-terrain
- https://help.sketchup.com/en/sketchup/placing-models-and-objects-your-terrain

**Resmi blog ve forum**
- https://sketchup.trimble.com/en/blog/article/2023-makes-modeling-a-snap-with-ui-updates-and-custom-controls
- https://forums.sketchup.com/t/sketchup-2026-1-is-here/342813
- https://forums.sketchup.com/t/new-sketchup-2023-overlay-feature/220244
- https://forums.sketchup.com/t/where-is-instructor/20798
- https://forums.sketchup.com/t/i-dont-see-from-contours-as-an-option-in-the-sandbox-tools-menu/288085 (arama özeti)
- https://forums.sketchup.com/t/has-the-flip-tool-been-eliminated-in-sketchup-2023/242490 (arama özeti)
- https://forums.sketchup.com/t/environments-panel-on-sketchup-2024-mac-os/326720 (arama özeti)
- https://forums.sketchup.com/t/docking-tray-issues/341892 (arama özeti)
- https://forums.sketchup.com/t/delete-guides/156936 (arama özeti)
- https://forums.sketchup.com/t/how-to-unselect-everything-in-one-or-few-clicks-without-clicking-outside-the-model/94847 (arama özeti)
- https://forums.sketchup.com/t/understanding-how-overlay-works/244616 (arama özeti)
