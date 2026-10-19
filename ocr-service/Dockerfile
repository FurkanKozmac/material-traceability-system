FROM python:3.10-slim-bookworm

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    HOME=/opt/paddleocr

WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    libgomp1 \
    libglib2.0-0 \
    libgl1 \
    libsm6 \
    libxext6 \
    libxrender1 \
    && rm -rf /var/lib/apt/lists/*
RUN mkdir -p /opt/paddleocr

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Fetch and cache PP-OCRv4 detection, recognition, and angle-classifier models
# in the image so runtime OCR does not depend on an external model download.
RUN python -c "from paddleocr import PaddleOCR; PaddleOCR(use_angle_cls=True, lang='en', ocr_version='PP-OCRv4', show_log=False)"

COPY main.py .

EXPOSE 8080
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8080"]
