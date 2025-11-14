@echo off
setlocal

:: --- Configuration ---
set "source_fu=VL.Fu\src"
set "source_fu_core=VL.Fu.Core\src"
set "dest_fu=.tmp\fu"
set "dest_fu_core=.tmp\fu-core"
set "exclude_dir=\obj\"
:: --- End Configuration ---

:: Create destination directories, clearing them first if they exist
echo Creating and cleaning destination directories...
if exist "%dest_fu%" (
    echo Deleting old files in %dest_fu%...
    del /q "%dest_fu%\*.cs"
) else (
    mkdir "%dest_fu%"
)

if exist "%dest_fu_core%" (
    echo Deleting old files in %dest_fu_core%...
    del /q "%dest_fu_core%\*.cs"
) else (
    mkdir "%dest_fu_core%"
)
echo.

:: Copy files from VL.Fu\src
echo Copying .cs files from %source_fu% to %dest_fu%...
if exist "%source_fu%" (
    for /r "%source_fu%" %%f in (*.cs) do (
        echo "%%f" | findstr /i /c:"%exclude_dir%" >nul || copy "%%f" "%dest_fu%\"
    )
) else (
    echo Source directory %source_fu% not found.
)
echo.

:: Copy files from VL.Fu.Core\src
echo Copying .cs files from %source_fu_core% to %dest_fu_core%...
if exist "%source_fu_core%" (
    for /r "%source_fu_core%" %%f in (*.cs) do (
        echo "%%f" | findstr /i /c:"%exclude_dir%" >nul || copy "%%f" "%dest_fu_core%\"
    )
) else (
    echo Source directory %source_fu_core% not found.
)
echo.

echo All files have been copied successfully.
pause