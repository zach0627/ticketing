import { useEffect, useRef, useState } from 'react';

export function useCatalogSearch(
  value: string,
  onChange: (value: string) => void,
) {
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

  return { draft, setDraft, setComposing };
}
