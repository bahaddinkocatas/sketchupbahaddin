@echo off
rem Sketch uygulamasini derler ve baslatir. Cift tiklamaniz yeterli.
cd /d "%~dp0"
echo Sketch derleniyor, lutfen bekleyin...
dotnet build src\Sketch.App\Sketch.App.csproj -c Release -nologo -v q
if errorlevel 1 (
  echo.
  echo Derleme basarisiz oldu. Bu pencereyi Claude'a gosterin.
  pause
  exit /b 1
)
start "" "src\Sketch.App\bin\Release\net10.0-windows\Sketch.App.exe"
