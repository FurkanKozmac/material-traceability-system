import test from 'node:test';
import assert from 'node:assert/strict';
import {
  canConfirmImport,
  formatImportSummary,
  importSeverity,
  rowStatusColor,
} from '../src/domain/chemicalImport.js';

const preview = (overrides = {}) => ({
  dryRun: true, committed: false, totalRows: 3, insertCount: 2, skippedCount: 1, failedCount: 0, ...overrides,
});

test('confirm is allowed only for a clean dry-run preview that inserts rows', () => {
  assert.equal(canConfirmImport(preview()), true);
  assert.equal(canConfirmImport(preview({ failedCount: 1 })), false);
  assert.equal(canConfirmImport(preview({ insertCount: 0 })), false);
  assert.equal(canConfirmImport(preview({ dryRun: false })), false);
  assert.equal(canConfirmImport(null), false);
});

test('severity reflects errors, nothing-to-add, and success', () => {
  assert.equal(importSeverity(preview({ failedCount: 2 })), 'error');
  assert.equal(importSeverity(preview({ insertCount: 0 })), 'warning');
  assert.equal(importSeverity(preview()), 'success');
  assert.equal(importSeverity(null), 'info');
});

test('row status colors and summary text', () => {
  assert.equal(rowStatusColor('Insert'), 'success');
  assert.equal(rowStatusColor('Skip'), 'default');
  assert.equal(rowStatusColor('Error'), 'error');
  assert.equal(
    formatImportSummary('{total} · {insert} · {skipped} · {failed}', preview()),
    '3 · 2 · 1 · 0',
  );
});
