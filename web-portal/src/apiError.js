export function getLocalizedApiError(error, t, fallback) {
  const payload = error.response?.data;
  const errorCode = payload?.errorCode;
  const translated = errorCode ? t(`apiErrors.${errorCode}`) : null;

  if (translated && translated !== `apiErrors.${errorCode}`) {
    const storageType = payload?.details?.storageType;
    const localizedStorageType = storageType ? t(storageType.toLowerCase()) : storageType;
    return translated.replace('{storageType}', localizedStorageType || storageType || '');
  }

  // API validation text is not localized yet. Keep the selected interface language
  // consistent until the backend returns culture-aware error resources.
  return fallback;
}
