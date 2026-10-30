// Pure helpers for the dashboard alert panels (near-expiry batches and low-stock chemicals).

export const lowStockSeverity = (activeUnits) => (Number(activeUnits) <= 0 ? 'out' : 'low');

// The API returns at most 10 rows per list plus the full total; show both, e.g. "10 / 14".
export const formatShownOfTotal = (shown, total) => {
  const safeShown = Math.max(0, Number(shown) || 0);
  const safeTotal = Math.max(safeShown, Number(total) || 0);
  return `${safeShown} / ${safeTotal}`;
};

export const formatDaysLeft = (days, t) => {
  const value = Math.max(0, Math.trunc(Number(days) || 0));
  if (value === 0) return t('expiresToday');
  return t('daysLeftValue').replace('{days}', String(value));
};
