
@echo off
SETLOCAL EnableDelayedExpansion
cd %~dp0
SET ROOT=%~dp0
set WINGET_SRC=D:\git\clovett\winget-pkgs

echo Remove old winget package for MyMoney.Net.

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

set TARGET=%WINGET_SRC%\manifests\l\LovettSoftware\MyMoney\Net
if not exist %TARGET% goto :nothing

pushd %TARGET%

set first=
set last=
for /f "usebackq" %%i in (`dir /b`) do (
    if !first! == !! set first=%%i
    set last=%%i
)

echo oldest=!first! and latest=!last!
if !first! == !! goto :nothing
if !first! == !last! goto :onlyone

set branch="clovett/remove_!first!"
git checkout -b "%branch%"
rd /s /q !first!
git commit -a -m "remove Money.Net version %VERSION%"
git push -u origin "%branch%"

echo =============================================================================================================
echo Please create Pull Request for the "%branch%" branch.
call gitweb
goto :eof

:nowinget
echo Please clone git@github.com:lovettchris/winget-pkgs.git into %WINGET_SRC%
exit /b 1

:nothing
echo Nothing there in %TARGET% ?
exit /b 1

:onlyone
echo There's only one versioon there; !last!
exit /b 1