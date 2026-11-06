# SketchUp 2026 Analizi — 04: Veri Modeli, İşlem/Araç Mimarisi, Dosya Biçimleri ve Eklentiler

> **Amaç:** Kendi adı ve kendi kodu olan, işlevselliği SketchUp 2026'yı örnek alan özgün bir Windows 3B modelleme uygulaması için referans notu.
> **Yöntem:** Yalnızca herkese açık resmi belgeler (ruby.sketchup.com, extensions.sketchup.com/developers, help.sketchup.com, developer.sketchup.com, Trimble'ın resmi plan sayfası ve GitHub "SketchUp" organizasyonu) incelendi. Hiçbir yazılım indirilmedi, tersine mühendislik yapılmadı. Metinler özetlenerek ve yeniden ifade edilerek aktarıldı.
> **Araştırma tarihi:** 2026-10-20
> **İşaretleme:** Belgelerde açıkça bulunamayan veya resmi kaynaklarla teyit edilemeyen noktalar **"doğrulanamadı"** olarak işaretlendi. "Çıkarım" etiketi, belgedeki metot listelerinden yapılan yorumlar içindir.

---

## İçindekiler

1. [Herkese açık veri modeli](#1-herkese-açık-veri-modeli)
2. [İşlem (operation) / geri alma modeli ve araç (Tool) arayüzü](#2-i̇şlem-operation--geri-alma-modeli-ve-araç-tool-arayüzü)
3. [Dosya biçimleri](#3-dosya-biçimleri)
4. [Eklentiler, 3D Warehouse, LayOut, Style Builder, sürümler, sistem gereksinimleri, grafik motoru](#4-eklentiler-3d-warehouse-layout-style-builder-sürümler-sistem-gereksinimleri-grafik-motoru)
5. [Benzer uygulama geliştirmek için yaygın açık kaynak kütüphaneler ve yaklaşımlar](#5-benzer-uygulama-geliştirmek-için-yaygın-açık-kaynak-kütüphaneler-ve-yaklaşımlar)
6. [Belgeler arasında fark edilen çelişkiler / dikkat noktaları](#6-belgeler-arasında-fark-edilen-çelişkiler--dikkat-noktaları)
7. [Kaynaklar](#kaynaklar)

---

## 1. Herkese açık veri modeli

### 1.1 Sınıf hiyerarşisi (Ruby API'ye göre)

```
Object
 └─ Sketchup::Entity                      (tüm model nesnelerinin tabanı)
     ├─ Drawingelement                    (görüntülenebilir öğeler)
     │   ├─ Edge, Face
     │   ├─ Group, ComponentInstance, ComponentDefinition
     │   ├─ ConstructionLine, ConstructionPoint (kılavuz çizgi/nokta)
     │   ├─ Dimension, Text, Image, SectionPlane, Snap
     ├─ Vertex, Loop, EdgeUse              (topoloji, görüntülenmeyen)
     ├─ Curve └─ ArcCurve
     ├─ Material, Texture, Layer (Tag), LayerFolder
     ├─ Page (Scene), Style, LineStyle, Environment
     ├─ AttributeDictionary, AttributeDictionaries
     └─ Koleksiyonlar: Materials, Layers, Pages, Styles, DefinitionList, LineStyles, Environments ...
```

- **Entity** (taban sınıf): oturum içinde geçerli bir `entityID`, oturumlar arasında kalıcı bir `persistent_id` (yalnızca bazı türlerde; 2017–2025 arasında aşamalı eklenmiş), `valid?`/`deleted?`, `parent`, `model`, öznitelik sözlükleri ve gözlemci (observer) ekleme yeteneği sağlar.
- **Drawingelement**: görüntülenebilen öğelerin tabanı; `bounds` (sınırlayıcı kutu), `layer` (etiket), `material`, `hidden?`/`visible?`, gölge düşürme/alma bayrakları ve `erase!` sunar. Bir kenar silindiğinde onu kullanan yüzlerin de silindiği belgelenmiştir.

### 1.2 Model ve koleksiyonlar

- `Sketchup.active_model` açık modeli verir. **Model** şu koleksiyonlara erişim sağlar: `entities` (yalnızca en üst seviye), `active_entities` (o an düzenlenen grup/bileşen içeriği), `definitions` (DefinitionList), `materials`, `layers`, `pages`, `styles`, `tools`, `environments` (2025+), `line_styles` (2019+), `overlays` (2023+), `rendering_options`, `shadow_info`, `options`, `axes`, `selection`, `active_view`.
- **Düzenleme bağlamı:** `active_path` kullanıcının içine girdiği grup/bileşen zincirini, `edit_transform` ise bu düzenleme oturumunun dönüşümünü verir. Yani "grubun içine girip düzenleme" kavramı veri modelinde açıkça temsil edilir.
- **Entities**: bir çizim bağlamına karşılık gelen koleksiyondur; ya Model'e ya da bir ComponentDefinition'a aittir. Geometri oluşturma metotları (`add_line`, `add_edges`, `add_face`, `add_arc`, `add_circle`, `add_ngon`, `add_curve`, `add_group`, `add_instance`, `add_cpoint`, `add_cline`, `add_dimension_*`, `add_text`, `add_3d_text`, `add_image`, `add_section_plane`, `add_faces_from_mesh`, `fill_from_mesh`, `build`), dönüştürme (`transform_entities`), silme (`erase_entities`), kesişim (`intersect_with`) ve eğri birleştirme (`weld`) bu sınıftadır.

### 1.3 Topoloji: Vertex – Edge – Loop – EdgeUse – Face

| Sınıf | Rolü | Önemli ilişkiler |
|---|---|---|
| **Vertex** | Kenar ucu veya yüz içindeki nokta | `position` (Point3d), `edges`, `faces`, `loops`, `common_edge(v2)` |
| **Edge** | İki köşe arasındaki doğru parçası | `start`/`end` (2 Vertex), `faces` (dizi), `curve`, `smooth`/`soft`, `split`, `find_faces`, `reversed_in?(face)` |
| **Loop** | Bir yüzü sınırlayan sıralı kenar zinciri | `edges`, `edgeuses`, `vertices` (sıralı), `face`, `outer?`, `convex?` |
| **EdgeUse** | Bir kenarın belirli bir döngüde (yönlü) kullanımı | `edge`, `face`, `loop`, `next`/`previous`, `partners`, `reversed?`, köşe normalleri |
| **Face** | Düzlemsel, iki yüzlü çokgen | `outer_loop`, `loops`, `edges`, `vertices`, `normal`, `plane`, `area`, `material`/`back_material` |

Belgelerden çıkan temel kurallar:

- **Kenar = tam 2 köşe.** Her kenarın bir başlangıç ve bir bitiş köşesi vardır.
- **Kenar–yüz komşuluğu:** Bir kenar birden fazla yüz tarafından paylaşılabilir (`Edge#faces` dizi döndürür). Bir kenarın en fazla kaç yüze bağlanabileceği belgede belirtilmemiştir (**doğrulanamadı**; metot yapısı 2'den fazla yüzü, yani manifold olmayan durumları da desteklediğini düşündürüyor — çıkarım).
- **Yüz = 1 dış döngü + 0..n iç döngü.** Her yüzün tek bir dış döngüsü, her delik için de bir iç döngüsü vardır.
- **Yüzler düzlemseldir** ve en az 3 kenarlıdır; ön ve arka tarafı vardır, her taraf ayrı malzeme alabilir. `normal` ön tarafı gösterir, `reverse!` tarafları çevirir.
- **EdgeUse yapısı (çıkarım):** EdgeUse yönlüdür ve döngüde `next`/`previous` ile bağlıdır (half-edge benzeri); `partners` ise aynı kenarın diğer yüzlerdeki kullanımlarını verir. Bu, birden fazla yüzün tek kenarı paylaşabildiği **radial-edge** benzeri bir sınır temsili (B-rep) olduğunu düşündürür. Belge yapının adını vermez (**doğrulanamadı**).
- **Otomatik yüz oluşturma:** API'de `add_edges` ile kapalı bir zincir çizmek yüzü kendiliğinden oluşturmaz; `add_face` veya `Edge#find_faces` gerekir. (Arayüzdeki Çizgi aracının kapanan döngüde yüz oluşturması kullanıcı davranışıdır; araç düzeyinde `find_faces` benzeri bir işlem çalıştığı çıkarımdır.)
- **Birleştirme/bölme (merge/split):** `Entities#add_face`/`add_line`/`add_edges` geometriyi, yerleşik Çizgi/Dikdörtgen araçlarına benzer şekilde birleştirir ve böler; ortak kenarı olan yüzler eklenince ortak kenar otomatik bölünür. Bu yüzden yavaştır ve az sayıda öğe için önerilir.
- **Toplu geometri:** `Geom::PolygonMesh` + `fill_from_mesh` (birleştirme/bölme yapmaz, yalnızca köşe/kenar tekilleştirir) ve `EntitiesBuilder` (2022.0+, `entities.build { |b| ... }`; tekilleştirme yapar, kesişim/bölme yapmaz, doğruluk sorumluluğu çağırandadır) hızlı yollardır.
- **"Yapışkan geometri":** Etiketler (Layer/Tag) geometriyi yalıtmaz; farklı etiketteki öğeler de birbiriyle kesişir/birleşir. Geometriyi ayrı tutmanın yolu **grup veya bileşen** kullanmaktır (Layer belgesindeki açık not).

### 1.4 Eğriler: Curve ve ArcCurve

- SketchUp bir **yüzey modelleyicidir**; daire, yay ve serbest eğriler aslında **ardışık kenarlardır**. `Curve`, bu kenar dizisini tek nesne gibi gruplayan "ek veri"dir (`edges`, `vertices`, `length`, `first_edge`/`last_edge`, `move_vertices`).
- `ArcCurve`, Curve'ün daire parçası olan alt sınıfıdır: `center`, `normal`, `radius`, `start_angle`/`end_angle` (radyan), `xaxis`/`yaxis`, `plane`, `circular?` (2023.1+). Yani yay hem **parametrik tanım** (merkez, yarıçap, açılar) hem de **segmentlere bölünmüş kenarlar** olarak tutulur (çıkarım; belge "eğriler aslında ek veri eklenmiş kenarlardır" der).
- Kenarın `curve` metodu ait olduğu eğriyi verir; `explode_curve` eğriyi tek tek kenarlara ayırır. `Vertex#curve_interior?` köşenin bir eğrinin iç noktası olup olmadığını söyler.

### 1.5 Gruplar, bileşen tanımları ve örnekleri (geometri paylaşımı)

- **ComponentDefinition** paylaşılan içeriği (kendi `entities` koleksiyonunu) taşır; **ComponentInstance** bu tanıma bir referans + bir **Transformation** (konum/yön/ölçek) taşır. Tanımdaki değişiklik tüm örneklere yansır.
- **Group de bir tanım kullanır:** `Group#definition` bir ComponentDefinition döndürür; `ComponentDefinition#group?` bunu ayırt eder. Grup ve görüntü (image) tanımları Bileşen tarayıcısında gizlidir.
- **Kopyalanan gruplar tanım paylaşabilir.** Arayüz, kullanıcı bir grubu düzenlediğinde onu sessizce **benzersizleştirir (make_unique)**; API'de `Group#entities` üzerinden düzenleme de otomatik benzersizleştirir, `definition` üzerinden düzenleme ise tüm kopyaları etkiler. `ComponentInstance#make_unique` örneğe kendi tanımını verir.
- Diğer ayrıntılar: `count_instances` / `count_used_instances` (2016+), `guid` (düzenleme sonrası değişir), `live_component?` (parametrik "Live Components"; API ile değiştirilmemeli), `glued_to` (bir yüze yapıştırılmış bileşenler, behavior izin verirse), `locked?`, `explode`.
- **Katı (solid) işlemleri:** `union`, `subtract`, `intersect`, `trim`, `split`, `outer_shell`; her iki işlenen de **manifold** olmalıdır, sonuç yeni bir gruptur. Belgede bunların çoğu "SketchUp Make'te yok" diye işaretlidir (Pro özelliği olduğu çıkarımı). `manifold?` 2026.2'de tanım düzeyine (`ComponentDefinition#manifold?`) taşınmıştır; `volume` manifold değilse negatif döner.
- `insertion_point` 2020'den beri kullanım dışıdır (her zaman orijin).

### 1.6 Transformation (dönüşüm)

- `Geom::Transformation` 4×4 matris temsilidir; `to_a` 16 elemanlı dizi verir. Satır/sütun sırası belgede açıkça yazılmamıştır (**doğrulanamadı**).
- Kurucular: birim, öteleme, dönme (nokta+eksen+açı), ölçekleme (düzgün/düzgün olmayan, nokta etrafında), eksen takımı (`axes`), `interpolate`. Çarpma operatörü noktaya, vektöre, düzleme ve başka dönüşüme uygulanır.
- **2026 değişikliği:** Tersi alınamayan (non-invertible) dönüşümler artık `inverse`, `invert!` ve `transformation=` setter'larında `ArgumentError` fırlatıyor (önceden sessizce kabul ediliyordu).
- `move!` geri alma yığınına kayıt düşmeden konum ayarlar (animasyon için); `transform!` göreli uygular.

### 1.7 Malzeme ve doku (Material / Texture)

- **Material:** renk (`color`), saydamlık (`alpha`, 0.0–1.0; rengin alfa değeri saydamlık için kullanılmaz), isteğe bağlı doku. Türler: düz, dokulu, renklendirilmiş doku (`COLORIZE_SHIFT`/`TINT`). İç ad (`name`, benzersiz) ve görünen ad (`display_name`) ayrıdır. `.skm` dosyasına kaydedilebilir.
- **PBR (2025+):** `workflow` (klasik veya PBR metalik-pürüzlülük), metallik, pürüzlülük, normal haritası (OpenGL veya DirectX stili), ortam örtmesi (AO) dokuları ve katsayıları.
- **Texture:** `width`/`height` = desenin **model ölçüsündeki boyutu (inç)**; `image_width`/`image_height` = piksel. Doku yüzeyde döşenerek (tile) tekrarlar. Yüze özel yerleştirme `Face#position_material` (2/4/6/8 nokta çiftleri; 2021.1'den itibaren projeksiyon) ve `UVHelper` ile yapılır.

### 1.8 Etiket (Layer/Tag) ve Etiket Klasörü (LayerFolder)

- 2020'den itibaren arayüzde "Layer" adı **"Tag"** olmuştur; API'de hâlâ `Layer`. "Layer 0" silinemez ve arayüzde **"Untagged"** olarak görünür.
- Özellikler: görünürlük, renk, çizgi stili (`line_style`), sahne (page) davranışı bayrakları, üst klasör (`folder`).
- **LayerFolder** (2021.0+): iç içe klasörler; klasör hem etiket hem alt klasör içerebilir; klasör görünürlüğü ve "yeni sahnelerde görünür" ayarı vardır.
- **Önemli tasarım ilkesi:** Etiketler yalnızca görünürlük/organizasyon içindir, geometri yalıtımı sağlamaz.

### 1.9 Sahne (Page), Kamera, Stil

- **Page (Scene)** şunları saklayabilir: kamera, eksenler, stil/görüntüleme seçenekleri, gölge ayarları, gizli geometri ve gizli nesneler (2020.1+ ayrı ayrı), etiket görünürlüğü, aktif kesit düzlemleri, ortam (2025+). Her biri `use_*` bayraklarıyla açılıp kapanır; `update(flags)` mevcut görünümü sahneye yakalar. Geçiş süresi, bekleme süresi, açıklama, etiket özellikleri vardır.
- **2026 değişikliği:** Sahnenin eksen, kamera, görüntüleme seçeneği ve gölge değişiklikleri artık **geri alınabilir**; eklentiler bunları bir işlem (operation) içinde yapmalıdır.
- **Camera:** `eye`, `target`, `up`, `direction`; perspektif/ortografik; perspektifte `fov` (1–120°), `focal_length` (mm), `image_width`; ortografikte `height` (inç); `aspect_ratio`; iki nokta perspektif/foto eşleme modu (`is_2d?`, `scale_2d`, `center_2d`).
- **Style:** modelin nasıl çizileceğini belirleyen görüntü ayarları topluluğu. `Styles` içinde `selected_style` (seçili) ve `active_style` (düzenlenen geçici kopya) ayrımı vardır; değişiklikler `update_selected_style` ile işlenir. Stilin tam olarak hangi ayarları içerdiği (kenar/yüz/arka plan/filigran) Style sayfasında listelenmemiştir (**doğrulanamadı** — RenderingOptions üzerinden yönetildiği çıkarımı).

### 1.10 Kesit düzlemi (SectionPlane)

- `get_plane`/`set_plane`, `activate`/`active?` (bir grup/bileşen veya modelde aktif kesit), `name`, `symbol` (en çok 3 karakter). 2026'da Model ve Page'e `active_section_planes` eklendi. Kesit dolgusu (section fill) API ayrıntıları bu sayfada yoktur (**doğrulanamadı**).

### 1.11 AttributeDictionary (öznitelik sözlükleri)

- Her Entity'ye (Model dahil) adlandırılmış anahtar/değer sözlükleri eklenebilir. Anahtarlar dizgedir; değerler Boolean, Integer, Float, Length, nil, String, Time, Array, Point3d, Vector3d olabilir.
- Kullanım: `set_attribute(sözlük, anahtar, değer)` / `get_attribute`. 2018'den itibaren tanım ve örneklerde silinemeyen varsayılan sözlükler vardır (ör. "SU_DefinitionSet", "Owner"/"Status" öznitelikleri).
- Verinin `.skp` içinde kalıcı olduğu yaygın kullanım amacından anlaşılır; AttributeDictionary sayfası bunu açıkça yazmaz (**doğrulanamadı**).

### 1.12 Selection (seçim)

- `add`, `remove`, `toggle`, `clear`, `contains?`, `first`, `count`, `each`. Sıra garanti edilmez. Gözlemci eklenebilir.

### 1.13 Gözlemciler (observers)

- Gözlemci sınıfları: `AppObserver`, `ModelObserver`, `EntitiesObserver`, `EntityObserver`, `SelectionObserver`, `ToolsObserver`, `ViewObserver`, `LayersObserver`, `MaterialsObserver`, `PagesObserver`, `DefinitionsObserver`, `DefinitionObserver`, `InstanceObserver`, `DimensionObserver`, `EnvironmentsObserver`, `FrameChangeObserver`, `OptionsProviderObserver`, `RenderingOptionsObserver`, `ShadowInfoObserver`.
- Örnek olaylar: `onElementAdded`, `onElementModified`, `onElementRemoved` (yalnızca kimlik gelir; nesne silinmiştir), `onEraseEntities`, `onActiveSectionPlaneChanged`; Model düzeyinde `onTransactionStart/Commit/Undo/Redo/Abort/Empty`, `onPreSaveModel`/`onPostSaveModel`, `onActivePathChanged`, `onExplode`, `onPlaceComponent`.
- **Kural:** Gözlemci geri çağrılarında modeli değiştirmekten kaçınılmalıdır (çökme veya model bozulması riski). Gerekirse **şeffaf (transparent) işlem** kullanılır (bkz. §2). 2016'dan beri `onTransactionStart` commit anında kuyruktan tetiklenir.

### 1.14 Birimler ve toleranslar

- **Dahili birim inçtir.** Uzunluk, alan ve hacim değerleri dahili olarak inç tabanlıdır; `.skp` ve `.layout` dosyalarında da öyledir (C API belgesi). Ruby `Length` sınıfı değerleri inç olarak tutar; `to_s` modelin birim ayarlarına göre biçimlendirir; ayarlar `model.options["UnitsOptions"]` (`LengthFormat`, `LengthUnit`) üzerinden okunur. Desteklenen birimler: inç, fit, mm, cm, m, yarda (2020+).
- **Tolerans:** C API belgesine göre 3B nokta ve vektör eşitlik karşılaştırmaları **0.001** dahili toleransla yapılır (birim belirtilmemiş; dahili birim inç olduğu için 0.001 inç olduğu çıkarımı). Ruby `Length` karşılaştırmaları (`==`, `<`, `<=>` …) "varsayılan uzunluk toleransını" kullanır ama sayısal değeri Ruby belgesinde verilmez (**doğrulanamadı**).
- Tolerans, bir köşe önbelleğiyle (vertex cache) birlikte geometrinin otomatik birleşmesinde rol oynar: EntitiesBuilder "eşit sayılan" noktalarla kenar eklenmesini reddeder.

### 1.15 C SDK (C API) açısından nesne modeli

- C API aynı modeli opak tutamaçlarla (handle) sunar: `SUModelRef`, `SUEntitiesRef`, `SUFaceRef`, `SUEdgeRef`, `SUVertexRef`, `SULoopInputRef`, `SUGeometryInputRef`, `SUComponentDefinitionRef`, `SUComponentInstanceRef`, `SUGroupRef`, `SUImageRef`, `SUMaterialRef`, `SUTextureRef`, `SULayerRef`, `SUCurveRef`, `SUMeshHelperRef`, `SUUVHelperRef`, `SUStringRef`.
- **Sahiplik kuralları:** `SU*Create*` ile oluşturulan nesne modele/koleksiyona eklenince model serbest bırakıldığında birlikte serbest kalır; mevcut nesneden alınan tutamaçlar elle serbest bırakılmaz; `SUModelRef`, `SUStringRef`, `SUMeshHelperRef` ve geçici girdi nesneleri elle serbest bırakılmalıdır.
- `SUEntitiesAddFaces` geometriyi birleştirmez; birleştirilmiş geometri için `SUGeometryInput` + `SUEntitiesFill` kullanılır.
- Tüm çağrılar **ana iş parçacığından** yapılmalıdır.

---

## 2. İşlem (operation) / geri alma modeli ve araç (Tool) arayüzü

### 2.1 İşlem ve geri alma (undo)

- `model.start_operation(ad, disable_ui = false, next_transparent = false, transparent = false)` ile başlatılır, `commit_operation` ile tek bir geri alma adımı olarak kapanır; hata durumunda `abort_operation` ile geri sarılır (genelde `rescue` içinde).
  - `disable_ui = true`: işlem sırasında arayüz güncellemelerini kapatır; performans için önerilir.
  - `transparent = true`: işlem bir önceki işleme **eklenir** (ayrı geri alma adımı oluşturmaz) — özellikle gözlemcilerde kullanılır.
  - `next_transparent`: kullanım dışı (deprecated); sonraki işlemi bu işleme katar.
- **İç içe işlem yoktur.** Yeni işlem başlatmak öncekini örtük olarak kapatır. Şeffaf bir işlem iptal edilmemelidir (zincirlendiği işlemi de iptal eder); temizleyip commit etmek önerilir.
- **Düzenleme bağlamı değişimi:** `active_path` değiştiğinde koordinat sistemi değiştiği için SketchUp işi otomatik olarak şeffaf işlemlere böler; kullanıcı tek geri alma adımı görür ama gözlemciler birden çok işlem bildirimi alır.
- `Entities#build` örtük bir işlem başlatır; geri alma için `start_operation`/`commit_operation` ile sarmalanmalıdır.
- `move!` geri alma kaydı oluşturmaz (animasyon).
- Eklenti kuralları: çok adımlı çizim işleri tek işlem içinde gruplanmalı ve anlamlı işlem adları verilmelidir; model sessizce kaydedilmemeli.

**Mimari çıkarım:** Uygulama çekirdeği **işlem tabanlı (transaction)** bir değişiklik kaydı tutar: her işlem, modeldeki tüm değişiklikleri (geometri, malzeme, sahne ayarları — 2026'dan beri kamera da) tek bir geri alınabilir birim olarak toplar. Gözlemciler bu işlem sınırlarında (commit/undo/redo) bilgilendirilir. Kendi uygulamamızda bunun karşılığı: komut (command) / işlem günlüğü (transaction log) + değişiklik bildirimi (change notification) sistemi.

### 2.2 Tool (araç) arayüzü

Bir araç, yalnızca ihtiyaç duyduğu geri çağrıları uygulayan bir sınıftır; `model.select_tool(arac)` ile seçilir veya `model.tools.push_tool` / `pop_tool` ile bir **araç yığınına** eklenip çıkarılır.

| Grup | Geri çağrılar |
|---|---|
| Yaşam döngüsü | `activate`, `deactivate(view)`, `suspend(view)` (ör. orta tuşla Orbit geçici devraldığında), `resume(view)`, `onCancel(reason, view)` (0=Esc, 1=aracı yeniden seçme, 2=geri alma), `onReturn(view)` |
| Fare | `onLButtonDown/Up/DoubleClick`, `onRButtonDown/Up/DoubleClick`, `onMButtonDown/Up`, `onMouseMove`, `onMouseWheel(flags, delta, x, y, view)`, `onMouseEnter/Leave` — parametreler `(flags, x, y, view)` |
| Klavye / Ölçü kutusu (VCB) | `onKeyDown/onKeyUp(key, repeat, flags, view)`, `enableVCB?`, `onUserText(text, view)` (ölçü kutusuna yazılan değer; ör. `text.to_l`) |
| Çizim | `draw(view)` (geçici grafik), `getExtents` (çizimin kırpılmaması için sınır kutusu) |
| Diğer | `onSetCursor`, `getMenu` (sağ tık menüsü), `getInstructorContentDirectory` (Eğitmen penceresi içeriği) |

- **2025.0'dan itibaren** fare koordinatları fiziksel piksel tamsayısı yerine **mantıksal piksel ondalık** değerdir (monitör başına DPI desteği; kırıcı değişiklik).
- Bilinen hatalar: 2026.0'da Windows'ta Return/Enter için `onKeyDown` tetiklenmiyordu (2026.1'de düzeltildi).

### 2.3 InputPoint ve çıkarım (inference)

- **InputPoint**, imleç altındaki 3B noktayı **çıkarımla** (yakındaki köşe, kenar, yüz, eksen vb.'ye "yapışma") bulur; PickHelper'dan farkı budur. Tipik akış: `onMouseMove` içinde `ip.pick(view, x, y [, onceki_ip])`, ardından `view.invalidate`, sonra `draw` içinde `ip.draw(view)`.
- İkinci bir InputPoint verilirse o referansa göre eksen yönü gibi ek çıkarımlar yapılır.
- `degrees_of_freedom`: 3 = serbest uzay, 2 = yüz üzerinde, 1 = kenar/eksen üzerinde, 0 = uç nokta/kesişim.
- `edge`, `face`, `vertex`, `transformation` (bileşen içindeyse örneğin dönüşümü), `instance_path`, `depth`, `tooltip`, `valid?`, `copy!`, `clear`.
- `View#lock_inference(ip1 [, ip2])` çıkarımı bir öğeye veya iki noktayla tanımlı bir eksene kilitler (yerleşik araçlardaki Shift kilidi gibi); `inference_locked?` durumu bildirir.
- 2025'te `Snap` sınıfı ve `Entities#add_snap` eklenmiştir (özel yapışma noktaları).

### 2.4 View üzerinde çizim ve seçme

- `view.draw(GL_LINES | GL_TRIANGLES | GL_QUADS …, noktalar)` OpenGL ilkel türleri adlarıyla model uzayında çizer (2020.0+ normal, doku ve UV seçenekleri); `draw2d` ekran uzayında çizer; `draw_line(s)`, `draw_polyline`, `draw_points`, `draw_text`.
- Stil: `drawing_color=`, `set_color_from_line` (eksene paralel çizgileri kırmızı/yeşil/mavi boyar), `line_width=` (2026'da en az 1.0'a sıkıştırılır), `line_stipple=` (kesikli/noktalı).
- Yenileme: `invalidate` (tercih edilen; bir sonraki karede yeniden çizim) / `refresh` (anında; performans riski).
- Seçme: `pickray(x, y)` + `Model#raytest`, `pick_helper`, `inputpoint`, `screen_coords`, `pixels_to_model`.
- Kamera: `camera=`, geçiş süresiyle; `zoom`, `zoom_extents`, `field_of_view`.
- **Overlay** (2023.0+): etkin araçtan bağımsız olarak görünüm üstüne 2B/3B bilgi çizen katmanlar; seçilemez, dışa aktarılamaz, modeli değiştiremez.

### 2.5 Araç mimarisi hakkında çıkarımlar (kendi uygulamamız için)

1. **Durum makinesi (state machine) araçları:** Her araç kendi durumunu (ör. "ilk nokta bekleniyor", "ikinci nokta bekleniyor") tutar; olaylar (fare/klavye/ölçü kutusu) durumu ilerletir. `onCancel` ve `resume/suspend` ile durum sıfırlanır/korunur.
2. **Geçici (preview) çizim ile kalıcı model ayrımı:** Fare hareketinde model değiştirilmez; yalnızca `draw` ile önizleme çizilir. Model değişikliği tıklama/Enter anında **tek bir işlem** içinde yapılır.
3. **Ortak çıkarım motoru:** Yapışma ve eksen kilidi tek bir paylaşılan bileşende (InputPoint) toplanmıştır; tüm çizim araçları bunu kullanır.
4. **Araç yığını:** Gezinme araçları (Orbit/Pan/Zoom) etkin aracı askıya alıp geri döner.
5. **Ölçü kutusu (VCB) entegrasyonu:** Araçlar sayısal girdiyi metin olarak alıp birim ayrıştırmasıyla uzunluğa çevirir.
6. **Yüksek frekanslı olaylarda hafiflik:** `onMouseMove` ve `draw` çok sık çağrılır; ağır hesap önbelleğe alınmalıdır.

---

## 3. Dosya biçimleri

### 3.1 .skp (yerel, kapalı biçim)

- `.skp` biçimi Trimble'a özeldir ve belgesi herkese açık değildir; okuma/yazma resmi olarak **SketchUp C SDK** ile yapılır. SDK erişimi developer.sketchup.com üzerinden **talep (request access)** ile verilmektedir.
- **C SDK neler sağlar:**
  - **Bağımsız kullanım:** SketchUp çalışmadan `.skp` dosyası okuma, oluşturma ve değiştirme (üçüncü taraf uygulamalar, içe/dışa aktarıcılar).
  - **Live C API:** SketchUp içinde çalışan bir eklentiden açık modele **yalnızca okuma** erişimi (`SUApplicationGetActiveModel`).
  - **İçe/Dışa Aktarıcı Arayüzü:** SketchUp'ın Dosya > İçe/Dışa Aktar menüsünde görünen yerel modüller yazmak için arayüz; SDK'da `skp_to_xml` ve `xml_to_skp` örnek projeleri bulunur.
- **Platform:** Windows'ta yalnızca 64 bit, Visual Studio 2022 (v143) ile derlenmiş; dağıtımda `SketchUpAPI.dll` ve `SketchUpCommonPreferences.dll` gerekir. macOS'ta 2022.0'dan itibaren Universal ikili.
- **Sürüm uyumluluğu:** Ruby belgesine göre **2021'den itibaren tüm sürümler tek bir dosya biçimini paylaşır**; daha eski sürümlere `save(path, version)` / `save_copy` ile alt sürüm kaydı yapılabilir. C API belgesi, daha yeni bir sürümde oluşturulmuş modelin okunamayabileceğini ve üzerine kaydetmenin kalıcı veri kaybına yol açabileceğini belirtir. C SDK'nın hangi eski `.skp` sürümlerine yazabildiği belge sayfasında açıkça listelenmemiştir (**doğrulanamadı**).
- **Lisans/şartlar:** C SDK'nın kullanım koşulları bu araştırmada incelenmedi (**doğrulanamadı**). Kendi uygulamamızda `.skp` desteği düşünülürse SDK lisansı ayrıca hukuki olarak incelenmelidir.

### 3.2 İçe/dışa aktarma biçimleri (masaüstü)

Kaynak: help.sketchup.com "Using SketchUp Data with Other Applications" sayfası (plan sütunu bu sayfadandır) ve Ruby API içe/dışa aktarıcı seçenek belgeleri.

| Biçim | Uzantı | Yön | Belgede belirtilen planlar |
|---|---|---|---|
| JPG, PNG | .jpg .png | İçe + dışa | Free, Go, Pro, Studio |
| TIF, BMP, PSD, TGA, EPS | .tif .bmp .psd .tga .eps | İçe + dışa (sayfaya göre) | Go, Pro, Studio |
| PDF | .pdf | İçe + dışa | Free, Go, Pro, Studio |
| AutoCAD | .dwg .dxf | İçe + dışa (2B/3B) | Go, Pro, Studio |
| STL | .stl | İçe + dışa | Free, Go, Pro, Studio |
| COLLADA | .dae | İçe + dışa | Go, Pro, Studio |
| 3D Studio | .3ds | İçe + dışa | Go, Pro, Studio |
| DEM (arazi) | .dem .ddf | Yalnız içe | Go, Pro, Studio |
| FBX | .fbx | Yalnız dışa | Go, Pro, Studio |
| Google Earth | .kmz | Dışa (Ruby importer belgesinde KMZ içe aktarma seçenekleri de var) | Go, Pro, Studio |
| OBJ | .obj | Yalnız dışa | Go, Pro, Studio |
| VRML | .wrl | Yalnız dışa | Go, Pro, Studio |
| XSI | .xsi | Yalnız dışa | Go, Pro, Studio |
| IFC | .ifc | İçe + dışa | Pro, Studio |
| USDZ | .usdz | Belirtilmemiş (Ruby exporter belgesinde dışa aktarıcı var) | Pro, Studio |
| glTF / GLB | (sayfada ".gib" yazım hatası) | Ruby exporter belgesinde GLB dışa aktarıcı var | Pro, Studio |

Notlar:

- **Ruby `Model#export` seçenek belgesi** şu dışa aktarıcıları listeler: 3DS, DWG/DXF, DAE, FBX, KMZ, GLB, IFC, PDF (Windows ve Mac ayrı seçenekler), STL (ASCII/ikili), XSI, USDZ, VRML, OBJ. **İçe aktarıcı belgesi:** 3DS, DAE, DEM/DDF, DWG/DXF, IFC, KMZ, STL.
- **IFC:** SketchUp 2026'da **yeni bir IFC dışa aktarıcısı** devreye girdi (yeni seçenekler: IFC sürümü, standart IFC hiyerarşisi, yalnız seçim, mozaiklenmiş/tessellated geometri). 2026.2'de IFC içe aktarıcısına `preserve_origin` ve `tags_handling` seçenekleri eklendi.
- **Model#export belgesine göre** web tabanlı ücretsiz sürüm (SketchUp Free) yalnızca DAE ve KMZ dışa aktarır; Pro çok daha fazla biçim destekler.
- **"Go" planı ve CAD biçimleri:** Biçim sayfası DWG/DXF vb. için "Go"yu da listeler; ancak resmi plan sayfasına göre Go'da masaüstü modelleyici yoktur (yalnız web ve iPad). Bu biçimlerin Go'da hangi platformda (web/iPad) ve hangi yönde kullanılabildiği **doğrulanamadı**.
- **OBJ içe aktarma:** Resmi listede OBJ yalnızca dışa aktarma olarak geçer; yerel OBJ içe aktarma resmi kaynakta bulunamadı (**doğrulanamadı**).
- 2B dışa aktarmada Windows PDF dışa aktarıcısının çizgi kalınlığı, kesit/profil çizgileri, tam ölçek, yazı tipi ve birim gibi geniş seçenekleri vardır. Raster görüntü dışa aktarımı API'de `View#write_image` ile yapılır.
- Plan sayfası Pro için "geliştirilmiş IFC ve DWG uyumluluğu", Studio için "işlenmiş (rendered) animasyon ve 360° panorama dışa aktarımı" belirtir.

---

## 4. Eklentiler, 3D Warehouse, LayOut, Style Builder, sürümler, sistem gereksinimleri, grafik motoru

### 4.1 Eklenti (extension) sistemi

- **Dil:** Eklentiler gömülü **Ruby** yorumlayıcısı üzerinde çalışır; performans veya kod koruma için Ruby C eklentileri (C/C++) de kullanılabilir. 2026 sürümündeki Ruby sürüm numarası sürüm notlarında belirtilmemiştir (**doğrulanamadı**).
- **Paket:** `.rbz` = yeniden adlandırılmış ZIP. İçinde **tam olarak iki öğe** bulunur: kök `.rb` yükleyici dosyası ve aynı adlı destek klasörü. Kök dosya yalnızca `SketchupExtension` nesnesi oluşturup `Sketchup.register_extension` ile kaydeder; mantık içermez.
- **Kurallar (Extension Warehouse gereksinimleri):** tüm kod tek ve benzersiz bir modül ad alanında; global değişken yok; Ruby API sınıflarını yamalamak (monkey patching), `$LOAD_PATH`/`ENV` değiştirmek, `eval`/`exit`, gem kurmak yasak; dosyalar `Sketchup.require` ile uzantısız yüklenir (şifreli `.rbe` için); kendi kendini güncelleme yasak; model sessizce kaydedilmemeli; çok adımlı işlemler tek undo işlemi olmalı; arayüz için `UI::HtmlDialog` (Chromium Embedded Framework tabanlı; 2026.0'da CEF 137) kullanılmalı. Statik analiz için `rubocop-sketchup` önerilir.
- **İmzalama ve şifreleme:** Extension Warehouse'a yükleme veya **Extension Signing Portal** paket içine bir imza dosyası ekler; SketchUp kurulum/yükleme sırasında imzayı içerikle karşılaştırır. İmzasız veya imzası tutmayan eklentinin yüklenip yüklenmeyeceği kullanıcının **Yükleme Politikasına (Loading Policy)** bağlıdır; üç politika vardır (adları bu araştırmada resmi sayfadan teyit edilemedi — **doğrulanamadı**). İsteğe bağlı `.rbe` şifrelemesi yapılır (kök `.rb` şifrelenmez; eski `.rbs` kaldırıldı). Resmi belge hiçbir şifrelemenin tam koruma sağlamadığını vurgular.
- **Extension Warehouse:** eklenti dağıtım mağazası; Go planında yoktur, Pro ve Studio'da vardır. 2026.1'de `UI.show_extension_warehouse` ve HtmlDialog için `sketchup.launchEW` / `sketchup.installRBZ` JavaScript geri çağrıları eklendi. Kurulum masaüstünde Extension Manager üzerinden `.rbz` yüklenerek de yapılabilir.
- **Geliştirici araçları (GitHub "SketchUp" org):** `ruby-api-stubs` (IDE otomatik tamamlama), `rubocop-sketchup`, `testup-2` (SketchUp içinde Minitest), `sketchup-ruby-debugger`, `sketchup-ruby-api-tutorials`, `ruby-c-extension-examples`, `sketchup-extension-vscode-project`, `api-issue-tracker`, `sketchup-live-c-api` (C API belgesinde bağlantısı var).

**Kendi uygulamamız için çıkarım:** Eklenti sistemi; (a) betik dili yorumlayıcısı gömme, (b) kararlı ve belgelenmiş bir nesne modeli API'si, (c) araç/gözlemci/overlay/menü/araç çubuğu kayıt noktaları, (d) HTML tabanlı diyalog altyapısı, (e) paket biçimi + imza doğrulama + yükleme politikası, (f) merkezi mağaza bileşenlerinden oluşur.

### 4.2 3D Warehouse entegrasyonu

- 3D Warehouse, kullanıcıların paylaştığı modeller/bileşenler için çevrim içi kütüphanedir. Masaüstünde **Bileşenler (Components) panelinin Seç sekmesinden** hazır koleksiyonlar (ör. Mimari, Peyzaj) gezilebilir, aranabilir ve indirilen bileşen doğrudan modele yerleştirilebilir. Coğrafi konumlu modellerde "Yakındaki Modeller" koleksiyonu görünür. Tam model indirilirken mevcut modele yükleme veya diske kaydetme seçeneği sunulur.
- Plan sayfasına göre tüm planlarda sınırsız indirme vardır.

### 4.3 LayOut ve Style Builder (ayrı uygulamalar)

- **LayOut:** SketchUp modellerinden 2B belge ve sunum (paftalar, çizim setleri) hazırlayan eşlik uygulaması. Belge penceresine SketchUp modelleri, görüntüler, metinler eklenir; CAD verisi içe aktarılabilir; panel tabanlı arayüz, durum çubuğunda ölçü kutusu vardır. Pro ve Studio planlarında bulunur (Go'da yok). `.layout` dosyaları da dahili olarak inç birimini kullanır. Ruby API içinde `Layout` modülü (2018+) ile LayOut belgeleri programla oluşturulabilir/düzenlenebilir.
- **Style Builder:** Kullanıcının kendi çizimlerinden "eskiz (sketchy) kenar" stilleri üreten ayrı uygulama. Resmi Yardım Merkezi'ne göre **artık SketchUp indirmesine dahil değil ve desteklenmeyecek**; 2023 ve 2024 masaüstü paketlerinde hâlâ bulunuyor; bu sürümlerin desteği bitince indirilemez olacak. Tam olarak hangi sürümde çıkarıldığı sayfada yazmıyor (**doğrulanamadı**; 2025'ten itibaren olduğu çıkarımı). Alternatif olarak Stiller panelindeki hazır stiller (Sketchy Edges, Ambient Occlusion vb.) önerilir.

### 4.4 Sürümler / planlar (resmi plan sayfasına göre, fiyatlar hariç)

| Özellik | Go | Pro | Studio |
|---|---|---|---|
| Web ve iPad modelleyici | Var | Var | Var |
| **Masaüstü modelleyici** | Yok | Var | Var |
| LayOut | Yok | Var | Var |
| Extension Warehouse | Yok | Var | Var |
| Dynamic Components | Yok | Var | Var |
| Live Components | Var | Var | Var |
| PreDesign | Yok | Var | Var |
| Fotogerçekçi malzemeler | Yalnız iPad | Var | Var |
| Environments | Var | Var | Var |
| Scan Essentials (nokta bulutu) | Yok | Yok | Var |
| Revit Importer | Yok | Yok | Var |
| V-Ray | Yok | Yok | Var |
| 3D Warehouse | Sınırsız indirme | Sınırsız | Sınırsız |
| Trimble Connect | Sınırsız bulut | Sınırsız | Sınırsız |

- Pro için eklenti paketleri de vardır (ör. "Pro Scan", "Pro Advanced Workflows" — bunlar Windows'a özgü olarak belirtilmiş). Eğitim planları ayrıca listelenir.
- Ücretsiz web sürümü (SketchUp Free) plan sayfasında görünmüyor; ancak biçim sayfası ve Ruby belgesi "Free"den söz ediyor. Güncel durumu **doğrulanamadı**.

### 4.5 SketchUp 2026 — Windows sistem gereksinimleri (resmi Yardım Merkezi)

- Sayfa desteklenen sürümleri **2024, 2025, 2026** olarak verir; 2023 ve öncesi desteklenmez.
- **İşletim sistemi:** Yalnızca **Windows 11** bölümü vardır. Windows 10, ARM64 ve 32 bit hakkında sayfada bilgi yoktur (**doğrulanamadı**).
- **Yazılım:** Kurulum ve abonelik yetkilendirmesi için internet; .NET Framework 4.5.2.
- **Önerilen donanım** (sayfada ayrı bir "minimum" bölümü yok):
  - İşlemci: 2+ GHz
  - Grafik: CPU'dan ayrı (harici) GPU, en az **8 GB VRAM**; klasik işleyici için **OpenGL 3.1+** sürücü desteği
  - PBR, Ambient Occlusion ve Image Based Lighting kullanan modellerde sayfada **en az 32 GB VRAM** yazmaktadır (değer alışılmadık derecede yüksek; resmi sayfada böyle yazdığı teyit edildi ancak gerçek ihtiyaç **doğrulanamadı**)
  - RAM: en az 8 GB; Disk: en az 6 GB boş alan; 3 tuşlu tekerlekli fare

### 4.6 Grafik motoru notları

- **SketchUp 2024** ile Windows'ta **yeni grafik motoru** geldi (PBR doku haritalarını destekler). Gereksinim: **DirectX 12, feature level 11.0**. Intel HD Graphics 4x00 serisi desteklenmez. Cihaz uygun değilse SketchUp otomatik olarak **klasik (OpenGL 3.1+)** motora döner; Tercihler > Grafik'ten motorlar arasında geçiş yapılabilir.
- Ruby API: `View#graphics_engine` (2024.0+) `:graphics_engine_classic` veya `:graphics_engine_2024` döndürür. Araçların `view.draw` çağrıları OpenGL ilkel adlarını (GL_LINES vb.) kullanmaya devam eder; bu, çizim API'sinin alttaki grafik arka ucundan soyutlandığını gösterir (çıkarım).
- 2025: monitör başına DPI ve mantıksal piksel desteği; ortam/IBL (Environments); PBR malzemeler. 2026: işleme seçeneklerine Ambient Occlusion eklendi.

---

## 5. Benzer uygulama geliştirmek için yaygın açık kaynak kütüphaneler ve yaklaşımlar

> Bu bölüm **yalnızca bilgilendirme amaçlıdır**, karar içermez. Liste genel alan bilgisine dayanır; bağlantılar ve lisans bilgileri bu oturumda tek tek açılıp **teyit edilmemiştir (doğrulanamadı)** — kullanım öncesinde her projenin güncel lisansı kontrol edilmelidir.

### 5.1 Geometri çekirdeği / sınır temsili (B-rep, mesh)

| Kütüphane | Kısa not | Bağlantı |
|---|---|---|
| Open CASCADE Technology (OCCT) | Tam B-rep CAD çekirdeği (NURBS, Boolean, STEP/IGES); LGPL 2.1 + istisna (lisans teyit edilmeli) | https://dev.opencascade.org/ · https://github.com/Open-Cascade-SAS/OCCT |
| CGAL | Hesaplamalı geometri; yarım-kenar (halfedge) yapıları, Nef çokyüzlüleri, Boolean, üçgenleme; GPL/LGPL + ticari çift lisans | https://www.cgal.org/ |
| Manifold | Hızlı ve sağlam mesh Boolean (katı işlemleri) kütüphanesi; Apache-2.0 | https://github.com/elalish/manifold |
| OpenMesh | Half-edge tabanlı çokgen mesh veri yapısı; BSD | https://www.graphics.rwth-aachen.de/software/openmesh/ |
| PMP Library | Çokgen mesh işleme (half-edge); MIT | https://www.pmp-library.org/ |
| Geometry Central | Yüzey mesh veri yapıları ve algoritmalar; MIT | https://geometry-central.net/ |
| libigl | Geometri işleme algoritmaları; MPL-2.0 | https://libigl.github.io/ |
| Clipper2 | 2B çokgen kırpma/ofset (Boolean) | https://github.com/AngusJohnson/Clipper2 |
| earcut / libtess2 | Delikli çokgenlerin üçgenlenmesi (yüz render'ı için) | https://github.com/mapbox/earcut.hpp · https://github.com/memononen/libtess2 |
| Shewchuk sağlam yüklemleri | Kesin geometrik yüklemler (orient/incircle) | https://www.cs.cmu.edu/~quake/robust.html |
| Eigen / GLM | Doğrusal cebir / grafik matematik | https://eigen.tuxfamily.org/ · https://github.com/g-truc/glm |
| Embree | CPU ışın izleme (seçme/raytest, işleme) | https://github.com/RenderKit/embree |

**Yaklaşım notu:** SketchUp benzeri bir "yapışkan geometri" modelleyicisi genellikle NURBS çekirdeği yerine **düzlemsel çokgen yüzlere dayalı, manifold olmayan durumlara izin veren bir B-rep** (radial-edge veya genişletilmiş half-edge) ile yapılır; Boolean işlemleri için ayrıca sağlam bir mesh Boolean kütüphanesi (ör. Manifold, CGAL) kullanılabilir.

### 5.2 Görüntüleme (rendering)

| Seçenek | Kısa not | Bağlantı |
|---|---|---|
| Direct3D 12 / 11 | Windows yerel API'si (SketchUp'ın yeni motoru DX12 FL 11.0 istiyor) | https://learn.microsoft.com/windows/win32/direct3d12/ |
| OpenGL | Taşınabilir, eski ama yaygın (SketchUp klasik motoru) | https://www.khronos.org/opengl/ |
| Vulkan | Modern, düşük seviye, çok platformlu | https://www.vulkan.org/ |
| WebGPU (Dawn / wgpu) | Yerel uygulamada da kullanılabilen modern soyutlama | https://dawn.googlesource.com/dawn · https://wgpu.rs/ |
| bgfx | Çoklu arka uçlu (DX/GL/Vulkan/Metal) render soyutlaması | https://github.com/bkaradzic/bgfx |
| Diligent Engine | Çoklu arka uçlu modern grafik katmanı | https://github.com/DiligentGraphics/DiligentEngine |
| Filament | PBR odaklı gerçek zamanlı render motoru | https://github.com/google/filament |

### 5.3 Kullanıcı arayüzü (UI)

| Seçenek | Kısa not | Bağlantı |
|---|---|---|
| Qt (Widgets/QML) | C++ masaüstü arayüz; LGPL/GPL/ticari | https://www.qt.io/ |
| Dear ImGui | Anlık mod (immediate-mode) araç arayüzleri; MIT | https://github.com/ocornut/imgui |
| WinUI 3 / WPF (.NET) | Windows yerel arayüz çerçeveleri | https://learn.microsoft.com/windows/apps/winui/ |
| Avalonia | .NET çok platformlu arayüz | https://avaloniaui.net/ |
| wxWidgets | Yerel görünümlü C++ arayüz | https://www.wxwidgets.org/ |
| CEF / WebView2 | HTML tabanlı paneller (SketchUp HtmlDialog için CEF kullanıyor) | https://bitbucket.org/chromiumembedded/cef · https://learn.microsoft.com/microsoft-edge/webview2/ |

### 5.4 Dosya biçimi kütüphaneleri

| Kütüphane | Biçim | Bağlantı |
|---|---|---|
| Assimp | Çok sayıda 3B biçim (OBJ, FBX, DAE, 3DS, STL, glTF …) içe/dışa | https://github.com/assimp/assimp |
| tinygltf / cgltf | glTF/GLB | https://github.com/syoyo/tinygltf · https://github.com/jkuhlmann/cgltf |
| tinyobjloader | OBJ | https://github.com/tinyobjloader/tinyobjloader |
| ufbx | FBX okuma | https://github.com/ufbx/ufbx |
| OpenUSD | USD/USDZ | https://github.com/PixarAnimationStudios/OpenUSD |
| IfcOpenShell | IFC okuma/yazma; LGPL | https://ifcopenshell.org/ |
| LibreDWG | DWG (GPLv3 — kapalı kaynak ürünle uyumu dikkat ister) | https://www.gnu.org/software/libredwg/ |
| Open Design Alliance (ODA) | DWG/DXF için ticari, üyelik tabanlı SDK (açık kaynak değil) | https://www.opendesign.com/ |
| libdxfrw | DXF | https://github.com/LibreCAD/libdxfrw |
| PDFium / libharu | PDF okuma / yazma | https://pdfium.googlesource.com/pdfium/ · https://github.com/libharu/libharu |

### 5.5 Mimari yaklaşımlar (genel bilgi)

- **Komut (command) deseni + işlem günlüğü** ile geri al/yinele; işlem başlangıç/bitiş sınırları ve değişiklik bildirimleri (SketchUp'ın operation/observer modeline karşılık).
- **Sahne grafiği (scene graph):** Tanım/örnek (definition/instance) ayrımı ve örnek başına dönüşüm matrisi; örnekleme (instancing) ile GPU'da tekrar çizim.
- **Uzamsal indeksleme:** BVH / octree ile seçme, çıkarım ve yapışma sorguları.
- **Gömülü betik dili:** Ruby (MRI gömme), Python (pybind11), Lua veya .NET tabanlı eklenti sistemi.

---

## 6. Belgeler arasında fark edilen çelişkiler / dikkat noktaları

1. **IFC içe aktarma seçenekleri sürümü:** Ruby "Importer Options" sayfası `preserve_origin` ve `tags_handling` seçeneklerini "SketchUp 2027+" başlığı altında veriyor; Ruby sürüm notları ise bunları **2026.2**'ye ekliyor. Hangisinin doğru olduğu **doğrulanamadı** (sürüm notları daha olası görünüyor).
2. **32 GB VRAM ifadesi:** Resmi sistem gereksinimleri sayfası PBR/AO/IBL için "en az 32 GB VRAM" diyor; tüketici GPU'ları için olağan dışı. Yazım hatası olup olmadığı **doğrulanamadı**.
3. **glTF uzantısı:** Biçim sayfasında glTF için ".gib" yazıyor (büyük olasılıkla ".glb" yazım hatası); Ruby exporter belgesi GLB dışa aktarıcısını listeliyor.
4. **Go planı ve CAD/3B biçimler:** Biçim sayfası Go'yu DWG/DXF/3DS vb. için listeliyor, ancak plan sayfasına göre Go'da masaüstü uygulaması yok. Go'daki biçim desteğinin platformu (web/iPad) **doğrulanamadı**.
5. **Windows 10 desteği:** Resmi gereksinim sayfası yalnızca Windows 11'i listeliyor; üçüncü taraf kaynaklarda çelişkili iddialar var. Windows 10'un 2026'da destekli olup olmadığı **doğrulanamadı**.
6. **Tolerans değeri:** Sayısal tolerans (0.001) yalnızca C API belgesinde açıkça geçiyor; Ruby belgesi değeri vermiyor.

---

## Kaynaklar

Bu belgede gerçekten açılıp kullanılan resmi sayfalar:

**Ruby API (ruby.sketchup.com)**
- https://ruby.sketchup.com/
- https://ruby.sketchup.com/_index.html
- https://ruby.sketchup.com/Sketchup/Entity.html
- https://ruby.sketchup.com/Sketchup/Drawingelement.html
- https://ruby.sketchup.com/Sketchup/Model.html
- https://ruby.sketchup.com/Sketchup/Entities.html
- https://ruby.sketchup.com/Sketchup/EntitiesBuilder.html
- https://ruby.sketchup.com/Sketchup/Edge.html
- https://ruby.sketchup.com/Sketchup/Face.html
- https://ruby.sketchup.com/Sketchup/Vertex.html
- https://ruby.sketchup.com/Sketchup/Loop.html
- https://ruby.sketchup.com/Sketchup/EdgeUse.html
- https://ruby.sketchup.com/Sketchup/Curve.html
- https://ruby.sketchup.com/Sketchup/ArcCurve.html
- https://ruby.sketchup.com/Sketchup/Group.html
- https://ruby.sketchup.com/Sketchup/ComponentDefinition.html
- https://ruby.sketchup.com/Sketchup/ComponentInstance.html
- https://ruby.sketchup.com/Geom/Transformation.html
- https://ruby.sketchup.com/Sketchup/Material.html
- https://ruby.sketchup.com/Sketchup/Texture.html
- https://ruby.sketchup.com/Sketchup/Layer.html
- https://ruby.sketchup.com/Sketchup/LayerFolder.html
- https://ruby.sketchup.com/Sketchup/Page.html
- https://ruby.sketchup.com/Sketchup/Camera.html
- https://ruby.sketchup.com/Sketchup/Style.html
- https://ruby.sketchup.com/Sketchup/SectionPlane.html
- https://ruby.sketchup.com/Sketchup/AttributeDictionary.html
- https://ruby.sketchup.com/Sketchup/Selection.html
- https://ruby.sketchup.com/Sketchup/EntitiesObserver.html
- https://ruby.sketchup.com/Sketchup/ModelObserver.html
- https://ruby.sketchup.com/Sketchup/Tool.html
- https://ruby.sketchup.com/Sketchup/Tools.html
- https://ruby.sketchup.com/Sketchup/InputPoint.html
- https://ruby.sketchup.com/Sketchup/View.html
- https://ruby.sketchup.com/Sketchup/Overlay.html
- https://ruby.sketchup.com/Length.html
- https://ruby.sketchup.com/file.ReleaseNotes.html
- https://ruby.sketchup.com/file.generating_geometry.html
- https://ruby.sketchup.com/file.exporter_options.html
- https://ruby.sketchup.com/file.importer_options.html
- https://ruby.sketchup.com/file.extension_requirements.html
- https://ruby.sketchup.com/file.intellectual_property.html

**C SDK ve geliştirici merkezi**
- https://extensions.sketchup.com/developers/sketchup_c_api/sketchup/index.html
- https://developer.sketchup.com/

**Yardım Merkezi (help.sketchup.com)**
- https://help.sketchup.com/en/sketchup/system-requirements
- https://help.sketchup.com/en/sketchup/using-sketchup-data-other-modeling-programs-or-tools
- https://help.sketchup.com/en/sketchup-schools/exporting-model
- https://help.sketchup.com/en/extension-warehouse/extension-encryption-and-signing
- https://help.sketchup.com/en/style-builder/introducing-style-builder-interface
- https://help.sketchup.com/en/style-builder/end-of-life-style-builder
- https://help.sketchup.com/en/layout/introducing-layout-interface (arama sonucu özeti üzerinden)
- https://help.sketchup.com/en/sketchup/components-3d-warehouse (arama sonucu özeti üzerinden)

**Trimble resmi plan sayfası**
- https://sketchup.trimble.com/plans-and-pricing

**GitHub**
- https://github.com/SketchUp

> Not: §5'teki açık kaynak kütüphane bağlantıları bu oturumda açılmadı; genel bilgiye dayanır ve **doğrulanamadı** olarak değerlendirilmelidir. Üçüncü taraf blog/forum sayfaları arama sonuçlarında görüldü ancak bilgi kaynağı olarak kullanılmadı.
