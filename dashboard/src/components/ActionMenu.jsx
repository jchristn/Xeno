import { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { MoreVertical } from 'lucide-react';
import { useTranslation } from 'react-i18next';

export default function ActionMenu({ items = [] }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const [position, setPosition] = useState({ left: 0, top: 0 });
  const containerRef = useRef(null);
  const menuRef = useRef(null);
  const triggerRef = useRef(null);

  useEffect(() => {
    if (!open) return undefined;

    function updatePosition() {
      const rect = triggerRef.current?.getBoundingClientRect();
      if (!rect) return;
      const width = 190;
      const menuHeight = menuRef.current?.offsetHeight || Math.min(360, Math.max(44, items.filter((item) => !item.divider).length * 38 + items.filter((item) => item.divider).length * 9 + 16));
      const left = Math.min(window.innerWidth - width - 12, Math.max(12, rect.right - width));
      const belowTop = rect.bottom + 6;
      const aboveTop = rect.top - menuHeight - 6;
      const hasRoomBelow = belowTop + menuHeight <= window.innerHeight - 12;
      const top = hasRoomBelow ? belowTop : Math.max(12, aboveTop);
      setPosition({ left, top });
    }

    function handlePointerDown(event) {
      if (
        menuRef.current
        && !menuRef.current.contains(event.target)
        && triggerRef.current
        && !triggerRef.current.contains(event.target)
      ) {
        setOpen(false);
      }
    }

    function handleKeyDown(event) {
      if (event.key === 'Escape') setOpen(false);
    }

    updatePosition();
    document.addEventListener('mousedown', handlePointerDown);
    window.addEventListener('resize', updatePosition);
    window.addEventListener('scroll', updatePosition, true);
    window.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('mousedown', handlePointerDown);
      window.removeEventListener('resize', updatePosition);
      window.removeEventListener('scroll', updatePosition, true);
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [items, open]);

  function toggle(event) {
    event.preventDefault();
    event.stopPropagation();
    setOpen((current) => !current);
  }

  function select(event, item) {
    event.preventDefault();
    event.stopPropagation();
    setOpen(false);
    item.onSelect?.();
  }

  return (
    <div className="action-menu" data-row-click-ignore="true" ref={containerRef}>
      <button aria-label={t('rowActions')} className="action-menu-trigger" onClick={toggle} ref={triggerRef} title={t('tooltipRowActions')} type="button">
        <MoreVertical size={16} />
      </button>
      {open ? createPortal(
        <div className="action-menu-popover" data-row-click-ignore="true" ref={menuRef} style={{ left: position.left, top: position.top }}>
          {items.map((item) => item.divider ? (
            <div className="action-menu-divider" key={item.key} />
          ) : (
            <button
              className={`action-menu-item ${item.destructive ? 'destructive' : ''}`}
              disabled={item.disabled}
              key={item.key}
              onClick={(event) => select(event, item)}
              title={item.tooltip || t('tooltipGenericButton', { label: item.label })}
              type="button"
            >
              {item.icon ? <span>{item.icon}</span> : null}
              <span>{item.label}</span>
            </button>
          ))}
        </div>,
        document.body
      ) : null}
    </div>
  );
}
