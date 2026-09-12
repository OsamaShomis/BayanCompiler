@echo off
echo ========================================================
echo Publishing Bayan Compiler Suite...
echo ========================================================

echo [1/2] Publishing Bayan.Compiler.Cli...
dotnet publish src/Bayan.Compiler.Cli/Bayan.Compiler.Cli.csproj -c Release -o ./publish/Bayan.Compiler.Cli
if %errorlevel% neq 0 (
    echo Error publishing CLI!
    exit /b %errorlevel%
)

echo [2/2] Publishing BayanCompiler (Windows Forms 9-Stage GUI)...
dotnet publish src/BayanCompiler/BayanCompiler.csproj -c Release -o ./publish/BayanCompiler
if %errorlevel% neq 0 (
    echo Error publishing Windows Forms GUI!
    exit /b %errorlevel%
)

echo.
echo ========================================================
echo Publish completed successfully!
echo Executable files are ready in:
echo   - GUI: publish\BayanCompiler\Bayan.Editor.WinForms.exe
echo   - CLI: publish\Bayan.Compiler.Cli\Bayan.Compiler.Cli.exe
echo ========================================================
