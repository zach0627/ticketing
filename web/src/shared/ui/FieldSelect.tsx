import { useId } from 'react';
import * as Select from '@radix-ui/react-select';
import { Icon } from './Icon';
import './fieldSelect.css';

export interface SelectOption {
  value: string;
  label: string;
}

/** 共用選單負責鍵盤、焦點與呈現；篩選值由呼叫端控制。 */
export function FieldSelect({
  label,
  value,
  options,
  onChange,
  disabled = false,
}: {
  label: string;
  value: string;
  options: SelectOption[];
  onChange: (value: string) => void;
  disabled?: boolean;
}) {
  const id = useId();
  const current = options.some((option) => option.value === value)
    ? value
    : (options[0]?.value ?? '');
  return (
    <div className="select-field">
      <label htmlFor={id}>{label}</label>
      <Select.Root
        value={current || '__all'}
        onValueChange={(next) => onChange(next === '__all' ? '' : next)}
        disabled={disabled}
      >
        <Select.Trigger
          id={id}
          aria-label={label}
          className="select-trigger"
          data-filtered={current !== options[0]?.value || undefined}
        >
          <Select.Value />
          <Select.Icon className="select-chevron">
            <Icon name="chevron" size={16} />
          </Select.Icon>
        </Select.Trigger>
        <Select.Portal>
          <Select.Content
            className="select-menu"
            position="popper"
            sideOffset={6}
            collisionPadding={12}
          >
            <Select.ScrollUpButton className="select-scroll">
              <Icon
                name="chevron"
                size={14}
                style={{ transform: 'rotate(-90deg)' }}
              />
            </Select.ScrollUpButton>
            <Select.Viewport className="select-options">
              {options.map((option) => (
                <Select.Item
                  key={option.value}
                  value={option.value || '__all'}
                  className="select-option"
                >
                  <Select.ItemText>{option.label}</Select.ItemText>
                  <Select.ItemIndicator className="select-check">
                    <Icon name="check" size={17} />
                  </Select.ItemIndicator>
                </Select.Item>
              ))}
            </Select.Viewport>
            <Select.ScrollDownButton className="select-scroll">
              <Icon
                name="chevron"
                size={14}
                style={{ transform: 'rotate(90deg)' }}
              />
            </Select.ScrollDownButton>
          </Select.Content>
        </Select.Portal>
      </Select.Root>
    </div>
  );
}
