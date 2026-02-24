@echo off
echo Starting Build Process for OpenUtau WASM...

cd /d "%~dp0\OpenUtau.Browser"

echo.
echo ==========================================
echo Running dotnet publish (-c Release)
echo ==========================================
dotnet publish -c Release -f net8.0-browser

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Build Failed!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo ==========================================
echo Build Success. Starting Server...
echo Serving from: bin\Release\net8.0-browser\publish\wwwroot
echo ==========================================

dotnet serve -p 5000 -d bin\Release\net8.0-browser\publish\wwwroot
pause
