const keys = {
  BATCH_REQUIRED: 'batchRequired',
  BATCH_BARCODE_INVALID: 'batchBarcodeInvalid',
  BATCH_MISMATCH: 'batchMismatch',
};

export const consumeErrorKey = (code) => keys[code] || 'consumeFailed';
