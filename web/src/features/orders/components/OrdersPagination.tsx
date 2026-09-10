export function OrdersPagination({
  total,
  page,
  previousPage,
  nextPage,
}: {
  total: number;
  page: number;
  previousPage: () => void;
  nextPage: () => void;
}) {
  return (
    <>
      {total > 10 && (
        <nav className="pagination" aria-label="訂單分頁">
          <button
            className="cta cta--secondary"
            disabled={page <= 1}
            onClick={previousPage}
          >
            上一頁
          </button>
          <span>
            第 {page} / {Math.ceil(total / 10)} 頁
          </span>
          <button
            className="cta cta--secondary"
            disabled={page * 10 >= total}
            onClick={nextPage}
          >
            下一頁
          </button>
        </nav>
      )}
    </>
  );
}
