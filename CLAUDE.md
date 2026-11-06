# Proje Çalışma Talimatları

Bu dosya projenin ana ve kalıcı çalışma talimatlarını içerir. Claude Code bu dosyayı doğrudan kullanır. Codex, `AGENTS.md` üzerinden bu dosyaya yönlendirilir.

## Proje bağlamını kullanma

- Her görevin başında `context/README.md` dosyasını oku.
- Ana haritada “her görevde oku” olarak belirtilen dosyaları oku.
- Görevin konusuna göre haritada belirtilen diğer bağlam dosyalarını gerektiğinde oku.
- Görevle ilgisi olmayan dosyaları gereksiz yere bağlama yükleme.
- Proje bilgileri için `context/` altındaki doğrulanmış ve güncel dosyaları esas al.
- Bağlam dosyaları çelişiyorsa veya güncelliğinden şüphe ediyorsan sessizce seçim yapma; kullanıcıya bildir.

## Bağlam haritasını koruma

- `context/` altında yeni bir bağlam dosyası oluşturur, taşır, yeniden adlandırır, amacını değiştirir veya silersen aynı işlem içinde `context/README.md` haritasını güncelle.
- Bir bağlam dosyasının yalnızca içeriği değişmiş ve dosyanın amacı aynı kalmışsa ana haritayı değiştirme.
- Her bağlam dosyasının ana haritadan doğrudan veya bir alt harita üzerinden ulaşılabilir olmasını sağla.
- Haritada bulunmayan bir bağlam dosyası fark edersen amacı açıksa haritaya ekle; belirsizse kullanıcıya sor.
- Gerekli harita güncellemesi tamamlanmadan bağlamla ilgili görevi tamamlanmış sayma.

## Kaynakları yönetme

- `context/kaynaklar/` altındaki dosyaları kullanıcı veya proje tarafından sağlanan dayanak girdileri olarak değerlendir.
- Bir kaynak ekler, taşır, yeniden adlandırır veya silersen aynı işlem içinde `context/kaynaklar/kaynak-indeksi.md` dosyasını güncelle.
- Kaynakların orijinal içeriğini açıkça istenmedikçe değiştirme.
- Ajan tarafından üretilen çıktı, yorum veya özetleri ham kaynak olarak kaydetme.
- Bir kaynaktan çıkarılan kalıcı sonucu, kullanıcı tarafından doğrulandığında ilgili bağlam dosyasına kaynak yolu ve dönemiyle birlikte işle.
- Kullanıcı tarafından yeni bir kaynak eklenmedikçe kaynak indeksine yeni kaynak kaydı oluşturma.
- Parola, erişim anahtarı veya gereksiz kişisel veri gibi hassas bilgileri proje bağlamına ekleme.

## Proje hafızasını güncel tutma

- Bir görev sırasında hedef, karar, kural, kapsam, sınır veya başka bir kalıcı proje bilgisi açıkça değişirse ilgili bağlam dosyasını aynı görev içinde güncelle.
- Bir bilginin geçici mi kalıcı mı olduğu belirsizse bağlama eklemeden önce kullanıcıya sor.
- Geçici konuşmaları, reddedilmiş seçenekleri ve doğrulanmamış varsayımları kalıcı bağlam olarak kaydetme.
- Sohbetin tamamını bağlam dosyalarına kopyalama; yalnızca gelecekte tekrar kullanılacak kesinleşmiş bilgileri işle.
- Kullanıcı “hafızayı güncelle”, “bağlamı denetle” veya “kaynakları denetle” dediğinde `proje-hafizasi` becerisini kullan.
- Uzun bir konuşmada kalıcı bağlama işlenmemiş önemli bilgiler oluştuysa, uygun zamanda kullanıcıya hafızayı güncellemeyi isteyip istemediğini sor.

## Çalışma yaklaşımı

- İşleme başlamadan önce talebi, ilgili dosyaları ve mevcut yapıyı incele.
- Kullanıcıya ait mevcut çalışmaları koru ve yalnızca görev için gerekli alanları değiştir.
- Görev kapsamı dışında kalan dosyaları değiştirme.
- Sonucu önemli ölçüde etkileyen bir varsayımı açıkça belirt.
- Sonucu değiştirecek bir belirsizlik varsa kullanıcıya sor; küçük ayrıntılarda makul karar vererek ilerle.
- Güncel veya dış dünyaya ilişkin önemli bir iddiayı doğrulamadan kesin bilgi gibi sunma.

## Yetki ve güvenlik

- Geri alınması zor, veri kaybına yol açabilecek veya dış dünyada etkisi olacak işlemleri açık yetki olmadan yapma.
- Dosya silme, mevcut içeriğin üzerine yazma, yayımlama, dışarıya mesaj gönderme veya ödeme oluşturma gibi işlemlerde hedefi ve kapsamı doğrula.
- Kullanıcının açıkça istediği ve görev kapsamında bulunan güvenli işlemler için gereksiz yere tekrar onay isteme.
- Gizli bilgileri yanıtlarda, günlüklerde veya proje belgelerinde açığa çıkarma.

