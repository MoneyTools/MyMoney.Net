@echo off
setlocal ENABLEDELAYEDEXPANSION
cd %~dp0
SET ROOT=%~dp0
set WINGET_SRC=D:\git\clovett\winget-pkgs
set PATH=%PATH%;%ROOT%\tools;%LOCALAPPDATA%\Microsoft\WindowsApps\
for /f "usebackq" %%i in (`xsl -e -s Version\version.xsl Version\version.props`) do (
    set VERSION=%%i
)

call SetupWinget
if '!WINGETVERSION!' == '' goto :eof
if '!WINGETCREATEVERSION!' == '' goto :eof

echo ### Publishing version %VERSION%...
set WINGET=1
set GITRELEASE=1
set UPLOAD=1
set ClickOnceBits=%ROOT%MyMoney\bin\publish
set DOBUILD=1
:parse
if "%1"=="/nowinget" set WINGET=0
if "%1"=="/norelease" set GITRELEASE=0
if "%1"=="/noupload" set UPLOAD=0
if "%1"=="/nobuild" set DOBUILD=0
if "%1"=="" goto :done
shift
goto :parse

:done

if "%LOVETTSOFTWARE_STORAGE_CONNECTION_STRING%" == "" goto :nokey

if "%DevEnvDir%"=="" goto :novsdev

if "%DOBUILD%"=="0" goto :dorelease

if EXIST %ClickOnceBits% rd /s /q %ClickOnceBits%

UpdateVersion .\Version\VersionMaster.txt
if ERRORLEVEL 1 goto :err_version

msbuild /target:restore MyMoney.sln /p:Configuration=Release "/p:Platform=Any CPU"
if ERRORLEVEL 1 goto :err_restore

msbuild /target:rebuild MyMoney.sln /p:Configuration=Release "/p:Platform=Any CPU"
if ERRORLEVEL 1 goto :err_build

pushd MyMoney
msbuild /target:publish /p:PublishProfile=.\Properties\PublishProfiles\ClickOnceProfile.pubxml MyMoney.csproj /p:Configuration=Release "/p:Platform=Any CPU" /p:PublishDir=%ClickOnceBits%
if ERRORLEVEL 1 goto :err_publish
popd
if not EXIST %ClickOnceBits%\MyMoney.application goto :nopub
CleanupPublishFolder %VERSION% %ClickOnceBits%

echo Please check the click once bits at %ClickOnceBits%
pause

echo ### BUGBUG: TODO fix msix package build of .NET 7.0 apps, it is currently not supported, skipping winget package creation...
rem if EXIST MoneyPackage\AppPackages rd /s /q MoneyPackage\AppPackages
rem msbuild /target:publish MyMoneyPackage.sln /p:Configuration=Release "/p:Platform=Any CPU"
rem if not EXIST MoneyPackage\AppPackages\MoneyPackage_%VERSION%_Test\MoneyPackage_%VERSION%_AnyCPU.msixbundle goto :noappx
rem https://developercommunity.visualstudio.com/t/PublishReadyToRun-doesnt-seem-to-work-o/10272475

:dorelease
if "%GITRELEASE%" == "0" goto :upload
echo Creating new tag for version %VERSION%
git tag %VERSION%
git push origin --tags

echo Creating new release for version %VERSION%
xsl -e -s MyMoney\Setup\LatestVersion.xslt MyMoney\Setup\changes.xml > notes.txt
gh release create %VERSION% --notes-file notes.txt --title "MyMoney.Net %VERSION%"
REM gh release create %VERSION% %ROOT%MoneyPackage\AppPackages\MoneyPackage_%VERSION%_Test\MoneyPackage_%VERSION%_AnyCPU.msixbundle --notes-file notes.txt --title "MyMoney.Net %VERSION%"
del notes.txt

:upload
if "%UPLOAD%" == "0" goto :dowinget
echo Uploading ClickOnce installer
copy /y MyMoney\Setup\changes.xml %ClickOnceBits%
call AzurePublishClickOnce %ClickOnceBits% downloads/MyMoney "%LOVETTSOFTWARE_STORAGE_CONNECTION_STRING%"
REM call AzurePublishClickOnce %ROOT%MoneyPackage\AppPackages downloads/MyMoney.Net "%LOVETTSOFTWARE_STORAGE_CONNECTION_STRING%"

echo ============ Done publishing ClickOnce installer ==============
:dowinget
goto :skipwinget
rem if "%WINGET%"=="0" goto :skipwinget
if not exist %WINGET_SRC% goto :nowinget
if not EXIST MoneyPackage\AppPackages\MoneyPackage_%VERSION%_Test\MoneyPackage_%VERSION%_AnyCPU.msixbundle goto :eof

:syncwinget
echo Syncing winget master branch
pushd %WINGET_SRC%\manifests\l\LovettSoftware\MyMoney\Net
git checkout master
if ERRORLEVEL 1 goto :eof
git pull
if ERRORLEVEL 1 goto :eof
git fetch upstream master
if ERRORLEVEL 1 goto :eof
git merge upstream/master
if ERRORLEVEL 1 goto :eof
git push

set OLDEST=
for /f "usebackq" %%i in (`dir /b`) do (
  if "!OLDEST!" == "" set OLDEST=%%i
)

if "!OLDEST!" == "" goto :prepare
echo ======================== Replacing "!OLDEST!" version...

git mv "!OLDEST!" %VERSION%

:prepare
popd

echo Preparing winget package
set TARGET=%WINGET_SRC%\manifests\l\LovettSoftware\MyMoney\Net\%VERSION%
if not exist %TARGET% mkdir %TARGET%
copy /y WinGetTemplate\LovettSoftware*.yaml  %TARGET%
wingetcreate update LovettSoftware.MyMoney.Net --version %VERSION% -o %WINGET_SRC% -u https://github.com/MoneyTools/MyMoney.Net/releases/download/%VERSION%/MoneyPackage_%VERSION%_AnyCPU.msixbundle
if ERRORLEVEL 1 goto :eof

pushd %TARGET%
winget validate .
winget install -m .
if ERRORLEVEL 1 goto :installfailed

git checkout -b "clovett/mymoney_%VERSION%"
git add *
git commit -a -m "Money.Net version %VERSION%"
git push -u origin "clovett/mymoney_%VERSION%"

echo =============================================================================================================
echo Please create Pull Request for the new "clovett/mymoney_%VERSION%" branch.

call gitweb

:skipwinget
goto :eof

:nokey
echo Please set your LOVETTSOFTWARE_STORAGE_CONNECTION_STRING
exit /b 1

:novsdev
echo Please run this from a VS 2022 developer command prompt
exit /b 1

:nowinget
echo Please clone git@github.com:lovettchris/winget-pkgs.git into %WINGET_SRC%
exit /b 1

:noappx
echo Could not find MoneyPackage\AppPackages\MoneyPackage_%VERSION%_Test\MoneyPackage_%VERSION%_AnyCPU.msixbundle
exit /b /1

:nopub
echo Could not find %ClickOnceBits%\MyMoney.application
exit /b /1

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