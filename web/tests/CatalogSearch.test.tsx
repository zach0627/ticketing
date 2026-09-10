import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
} from '@testing-library/react';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import { CatalogSearch } from '../src/features/catalog/CatalogSearch';

beforeEach(() => vi.useFakeTimers());
afterEach(() => {
  cleanup();
  vi.useRealTimers();
});
const search = () => screen.getByRole('searchbox') as HTMLInputElement;
const advance = () => act(() => vi.advanceTimersByTime(250));

test('中文組字期間不更新網址，選字完成後搜尋完整文字', () => {
  const onChange = vi.fn();
  render(<CatalogSearch value="" onChange={onChange} />);
  fireEvent.compositionStart(search());
  fireEvent.change(search(), { target: { value: 'ㄊㄞ' } });
  advance();
  expect(search().value).toBe('ㄊㄞ');
  expect(onChange).not.toHaveBeenCalled();
  fireEvent.change(search(), { target: { value: '台北' } });
  fireEvent.compositionEnd(search(), { data: '台北' });
  advance();
  expect(search().value).toBe('台北');
  expect(onChange).toHaveBeenCalledExactlyOnceWith('台北');
});

test('快速輸入中英文只搜尋最後內容，網址回應不覆蓋較新的輸入', () => {
  const onChange = vi.fn();
  const { rerender } = render(<CatalogSearch value="" onChange={onChange} />);
  fireEvent.change(search(), { target: { value: 'LUM' } });
  fireEvent.change(search(), { target: { value: 'LUMINA' } });
  advance();
  expect(onChange).toHaveBeenCalledExactlyOnceWith('LUMINA');
  fireEvent.change(search(), { target: { value: 'LUMINA 台北' } });
  rerender(<CatalogSearch value="LUMINA" onChange={onChange} />);
  expect(search().value).toBe('LUMINA 台北');
  advance();
  expect(onChange).toHaveBeenLastCalledWith('LUMINA 台北');
});

test('外部清除篩選與導覽會同步輸入框，不殘留舊查詢', () => {
  const onChange = vi.fn();
  const { rerender } = render(
    <CatalogSearch value="台北" onChange={onChange} />,
  );
  expect(search().value).toBe('台北');
  rerender(<CatalogSearch value="" onChange={onChange} />);
  expect(search().value).toBe('');
  advance();
  expect(onChange).not.toHaveBeenCalled();
  rerender(<CatalogSearch value="周以川" onChange={onChange} />);
  expect(search().value).toBe('周以川');
});

test('開始組字會取消尚未送出的英文搜尋', () => {
  const onChange = vi.fn();
  render(<CatalogSearch value="" onChange={onChange} />);
  fireEvent.change(search(), { target: { value: 'L' } });
  fireEvent.compositionStart(search());
  advance();
  expect(onChange).not.toHaveBeenCalled();
  fireEvent.change(search(), { target: { value: '陸承恩' } });
  fireEvent.compositionEnd(search(), { data: '陸承恩' });
  advance();
  expect(onChange).toHaveBeenCalledExactlyOnceWith('陸承恩');
});
