# Proje Özeti

Bu dosya projenin ne olduğunu, neden var olduğunu, kapsamını ve mevcut durumunu açıklar.

## Projenin adı

Sketch (geçici çalışma adı; kesin ad sonra belirlenecek).

## Kısa tanım

Windows için masaüstü 3D modelleme uygulaması. Araçları, arayüzü ve modelleme mantığı SketchUp 2026'yı örnek alır; kod, ad ve görseller özgündür.

## Ana amaç

Kullanıcının kendi kişisel kullanımı için SketchUp benzeri bir 3D modelleme yazılımı geliştirmek.

## Kapsama dahil olanlar

- SketchUp'ın kamuya açık belgelerden öğrenilen işlevlerinin özgün kodla yeniden yazılması.
- İlk sürüm (MVP): kamera gezinmesi, Line/Rectangle/Circle, Push/Pull, Move, Select, Eraser, çıkarım (inference) motoru, kaydetme/açma.

## Kapsam dışında kalanlar

- SketchUp ikili dosyalarının çözülmesi veya tersine mühendisliği.
- SketchUp kodunun, ikonlarının, görsellerinin ve markasının kopyalanması.
- Ticari dağıtım (proje kişisel kullanım içindir).

## Mevcut durum

2026-10-20: SketchUp işlev araştırması tamamlandı (`docs/sketchup-analizi/`). Teknoloji: C# / .NET 10 + WPF + OpenGL. İlk sürüm (MVP, M0–M6) tamamlandı; ardından kullanıcının termal modelleme iş akışı (etiketler, malzemeler, gruplar, sahneler, sağ panel) ve eksik araçların çoğu eklendi. Ayrıntı: `docs/yol-haritasi.md`.

## Önemli konumlar

- `docs/yol-haritasi.md`: Mimari ve MVP geliştirme aşamaları.

- `docs/sketchup-analizi/`: SketchUp araçları, arayüzü ve veri modeli üzerine kamuya açık belgelerden derlenmiş araştırma.

