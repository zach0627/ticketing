import test from 'node:test';
import assert from 'node:assert/strict';
import { filterEvents } from '../src/features/catalog/model/catalogFilters.ts';

const events = [
  {
    id: 2,
    title: 'BLOOM 台北演唱會',
    performer: 'LUMINA',
    category: 'Concert',
    city: '台北',
    salesStatus: 'OnSale',
    minPrice: 2800,
    startsAtUtc: '2026-10-20T11:00:00Z',
  },
  {
    id: 1,
    title: '港都棒球賽',
    performer: '海鷗',
    category: 'Sport',
    city: '高雄',
    salesStatus: 'Paused',
    minPrice: 500,
    startsAtUtc: '2026-10-10T11:00:00Z',
  },
  {
    id: 3,
    title: '沿途有光',
    performer: '周以川',
    category: 'Concert',
    city: '台北',
    salesStatus: 'NotYetOnSale',
    minPrice: 1500,
    startsAtUtc: '2026-10-25T11:00:00Z',
  },
];
test('combines category, city, sale status and normalized performer search', () => {
  assert.deepEqual(
    filterEvents(
      events,
      new URLSearchParams(
        'category=Concert&city=台北&sale=OnSale&q=%20lumina%20',
      ),
    ).map((e) => e.id),
    [2],
  );
});
test('price sort keeps API data intact', () => {
  const original = [...events];
  assert.deepEqual(
    filterEvents(events, new URLSearchParams('sort=price')).map((e) => e.id),
    [1, 3, 2],
  );
  assert.deepEqual(events, original);
});
test('no results remains empty and clearing filters restores date order', () => {
  assert.deepEqual(filterEvents(events, new URLSearchParams('q=missing')), []);
  assert.deepEqual(
    filterEvents(events, new URLSearchParams()).map((e) => e.id),
    [1, 2, 3],
  );
});
test('invalid category, city and sale URL values fall back to all activities', () => {
  assert.equal(
    filterEvents(
      events,
      new URLSearchParams('category=unknown&sale=unknown&city=unknown'),
    ).length,
    3,
  );
});
