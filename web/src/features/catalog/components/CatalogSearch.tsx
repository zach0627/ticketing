import { Icon } from '../../../shared/ui/Icon';
import { useCatalogSearch } from '../hooks/useCatalogSearch';

export function CatalogSearch({
  value,
  onChange,
}: {
  value: string;
  onChange: (value: string) => void;
}) {
  const { draft, setDraft, setComposing } = useCatalogSearch(value, onChange);
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
