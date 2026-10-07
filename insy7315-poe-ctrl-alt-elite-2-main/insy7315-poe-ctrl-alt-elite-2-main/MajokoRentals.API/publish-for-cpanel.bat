@echo off
echo ====================================
echo Publishing Majoko Rentals API
echo ====================================
echo.

REM Clean previous publish folder
if exist ".\publish-cpanel\" (
    echo Cleaning previous publish folder...
    rmdir /s /q ".\publish-cpanel"
)

echo Publishing application (self-contained for Linux)...
dotnet publish MajokoRentals.API.csproj -c Release -f net9.0 -o ./publish-cpanel --runtime linux-x64 --self-contained true

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ====================================
    echo ERROR: Publish failed!
    echo ====================================
    pause
    exit /b 1
)

echo.
echo ====================================
echo Publishing completed successfully!
echo ====================================
echo.
echo Output folder: .\publish-cpanel
echo.
echo Next steps:
echo 1. Compress the publish-cpanel folder to a .zip file
echo 2. Upload to your cPanel hosting via FTP or File Manager
echo 3. Extract on the server
echo 4. Follow the deployment instructions in DEPLOYMENT.md
echo.
pause
