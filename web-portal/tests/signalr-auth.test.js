import test from 'node:test';
import assert from 'node:assert/strict';
import {
  isUnauthorizedHubError, shouldRetryClosedHub, startHubWithSingleRefresh,
} from '../src/domain/signalrAuth.js';

const run = async ({ errors, refreshFails = false }) => {
  let startCalls = 0;
  let refreshCalls = 0;
  let clears = 0;
  let redirects = 0;

  const result = await startHubWithSingleRefresh({
    start: async () => {
      const error = errors[startCalls];
      startCalls += 1;
      if (error) throw error;
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

test('hub auth detects the SignalR negotiate error shapes', () => {
  assert.equal(isUnauthorizedHubError({ statusCode: 401 }), true);
  assert.equal(isUnauthorizedHubError({
    message: "Unexpected status code returned from negotiate '401'",
  }), true);
  assert.equal(isUnauthorizedHubError(new Error('Unauthorized')), true);
  assert.equal(isUnauthorizedHubError(new Error('network unavailable')), false);
  assert.equal(shouldRetryClosedHub({ statusCode: 401 }, false), true);
  assert.equal(shouldRetryClosedHub({ statusCode: 401 }, true), false);
});

test('hub auth retries once after a wrapped negotiate 401 and uses refreshed token', async () => {
  const outcome = await run({
    errors: [new Error("Unexpected status code returned from negotiate '401'"), null],
  });
  assert.deepEqual(outcome, {
    result: 'connected', startCalls: 2, refreshCalls: 1, clears: 0, redirects: 0,
  });
});

test('hub auth also handles a statusCode error', async () => {
  const outcome = await run({ errors: [{ statusCode: 401 }, null] });
  assert.deepEqual(outcome, {
    result: 'connected', startCalls: 2, refreshCalls: 1, clears: 0, redirects: 0,
  });
});

test('hub auth clears the session when refresh fails', async () => {
  const outcome = await run({
    errors: [new Error('Unauthorized')],
    refreshFails: true,
  });
  assert.deepEqual(outcome, {
    result: 'logged-out', startCalls: 1, refreshCalls: 1, clears: 1, redirects: 1,
  });
});

test('hub auth does not loop when the retry is still unauthorized', async () => {
  const outcome = await run({
    errors: [new Error('Unauthorized'), new Error("Unexpected status code returned from negotiate '401'")],
  });
  assert.deepEqual(outcome, {
    result: 'logged-out', startCalls: 2, refreshCalls: 1, clears: 1, redirects: 1,
  });
});

test('hub auth leaves non-401 failures alone', async () => {
  const outcome = await run({ errors: [new Error('network')] });
  assert.deepEqual(outcome, {
    result: 'failed', startCalls: 1, refreshCalls: 0, clears: 0, redirects: 0,
  });
});
