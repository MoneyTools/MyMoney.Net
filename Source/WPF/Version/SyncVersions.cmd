 @echo off

 set VERSIONS_FOLDER=%~dp0
set EXE=%VERSIONS_FOLDER%..\UpdateVersion\bin\Any CPU\Release\net7.0\UpdateVersion.exe

if not EXIST "%EXE%" goto :build
goto :run

:build
msbuild %VERSIONS_FOLDER%..\UpdateVersion\UpdateVersion.csproj "/p:Platform=Any CPU" /p:Configuration=Release
if ERRORLEVEL 1 goto :failed

:run
"%EXE%" "%VERSIONS_FOLDER%VersionMaster.txt"
if ERRORLEVEL 1 goto :failed

goto :eof

:missing
echo cannot find: "%EXE%"

:failed
echo ### sync versions failed.