import test from 'node:test';
import assert from 'node:assert/strict';
import {
  remainingSeconds,
  formatRemaining,
} from '../src/features/booking/countdown.ts';

test('countdown uses server duration and monotonic elapsed time, clamped at zero', () => {
  const server = '2026-09-10T01:00:00Z',
    expiry = '2026-09-10T01:05:00Z';
  assert.equal(remainingSeconds(server, expiry), 300);
  assert.equal(remainingSeconds(server, expiry, 61_000), 239);
  assert.equal(remainingSeconds(server, expiry, 400_000), 0);
  assert.equal(formatRemaining(239), '3:59');
});
test('missing response dates never display NaN and a fresh response resets the baseline', () => {
  assert.equal(remainingSeconds('', ''), 0);
  assert.equal(remainingSeconds('invalid', ''), 0);
  assert.equal(
    remainingSeconds('2026-09-10T01:03:00Z', '2026-09-10T01:05:00Z'),
    120,
  );
});
