import { useEventTabs } from '../hooks/useEventTabs';

export function EventTabs() {
  const { active, setActive, sections } = useEventTabs();
  return (
    <nav className="detail-tabs" aria-label="活動資訊">
      {sections.map((section) => (
        <a
          key={section.id}
          href={`#${section.id}`}
          aria-current={active === section.id ? 'location' : undefined}
          onClick={() => setActive(section.id)}
        >
          {section.label}
        </a>
      ))}
    </nav>
  );
}
