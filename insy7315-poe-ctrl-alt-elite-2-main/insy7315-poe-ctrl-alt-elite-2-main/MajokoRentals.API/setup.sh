#!/bin/bash
# Majoko Rentals API Setup Script
# This script sets up the API without needing terminal access

echo "======================================"
echo "Majoko Rentals API Setup"
echo "======================================"
echo ""

# Set the API directory
API_DIR="/home/drmp/majoko-api"

echo "Setting executable permissions..."
cd "$API_DIR"
chmod +x MajokoRentals.API
find . -name "*.so" -exec chmod +x {} \;

echo "Creating necessary directories..."
mkdir -p logs
mkdir -p Firebase

echo "Setting permissions on Firebase key..."
if [ -f "Firebase/serviceAccountKey.json" ]; then
    chmod 600 Firebase/serviceAccountKey.json
    echo "Firebase key permissions set."
else
    echo "WARNING: Firebase/serviceAccountKey.json not found!"
    echo "Please upload it to $API_DIR/Firebase/"
fi

echo ""
echo "======================================"
echo "Setup Complete!"
echo "======================================"
echo ""
echo "To start the API, run:"
echo "cd $API_DIR"
echo "./MajokoRentals.API"
echo ""
