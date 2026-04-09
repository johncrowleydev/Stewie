# Governance Worker — Stewie governance check runner
# Stack: Alpine + bash + jq + git + dotnet-sdk + nodejs/npm
# REF: JOB-007 T-070, CON-001 §6

FROM alpine:3.19

# Install base tools
RUN apk add --no-cache bash jq git

# Install .NET SDK for dotnet build/test detection
RUN apk add --no-cache dotnet8-sdk || true

# Install Node.js for npm test detection
RUN apk add --no-cache nodejs npm || true

WORKDIR /workspace

# Copy entrypoint and rules
COPY entrypoint.sh /entrypoint.sh
COPY rules/ /rules/
RUN chmod +x /entrypoint.sh
RUN chmod +x /rules/*.sh 2>/dev/null || true

ENTRYPOINT ["/entrypoint.sh"]
