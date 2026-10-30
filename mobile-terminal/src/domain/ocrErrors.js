const keys = {
  OCR_DISABLED: 'receiptOcrDisabled',
  OCR_UNAVAILABLE: 'receiptOcrUnavailable',
};

export const ocrErrorKey = (code) => keys[code] || 'receiptOcrFailed';
