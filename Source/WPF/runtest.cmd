@echo off
vstest.console  UnitTests\bin\Release\net7.0-windows\UnitTests.dll
if ERRORLEVEL 1 goto :eof

if exist C:\Users\lovet\AppData\Local\Temp\Screen.png del C:\Users\lovet\AppData\Local\Temp\Screen.png
vstest.console  ScenarioTest\bin\Release\net7.0-windows\ScenarioTest.dll > test.log 2>&1
if ERRORLEVEL 1 start notepad test.log
if exist C:\Users\lovet\AppData\Local\Temp\Screen.png start C:\Users\lovet\AppData\Local\Temp\Screen.png

