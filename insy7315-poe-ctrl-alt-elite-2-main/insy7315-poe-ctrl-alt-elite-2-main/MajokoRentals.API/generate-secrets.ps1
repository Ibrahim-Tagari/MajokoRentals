# Security Secret Generator for Majoko Rentals API
# Run this script to generate secure random keys

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  Majoko Rentals API - Secret Generator" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""

# Function to generate random string
function Generate-RandomKey {
    param (
        [int]$Length = 64
    )
    $chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()-_=+[]{}|;:,.<>?'
    -join ((1..$Length) | ForEach-Object { $chars[(Get-Random -Maximum $chars.Length)] })
}

# Generate JWT Secret Key (64 characters)
Write-Host "1. JWT Secret Key (64 characters):" -ForegroundColor Yellow
$jwtSecret = Generate-RandomKey -Length 64
Write-Host $jwtSecret -ForegroundColor Green
Write-Host ""

# Generate API Key (32 characters)
Write-Host "2. Generic API Key (32 characters):" -ForegroundColor Yellow
$apiKey = Generate-RandomKey -Length 32
Write-Host $apiKey -ForegroundColor Green
Write-Host ""

# Generate Password (24 characters)
Write-Host "3. Strong Password (24 characters):" -ForegroundColor Yellow
$password = Generate-RandomKey -Length 24
Write-Host $password -ForegroundColor Green
Write-Host ""

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "IMPORTANT: Store these securely!" -ForegroundColor Red
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Add to your .env file or environment variables:" -ForegroundColor Yellow
Write-Host ""
Write-Host "JWT__SecretKey=$jwtSecret" -ForegroundColor White
Write-Host ""
Write-Host "NEVER commit these values to version control!" -ForegroundColor Red
Write-Host ""
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
