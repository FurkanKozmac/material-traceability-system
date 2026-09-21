import * as SecureStore from 'expo-secure-store';
import AsyncStorage from '@react-native-async-storage/async-storage';
import Constants from 'expo-constants';

const expoHost = Constants.expoConfig?.hostUri?.split(':')[0]
  || Constants.manifest2?.extra?.expoClient?.hostUri?.split(':')[0];
const DEFAULT_IP = `http://${expoHost || 'localhost'}:5059`;

const normalizeBaseUrl = (value) => {
  const withProtocol = /^https?:\/\//i.test(value) ? value : `http://${value}`;
  return withProtocol.replace(/\/+$/, '').replace(/\/api$/i, '');
};

export const getBackendUrl = async () => {
  try {
    const savedIp = await AsyncStorage.getItem('backend_ip');
    if (savedIp) {
      if (savedIp.startsWith('http://') || savedIp.startsWith('https://')) {
        return normalizeBaseUrl(savedIp);
      }
      return normalizeBaseUrl(savedIp);
    }
  } catch (e) {
    console.error('Sunucu IP adresi yüklenemedi', e);
  }
  return DEFAULT_IP;
};

const getHeaders = async (isJson = true) => {
  let token = null;
  try {
    token = await SecureStore.getItemAsync('token');
  } catch (e) {
    console.error('Güvenli depodan oturum anahtarı alınamadı', e);
  }
  
  const headers = {};
  if (isJson) {
    headers['Content-Type'] = 'application/json';
  }
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }
  return headers;
};

const extractErrorMessage = async (response, defaultMsg) => {
  try {
    const errText = await response.text();
    try {
      const errObj = JSON.parse(errText);
      return errObj.message || errObj.error || errText || defaultMsg;
    } catch (parseErr) {
      return errText || defaultMsg;
    }
  } catch (e) {
    return defaultMsg;
  }
};

const readJsonResponse = async (response, context) => {
  const contentType = response.headers.get('content-type') || '';
  const text = await response.text();

  if (!contentType.toLowerCase().includes('application/json')) {
    const htmlReceived = text.trimStart().startsWith('<');
    throw new Error(htmlReceived
      ? `${context}: Sunucu JSON yerine HTML döndürdü. Server Connection Setup ekranında bilgisayarın yerel ağ IP adresini ve 5059 portunu kullanın.`
      : `${context}: Geçersiz sunucu yanıtı.`);
  }

  try {
    return JSON.parse(text);
  } catch {
    throw new Error(`${context}: Sunucudan geçersiz JSON yanıtı alındı.`);
  }
};

let refreshPromise = null;

const refreshAccessToken = async () => {
  if (refreshPromise) return refreshPromise;
  refreshPromise = (async () => {
    const refreshToken = await SecureStore.getItemAsync('refreshToken');
    if (!refreshToken) throw new Error('Oturum süresi doldu');
    const baseUrl = await getBackendUrl();
    const response = await fetch(`${baseUrl}/api/auth/refresh`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken }),
    });
    if (!response.ok) throw new Error(await extractErrorMessage(response, 'Oturum yenilenemedi'));
    const data = await response.json();
    await SecureStore.setItemAsync('token', data.accessToken);
    await SecureStore.setItemAsync('refreshToken', data.refreshToken);
    return data.accessToken;
  })().catch(async (error) => {
    await SecureStore.deleteItemAsync('token');
    await SecureStore.deleteItemAsync('refreshToken');
    throw error;
  }).finally(() => { refreshPromise = null; });
  return refreshPromise;
};

export const hasSession = async () => Boolean(
  await SecureStore.getItemAsync('token') || await SecureStore.getItemAsync('refreshToken'));

export const fetchWithAuth = async (url, options = {}) => {
  const headers = options.headers || (await getHeaders(!options.method || options.method === 'GET' ? false : true));
  options.headers = headers;
  
  let response = await fetch(url, options);

  if (response.status === 401 && !options._retried) {
    const newToken = await refreshAccessToken();
    const retryOptions = { ...options, _retried: true,
      headers: { ...options.headers, Authorization: `Bearer ${newToken}` } };
    return fetch(url, retryOptions);
  }

  return response;
};

export const scanUnit = async (barcode) => {
  const baseUrl = await getBackendUrl();
  const headers = await getHeaders(false);
  const response = await fetchWithAuth(`${baseUrl}/api/units/barcode/${encodeURIComponent(barcode)}`, { headers });
  if (!response.ok) {
    throw new Error(await extractErrorMessage(response, 'Stok birimi sorgulanamadı'));
  }
  return readJsonResponse(response, 'Barkod sorgusu başarısız');
};

export const scanBatch = async (barcode) => {
  const baseUrl = await getBackendUrl();
  const headers = await getHeaders(false);
  const response = await fetchWithAuth(
    `${baseUrl}/api/batches/barcode/${encodeURIComponent(barcode)}`,
    { headers }
  );
  if (!response.ok) {
    throw new Error(await extractErrorMessage(response, 'Hammadde partisi sorgulanamadı'));
  }
  return readJsonResponse(response, 'Hammadde partisi sorgusu başarısız');
};

export const consumeUnit = async (barcode) => {
  const baseUrl = await getBackendUrl();
  const headers = await getHeaders();
  const response = await fetchWithAuth(
    `${baseUrl}/api/units/${encodeURIComponent(barcode)}/consume`,
    { method: 'POST', headers }
  );
  if (!response.ok) {
    throw new Error(await extractErrorMessage(response, 'Stok birimi tüketime verilemedi'));
  }
  return readJsonResponse(response, 'Tüketim işlemi başarısız');
};

