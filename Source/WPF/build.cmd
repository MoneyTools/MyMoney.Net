rem @echo off
setlocal ENABLEDELAYEDEXPANSION
cd %~dp0
SET ROOT=%~dp0
set ClickOnceBits=%ROOT%MyMoney\bin\publish
set PATH=%PATH%;%ROOT%\tools;

set BuildType=%1
if "!BuildType!" == "" set BuildType=Release

UpdateVersion .\Version\VersionMaster.txt
if ERRORLEVEL 1 goto :err_version

msbuild /target:restore MyMoney.sln /p:Configuration=!BuildType! "/p:Platform=Any CPU"
if ERRORLEVEL 1 goto :err_restore

msbuild /target:rebuild MyMoney.sln /p:Configuration=!BuildType! "/p:Platform=Any CPU"
if ERRORLEVEL 1 goto :err_build

if "%BuildType%" == "Debug" goto :done

pushd MyMoney
msbuild /target:publish /p:PublishProfile=.\Properties\PublishProfiles\ClickOnceProfile.pubxml MyMoney.csproj /p:Configuration=Release "/p:Platform=Any CPU" /p:PublishDir=%ClickOnceBits%
if ERRORLEVEL 1 goto :err_publish
echo Click once build in: %ClickOnceBits%

:done
goto :eof

:err_restore
echo Error: msbuild /target:restore failed.
exit /b /1

:err_build
echo Error: msbuild /target:rebuild failed.
exit /b /1

:err_publish
echo Error: msbuild /target:publish failed.
popd
exit /b /1

:err_version
echo Error: update version failed.
exit /b /1