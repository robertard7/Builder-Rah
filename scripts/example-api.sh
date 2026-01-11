#!/usr/bin/env bash
set -euo pipefail

session=$(curl -s -X POST http://localhost:5050/sessions | jq -r .sessionId)
curl -s -X POST "http://localhost:5050/sessions/${session}/message" -H "Content-Type: application/json" -d '{"text":"Summarize the attached document"}'
curl -s "http://localhost:5050/sessions/${session}/status"
curl -s -X POST "http://localhost:5050/sessions/${session}/cancel"
curl -s "http://localhost:5050/provider/metrics"
