import type { EventCardDto } from '../../shared/api/types';
import { FieldSelect } from '../../shared/ui/FieldSelect';

export function CatalogToolbar({
  items,
  params,
  onChange,
}: {
  items: EventCardDto[];
  params: URLSearchParams;
  onChange: (key: string, value: string) => void;
}) {
  const category = ['Concert', 'Sport'].includes(params.get('category') ?? '')
    ? params.get('category')!
    : '';
  const cities = [...new Set(items.map((event) => event.city))];
  return (
    <>
      <nav className="catalog-tabs" aria-label="活動類別">
        {[
          { value: '', label: '全部活動' },
          { value: 'Concert', label: '演唱會' },
          { value: 'Sport', label: '運動賽事' },
        ].map((tab) => (
          <button
            key={tab.value}
            type="button"
            className={category === tab.value ? 'is-active' : ''}
            aria-pressed={category === tab.value}
            onClick={() => onChange('category', tab.value)}
          >
            {tab.label}
            <span>
              {
                items.filter(
                  (event) => !tab.value || event.category === tab.value,
                ).length
              }
            </span>
          </button>
        ))}
      </nav>
      <div className="catalog-filter-row">
        <FieldSelect
          label="活動城市"
          value={params.get('city') ?? ''}
          options={[
            { value: '', label: '所有城市' },
            ...cities.map((city) => ({ value: city, label: city })),
          ]}
          onChange={(value) => onChange('city', value)}
        />
        <FieldSelect
          label="售票狀態"
          value={params.get('sale') ?? ''}
          options={[
            { value: '', label: '所有售票狀態' },
            { value: 'OnSale', label: '售票中' },
            { value: 'NotYetOnSale', label: '即將開賣' },
            { value: 'Paused', label: '暫停售票' },
            { value: 'SalesClosed', label: '已停售' },
          ]}
          onChange={(value) => onChange('sale', value)}
        />
        <FieldSelect
          label="活動排序"
          value={params.get('sort') === 'price' ? 'price' : 'date'}
          options={[
            { value: 'date', label: '活動日期由近到遠' },
            { value: 'price', label: '票價由低到高' },
          ]}
          onChange={(value) => onChange('sort', value)}
        />
      </div>
    </>
  );
}
