#!/bin/bash
# Build the stewie-opencode-agent Docker image.
# Run from the repository root: bash docker/opencode-agent/build.sh
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
docker build -t stewie-opencode-agent "$SCRIPT_DIR"
echo "✓ stewie-opencode-agent image built successfully"