export const listRelocationTasks = async () => {
  const baseUrl = await getBackendUrl();
  const response = await fetchWithAuth(`${baseUrl}/api/relocation-tasks`, { headers: await getHeaders(false) });
  if (!response.ok) throw new Error(await extractErrorMessage(response, 'Taşıma görevleri yüklenemedi'));
  return readJsonResponse(response, 'Taşıma görevleri yüklenemedi');
};

export const completeRelocationTask = async (taskId, unitBarcode, targetAddressBarcode) => {
  const baseUrl = await getBackendUrl();
  const response = await fetchWithAuth(`${baseUrl}/api/relocation-tasks/${taskId}/complete`, {
    method: 'PUT', headers: await getHeaders(), body: JSON.stringify({ unitBarcode, targetAddressBarcode }),
  });
  if (!response.ok) throw new Error(await extractErrorMessage(response, 'Raf taşıma tamamlanamadı'));
};

const pendingKey = async (intent, body) => {
  const storageKey = `pending_operation:${intent}`;
  const bodyFingerprint = JSON.stringify(body);
  let pending = null;
  try { pending = JSON.parse(await AsyncStorage.getItem(storageKey)); } catch { pending = null; }
  if (!pending || pending.bodyFingerprint !== bodyFingerprint) {
    pending = { key: `${Date.now()}-${Math.random().toString(36).slice(2)}`, bodyFingerprint };
    await AsyncStorage.setItem(storageKey, JSON.stringify(pending));
  }
  return { key: pending.key, clear: () => AsyncStorage.removeItem(storageKey) };
};

const pickingCommand = async (intent, path, body) => {
  const baseUrl = await getBackendUrl();
  const headers = await getHeaders();
  const operation = await pendingKey(intent, body);
  headers['Idempotency-Key'] = operation.key;
  const response = await fetchWithAuth(`${baseUrl}${path}`, { method: 'POST', headers, body: JSON.stringify(body) });
  if (!response.ok) throw new Error(await extractErrorMessage(response, 'Picking command failed'));
  await operation.clear();
  return response.json();
};

export const getPickingOrder = async (orderId) => {
  const baseUrl = await getBackendUrl();
  const response = await fetchWithAuth(`${baseUrl}/api/picking-orders/${orderId}`, { headers: await getHeaders(false) });
  if (!response.ok) throw new Error(await extractErrorMessage(response, 'İş emri bulunamadı'));
  return response.json();
};

export const listPickingOrders = async () => {
  const baseUrl = await getBackendUrl();
  const response = await fetchWithAuth(`${baseUrl}/api/picking-orders`, { headers: await getHeaders(false) });
  if (!response.ok) throw new Error(await extractErrorMessage(response, 'Aktif iş emirleri yüklenemedi'));
  return response.json();
};

export const reservePickingUnitAutomatically = (orderId, barcode, overrideReason = null) =>
  pickingCommand(`reserve-auto:${orderId}:${barcode}`, `/api/picking-orders/${orderId}/scan`,
    { barcode, overrideReason });

export const issuePickingOrder = (orderId, expectedVersion) =>
  pickingCommand(`issue:${orderId}:${expectedVersion}`, `/api/picking-orders/${orderId}/issue`, { expectedVersion });

export const consumePickingOrder = (orderId, expectedVersion) =>
  pickingCommand(`consume:${orderId}:${expectedVersion}`, `/api/picking-orders/${orderId}/consume`, { expectedVersion });

export const login = async (username, password, authSource = 'DB') => {
  const baseUrl = await getBackendUrl();
  const response = await fetch(`${baseUrl}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username, password, authSource }),
  });
  if (!response.ok) {
    throw new Error(await extractErrorMessage(response, 'Giriş başarısız'));
  }
  return readJsonResponse(response, 'Giriş başarısız');
};

export const testConnection = async (candidateUrl) => {
  const normalized = candidateUrl.trim().replace(/\/+$/, '');
  const baseUrl = normalizeBaseUrl(normalized);
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), 5000);
  try {
    const response = await fetch(`${baseUrl}/api/health`, { signal: controller.signal });
    if (!response.ok) throw new Error(`Sunucu HTTP ${response.status} yanıtını döndürdü`);
    await readJsonResponse(response, 'Bağlantı testi başarısız');
    return baseUrl;
  } catch (error) {
    if (error.name === 'AbortError') throw new Error('Bağlantı 5 saniye içinde kurulamadı');
    throw error;
  } finally {
    clearTimeout(timer);
  }
};

export const logout = async () => {
  const refreshToken = await SecureStore.getItemAsync('refreshToken');
  await SecureStore.deleteItemAsync('token');
  await SecureStore.deleteItemAsync('refreshToken');
  await AsyncStorage.removeItem('session_user');
  try {
    if (refreshToken) {
      const baseUrl = await getBackendUrl();
      const controller = new AbortController();
      const timer = setTimeout(() => controller.abort(), 3000);
      try {
        await fetch(`${baseUrl}/api/auth/logout`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ refreshToken }),
          signal: controller.signal,
        });
      } finally {
        clearTimeout(timer);
      }
    }
  } catch (error) {
    if (error.name !== 'AbortError') console.warn('Yerel oturum kapatıldı ancak sunucu oturumu kapatılamadı', error);
  }
};
