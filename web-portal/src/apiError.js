const messageKeys = {
  'Geçersiz kullanıcı adı veya parola!': 'apiMessages.invalidCredentials',
  'Geçersiz kullanıcı adı veya şifre!': 'apiMessages.invalidCredentials',
  'Çok fazla başarısız giriş denemesi. Lütfen bir dakika sonra tekrar deneyin.': 'apiMessages.tooManyAttempts',
  'Oturum yenilenemedi.': 'apiMessages.sessionExpired',
};

export function getLocalizedApiError(error, t, fallback) {
  const payload = error.response?.data;
  const errorCode = payload?.errorCode;
  const translated = errorCode ? t(`apiErrors.${errorCode}`) : null;

  if (translated && translated !== `apiErrors.${errorCode}`) {
    const storageType = payload?.details?.storageType;
    const localizedStorageType = storageType ? t(storageType.toLowerCase()) : storageType;
    return translated.replace('{storageType}', localizedStorageType || storageType || '');
  }

  const messageKey = messageKeys[payload?.message];
  if (messageKey) {
    const message = t(messageKey);
    if (message && message !== messageKey) return message;
  }

  // API validation text is not localized yet. Keep the selected interface language
  // consistent until the backend returns culture-aware error resources.
  return fallback;
}
