# Kurulu SketchUp 2026 Dosyalarının İncelemesi

Bu belge, kullanıcının bilgisayarında kurulu SketchUp 2026'nın **açık metin ve yapılandırma dosyalarından** çıkarılan bilgileri içerir (inceleme tarihi: 2026-10-20).

**Yöntem ve sınır:** EXE/DLL dosyaları çözülmedi, şifreli Ruby eklentileri (`.rbe`, `.rbs`) açılmadı, `login_session.dat` okunmadı. Yalnızca dosya/klasör adları, `opensourcecredits.html`, `Resources/en-US/commands.json` ve kullanıcının `SharedPreferences.json` dosyası okundu. Buradaki İngilizce açıklama metinleri yalnızca referanstır; uygulamaya kopyalanmaz.

## 1. Teknoloji yığını (dosya adlarından ve açık kaynak bildirimlerinden)

| Katman | SketchUp 2026'da görülen | Kanıt |
|---|---|---|
| Ana dil | C++ (Windows x64) | `SketchUp.exe` (~49 MB), boost, abseil, {fmt}, robin-map, GSL |
| Arayüz çatısı | Qt 6 (Widgets + QML/Qt Quick) | `Qt6*.dll`, `QtQml/`, `QtQuick/` klasörleri |
| Yanaşan paneller (tray) | KDDockWidgets | `kddockwidgets-qt6.dll` |
| Yeni grafik motoru | Direct3D 12 | `D3D12MemAlloc`, `d3dx12.h`, `d3dcompiler_47.dll` |
| Eski/yedek grafik yolu | OpenGL (ANGLE, GLEW) | `libEGL.dll`, `libGLESv2.dll`, GLEW, GLFW |
| Matematik | glm | açık kaynak bildirimi |
| Çokgen işlemleri | Clipper (çokgen kırpma), libtess (delikli yüz üçgenleme) | açık kaynak bildirimi |
| Katı/CAD çekirdeği | OpenCascade (büyük olasılıkla içe/dışa aktarma için; kullanım yeri doğrulanamadı) | açık kaynak bildirimi |
| 2D çizim | Skia, FreeType, nanosvg | açık kaynak bildirimi |
| Paralel işlem | Intel TBB | `tbb.dll` |
| HTML diyaloglar | Chromium Embedded Framework (CEF) + Vue/jQuery | `libcef.dll`, `cef/`, `Dialogs/` |
| Betik/eklenti dili | Ruby 3.2 gömülü | `x64-ucrt-ruby320.dll`, `Tools/RubyStdLib` |
| Dosya biçimleri | COLLADA, lib3ds, USD, GDAL, OpenImageIO, OpenEXR, libpng/jpeg/tiff, PDFlib, xlnt | DLL ve bildirimler |
| Genel API | `SketchUpAPI.dll` (C SDK), `SketchUpViewerAPI.dll` | DLL adları |
| Hata raporlama | BugSplat | `BugSplat64.dll` |

