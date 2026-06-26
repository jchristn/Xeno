import { ChevronsLeft, ChevronsRight, ChevronLeft, ChevronRight, RefreshCw } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';

const pageSizes = [10, 25, 50, 100];
const refreshIntervals = [0, 15, 30, 60, 180, 300];

export default function Pagination({ currentPage, onPageChange, onPageSizeChange, onRefresh, pageSize, totalItems }) {
  const { t } = useTranslation();
  const [autoRefreshSeconds, setAutoRefreshSeconds] = useState(15);
  const refreshRef = useRef(onRefresh);
  const totalPages = Math.max(1, Math.ceil(Math.max(totalItems, 0) / pageSize));
  const start = totalItems === 0 ? 0 : (currentPage - 1) * pageSize + 1;
  const end = Math.min(currentPage * pageSize, totalItems);

  useEffect(() => {
    refreshRef.current = onRefresh;
  }, [onRefresh]);

  useEffect(() => {
    if (!autoRefreshSeconds || !refreshRef.current) return undefined;
    const interval = window.setInterval(() => {
      refreshRef.current?.();
    }, autoRefreshSeconds * 1000);
    return () => window.clearInterval(interval);
  }, [autoRefreshSeconds]);

  function goTo(page) {
    onPageChange(Math.min(Math.max(1, page), totalPages));
  }

  return (
    <div className="pagination-bar">
      <div className="pagination-summary">
        <span>{t('showingRange', { start, end, total: totalItems })}</span>
        <label title={t('tooltipPageSize')}>
          <span>{t('perPage')}</span>
          <select value={pageSize} onChange={(event) => onPageSizeChange(Number(event.target.value))} title={t('tooltipPageSize')}>
            {pageSizes.map((size) => <option key={size} value={size}>{size}</option>)}
          </select>
        </label>
      </div>
      <div className="pagination-controls">
        <label className="pagination-refresh" title={t('tooltipAutoRefresh')}>
          <span>{t('autoRefresh')}</span>
          <select
            disabled={!onRefresh}
            value={autoRefreshSeconds}
            onChange={(event) => setAutoRefreshSeconds(Number(event.target.value))}
            title={t('tooltipAutoRefresh')}
          >
            {refreshIntervals.map((seconds) => (
              <option key={seconds} value={seconds}>
                {seconds === 0 ? t('none') : `${seconds}s`}
              </option>
            ))}
          </select>
        </label>
        {onRefresh ? (
          <button className="pagination-button" onClick={onRefresh} title={t('tooltipManualRefresh')} type="button">
            <RefreshCw size={14} />
          </button>
        ) : null}
        <button className="pagination-button" disabled={currentPage <= 1} onClick={() => goTo(1)} title={t('tooltipFirstPage')} type="button">
          <ChevronsLeft size={14} />
        </button>
        <button className="pagination-button" disabled={currentPage <= 1} onClick={() => goTo(currentPage - 1)} title={t('tooltipPreviousPage')} type="button">
          <ChevronLeft size={14} />
        </button>
        <span className="pagination-page">{t('pageOf', { page: currentPage, total: totalPages })}</span>
        <button className="pagination-button" disabled={currentPage >= totalPages} onClick={() => goTo(currentPage + 1)} title={t('tooltipNextPage')} type="button">
          <ChevronRight size={14} />
        </button>
        <button className="pagination-button" disabled={currentPage >= totalPages} onClick={() => goTo(totalPages)} title={t('tooltipLastPage')} type="button">
          <ChevronsRight size={14} />
        </button>
      </div>
    </div>
  );
}
