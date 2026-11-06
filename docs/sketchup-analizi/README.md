# SketchUp 2026 İşlev Analizi

Bu klasör, Sketch uygulamasının örnek aldığı SketchUp 2026 davranışlarının araştırmasıdır. Kaynaklar: kamuya açık resmi belgeler ve kullanıcının kurulu SketchUp 2026'sındaki açık metin/yapılandırma dosyaları. İkili dosya çözülmemiştir. Araştırma tarihi: 2026-10-20.

| Dosya | İçerik | Ne zaman okunmalı? |
|---|---|---|
| `01-cizim-ve-duzenleme-araclari.md` | Çizim, düzenleme, yapı araçları; kısayollar, değiştirici tuşlar, ölçü kutusu, oluşan geometri, sürüm geçmişi | Bir araç uygulanırken |
| `02-gezinme-cikarim-ve-model-yapisi.md` | Kamera/gezinme, çıkarım motoru, ölçü kutusu, yapışkan geometri, grup/bileşen, tag, sahne, kesit | Kamera, çıkarım veya model yapısı geliştirilirken |
| `03-arayuz-menuler-ve-paneller.md` | Pencere düzeni, menüler, paneller, sağ tık menüleri, tercihler, stiller, sandbox araçları | Arayüz geliştirilirken |
| `04-veri-modeli-dosyalar-ve-eklentiler.md` | Ruby API/C SDK veri modeli, geri alma ve araç mimarisi, dosya biçimleri, eklenti sistemi, kütüphane seçenekleri | Çekirdek mimari ve dosya biçimleri tasarlanırken |
| `05-kurulu-dosyalar-incelemesi.md` | Kurulu SketchUp'ın teknoloji yığını, klasör yapısı, kısayol kayıtları, tercih anahtarları, 184 komutluk katalog | Komut sistemi, ayarlar ve teknoloji kararlarında |

## Gerçek SketchUp'ta doğrulanacak noktalar

Resmi kaynaklar arasında çelişen veya doğrulanamayan davranışlar. Ayrıntılar her dosyanın "doğrulanamadı" / çelişki bölümlerindedir.

- Line aracında Alt tuşunun işlevi; dikdörtgeni merkezden çizen tuş (Ctrl mi Alt mi).
- Lasso seçimde sürükleme yönü ve değiştirici tuşlar.
- Yay segment sayısını değiştirme yöntemi (Windows) ve varsayılan segment sayısı.
- Çıkarım renkleri (uç nokta, kenar üstü, yüz üstü) ve çıkarım öncelik sırası.
- Varsayılan görüş açısı (35°?) ve açı yakalama adımı (15°?).
- Kısayol kaydındaki ilk değiştirici bayrağın anlamı (Ctrl mi Alt mi) ve `I` kısayolunun (`selectImageIglooTool`) işlevi.
- Grup içi düzenlemede modelin geri kalanının soluklaşma düzeyi.
- Ön seçim olmadan Move, negatif Scale ile aynalama, Solid Tools sonucunun grup/bileşen kalıp kalmadığı.
