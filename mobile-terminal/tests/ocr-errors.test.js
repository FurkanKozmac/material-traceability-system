import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { ocrErrorKey } from '../src/domain/ocrErrors.js';

test('OCR error codes map to specific receipt messages', () => {
  assert.equal(ocrErrorKey('OCR_DISABLED'), 'receiptOcrDisabled');
  assert.equal(ocrErrorKey('OCR_UNAVAILABLE'), 'receiptOcrUnavailable');
  assert.equal(ocrErrorKey(undefined), 'receiptOcrFailed');
  assert.equal(ocrErrorKey('SOMETHING_ELSE'), 'receiptOcrFailed');
});

test('both OCR messages are translated in TR and EN', () => {
  const source = readFileSync(new URL('../src/context/LanguageContext.js', import.meta.url), 'utf8');
  for (const key of ['receiptOcrDisabled', 'receiptOcrUnavailable']) {
    assert.equal(source.match(new RegExp(`\\b${key}:`, 'g'))?.length, 2, key);
  }
});
