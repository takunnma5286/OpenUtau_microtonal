@echo off
REM Emscripten Build Script for Worldline WASM Module (Full Path Version)
REM This builds worldline as a standalone WebAssembly module

echo Building Worldline WASM module...

REM Set Emscripten paths
set EMCC=C:\emsdk\upstream\emscripten\emcc.bat
set EMSDK_NODE=C:\emsdk\node\22.16.0_64bit\bin\node.exe
set EMSDK_PYTHON=C:\emsdk\python\3.13.3_64bit\python.exe

REM Check if emcc exists
if not exist "%EMCC%" (
    echo ERROR: emcc not found at %EMCC%
    echo Please verify Emscripten installation
    exit /b 1
)

REM Set output directory
set OUTPUT_DIR=..\..\OpenUtau.Browser\wwwroot

echo Compiling Worldline C++ sources to WebAssembly...
echo This may take a few minutes...

REM Compile all source files
"%EMCC%" ^
  worldline.cpp ^
  common/timer.cpp ^
  common/vec_utils.cpp ^
  f0/dio_estimator.cpp ^
  f0/dio_ss_estimator.cpp ^
  f0/frq_estimator.cpp ^
  f0/harvest_estimator.cpp ^
  f0/pyin_estimator.cpp ^
  model/effects.cpp ^
  model/model.cpp ^
  classic/classic_args.cpp ^
  classic/frq.cpp ^
  classic/resampler.cpp ^
  classic/timing.cpp ^
  phrase_synth.cpp ^
  audio_output.cc ^
  ../third_party/world/src/cheaptrick.cpp ^
  ../third_party/world/src/codec.cpp ^
  ../third_party/world/src/common.cpp ^
  ../third_party/world/src/d4c.cpp ^
  ../third_party/world/src/dio.cpp ^
  ../third_party/world/src/fft.cpp ^
  ../third_party/world/src/harvest.cpp ^
  ../third_party/world/src/matlabfunctions.cpp ^
  ../third_party/world/src/stonemask.cpp ^
  ../third_party/world/src/synthesis.cpp ^
  ../third_party/world/src/synthesisrealtime.cpp ^
  ../third_party/libpyin/pyin.c ^
  ../third_party/libpyin/yin.c ^
  ../third_party/libpyin/math-funcs.c ^
  ../third_party/libgvps/gvps_full.c ^
  ../third_party/libgvps/gvps_obsrv.c ^
  ../third_party/libgvps/gvps_sampled.c ^
  ../third_party/libgvps/gvps_variable.c ^
  -o %OUTPUT_DIR%\worldline.js ^
  -s EXPORTED_FUNCTIONS=_WorldlineTest,_F0,_DecodeMgc,_DecodeBap,_InitAnalysisConfig,_WorldAnalysis,_WorldAnalysisF0In,_WorldSynthesis,_malloc,_free ^
  -s EXPORTED_RUNTIME_METHODS=ccall,cwrap,getValue,setValue ^
  -sERROR_ON_UNDEFINED_SYMBOLS=0 ^
  -sWARN_ON_UNDEFINED_SYMBOLS=1 ^
  -s MODULARIZE=1 ^
  -s EXPORT_NAME=WorldlineModule ^
  -s ALLOW_MEMORY_GROWTH=1 ^
  -s TOTAL_STACK=5242880 ^
  -s INITIAL_MEMORY=16777216 ^
  -s MAXIMUM_MEMORY=4294967296 ^
  -DFP_TYPE=double ^
  -DXXH_INLINE_ALL ^
  -I. ^
  -I.. ^
  -I../third_party ^
  -I../third_party/world/src ^
  -I../third_party/libpyin ^
  -I../third_party/libgvps ^
  -I../third_party/spline/src ^
  -I../third_party/miniaudio ^
  -I../third_party/xxhash ^
  -O2 ^
  --no-entry

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================
    echo SUCCESS: Worldline WASM module built!
    echo Output: %OUTPUT_DIR%\worldline.js
    echo        %OUTPUT_DIR%\worldline.wasm
    echo ========================================
) else (
    echo.
    echo ========================================
    echo ERROR: Build failed with exit code %ERRORLEVEL%
    echo ========================================
    exit /b 1
)
