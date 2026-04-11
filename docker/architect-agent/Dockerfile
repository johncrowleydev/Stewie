FROM stewie-opencode-agent:latest

# Copy Architect-specific scripts
COPY architect_main.py /app/architect_main.py
COPY stewie_api_client.py /app/stewie_api_client.py
COPY context_builder.py /app/context_builder.py
COPY job_parser.py /app/job_parser.py

# Install additional Python dependencies
RUN pip3 install --break-system-packages requests==2.31.0

# Override entrypoint to use Architect main
ENTRYPOINT ["python3", "/app/architect_main.py"]
