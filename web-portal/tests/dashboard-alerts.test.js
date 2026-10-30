import test from 'node:test';
import assert from 'node:assert/strict';
import { formatDaysLeft, formatShownOfTotal, lowStockSeverity } from '../src/domain/dashboardAlerts.js';

const t = (key) => ({ expiresToday: 'today', daysLeftValue: '{days} days' })[key] ?? key;

test('chemicals without active units are out of stock, others are low', () => {
  assert.equal(lowStockSeverity(0), 'out');
  assert.equal(lowStockSeverity(-1), 'out');
  assert.equal(lowStockSeverity(2), 'low');
});

test('shown/total label never shows a total smaller than the visible rows', () => {
  assert.equal(formatShownOfTotal(10, 14), '10 / 14');
  assert.equal(formatShownOfTotal(3, 3), '3 / 3');
  assert.equal(formatShownOfTotal(2, undefined), '2 / 2');
  assert.equal(formatShownOfTotal(undefined, undefined), '0 / 0');
});

test('days left uses the translated template and clamps negative values', () => {
  assert.equal(formatDaysLeft(10, t), '10 days');
  assert.equal(formatDaysLeft(0, t), 'today');
  assert.equal(formatDaysLeft(-2, t), 'today');
});
