#!/bin/bash

# Configuration
# Replace these with your actual values or set them in your shell profile
export NGROK_DOMAIN=${NGROK_DOMAIN:-"promenade-relight-refund.ngrok-free.dev"}
export USE_NGROK=true

echo "[WRAPER] Starting Detection Server with ngrok Static Domain..."
echo "[WRAPER] Domain: $NGROK_DOMAIN"

# Run the server
cd "$(dirname "$0")"
python server.py
