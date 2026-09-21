import test from 'node:test';
import assert from 'node:assert/strict';
import { evaluateScanAction } from '../src/domain/scannerRules.js';

const valid = { status: 'ON_HAND', blocked: false, expired: false, fifoCompliant: true,
  allowedActions: ['PICK'], ownedByOrder: true, resolvedLine: { id: 10 }, order: { status: 'ALLOCATED' } };

test('blocked, expired and consumed packages never execute', () => {
  assert.equal(evaluateScanAction({ result: { ...valid, blocked: true }, mode: 'PICK' }).canExecute, false);
  assert.equal(evaluateScanAction({ result: { ...valid, expired: true }, mode: 'PICK' }).canExecute, false);
  assert.equal(evaluateScanAction({ result: { ...valid, status: 'CONSUMED' }, mode: 'PICK' }).canExecute, false);
});

test('FEFO override only changes PICK evaluation', () => {
  const violation = { ...valid, fifoCompliant: false };
  assert.equal(evaluateScanAction({ result: violation, mode: 'PICK' }).canExecute, false);
  assert.equal(evaluateScanAction({ result: violation, mode: 'PICK', fifoOverride: true }).canExecute, true);
  assert.equal(evaluateScanAction({ result: { ...violation, order: { status: 'ISSUED' } }, mode: 'CONSUME' }).canExecute, true);
});

test('issue and consume require order ownership and exact lifecycle state', () => {
  assert.equal(evaluateScanAction({ result: valid, mode: 'ISSUE' }).canExecute, true);
  assert.equal(evaluateScanAction({ result: { ...valid, ownedByOrder: false }, mode: 'ISSUE' }).canExecute, false);
  assert.equal(evaluateScanAction({ result: { ...valid, order: { status: 'ISSUED' } }, mode: 'CONSUME' }).canExecute, true);
});

test('pick requires a matching open order line', () => {
  assert.equal(evaluateScanAction({ result: { ...valid, resolvedLine: null }, mode: 'PICK' }).canExecute, false);
});
