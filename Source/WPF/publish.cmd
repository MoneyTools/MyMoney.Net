@echo off
if "%LOVETTSOFTWARE_STORAGE_CONNECTION_STRING%" == "" goto :nokey
pushd %~dp0
copy /y MyMoney\Setup\changes.xml publish
echo Uploading ClickOnce installer to XmlNotepad
AzurePublishClickOnce %~dp0publish downloads/MyMoney "%LOVETTSOFTWARE_STORAGE_CONNECTION_STRING%"
goto :eof

:nokey
echo Please set your LOVETTSOFTWARE_STORAGE_CONNECTION_STRING
exit /b 1