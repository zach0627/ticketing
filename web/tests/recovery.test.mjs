import test from 'node:test';
import assert from 'node:assert/strict';
import { pendingOperations } from '../src/shared/api/idempotency.ts';
import {
  shouldPollSeats,
  seatIdleTimeout,
} from '../src/features/booking/polling.ts';

test('pending writes restore only for the same buyer, operation and resource', () => {
  const values = new Map();
  globalThis.sessionStorage = {
    setItem: (k, v) => values.set(k, v),
    getItem: (k) => values.get(k) ?? null,
    removeItem: (k) => values.delete(k),
  };
  const operation = {
    userId: 'buyer-a',
    operation: 'hold',
    target: '1',
    key: 'unchanged-request-key',
    payload: { seatIds: [1104, 1105] },
  };
  pendingOperations.save(operation);
  assert.deepEqual(pendingOperations.read('buyer-a', 'hold', '1'), operation);
  assert.equal(pendingOperations.read('buyer-b', 'hold', '1'), null);
  assert.equal(pendingOperations.read('buyer-a', 'checkout', '1'), null);
  assert.equal(pendingOperations.read('buyer-a', 'hold', '2'), null);
  pendingOperations.clear();
  assert.equal(pendingOperations.read('buyer-a', 'hold', '1'), null);
  delete globalThis.sessionStorage;
});
test('unavailable browser storage does not crash the current operation', () => {
  globalThis.sessionStorage = {
    setItem() {
      throw Error('blocked');
    },
    getItem() {
      throw Error('blocked');
    },
    removeItem() {
      throw Error('blocked');
    },
  };
  assert.doesNotThrow(() =>
    pendingOperations.save({
      userId: 'a',
      operation: 'hold',
      target: '1',
      key: 'key',
    }),
  );
  assert.equal(pendingOperations.read('a', 'hold', '1'), null);
  assert.doesNotThrow(() => pendingOperations.clear());
  delete globalThis.sessionStorage;
});
test('seat polling stops at ten idle minutes and resumes after a new interaction', () => {
  const start = 1000;
  assert.equal(shouldPollSeats(start, start + seatIdleTimeout - 1), true);
  assert.equal(shouldPollSeats(start, start + seatIdleTimeout), false);
  const resumed = start + seatIdleTimeout + 10000;
  assert.equal(shouldPollSeats(resumed, resumed + 10000), true);
});
