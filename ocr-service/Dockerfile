FROM hertzg/tesseract-server:latest@sha256:29f62caf8cb22f6d47ee13e297f259e36597b7294d5b886c0fe78d9fbf82ef65

# Include Turkish training data in the image so OCR startup does not need internet access.
USER root
RUN apk add --no-cache tesseract-ocr-data-tur=5.5.1-r0
