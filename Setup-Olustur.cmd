@echo off
rem Baska bilgisayarlara kurmak icin SketchSetup-<surum>.exe olusturur (artifacts\installer klasorune).
rem Gerekenler: .NET SDK ve Inno Setup 6. Kurulum dosyasi .NET'i icinde tasir; diger bilgisayarda ek bir sey gerekmez.
cd /d "%~dp0"
set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" (
  echo Inno Setup 6 bulunamadi. Claude'dan kurmasini isteyin.
  pause
  exit /b 1
)
echo Program hazirlaniyor...
if exist "artifacts\publish\win-x64" rmdir /s /q "artifacts\publish\win-x64"
dotnet publish src\Sketch.App\Sketch.App.csproj -c Release -r win-x64 --self-contained true -o artifacts\publish\win-x64 -nologo -v q
if errorlevel 1 goto fail
echo Kurulum dosyasi olusturuluyor...
"%ISCC%" /Q installer\Sketch.iss
if errorlevel 1 goto fail
echo.
echo Hazir: artifacts\installer klasorundeki SketchSetup dosyasini diger bilgisayara kopyalayin.
ping -n 6 127.0.0.1 >nul
exit /b 0
:fail
echo.
echo Kurulum dosyasi olusturulamadi. Bu pencereyi Claude'a gosterin.
pause
exit /b 1
