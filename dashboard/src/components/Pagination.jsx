import { ChevronsLeft, ChevronsRight, ChevronLeft, ChevronRight, RefreshCw } from 'lucide-react';
import { useTranslation } from 'react-i18next';

const pageSizes = [10, 25, 50, 100];

export default function Pagination({ currentPage, onPageChange, onPageSizeChange, onRefresh, pageSize, totalItems }) {
  const { t } = useTranslation();
  const totalPages = Math.max(1, Math.ceil(Math.max(totalItems, 0) / pageSize));
  const start = totalItems === 0 ? 0 : (currentPage - 1) * pageSize + 1;
  const end = Math.min(currentPage * pageSize, totalItems);

  function goTo(page) {
    onPageChange(Math.min(Math.max(1, page), totalPages));
  }

  return (
    <div className="pagination-bar">
      <div className="pagination-summary">
        <span>{t('showingRange', { start, end, total: totalItems })}</span>
        <label>
          <span>{t('perPage')}</span>
          <select value={pageSize} onChange={(event) => onPageSizeChange(Number(event.target.value))}>
            {pageSizes.map((size) => <option key={size} value={size}>{size}</option>)}
          </select>
        </label>
      </div>
      <div className="pagination-controls">
        {onRefresh ? (
          <button className="pagination-button" onClick={onRefresh} title={t('refresh')} type="button">
            <RefreshCw size={14} />
          </button>
        ) : null}
        <button className="pagination-button" disabled={currentPage <= 1} onClick={() => goTo(1)} title={t('firstPage')} type="button">
          <ChevronsLeft size={14} />
        </button>
        <button className="pagination-button" disabled={currentPage <= 1} onClick={() => goTo(currentPage - 1)} title={t('previousPage')} type="button">
          <ChevronLeft size={14} />
        </button>
        <span className="pagination-page">{t('pageOf', { page: currentPage, total: totalPages })}</span>
        <button className="pagination-button" disabled={currentPage >= totalPages} onClick={() => goTo(currentPage + 1)} title={t('nextPage')} type="button">
          <ChevronRight size={14} />
        </button>
        <button className="pagination-button" disabled={currentPage >= totalPages} onClick={() => goTo(totalPages)} title={t('lastPage')} type="button">
          <ChevronsRight size={14} />
        </button>
      </div>
    </div>
  );
}
