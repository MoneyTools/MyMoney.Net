@echo off

cd %~dp0
SET ROOT=%~dp0
set WINGET_SRC=D:\git\clovett\winget-pkgs
for /f "usebackq" %%i in (`xsl -e -s Version\version.xsl Version\version.props`) do (
    set VERSION=%%i
)

echo ### Publishing version %VERSION%...
set WINGET=1

if "%LOVETTSOFTWARE_STORAGE_CONNECTION_STRING%" == "" goto :nokey
if not EXIST publish goto :nobits

if not EXIST MoneyPackage\AppPackages\MoneyPackage_%VERSION%_Test\MoneyPackage_%VERSION%_AnyCPU.msixbundle goto :noappx

echo Binaries to publish:
echo %ROOT%MoneyPackage\AppPackages\MoneyPackage_%VERSION%_Test\MoneyPackage_%VERSION%_AnyCPU.msixbundl
set /p response=Please publish github release named %VERSION% using the above binaries and press ENTER to continue...

copy /y MyMoney\Setup\changes.xml publish
echo Uploading ClickOnce installer 
call AzurePublishClickOnce %~dp0publish downloads/MyMoney "%LOVETTSOFTWARE_STORAGE_CONNECTION_STRING%"
call AzurePublishClickOnce %~dp0MoneyPackage\AppPackages downloads/MyMoney.Net "%LOVETTSOFTWARE_STORAGE_CONNECTION_STRING%"

echo ============ Done publishing ClickOnce installer ==============

if "%WINGET%"=="0" goto :skipwinget

if not exist %WINGET_SRC% goto :nowinget

:winget
echo Syncing winget master branch
pushd %WINGET_SRC%\manifests\l\LovettSoftware\MyMoney\Net
git checkout master
git pull
git fetch upstream master
git merge upstream/master
git push
popd

echo Preparing winget package
set TARGET=%WINGET_SRC%\manifests\l\LovettSoftware\MyMoney\Net\%VERSION%
if not exist %TARGET% mkdir %TARGET%
copy /y WinGetTemplate\LovettSoftware*.yaml  %TARGET%
wingetcreate update LovettSoftware.MyMoney.Net --version %VERSION% -o %WINGET_SRC% -u https://github.com/clovett/MyMoney.Net/releases/download/%VERSION%/MoneyPackage_%VERSION%_AnyCPU.msixbundle
if ERRORLEVEL 1 goto :eof

pushd %TARGET%
winget validate .
winget install -m .
if ERRORLEVEL 1 goto :installfailed

git checkout -b "clovett/mymoney_%VERSION%"
git add *
git commit -m "new Money version %VERSION%"
git push -u origin "clovett/mymoney_%VERSION%"

echo =============================================================================================================
echo Please create Pull Request for the new "clovett/mymoney_%VERSION%" branch.

call gitweb
goto :eof

 

goto :eof

:nokey
echo Please set your LOVETTSOFTWARE_STORAGE_CONNECTION_STRING
exit /b 1

:nobits
echo 'publish' folder not found, please run Solution/Publish first.
exit /b 1

:noappx
echo Please build the .msixbundle using MoneyPackage.wapproj project, publish/create appx packages.
exit /b 1

:nowinget
echo Please clone git@github.com:lovettchris/winget-pkgs.git into %WINGET_SRC%
exit /b 1
