@echo off
set PATH=%PATH%;C:\Program Files\dotnet
echo Building and publishing POS Admin Tool...
"C:\Program Files\dotnet\dotnet.exe" publish "%~dp0src\PosAdminTool.WinUI\PosAdminTool.WinUI.csproj" -c Debug -r win-x64 --self-contained false --nologo -v quiet
if %errorlevel% neq 0 ( echo Publish failed & exit /b 1 )
set POS_ADMIN_SKIP_ELEVATION=true
"%~dp0src\PosAdminTool.WinUI\bin\Debug\net10.0-windows10.0.19041.0\win-x64\publish\PosAdminTool.WinUI.exe"
