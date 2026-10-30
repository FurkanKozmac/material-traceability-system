import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { getLocalizedApiError } from '../src/apiError.js';

const translations = {
  'apiErrors.LAST_ACTIVE_ADMIN': 'Son aktif yönetici korunmalıdır.',
  'apiErrors.USERNAME_ALREADY_EXISTS': 'Bu kullanıcı adı zaten kullanılıyor.',
  'apiMessages.invalidCredentials': 'Kullanıcı adı veya parola hatalı.',
};

const t = (key) => translations[key] ?? key;

test('api errors prefer errorCode and fall back to known backend messages', () => {
  assert.equal(
    getLocalizedApiError({ response: { data: { errorCode: 'LAST_ACTIVE_ADMIN' } } }, t, 'fallback'),
    'Son aktif yönetici korunmalıdır.',
  );
  assert.equal(
    getLocalizedApiError({ response: { data: { errorCode: 'USERNAME_ALREADY_EXISTS' } } }, t, 'fallback'),
    'Bu kullanıcı adı zaten kullanılıyor.',
  );
  assert.equal(
    getLocalizedApiError({ response: { data: { message: 'Geçersiz kullanıcı adı veya parola!' } } }, t, 'fallback'),
    'Kullanıcı adı veya parola hatalı.',
  );
  assert.equal(
    getLocalizedApiError({ response: { data: { message: 'Bilinmeyen hata' } } }, t, 'fallback'),
    'fallback',
  );
});

test('OCR disabled and unavailable codes are shown through apiErrors without extra code', () => {
  const ocrTranslations = {
    'apiErrors.OCR_DISABLED': 'Etiket tarama (OCR) bu ortamda kapalı. Parti bilgilerini elle girin.',
    'apiErrors.OCR_UNAVAILABLE': 'OCR servisine şu an ulaşılamıyor. Daha sonra tekrar deneyin veya bilgileri elle girin.',
  };
  const translate = (key) => ocrTranslations[key] ?? key;
  const ocrError = (errorCode) => ({
    response: { status: 503, data: { statusCode: 503, errorCode, message: 'Sunucu mesajı' } },
  });

  assert.equal(getLocalizedApiError(ocrError('OCR_DISABLED'), translate, 'fallback'), ocrTranslations['apiErrors.OCR_DISABLED']);
  assert.equal(getLocalizedApiError(ocrError('OCR_UNAVAILABLE'), translate, 'fallback'), ocrTranslations['apiErrors.OCR_UNAVAILABLE']);
});

test('OCR api error translations exist in both TR and EN', () => {
  const source = readFileSync(new URL('../src/LanguageContext.jsx', import.meta.url), 'utf8');
  for (const code of ['OCR_DISABLED', 'OCR_UNAVAILABLE']) {
    assert.equal(source.match(new RegExp(`\\b${code}:`, 'g'))?.length, 2, code);
  }
});
