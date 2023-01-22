@echo off

for %%i in (Release, Debug) do (     
  msbuild /target:restore MyMoney.sln /p:Configuration=%%i "/p:Platform=Any CPU"
  if ERRORLEVEL 1 goto :err_restore

  msbuild /target:rebuild MyMoney.sln /p:Configuration=%%i "/p:Platform=Any CPU"
  if ERRORLEVEL 1 goto :err_build

  vstest.console  UnitTests\bin\%%i\net7.0-windows\UnitTests.dll

  echo To run the GUI tests type this...
  echo vstest.console  ScenarioTest\bin\%%i\net7.0-windows\ScenarioTest.dll
)

goto :eof
