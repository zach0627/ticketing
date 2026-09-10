import { useMemo, useState } from 'react';
import { useLocation, useParams } from 'react-router';
import type { SeatDto } from '../../catalog';
import { useReservation } from './useReservation';
import { useSeatMap } from './useSeatMap';
import {
  readSelectionDraft,
  selectedTotal,
  toggleSelection,
} from '../model/selection';

export function useSeatSelection() {
  const { performanceId = '' } = useParams();
  const id = Number(performanceId);
  const location = useLocation();
  const [draft] = useState(() =>
    readSelectionDraft(
      (location.state as { seatDraft?: unknown } | null)?.seatDraft,
      id,
    ),
  );
  const [selected, setSelected] = useState<number[]>(draft?.seatIds ?? []);
  const [sectionId, setSectionId] = useState<number | null>(
    draft?.sectionId ?? null,
  );
  const [contiguous, setContiguous] = useState(
    draft?.selectionMode === 'Contiguous',
  );
  const [quantity, setQuantity] = useState(draft?.quantity ?? 2);
  const query = useSeatMap(id);
  const { data, refetch } = query;

  const { user, error, setError, isSubmitting, uncertain, reserve } =
    useReservation(id, refetch, () => setSelected([]));

  const seatsById = useMemo(
    () => new Map(data?.seats.map((seat) => [seat.id, seat])),
    [data],
  );
  const section =
    data?.sections.find((s) => s.id === sectionId) ?? data?.sections[0];
  const onSale = data?.salesStatus === 'OnSale';
  const wanted = contiguous ? quantity : selected.length;
  const total = contiguous
    ? (section?.price ?? 0) * quantity
    : selectedTotal(selected, data?.seats ?? [], data?.sections ?? []);
  const staleSelection = selected.some(
    (seatId) => seatsById.get(seatId)?.status !== 'Available',
  );
  const locked = isSubmitting || uncertain;

  function toggleSeat(seat: SeatDto) {
    if (locked || contiguous || !onSale) return;
    if (
      !selected.includes(seat.id) &&
      selected.length >= data!.maxTicketsPerBuyer
    )
      setError(
        `本場每人最多 ${data!.maxTicketsPerBuyer} 張，可先取消已選座位再更換。`,
      );
    else setError(null);
    setSelected((current) =>
      toggleSelection(current, seat, data!.seats, data!.maxTicketsPerBuyer),
    );
  }

  function submit() {
    if (!section || (!uncertain && (!onSale || !wanted || staleSelection)))
      return;
    void reserve({
      sectionId: section.id,
      quantity: wanted,
      selectionMode: contiguous ? 'Contiguous' : 'Manual',
      seatIds: contiguous ? [] : selected,
    });
  }

  function changeSection(id: number) {
    setSectionId(id);
    setSelected([]);
    setError(null);
  }
  function changeMode(value: boolean) {
    setContiguous(value);
    setSelected([]);
    setError(null);
  }
  const removeSeat = (seatId: number) =>
    setSelected((current) => current.filter((id) => id !== seatId));
  return {
    query,
    section,
    onSale,
    wanted,
    total,
    staleSelection,
    locked,
    selected,
    seatsById,
    contiguous,
    quantity,
    user,
    error,
    isSubmitting,
    uncertain,
    changeSection,
    changeMode,
    setQuantity,
    toggleSeat,
    removeSeat,
    submit,
  };
}
