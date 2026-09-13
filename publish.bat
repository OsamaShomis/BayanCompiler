@echo off
echo ========================================================
echo Publishing Bayan Compiler Suite...
echo ========================================================

echo [1/2] Publishing Bayan.Compiler.Cli...
dotnet publish src/Bayan.Compiler.Cli/Bayan.Compiler.Cli.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./publish/Bayan.Compiler.Cli -p:DisableImplicitNuGetFallbackFolder=true
if %errorlevel% neq 0 (
    echo Error publishing CLI!
    exit /b %errorlevel%
)

echo [2/2] Publishing BayanCompiler (Windows Forms 9-Stage GUI)...
dotnet publish src/BayanCompiler/BayanCompiler.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish/BayanCompiler -p:DisableImplicitNuGetFallbackFolder=true
if %errorlevel% neq 0 (
    echo Error publishing Windows Forms GUI!
    exit /b %errorlevel%
)

echo [3/3] Copying official samples...
xcopy /E /I /Y "samples" "publish\BayanCompiler\samples" >nul

echo.
echo ========================================================
echo Publish completed successfully!
echo Executable files are ready in:
echo   - GUI (Portable IDE): publish\BayanCompiler\Bayan.Editor.WinForms.exe
echo   - CLI (Standalone):   publish\Bayan.Compiler.Cli\Bayan.Compiler.Cli.exe
echo   - Samples Included:   publish\BayanCompiler\samples\
echo ========================================================