## Doğrulama ve tamamlanma

- Bir işi tamamlandı olarak bildirmeden önce sonucu görevin riskine uygun biçimde kontrol et.
- Yazılım projesiyse mevcut test, denetim ve derleme komutlarını kullan; komut uydurma.
- Belge, analiz veya içerik projesiyse sonucu kaynaklar, bağlam kuralları ve istenen biçim açısından incele.
- Kontrol edemediğin bir noktayı kontrol edilmiş gibi gösterme.
- Tamamlandığında yapılan değişiklikleri, etkilenen dosyaları ve doğrulanamayan noktaları kısa şekilde bildir.

## İletişim

- Kullanıcının dilini ve bilgi seviyesini takip et.
- Sonucu önce, gerekli açıklamayı ardından ver.
- Teknik terimleri yalnızca gerektiğinde kullan ve ilk kullanımda sade biçimde açıkla.
- Belirsizlikleri, varsayımları ve engelleri gizleme.

## Projeye özel kalıcı talimatlar

<!--
Bu bölüm, context/ içeriği hazırlandıktan sonra Claude Code veya Codex
tarafından projeye göre yapılandırılır.

Buraya yalnızca her görevde geçerli olacak kısa talimatlar eklenmelidir.
Ayrıntılı proje geçmişi, hedef kitle, strateji ve kaynak bilgileri buraya
kopyalanmamalı; context/README.md üzerinden yönlendirilmelidir.
-->

- Proje, kişisel kullanım için SketchUp'ın işlevlerini örnek alan özgün bir Windows 3D modelleme uygulamasıdır (çalışma adı: "Sketch"). Ayrıntılar için `context/proje-ozeti.md` dosyasına bak.
- SketchUp'ın davranışını kamuya açık resmi belgelerden ve kullanıcının kurulu SketchUp 2026'sındaki açık metin/yapılandırma dosyalarından öğren. EXE/DLL dosyalarını çözme, şifreli eklentileri açma; REA gibi araçları SketchUp üzerinde kullanma. Kullanıcının kendi `.skp` dosyalarının yapısı ise (2026-10-21 kararı) yalnızca dosya içeriğine bakılarak incelenebilir ve özgün bir okuyucu yazılabilir; bkz. `context/kararlar.md`.
- SketchUp'ın kodunu, ikonlarını, görsellerini, metinlerini veya "SketchUp"/"Trimble" markalarını uygulamaya kopyalama; tüm kod ve görseller özgün olmalıdır.
- SketchUp işlev araştırması `docs/sketchup-analizi/` altındadır; araç ve davranış uygularken önce ilgili dosyayı oku.
- Teknoloji: C# / .NET 10 + WPF + OpenGL (Silk.NET). Geliştirme planı `docs/yol-haritasi.md` içindedir.
- Çözüm dosyası `Sketch.slnx`. Doğrulanmış komutlar: derleme `dotnet build Sketch.slnx`, test `dotnet test Sketch.slnx`, çalıştırma `dotnet run --project src/Sketch.App`. Kullanıcı uygulamayı masaüstündeki "Sketch" kısayolundan açar (kurulu kopya `%LOCALAPPDATA%\Programs\Sketch`, kökteki `Sketch-Kur.cmd` ile kurulur ve kısayollar oluşturulur). Koddaki her değişiklikten sonra `Sketch-Kur.cmd` çalıştırılarak kurulu kopya güncellenmelidir; Sketch açıksa kurulum durur. `Sketch-Baslat.cmd` derleyip doğrudan başlatır (yedek yol). Başka bilgisayarlar için kurulum dosyası `Setup-Olustur.cmd` ile üretilir (Inno Setup 6 gerekir; çıktı `artifacts/installer/`).
- Her değişiklikten sonra derle ve testleri çalıştır; anlamlı her adımı Git'e commit et.
- Arayüzü test ederken gerçek fare/klavye otomasyonu (SendInput, mouse_event, keybd_event vb.) kullanma; kullanıcının açık programlarına girdi gidebilir. Bunun yerine uygulamanın test modunu kullan: `Sketch.App.exe --script dosya.txt` (komutlar `src/Sketch.App/MainWindow.Script.cs` içinde); ekran görüntüleri dosyaya kaydedilir.
- Kullanıcının yazılım bilgisi yoktur ve geliştirmenin tamamını Claude'a bırakmıştır. Teknik kararları kendin ver; kullanıcıya yalnızca sonucu ve onun deneyebileceği şeyleri sade dille anlat. Uygulamayı nasıl açacağını her seferinde basitçe söyle.

