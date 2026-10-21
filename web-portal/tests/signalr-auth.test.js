import test from 'node:test';
import assert from 'node:assert/strict';
import { startHubWithSingleRefresh } from '../src/domain/signalrAuth.js';

const run = async ({ starts, refreshFails = false }) => {
  let startCalls = 0;
  let refreshCalls = 0;
  let clears = 0;
  let redirects = 0;

  const result = await startHubWithSingleRefresh({
    start: async () => {
      startCalls += 1;
      if (starts[startCalls - 1] === 'fail') throw { statusCode: 401 };
    },
    refresh: async () => {
      refreshCalls += 1;
      if (refreshFails) throw new Error('refresh failed');
    },
    clearSession: () => { clears += 1; },
    redirectToLogin: () => { redirects += 1; },
  });

  return { result, startCalls, refreshCalls, clears, redirects };
};

test('hub auth retries once after 401 and uses refreshed token on the second start', async () => {
  const outcome = await run({ starts: ['fail', 'ok'] });
  assert.deepEqual(outcome, {
    result: 'connected', startCalls: 2, refreshCalls: 1, clears: 0, redirects: 0,
  });
});

test('hub auth clears the session when refresh fails', async () => {
  const outcome = await run({ starts: ['fail'], refreshFails: true });
  assert.deepEqual(outcome, {
    result: 'logged-out', startCalls: 1, refreshCalls: 1, clears: 1, redirects: 1,
  });
});

test('hub auth does not loop when the retry is still unauthorized', async () => {
  const outcome = await run({ starts: ['fail', 'fail'] });
  assert.deepEqual(outcome, {
    result: 'logged-out', startCalls: 2, refreshCalls: 1, clears: 1, redirects: 1,
  });
});

test('hub auth leaves non-401 failures alone', async () => {
  let refreshCalls = 0;
  const result = await startHubWithSingleRefresh({
    start: async () => { throw new Error('network'); },
    refresh: async () => { refreshCalls += 1; },
    clearSession: () => {},
    redirectToLogin: () => {},
  });
  assert.equal(result, 'failed');
  assert.equal(refreshCalls, 0);
});
