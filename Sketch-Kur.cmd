@echo off
rem Sketch'i bu bilgisayara kurar veya gunceller: programi hazirlar ve masaustune / Baslat menusune
rem "Sketch" kisayolu koyar. Program degistikten sonra bir kez cift tiklamaniz yeterli.
cd /d "%~dp0"
tasklist /fi "imagename eq Sketch.App.exe" 2>nul | find /i "Sketch.App.exe" >nul
if not errorlevel 1 (
  echo Sketch su anda acik. Once Sketch'i kapatin, sonra bu dosyaya tekrar cift tiklayin.
  pause
  exit /b 1
)
echo Sketch kuruluyor, lutfen bekleyin...
dotnet publish src\Sketch.App\Sketch.App.csproj -c Release -o "%LOCALAPPDATA%\Programs\Sketch" -nologo -v q
if errorlevel 1 (
  echo.
  echo Kurulum basarisiz oldu. Bu pencereyi Claude'a gosterin.
  pause
  exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -Command "$exe = Join-Path $env:LOCALAPPDATA 'Programs\Sketch\Sketch.App.exe'; $shell = New-Object -ComObject WScript.Shell; foreach ($dir in @([Environment]::GetFolderPath('Desktop'), [Environment]::GetFolderPath('Programs'))) { $lnk = $shell.CreateShortcut((Join-Path $dir 'Sketch.lnk')); $lnk.TargetPath = $exe; $lnk.WorkingDirectory = (Split-Path $exe); $lnk.IconLocation = $exe + ',0'; $lnk.Description = 'Sketch - Thermal Modelling and DEAP Evidence'; $lnk.Save() }"
echo.
echo Sketch kuruldu. Masaustundeki "Sketch" simgesine cift tiklayarak acabilirsiniz.
ping -n 6 127.0.0.1 >nul
