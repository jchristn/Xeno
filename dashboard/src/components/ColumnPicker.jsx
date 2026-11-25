import { useEffect, useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { Columns3 } from 'lucide-react';
import { useTranslation } from 'react-i18next';

// Most columns a table shows before the user asks for more. Wide enough for the common case,
// narrow enough that a table on a laptop screen does not scroll sideways.
export const DEFAULT_VISIBLE_COLUMN_LIMIT = 6;

const storagePrefix = 'xeno.tableColumns.';

function readStoredColumns(tableId) {
  try {
    const raw = window.localStorage.getItem(storagePrefix + tableId);
    if (!raw) return null;
    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) ? parsed.filter((item) => typeof item === 'string') : null;
  } catch {
    return null;
  }
}

function writeStoredColumns(tableId, keys) {
  try {
    if (keys == null) window.localStorage.removeItem(storagePrefix + tableId);
    else window.localStorage.setItem(storagePrefix + tableId, JSON.stringify(keys));
  } catch {
    // Storage can be blocked (private window); the choice then lasts for this page view only.
  }
}

/**
 * Track which columns of a table are visible.
 * allKeys: every column key, in display order.
 * defaultKeys: the columns shown until the user chooses; falls back to the first DEFAULT_VISIBLE_COLUMN_LIMIT.
 * tableId: stable id used to remember the choice in this browser; defaults to the column keys.
 * Returns { visibleKeys, setVisibleKeys, resetVisibleKeys, defaultKeys }; visibleKeys keeps allKeys order and is never empty.
 */
export function useColumnVisibility(allKeys, defaultKeys, tableId) {
  const keySignature = allKeys.join('|');
  const id = tableId || keySignature;
  const defaults = useMemo(() => {
    const requested = Array.isArray(defaultKeys) && defaultKeys.length > 0
      ? allKeys.filter((key) => defaultKeys.includes(key))
      : allKeys.slice(0, DEFAULT_VISIBLE_COLUMN_LIMIT);
    return requested.length > 0 ? requested : allKeys.slice(0, 1);
  }, [keySignature, (defaultKeys || []).join('|')]);
  const [chosen, setChosen] = useState(() => readStoredColumns(id));

  useEffect(() => {
    setChosen(readStoredColumns(id));
  }, [id]);

  const visibleKeys = useMemo(() => {
    const source = chosen ? allKeys.filter((key) => chosen.includes(key)) : defaults;
    return source.length > 0 ? source : defaults;
  }, [chosen, defaults, keySignature]);

  function setVisibleKeys(keys) {
    const ordered = allKeys.filter((key) => keys.includes(key));
    if (ordered.length === 0) return;
    setChosen(ordered);
    writeStoredColumns(id, ordered);
  }

  function resetVisibleKeys() {
    setChosen(null);
    writeStoredColumns(id, null);
  }

  return { defaultKeys: defaults, resetVisibleKeys, setVisibleKeys, visibleKeys };
}

/**
 * Columns control for a table toolbar: a button that opens a checklist of the table's columns.
 * columns: [{ key, label }] in display order. At least one column always stays visible.
 */
export default function ColumnPicker({ columns, onChange, onReset, visibleKeys }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const [position, setPosition] = useState({ left: 0, top: 0 });
  const triggerRef = useRef(null);
  const menuRef = useRef(null);

  useEffect(() => {
    if (!open) return undefined;

    function updatePosition() {
      const rect = triggerRef.current?.getBoundingClientRect();
      if (!rect) return;
      const width = 240;
      const height = menuRef.current?.offsetHeight || 320;
      const left = Math.min(window.innerWidth - width - 12, Math.max(12, rect.right - width));
      const below = rect.bottom + 6;
      const top = below + height <= window.innerHeight - 12 ? below : Math.max(12, rect.top - height - 6);
      setPosition({ left, top });
    }

    function handlePointerDown(event) {
      if (menuRef.current?.contains(event.target) || triggerRef.current?.contains(event.target)) return;
      setOpen(false);
    }

    function handleKeyDown(event) {
      if (event.key === 'Escape') {
        event.stopPropagation();
        setOpen(false);
      }
    }

    updatePosition();
    document.addEventListener('mousedown', handlePointerDown);
    window.addEventListener('resize', updatePosition);
    window.addEventListener('scroll', updatePosition, true);
    window.addEventListener('keydown', handleKeyDown, true);
    return () => {
      document.removeEventListener('mousedown', handlePointerDown);
      window.removeEventListener('resize', updatePosition);
      window.removeEventListener('scroll', updatePosition, true);
      window.removeEventListener('keydown', handleKeyDown, true);
    };
  }, [open]);

  if (!Array.isArray(columns) || columns.length < 2) return null;

  function toggle(key) {
    const next = visibleKeys.includes(key) ? visibleKeys.filter((item) => item !== key) : [...visibleKeys, key];
    if (next.length === 0) return;
    onChange(next);
  }

  const shown = visibleKeys.length;
  return (
    <>
      <button
        aria-expanded={open}
        aria-haspopup="true"
        className="pagination-button column-picker-trigger"
        data-row-click-ignore="true"
        onClick={(event) => { event.stopPropagation(); setOpen((value) => !value); }}
        ref={triggerRef}
        title={t('tooltipColumns')}
        type="button"
      >
        <Columns3 size={14} />
        <span>{t('columns')}</span>
        <span className="column-picker-count">{shown}/{columns.length}</span>
      </button>
      {open ? createPortal(
        <div aria-label={t('columns')} className="column-picker-popover" data-row-click-ignore="true" ref={menuRef} role="dialog" style={{ left: position.left, top: position.top }}>
          <div className="column-picker-heading">
            <strong>{t('columns')}</strong>
            <span>{t('columnsShown', { shown, total: columns.length })}</span>
          </div>
          <div className="column-picker-list">
            {columns.map((column) => {
              const checked = visibleKeys.includes(column.key);
              const locked = checked && shown === 1;
              return (
                <label className={`column-picker-option${locked ? ' locked' : ''}`} key={column.key} title={locked ? t('tooltipColumnsKeepOne') : t('tooltipColumnToggle', { column: column.label })}>
                  <input checked={checked} disabled={locked} onChange={() => toggle(column.key)} type="checkbox" />
                  <span>{column.label}</span>
                </label>
              );
            })}
          </div>
          <div className="column-picker-actions">
            <button className="action-button" onClick={() => onChange(columns.map((column) => column.key))} title={t('tooltipColumnsShowAll')} type="button">{t('showAll')}</button>
            <button className="action-button" onClick={() => onReset?.()} title={t('tooltipColumnsReset')} type="button">{t('resetColumns')}</button>
          </div>
        </div>,
        document.body
      ) : null}
    </>
  );
}
