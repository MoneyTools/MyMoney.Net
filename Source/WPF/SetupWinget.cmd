@echo off
rem Assume this is called from publish.cmd with 'setlocal ENABLEDELAYEDEXPANSION'
rem this script installs winget and WingetCreateCLI

set PATH=%PATH%;%ROOT%\tools;%LOCALAPPDATA%\Microsoft\WindowsApps\
echo Checking winget version...
set WINGETVERSION=
for /f "usebackq" %%i in (`winget --version`) do (
    if '!WINGETVERSION!' == '' set WINGETVERSION=%%i
)

if '!WINGETVERSION!' == '' goto :installwinget
echo Found winget version !WINGETVERSION!
goto :checkwingetcreate

:installwinget
echo Installing winget...
curl -O --location https://github.com/microsoft/winget-cli/releases/download/v1.3.2691/Microsoft.DesktopAppInstaller_8wekyb3d8bbwe.msixbundle
if ERRORLEVEL 1 goto :winget_install_failed
powershell -command Add-AppXPackage -Path .\Microsoft.DesktopAppInstaller_8wekyb3d8bbwe.msixbundle
if ERRORLEVEL 1 goto :winget_install_failed
del .\Microsoft.DesktopAppInstaller_8wekyb3d8bbwe.msixbundle

:checkwingetcreate
echo Checking wingetcreate...

set WINGETCREATEVERSION=
for /f "usebackq tokens=1,2" %%i in (`wingetcreate version`) do (
    if '!WINGETCREATEVERSION!' == '' set WINGETCREATEVERSION=%%j
)

if NOT "!WINGETCREATEVERSION!" == "1.1.2.0" goto :installwingetcreate
echo Found WingetCreateCLI version !WINGETCREATEVERSION!

:installwingetcreate
if "!WINGETCREATEVERSION!" == "" winget install WingetCreateCLI
if ERRORLEVEL 1 goto :wingetcreate_install_failed
if NOT "!WINGETCREATEVERSION!" == "1.1.2.0" winget upgrade WingetCreateCLI
if ERRORLEVEL 1 goto :wingetcreate_install_failed

goto :eof

:winget_install_failed
echo Winget install failed, please install it manually from https://github.com/microsoft/winget-cli/releases/tag/v1.3.2691
exit /b /1

:wingetcreate_install_failed
echo winget install WingetCreateCLI failed, please try it manually.
exit /b /1