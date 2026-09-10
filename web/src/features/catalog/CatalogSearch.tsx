import { useEffect, useRef, useState } from 'react';
import { Icon } from '../../shared/ui/Icon';

/** 組字與網址條件分開，避免導頁更新中斷中文輸入法。 */
export function CatalogSearch({
  value,
  onChange,
}: {
  value: string;
  onChange: (value: string) => void;
}) {
  const [draft, setDraft] = useState(value);
  const [composing, setComposing] = useState(false);
  const committed = useRef<string | null>(null);

  useEffect(() => {
    if (value === committed.current) {
      committed.current = null;
      return;
    }
    setDraft(value);
  }, [value]);

  useEffect(() => {
    if (composing || draft === value) return;
    const timer = window.setTimeout(() => {
      committed.current = draft;
      onChange(draft);
    }, 200);
    return () => window.clearTimeout(timer);
  }, [composing, draft, value, onChange]);

  return (
    <div className="catalog-search">
      <label className="search-field">
        <Icon name="search" size={18} />
        <span className="sr-only">搜尋活動、演出者或城市</span>
        <input
          type="search"
          value={draft}
          onChange={(event) => setDraft(event.currentTarget.value)}
          onCompositionStart={() => setComposing(true)}
          onCompositionEnd={(event) => {
            setDraft(event.currentTarget.value);
            setComposing(false);
          }}
          placeholder="搜尋活動、演出者或城市"
        />
      </label>
    </div>
  );
}
