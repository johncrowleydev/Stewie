FROM python:3.12-slim

RUN pip install --no-cache-dir pika==1.3.2

COPY stub_agent.py /app/stub_agent.py

WORKDIR /app

CMD ["python", "-u", "stub_agent.py"]
