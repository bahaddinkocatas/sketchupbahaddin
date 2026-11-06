# Betik modu testleri

Uygulama gerçek fare/klavye kullanmadan kendi içinde girdi oynatır (`src/Sketch.App/MainWindow.Script.cs`). Betikler proje kökünden çalıştırılır; ekran görüntüleri ve `.log` dosyası betiğin yanına yazılır.

```bat
src\Sketch.App\bin\Release\net10.0-windows\Sketch.App.exe --script tests\scripts\duman.txt
src\Sketch.App\bin\Release\net10.0-windows\Sketch.App.exe --script tests\scripts\block4.txt
```

| Betik | Ne yapar | Beklenen |
|---|---|---|
| `duman.txt` | Dikdörtgen çizer, her araca tıklama ve sürükleme uygular (`alltools`) | Log'da her araç için `OK`, hiç `HATA` yok |
| `block4.txt` | Kökteki `Kinsale Rd Block 4.skp` dosyasını açar (SketchUp gerekmez), üstten görüntüler, kıyaslama alır, bir dairede Push/Pull ve Move sürükler, DEAP CSV'si üretir | `grup=128`; `dragbench` satırlarında araç birkaç ms, toplam ~16–25 ms/adım; DEAP mesajında 62 konut; CSV 200 satır |
| `secim.txt` | Block 4'te plan grubuna ve bir daireye tıklar, pencere/kesişim seçimi ve Ctrl ile ekleme yapar | `sel` satırları: Group=1, Group=1, Group=2, Group=128, sonra kesişimde daha az |

Komutlar: `move x y`, `click x y [shift|ctrl]`, `down`/`up`, `dblclick`, `orbit`, `wheel`, `key`, `keydown`/`keyup`, `type`, `vcb`, `tool <komut>`, `tag`, `scene`, `material`, `import`, `open`, `select <grup>`, `enter <grup>`, `pick x y`, `shot dosya.png`, `shotui dosya.png`, `log`, `dump`, `sel`, `pickcheck`, `alltools`, `bench`, `movebench`, `dragbench <araç> <grup|-> [adım]`, `deap <klasör>`, `wait ms`, `exit`.
