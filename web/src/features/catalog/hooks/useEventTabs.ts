import { useEffect, useState } from 'react';
import { eventSections as sections } from '../model/eventSections';

export function useEventTabs() {
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
  return { active, setActive, sections };
}
