import cv2
import numpy as np
from fastapi import FastAPI, File, HTTPException, UploadFile
from paddleocr import PaddleOCR

app = FastAPI(title="MTS PaddleOCR", docs_url=None, redoc_url=None)

# Initialize once at process startup. PaddleOCR downloads its model weights on
# first startup and reuses its local model cache for subsequent restarts.
# Başlatma parametrelerini el yazısı ve mobil fotoğraflar için optimize ediyoruz:
ocr_engine = PaddleOCR(
    use_angle_cls=True,
    lang="en",
    ocr_version="PP-OCRv4",
    show_log=False,
    det_limit_side_len=2048,
    det_db_thresh=0.2,
    det_db_box_thresh=0.35,
    det_db_unclip_ratio=2.2
)


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "healthy"}


@app.post("/ocr")
async def extract_text(file: UploadFile = File(...)) -> dict[str, str]:
    image_bytes = await file.read()
    if not image_bytes:
        raise HTTPException(status_code=400, detail="Image file is empty")

    try:
        image_array = cv2.imdecode(np.frombuffer(image_bytes, dtype=np.uint8), cv2.IMREAD_COLOR)
        if image_array is None:
            raise ValueError("Invalid image content")
    except (ValueError, cv2.error) as exc:
        raise HTTPException(status_code=400, detail="Image could not be decoded") from exc

    try:
        pages = ocr_engine.ocr(image_array, cls=True) or []
    except Exception as exc:
        raise HTTPException(status_code=500, detail="OCR processing failed") from exc

    detected_lines: list[tuple[float, float, str]] = []
    for page in pages:
        for detection in page or []:
            try:
                box, recognized = detection
                text = str(recognized[0]).strip()
                if not text:
                    continue
                center_y = sum(float(point[1]) for point in box) / len(box)
                center_x = sum(float(point[0]) for point in box) / len(box)
                detected_lines.append((center_y, center_x, text))
            except (TypeError, ValueError, IndexError):
                continue

    detected_lines.sort(key=lambda line: (line[0], line[1]))
    return {"text": "\n".join(line[2] for line in detected_lines)}
