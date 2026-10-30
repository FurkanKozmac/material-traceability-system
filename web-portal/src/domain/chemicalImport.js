// Mirrors the backend defaults (ExcelImport:MaxFileBytes / MaxRows). Update both if the server config changes.
export const MAX_IMPORT_BYTES = 2 * 1024 * 1024;
export const MAX_IMPORT_ROWS = 1000;

export const IMPORT_ROW_STATUS = Object.freeze({
  insert: 'Insert',
  skip: 'Skip',
  error: 'Error',
});

// A real import is all-or-nothing, so confirming only makes sense for a clean preview that adds something.
export function canConfirmImport(preview) {
  return Boolean(preview?.dryRun) && preview.failedCount === 0 && preview.insertCount > 0;
}

export function importSeverity(result) {
  if (!result) return 'info';
  if (result.failedCount > 0) return 'error';
  if (result.insertCount === 0) return 'warning';
  return 'success';
}

export function rowStatusColor(status) {
  if (status === IMPORT_ROW_STATUS.insert) return 'success';
  if (status === IMPORT_ROW_STATUS.skip) return 'default';
  return 'error';
}

export function formatImportSummary(template, result) {
  return template
    .replace('{total}', result.totalRows)
    .replace('{insert}', result.insertCount)
    .replace('{skipped}', result.skippedCount)
    .replace('{failed}', result.failedCount);
}
