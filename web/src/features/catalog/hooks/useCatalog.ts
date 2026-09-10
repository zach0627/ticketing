import { useQuery } from '@tanstack/react-query';
import { useSearchParams } from 'react-router';
import { catalogQueries } from '../api/catalogQueries';
import { filterEvents } from '../model/catalogFilters';

export function useCatalog() {
  const [params, setParams] = useSearchParams();
  const { data, isPending, error, refetch } = useQuery(catalogQueries.events());
  const events = data ? filterEvents(data.items, params) : [];
  function update(key: string, value: string) {
    setParams(
      (current) => {
        const next = new URLSearchParams(current);
        if (value) next.set(key, value);
        else next.delete(key);
        return next;
      },
      { replace: true, preventScrollReset: true },
    );
  }
  const clearFilters = () => setParams({}, { preventScrollReset: true });
  return {
    data,
    isPending,
    error,
    refetch,
    events,
    params,
    update,
    clearFilters,
  };
}
