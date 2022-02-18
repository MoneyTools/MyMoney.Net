@echo off
cd "%~dp0"
if "%MODERNWPF%" == "" goto :nomodernwpf
if not exist "%MODERNWPF%" goto :nomodernwpf
if not exist FindUnusedStyles\bin\x64\Debug\net5.0-windows\FindUnusedStyles.exe msbuild FindUnusedStyles\FindUnusedStyles.csproj
FindUnusedStyles\bin\x64\Debug\net5.0-windows\FindUnusedStyles.exe --import "%MODERNWPF%\ModernWpf\Styles" --import "%MODERNWPF%\ModernWpf\ThemeResources" .

goto :eof

:nomodernwpf
echo "Please set a MODERNWPF environment pointing to a clone of git@github.com:Kinnara/ModernWpf.git"