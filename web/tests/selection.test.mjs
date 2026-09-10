import test from 'node:test';
import assert from 'node:assert/strict';
import {
  toggleSelection,
  selectedTotal,
} from '../src/features/booking/model/selection.ts';
const seats = [
  { id: 1, sectionId: 10, status: 'Available' },
  { id: 2, sectionId: 10, status: 'Available' },
  { id: 3, sectionId: 20, status: 'Available' },
  { id: 4, sectionId: 10, status: 'Sold' },
];
test('selection cannot cross ticket zones or exceed the published limit', () => {
  assert.deepEqual(toggleSelection([1], seats[2], seats, 4), [1]);
  assert.deepEqual(toggleSelection([1], seats[1], seats, 1), [1]);
});
test('available seats can be added and removed; sold seats cannot be added', () => {
  assert.deepEqual(toggleSelection([1], seats[1], seats, 4), [1, 2]);
  assert.deepEqual(toggleSelection([1], seats[0], seats, 4), []);
  assert.deepEqual(toggleSelection([1], seats[3], seats, 4), [1]);
});
test('summary uses prices from loaded ticket sections', () => {
  assert.equal(
    selectedTotal([1, 2], seats, [
      { id: 10, price: 2300 },
      { id: 20, price: 800 },
    ]),
    4600,
  );
});

test('login draft restores only the matching performance and rejects malformed seats', async () => {
  const { readSelectionDraft } =
    await import('../src/features/booking/model/selection.ts');
  const draft = {
    performanceId: 1,
    sectionId: 11,
    quantity: 2,
    selectionMode: 'Manual',
    seatIds: [1102, 1103],
  };
  assert.deepEqual(readSelectionDraft(draft, 1)?.seatIds, [1102, 1103]);
  assert.equal(readSelectionDraft(draft, 2), null);
  assert.equal(
    readSelectionDraft({ ...draft, seatIds: [1102, 1102] }, 1),
    null,
  );
  assert.equal(readSelectionDraft({ ...draft, quantity: 9 }, 1), null);
  assert.equal(readSelectionDraft(null, 1), null);
});
