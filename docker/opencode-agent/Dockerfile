FROM node:22-slim

# Install OpenCode CLI globally
RUN npm install -g opencode

# Install Python 3 + pika for the entrypoint harness
RUN apt-get update && apt-get install -y python3 python3-pip && \
    pip3 install --break-system-packages pika==1.3.2 && \
    apt-get clean && rm -rf /var/lib/apt/lists/*

# Copy entrypoint harness
COPY entrypoint.py /app/entrypoint.py
COPY opencode.json.template /app/opencode.json.template
COPY mock_llm.py /app/mock_llm.py

WORKDIR /workspace

ENTRYPOINT ["python3", "/app/entrypoint.py"]
