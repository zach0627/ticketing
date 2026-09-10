import { Icon } from '../../../shared/ui/Icon';

export function ReservationButton({
  className,
  isSubmitting,
  uncertain,
  onSale,
  wanted,
  staleSelection,
  isAuthenticated,
  onSubmit,
}: {
  className: string;
  isSubmitting: boolean;
  uncertain: boolean;
  onSale: boolean;
  wanted: number;
  staleSelection: boolean;
  isAuthenticated: boolean;
  onSubmit: () => void;
}) {
  return (
    <button
      className={className}
      type="button"
      disabled={
        isSubmitting ||
        (!uncertain && (!onSale || wanted === 0 || staleSelection))
      }
      onClick={onSubmit}
    >
      {isSubmitting
        ? '正在確認…'
        : uncertain
          ? '確認保留結果'
          : isAuthenticated
            ? '確認保留'
            : '登入後保留'}
      <Icon name="arrow" size={17} />
    </button>
  );
}
