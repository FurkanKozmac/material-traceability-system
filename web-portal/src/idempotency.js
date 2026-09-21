const prefix = 'mts.pending-operation.';

export const pendingOperation = (intent) => {
  const storageKey = `${prefix}${intent}`;
  let key = localStorage.getItem(storageKey);
  if (!key) {
    key = crypto.randomUUID();
    localStorage.setItem(storageKey, key);
  }
  return {
    key,
    clear: () => localStorage.removeItem(storageKey),
  };
};
