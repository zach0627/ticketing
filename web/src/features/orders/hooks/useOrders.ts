import { useQuery } from '@tanstack/react-query';
import { useSearchParams } from 'react-router';
import { ordersApi } from '../api/ordersApi';

export function useOrders() {
  const [params, setParams] = useSearchParams();
  const parsedPage = Number(params.get('page') ?? 1);
  const page =
    Number.isSafeInteger(parsedPage) && parsedPage > 0 ? parsedPage : 1;
  const query = useQuery({
    queryKey: ['orders', page],
    queryFn: ({ signal }) => ordersApi.list(page, signal),
  });

  const previousPage = () => setParams({ page: String(page - 1) });
  const nextPage = () => setParams({ page: String(page + 1) });
  return { query, page, previousPage, nextPage };
}
