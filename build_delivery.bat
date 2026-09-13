@echo off
setlocal enabledelayedexpansion
title Bayan Project Delivery Packaging

set "GROUP_ID=01"
set "DELIVERY_DIR=d:\COMPILER\delivery"
set "PUBLISH_DIR=d:\COMPILER\publish\BayanCompiler"

echo ==============================================================================
echo [Bayan Delivery System] Packaging Official Delivery Artifacts (Group %GROUP_ID%)
echo ==============================================================================

echo.
echo [1/4] Publishing standalone compiler and editor...
call d:\COMPILER\publish.bat

echo.
echo [2/4] Copying official executable deliverables...
if exist "%PUBLISH_DIR%\Bayan.Editor.WinForms.exe" (
    copy /y "%PUBLISH_DIR%\Bayan.Editor.WinForms.exe" "%DELIVERY_DIR%\PREXE-G%GROUP_ID%.exe" > nul
    copy /y "%PUBLISH_DIR%\Bayan.Editor.WinForms.exe" "%DELIVERY_DIR%\EXE-G%GROUP_ID%-PREXE.exe" > nul
    echo    - PREXE-G%GROUP_ID%.exe copied successfully.
) else (
    echo    [ERROR] Executable not found in publish directory!
)
if exist "d:\COMPILER\publish\Bayan.Compiler.Cli\Bayan.Compiler.Cli.exe" (
    copy /y "d:\COMPILER\publish\Bayan.Compiler.Cli\Bayan.Compiler.Cli.exe" "%DELIVERY_DIR%\Bayan.Compiler.Cli.exe" > nul
    echo    - Bayan.Compiler.Cli.exe copied successfully.
)
xcopy /E /I /Y "d:\COMPILER\samples" "%DELIVERY_DIR%\samples" > nul

echo.
echo [3/4] Copying LaTeX report and HTML view...
copy /y "d:\COMPILER\report\RPT-G01.tex" "%DELIVERY_DIR%\RPT-G%GROUP_ID%.tex" > nul
copy /y "d:\COMPILER\report\RPT-G01.html" "%DELIVERY_DIR%\RPT-G%GROUP_ID%.html" > nul
if exist "d:\COMPILER\report\RPT-G01.pdf" (
    copy /y "d:\COMPILER\report\RPT-G01.pdf" "%DELIVERY_DIR%\RPT-G%GROUP_ID%.pdf" > nul
)

echo.
echo [4/4] Creating clean ZIP archives via Python packager...
python d:\COMPILER\package_delivery.py

echo.
echo ==============================================================================
echo [SUCCESS] All 3 official project deliverables are packaged in: %DELIVERY_DIR%
echo   1. Executable: PREXE-G%GROUP_ID%.exe
echo   2. Archive:    PRFL-G%GROUP_ID%.zip (Clean Source Code Archive)
echo   3. Report:     RPT-G%GROUP_ID%.tex / RPT-G%GROUP_ID%.html / report_latex_overleaf.zip
echo ==============================================================================
