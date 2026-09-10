import { useQuery } from '@tanstack/react-query';
import { useLocation, useParams } from 'react-router';
import { ordersApi } from '../api/ordersApi';

export function useOrderDetail() {
  const { orderId = '' } = useParams();
  const location = useLocation();
  const query = useQuery({
    queryKey: ['order', orderId],
    queryFn: ({ signal }) => ordersApi.get(orderId, signal),
  });

  return { query, purchased: !!location.state?.purchased };
}