**Çıkarım:** SketchUp, C++ çekirdek + Qt arayüzü + D3D12/OpenGL çizim + gömülü Ruby yapısındadır. Geometri çekirdeği büyük olasılıkla kendi yazdıkları bir yapıdır (OpenCascade'in çekirdek modelleme için kullanıldığına dair kanıt yok — doğrulanamadı).

## 2. Kurulum klasörü yapısı (`C:\Program Files\SketchUp\SketchUp 2026\SketchUp`)

| Klasör | İçerik (dosya türleri) |
|---|---|
| `Resources/en-US` | Komut kataloğu (`commands.json`), arayüz metinleri (`.strings`), örnek modeller, yardım HTML'leri |
| `Tools` | Ruby standart kütüphanesi, `sketchup.rb`, `extensions.rb`, `langhandler.rb` (açık), 259 şifreli `.rbs` |
| `ShippedExtensions` | Hazır gelen eklentiler (şifreli `.rbe`, ikonlar, DLL'ler) |
| `Importers` / `Exporters` | Her biçim için ayrı DLL eklentisi |
| `plugins` | 18 yerel eklenti DLL'i |
| `Dialogs`, `welcomescreen`, `cef` | HTML/JS tabanlı diyaloglar ve karşılama ekranı |
| `QtQml`, `QtQuick` | QML arayüz modülleri |
| `usd` | USD biçim desteği |
| `ShippedClassifications` | IFC sınıflandırma şemaları (`.skc`) |

Kullanıcı verileri `%APPDATA%\SketchUp\SketchUp 2026\SketchUp` altındadır: `Components`, `Materials`, `Styles`, `Templates`, `Environments`, `Plugins`, `Classifications`, `SharedPreferences.json`.

**Bizim için mimari dersi:** komutlar tek bir katalogda (anahtar, başlık, menü etiketi, ipucu, açıklama, arama etiketleri) tanımlanıyor; içe/dışa aktarıcılar ayrı eklentiler; diyaloglar ayrı bir arayüz katmanı; kullanıcı kitaplıkları (bileşen, malzeme, stil, şablon) ayrı klasörlerde.

## 3. Klavye kısayolları (kullanıcının `SharedPreferences.json` kaydı)

Kayıt biçimi `a b c TUŞ komut` şeklindedir; üç bayrak değiştirici tuşları gösterir. Üçüncü bayrağın Shift olduğu `Shift+Z = Zoom Extents` varsayılanından anlaşılıyor; ilk bayrağın Ctrl mı Alt mı olduğu doğrulanamadı. Bu liste kullanıcının özelleştirmelerini içerebilir.

| Tuş | Komut (iç ad) | Anlamı |
|---|---|---|
| Space | `selectSelectionTool` | Select |
| Shift+Space | `selectLassoSelectionTool` | Lasso Select |
| L | `selectLineTool` | Line |
| R | `selectRectangleTool` | Rectangle |
| C | `selectCircleTool` | Circle |
| A | `selectArcTool` | Arc |
| P | `selectPushPullTool` | Push/Pull |
| M | `selectMoveTool` | Move |
| Q | `selectRotateTool` | Rotate |
| S | `selectScaleTool` | Scale |
| F | `selectOffsetTool` | Offset |
| E | `selectEraseTool` | Eraser |
| B | `selectPaintTool` | Paint Bucket |
| T | `selectMeasureTool` | Tape Measure |
| O | `selectOrbitTool` | Orbit |
| H | `selectDollyTool` | Pan |
| Z | `selectZoomTool` | Zoom |
| Shift+Z | `viewZoomExtents` | Zoom Extents |
| I | `selectImageIglooTool` | Hide Rest of Model / izolasyon (doğrulanamadı) |
| K | View/Edge Style/Back Edges | Arka kenarlar |
| G | Edit/Make Component... | Bileşen yap |
| Shift+S | Help/Search SketchUp | Komut arama |
| (bayrak1)+H | `editHide` | Gizle |
| (bayrak1)+Shift+H | Edit/Unhide/All | Tümünü göster |

## 4. Tercih anahtarları (Preferences)

`SharedPreferences.json` içindeki genel tercihler, uygulamamızda karşılık gelecek ayarların listesi olarak kullanılabilir:

`AllowPreselectPushPull`, `AutoDrawingPlane`, `AutoSave`, `AutoSaveFreq`, `SaveBackup`, `CcwLassoPerformsCrossingSelection`, `ClickStyle`, `ContinueLineMode`, `DisplayCrosshairs`, `ShowInferenceTips`, `ColorX_RGB` / `ColorY_RGB` / `ColorZ_RGB` (eksen renkleri), `ColorParallelPerpendicular_RGB`, `ColorTangent_RGB` (çıkarım renkleri), `IntersectEdges`, `RotationGripsInMoveTool`, `EnableCheckValidity`, `NotifyAfterFix`, `VCBShowTypedInputBackground`, `VCBTimoutSeconds` (ölçü kutusu), `NewTagsVisibilityToggle`, AVI dışa aktarma ayarları, sahne geçişi (`ShowTransition`, `TransitionTime`), slayt gösterisi ayarları, `FovDisplayMode`.

Windows'a özel: yazdırma seçenekleri, harici resim düzenleyici, araç çubuğu özelleştirme, bileşen kitaplığı yolları.

## 5. Komut kataloğu (`commands.json`, 184 komut)

Her komutun bir anahtarı, başlığı, menü etiketi, ipucu, tek cümlelik açıklaması ve arama etiketleri var. Bu yapı, uygulamamızda komut sistemi ve "komut arama" özelliği için örnek alınabilir. Aşağıdaki liste uygulanacak işlevlerin envanteridir.

| # | Key | Title | Menu label | Description (EN) |
|---|---|---|---|---|
| 1 | `3d text` | 3D Text | 3D Text | Insert three-dimensional text. |
| 2 | `accessibility preferences` | Open Accessibility Settings |  | Open Accessibility Preference Page. |
| 3 | `align view` | Align View | Align View | Align the camera to a straight-on view of a selected face. |
| 4 | `ambient occlusion` | Ambient Occlusion | Ambient Occlusion | Increase perceived depth by applying shading to the intersection of faces. |
| 5 | `area > selection` | Area |  | Display the area of a selected face. |
| 6 | `camera/field of view` | Field of View | Field of View | See more or less of your model by changing the angle of SketchUp's camera. |
| 7 | `camera/look around` | Look Around | Look Around | Look around a model from a stationary viewpoint. |
| 8 | `camera/orbit` | Orbit | Orbit | Navigate by reorienting the camera view. |
| 9 | `camera/pan` | Pan | Pan | Move your view horizontally or vertically. |
| 10 | `camera/parallel` | Parallel Projection | Parallel Projection | Switch the camera between a perspective and orthographic view. |
| 11 | `camera/perspective` | Perspective | Perspective | Switch the camera between an orthographic and perspective view. |
| 12 | `camera/position camera` | Position Camera | Position Camera | Position your view at a specific place and eye height to check line of sight or walk through model. |
| 13 | `camera/two-point perspective` | Two-Point Perspective | Two-Point Perspective | Display your model with a perspective view that has two vanishing points. |
| 14 | `camera/walk` | Walk | Walk | Walk through a model by clicking and holding where you want to go. |
| 15 | `camera/zoom` | Zoom | Zoom | Move your view in or out. |
| 16 | `camera/zoom extents` | Zoom Extents | Zoom Extents | Zoom to fit the entire model on your screen. |
| 17 | `camera/zoom window` | Zoom Window | Zoom Window | Zoom to a selected area of the screen. |
| 18 | `check for update` | Check for Update | Check for Update | Check if an updated version of SketchUp is available for installation. |
| 19 | `color tags` | Color By Tag |  | Display objects as shaded by their assigned tag's color. |
| 20 | `create new model` | Create New Model |  | Start a new model from the default template. |
| 21 | `create scene` | Create Scene |  | Save the current view and style settings of the model. |
| 22 | `deselect edges` | Deselect Edges | Deselect Edges | Remove all edges from a selection including raw geometry. |
| 23 | `deselect faces` | Deselect Faces | Deselect Faces | Remove all faces from a selection including raw geometry. |
| 24 | `diffusion` | Diffusion |  | Use AI to generate images based on your model. |
| 25 | `display crosshairs` | View Crosshairs |  | Toggle on and off references axes that follow the cursor while modeling. |
| 26 | `divide` | Divide |  | Segment a selected edge into any number of divisions. |
| 27 | `download` | Download SKP |  | Download the current model as a .SKP file. |
| 28 | `edit/close group/component` | Close Group/Component | Close Group/Component | Close an object that is being edited. |
| 29 | `edit/copy` | Copy | Copy | Copy selection to place with the Move tool. |
| 30 | `edit/cut` | Cut | Cut | Remove selection to place with the Move tool. |
| 31 | `edit/delete` | Delete | Delete | Get rid of selected entities. |
| 32 | `edit/delete guides` | Delete Guides | Delete Guides | Delete all the dashed guide lines in your model. |
| 33 | `edit/hide` | Hide | Hide | Make selected entities invisible. |
| 34 | `edit/lock` | Lock | Lock | Prevent selection from being editable. |
| 35 | `edit/make component` | Make Component | Make Component... | Define selected geometry as an object that can be updated across multiple copies. |
| 36 | `edit/make group` | Make Group | Make Group | Define selected geometry as an independent object. |
| 37 | `edit/paste` | Paste | Paste | Place a cut or copied selection with the Move tool. |
| 38 | `edit/paste in place` | Paste in Place | Paste In Place | Paste geometry in the exact location from which it was cut or copied. |
| 39 | `edit/redo` | Redo | Redo | After undoing an action, do it again. |
| 40 | `edit/select all` | Select All | Select All | Select everything in the modeling context. |
| 41 | `edit/select none` | Select None | Select None | Deselect everything in the modeling context. |
| 42 | `edit/undo` | Undo | Undo | Reverse your most recent action. |
| 43 | `edit/unhide all` | Unhide All | All | Make all hidden objects visible. |
| 44 | `edit/unhide last` | Unhide Last | Last | Make the last hidden object visible. |
| 45 | `edit/unhide selected` | Unhide Selected | Selected | Make selected hidden geometry visible. |
| 46 | `edit/unlock all` | Unlock All | All | Make all locked objects editable. |
| 47 | `edit/unlock selected` | Unlock Selected | Selected | Make selected locked object editable. |
| 48 | `edit/weld edges` | Weld Edges | Weld Edges | Join a selection of edges into one polyline for smooth extrusions. |
| 49 | `enable grid snapping` | Grid Snapping |  | Toggle on or off cursor snapping to grid intersections. |
| 50 | `explode` | Explode | Explode | Ungroup selected object. |
| 51 | `export 3ds` | Export 3DS |  | Generate a 3DS file from your model. |
| 52 | `export dae` | Export Collada |  | Generate a DAE (Collada) file from your model. |
| 53 | `export dwg (2d)` | Export DWG (2D) |  | Generate a scaled 2D DWG file from your current model view.. |
| 54 | `export dwg (3d)` | Export DWG (3D) |  | Generate a 3D DWG file from your model. |
| 55 | `export dxf (2d)` | Export DXF (2D) |  | Generate a scaled 2D DXF file from your current model view.. |
| 56 | `export dxf (3d)` | Export DXF (3D) |  | Generate a 2D DXF file from your model. |
| 57 | `export fbx` | Export FBX |  | Generate a FBX file from your model. |
| 58 | `export kmz` | Export KMZ |  | Generate a KMZ file from your model. |
| 59 | `export obj` | Export OBJ |  | Generate an OBJ file from your model. |
| 60 | `export png` | Export Image |  | Capture a snapshot of your modeling window to export as a PNG image. |
| 61 | `export stl` | Export STL |  | Generate a STL file from your model. |
| 62 | `export vrml` | Export VRML |  | Generate a VRML file from your model. |
| 63 | `export xsi` | Export XSI |  | Generate a XSI file from you model. |
| 64 | `extension manager` | Extension Manager | Extension Manager | Manage installed extensions: update, disable, or uninstall. |
| 65 | `extension warehouse` | Extension Warehouse | Extension Warehouse | Find and install extensions that add custom, enhanced functionality to SketchUp. |
| 66 | `flip along tool` | Flip | Flip | Flip or mirror selected entities. |
| 67 | `home tab` | Home |  | Open the Home tab to access recent files, stored files, or account information. |
| 68 | `insert` | Insert |  | Import a 3D model, 2D image, or CAD data into your model. |
| 69 | `intersect faces with context` | Intersect Faces with Context | With Context | Creates edge where faces within the same group or component intersect. |
| 70 | `intersect faces with model` | Intersect Faces with Model | With Model | Creates new edges where selected faces intersect with other faces. |
| 71 | `intersect faces with selection` | Intersect Faces with Selection | With Selection | Creates new edges only where selected faces intersect with other selected faces. |
| 72 | `invert selection` | Invert Selection | Invert Selection | Select everything in the modeling context that isn't currently selected. |
| 73 | `launch 3dw` | 3D Warehouse | 3D Warehouse... | Find objects and materials to use in your model |
| 74 | `launch settings` | Settings |  | Customize your modeling settings and preferences. |
| 75 | `make unique texture` | Make Unique Texture |  | Make the texture of a selected face a unique material in the model. |
| 76 | `manage licensing` | Manage Licensing | Manage Licensing | View and manage licensing details for your account |
| 77 | `move rotate grips` | Move Rotation Grips |  | Toggle on or off rotation grips that appear when using the Move tool. |
| 78 | `new from template` | New from Template | New From Template... | Create a new model, choosing from default or custom templates |
| 79 | `next scene` | Next Scene |  | Jump your view to the next scene. |
| 80 | `open` | Open |  | Load a model into SketchUp. |
| 81 | `orient faces` | Orient Faces |  | Flip faces of a model to match the orientation of one selected face. |
| 82 | `play animation` | Play Animation |  | Animate between saved scenes. |
| 83 | `position texture` | Position Texture |  | Edit the size, position, and orientation of a texture on a selected face. |
| 84 | `predesign` | PreDesign | Start PreDesign... | Get information about the conditions of a location throughout the year. |
| 85 | `previous scene` | Previous Scene |  | Jump your view to the previous scene. |
| 86 | `print` | Print |  | Send a view of your model to print preview. |
| 87 | `project texture` | Project Texture |  | Apply a texture more evenly to a selection of curved or complex surfaces. |
| 88 | `purge unused` | Purge Unused | Purge Unused Assets | Purge unused components, materials, or styles to clean up your model. |
| 89 | `reset shortcuts` | Reset Shortcuts |  | Restore default shortcut settings and clear out customized shortcuts. |
| 90 | `reset texture` | Reset Texture |  | Reset changes you've made to the size or orientation of a texture on a selected face. |
| 91 | `reverse faces` | Reverse Faces |  | Swap the orientation of front and back faces for a selection. |
| 92 | `save as` | Save As |  | Save the model as a new file. |
| 93 | `scenes` | Scenes |  | Save and return to views of a model by creating scenes. |
| 94 | `search` | Search SketchUp |  | Find and activate tools by searching for them. |
| 95 | `set sun location` | Set Sun Location |  | Define the sun location in a model by clicking anywhere on the Environment thumbnail. |
| 96 | `shortcuts preferences` | Open Shortcuts Settings |  | Open Shortcuts Preference Page. |
| 97 | `show new tags` | Show New Tags |  | Control whether new tags are visible in existing scenes. |
| 98 | `soften/smooth edges` | Soften/Smooth Edges |  | Make a selection seem more realistic by hiding edges or adding shading to faces. |
| 99 | `tools/2 point arc` | 2 Point Arc | 2 Point Arc | Draw an arc by defining endpoints and a bulge. |
| 100 | `tools/3 point arc` | 3 Point Arc | 3 Point Arc | Draw an arc by defining 3 points on the circumference. |
| 101 | `tools/arc` | Arc | Arc | Draw an arc by defining a centerpoint, radius, and end point. |
| 102 | `tools/axes` | Axes | Axes | Move or reorient the drawing axes for an entire model, or a singular group or component. |
| 103 | `tools/circle` | Circle | Circle | Draw circles from centerpoint to radius. |
| 104 | `tools/comment` | Add Comment | Comment | Create a new comment on an object for collaborative feedback. |
| 105 | `tools/dimension` | Dimensions | Dimensions | Create linear dimensions by defining two points. |
| 106 | `tools/erase` | Eraser | Eraser | Delete, hide, soften or smooth geometry. |
| 107 | `tools/follow me` | Follow Me | Follow Me | Extrude a face along a path of edges. |
| 108 | `tools/freehand` | Freehand | Freehand | Draw irregular, hand-drawn lines or shapes. |
| 109 | `tools/intersect` | Intersect | Intersect | Combine multiple intersecting solids and only keep their overlapping geometry. |
| 110 | `tools/lasso` | Lasso | Lasso | Draw selection bounds manually to make custom selections. |
| 111 | `tools/line` | Line | Line | Draw edges, lines, shapes, or profiles. |
| 112 | `tools/move` | Move | Move | Move, copy, or distort selected entities. |
| 113 | `tools/offset` | Offset | Offset | Copy edges to create outlines or insets. |
| 114 | `tools/outer shell` | Outer Shell | Outer Shell | Combine multiple solids into one and remove interior geometry. |
| 115 | `tools/paint bucket` | Paint Bucket | Paint Bucket | Assign materials and colors. |
| 116 | `tools/pie` | Pie Arc | Pie | Draw a pie-shaped face by defining a centerpoint, radius, and end point. |
| 117 | `tools/polygon` | Polygon | Polygon | Draw polygons with a desired number of sides. |
| 118 | `tools/protractor` | Protractor | Protractor | Measure angles and create angled-based guide lines. |
| 119 | `tools/push/pull` | Push/Pull | Push/Pull | Extrude faces to create 3D forms. |
| 120 | `tools/rectangle` | Rectangle | Rectangle | Draw rectangles or squares. |
| 121 | `tools/rotate` | Rotate | Rotate | Rotate (or copy) an entity by specifying rotation center, start point, and degree. |
| 122 | `tools/rotated rectangle` | Rotated Rectangle | Rotated Rectangle | Draw rectangles at a desired angle. |
| 123 | `tools/sample material` | Sample Material |  | Sample materials or colors to use with the Paint Bucket tool. |
| 124 | `tools/scale` | Scale | Scale | Resize, stretch, or distort geometry. |
| 125 | `tools/section plane` | Section Plane | Section Plane | Create section cuts to view interior geometry. |
| 126 | `tools/select` | Select | Select | Select entities to modify with other tools or commands. |
| 127 | `tools/split` | Split | Split | Split two intersecting solids, preserving overlapping geometry in groups. |
| 128 | `tools/subtract` | Subtract | Subtract | Subtract one solid from a second selected solid; keep only the end result. |
| 129 | `tools/tag` | Tag | Tag | Apply tags to entities in model. |
| 130 | `tools/tape measure` | Tape Measure | Tape Measure | Measure distances, create guides, or scale a model. |
| 131 | `tools/text` | Text | Text | Insert text or add leader-based details. |
| 132 | `tools/trim` | Trim | Trim | Subtract one solid from a second solid. Keep the first solid and the resulting subtraction. |
| 133 | `tools/union` | Union | Union | Combine multiple solids into one and maintain interior geometry. |
| 134 | `use environment as skydome` | Use Environment as Skydome |  | Controls the visibility of the environment in the model view. |
| 135 | `use environment for reflections` | Use Environment for Reflections |  | Controls the visibility of the environment in the object reflections. |
| 136 | `view preferences` | View Preferences |  | Customize your modeling preferences. |
| 137 | `view watermarks` | View Watermarks |  | Toggle on or off the display of watermarks. |
| 138 | `view/axes` | View Axes | Axes | Toggle the visibility of model axes. |
| 139 | `view/back` | Back View | Back | View your model from behind. |
| 140 | `view/bottom` | Bottom View | Bottom | View your model from below. |
| 141 | `view/comments` | View Comments | Comments | Toggle the visibility of comment markers. |
| 142 | `view/component edit/hide rest of model` | Hide Rest of Model | Hide Rest Of Model | Hide or display the rest of your model while editing groups or components. |
| 143 | `view/component edit/hide similar components` | Hide Similar Components | Hide Similar Components | Hide or display similar components while editing a component. |
| 144 | `view/edge style/back edges` | View Back Edges | Back Edges | Toggle the visibilty of back edges. |
| 145 | `view/edge style/dashes` | View Dashes |  | Toggle the display of dashed line styles assigned to tags. |
| 146 | `view/edge style/depth cue` | Depth Cue | Depth Cue | Emphasize foreground lines over background lines. |
| 147 | `view/edge style/edges` | View Edges | Edges | Toggle the visibility of edges. |
| 148 | `view/edge style/endpoints` | View Endpoints |  | Emphasize the intersection of edges with endpoints. |
| 149 | `view/edge style/extension` | View Extensions | Extension | Extend each line slightly past its endpoint for a hand-drawn appearance. |
| 150 | `view/edge style/jitter` | View Jitter |  | Add variability to edge appearance for a hand-drawn look. |
| 151 | `view/edge style/profiles` | View Profiles | Profiles | Toggle the display of profile edges. |
| 152 | `view/face style/hidden line` | Hidden Line | Hidden Line | Display faces without any shading or textures. |
| 153 | `view/face style/monochrome` | Monochrome | Monochrome | Display faces with the default front and back face color. |
| 154 | `view/face style/shaded` | Shaded | Shaded | Display faces with shading but without textures. |
| 155 | `view/face style/shaded with photoreal materials` | Photoreal Materials | Photoreal Materials | Display faces with photoreal materials that react to environment lighting |
| 156 | `view/face style/shaded with textures` | Shaded with Textures | Shaded With Textures | Display faces with shading and any textures applied. |
| 157 | `view/face style/wireframe` | Wireframe | Wireframe | Display only model edges. |
| 158 | `view/face style/x-ray` | X-ray | X-ray | See through objects by toggling on or off face transparency. |
| 159 | `view/fog` | View Fog | Fog | Toggle the visibility of fog. |
| 160 | `view/front` | Front View | Front | View your model from the front. |
| 161 | `view/grid` | View Grid |  | Display or hide the modeling grid. |
| 162 | `view/guides` | View Guides | Guides | Toggle the visibility of guide lines. |
| 163 | `view/hidden geometry` | View Hidden Geometry | Hidden Geometry | Toggle the display of hidden edges and faces. |
| 164 | `view/hidden objects` | View Hidden Objects | Hidden Objects | Toggle the display of hidden groups and components. |
| 165 | `view/iso` | Isometric View | Iso | Display a 3D object in two dimensions; use with parallel projection. |
| 166 | `view/left` | Left View | Left | View your model from the left. |
| 167 | `view/rendering/edge/all same` | Color Edges all the Same |  | Color all edges with one color. |
| 168 | `view/rendering/edge/by axis` | Color Edges by Axis |  | Color edges by their alignment to the red, blue, or green axes. |
| 169 | `view/rendering/edge/by material` | Color Edges by Material |  | Color edges with the materials assigned from the Paint Bucket. |
| 170 | `view/rendering/ground` | View Ground |  | Toggle on and off visualization of a ground plane. |
| 171 | `view/rendering/sky` | View Sky |  | Toggle the visualization of the sky. |
| 172 | `view/rendering/transparent materials` | Toggle Transparency |  | Toggle the display of transparency in materials. |
| 173 | `view/rendering/use sun for shading` | Use Sun for Shading |  | With shadows off, use the sun simulation to shade faces. |
| 174 | `view/right` | Right View | Right | View your model from the right. |
| 175 | `view/section cuts` | View Section Cuts | Section Cuts | Toggle on or off active section cuts. |
| 176 | `view/section fill` | View Section Fill | Section Fill | Toggle the display of fill in section cuts. |
| 177 | `view/section planes` | View Section Planes | Section Planes | Toggle the visiblility of section planes. |
| 178 | `view/shadows` | View Shadows | Shadows | Toggle the visibility of shadows. |
| 179 | `view/top` | Top View | Top | View your model from above. |
| 180 | `welcome to sketchup` | Welcome to SketchUp | Welcome to SketchUp... | Open the Welcome dialog for recent files, templates, or licensing details. |
| 181 | `workspace preferences` | Open Workspace Settings |  | Open Workspace Preference Page. |
| 182 | `zoom next` | Camera Next | Next | Jump to the next camera view. |
| 183 | `zoom previous` | Camera Previous | Previous | Jump to the previous camera view. |
| 184 | `zoom selection` | Zoom Selection |  | Jump to a better view of your selection. |
