@echo off
echo Starting OPTIMIZED Build Process for OpenUtau WASM...
echo This will take significantly longer due to AOT compilation.
echo.

:: Explicitly set Configuration to Release for the entire environment
set Configuration=Release

cd /d "%~dp0\OpenUtau.Browser"

echo.
echo ======================================================
echo Cleaning and Publishing with AOT and Performance Flags
echo ======================================================
echo.

:: Clean only Release to avoid confusion
dotnet clean -c Release

:: -c Release: Build in release mode
:: -f net8.0-browser: Target WASM
:: -p:RunAOTCompilation=true: Ahead-of-Time compilation (Highest performance)
:: -p:WasmEnableSIMD=true: Enable SIMD for vector operations
:: -p:WasmEnableExceptionHandling=true: Use WASM native exception handling
:: -p:PublishTrimmed=false: Disabled because OpenUtau uses reflection for View/ViewModel mapping
:: -p:Configuration=Release: Explicitly force Release configuration for all dependencies

dotnet publish -c Release -f net8.0-browser ^
    -p:RunAOTCompilation=true ^
    -p:WasmEnableSIMD=true ^
    -p:WasmEnableExceptionHandling=true ^
    -p:PublishTrimmed=false ^
    -p:Configuration=Release

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Build Failed!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo ==========================================
echo Optimized Build Success. Starting Server...
echo Serving from: bin\Release\net8.0-browser\publish\wwwroot
echo ==========================================

:: Double check if bin\Debug exists and warn user if it's still being used
if exist "..\OpenUtau\bin\Debug" (
    echo [WARNING] bin\Debug directory still exists in OpenUtau project. 
    echo This might be from previous IDE builds.
)

dotnet serve -p 5000 -d bin\Release\net8.0-browser\publish\wwwroot
pause
