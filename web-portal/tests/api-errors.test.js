import test from 'node:test';
import assert from 'node:assert/strict';
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
