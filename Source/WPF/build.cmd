@echo off

msbuild /target:restore MyMoney.sln /p:Configuration=Release "/p:Platform=Any CPU"
if ERRORLEVEL 1 goto :err_restore

msbuild /target:rebuild MyMoney.sln /p:Configuration=Release "/p:Platform=Any CPU"
if ERRORLEVEL 1 goto :err_build

vstest.console  UnitTests\bin\Release\net7.0-windows\UnitTests.dll

echo To run the GUI tests type this...
echo vstest.console  ScenarioTest\bin\Release\net7.0-windows\ScenarioTest.dll