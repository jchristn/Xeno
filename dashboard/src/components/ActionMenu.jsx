import { useEffect, useRef, useState } from 'react';
import { MoreVertical } from 'lucide-react';
import { useTranslation } from 'react-i18next';

export default function ActionMenu({ items = [] }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const menuRef = useRef(null);

  useEffect(() => {
    if (!open) return undefined;

    function handlePointerDown(event) {
      if (menuRef.current && !menuRef.current.contains(event.target)) {
        setOpen(false);
      }
    }

    function handleKeyDown(event) {
      if (event.key === 'Escape') setOpen(false);
    }

    document.addEventListener('mousedown', handlePointerDown);
    window.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('mousedown', handlePointerDown);
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [open]);

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
    <div className="action-menu" data-row-click-ignore="true" ref={menuRef}>
      <button aria-label={t('rowActions')} className="action-menu-trigger" onClick={toggle} title={t('rowActions')} type="button">
        <MoreVertical size={16} />
      </button>
      {open ? (
        <div className="action-menu-popover">
          {items.map((item) => item.divider ? (
            <div className="action-menu-divider" key={item.key} />
          ) : (
            <button
              className={`action-menu-item ${item.destructive ? 'destructive' : ''}`}
              disabled={item.disabled}
              key={item.key}
              onClick={(event) => select(event, item)}
              type="button"
            >
              {item.icon ? <span>{item.icon}</span> : null}
              <span>{item.label}</span>
            </button>
          ))}
        </div>
      ) : null}
    </div>
  );
}
