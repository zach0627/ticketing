import { useEffect, useState } from 'react';

const sections = [
  { id: 'about-event', label: '活動介紹' },
  { id: 'ticket-info', label: '場次與票價' },
  { id: 'purchase-notes', label: '購票須知' },
];

export function EventTabs() {
  const [active, setActive] = useState(sections[0].id);
  useEffect(() => {
    let frame = 0;
    function update() {
      cancelAnimationFrame(frame);
      frame = requestAnimationFrame(() => {
        const current = sections
          .filter(
            (section) =>
              (document.getElementById(section.id)?.getBoundingClientRect()
                .top ?? Infinity) <= 150,
          )
          .at(-1);
        if (current) setActive(current.id);
      });
    }
    window.addEventListener('scroll', update, { passive: true });
    update();
    return () => {
      window.removeEventListener('scroll', update);
      cancelAnimationFrame(frame);
    };
  }, []);
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
