#!/usr/bin/env bash
# Build the stewie-stub-agent Docker image.
# Run from the repository root: bash docker/stub-agent/build.sh
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
docker build -t stewie-stub-agent "$SCRIPT_DIR"
echo "✅ stewie-stub-agent image built successfully"
