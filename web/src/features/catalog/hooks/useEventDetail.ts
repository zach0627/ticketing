import { useQuery } from '@tanstack/react-query';
import { useParams } from 'react-router';
import { catalogQueries } from '../api/catalogQueries';

export function useEventDetail() {
  const { code = '' } = useParams();
  return useQuery(catalogQueries.event(code));
}
