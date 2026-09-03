import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faChevronLeft,
  faChevronRight,
} from "@fortawesome/free-solid-svg-icons";
import "./Pagination.css";
function Pagination({ currentPage, totalPages, onPageChange }) {
  const safeTotalPages = Math.max(1, Number(totalPages) || 1);
  const safeCurrentPage = Math.min(Math.max(1, Number(currentPage) || 1), safeTotalPages);
  const handleClick = (page) => {
    if (page >= 1 && page <= safeTotalPages && page !== safeCurrentPage) {
      onPageChange?.(page);
    }
  };
  /** Keep the active page visible while avoiding duplicate or missing page buttons. */
  const getPages = () => {
    if (safeTotalPages <= 7) {
      return Array.from({ length: safeTotalPages }, (_, i) => i + 1);
    }
    const pages = [1];
    const start = Math.max(2, safeCurrentPage - 1);
    const end = Math.min(safeTotalPages - 1, safeCurrentPage + 1);
    if (start > 2) {
      pages.push('...');
    }
    for (let page = start; page <= end; page += 1) {
      pages.push(page);
    }
    if (end < safeTotalPages - 1) {
      pages.push('...');
    }
    pages.push(safeTotalPages);
    return pages;
  };
  return (
    <div className="pagination" id="pagination">
      <button
        className="pagination-btn pagination-arrow"
        disabled={safeCurrentPage === 1}
        onClick={() => handleClick(safeCurrentPage - 1)}
        aria-label="Trang trước"
        type="button"
      >
        <FontAwesomeIcon icon={faChevronLeft} />
      </button>

      {getPages().map((page, i) =>
        page === "..." ? (
          <span key={`dots-${i}`} className="pagination-dots">
            ...
          </span>
        ) : (
          <button
            key={page}
            className={`pagination-btn${page === safeCurrentPage ? " pagination-btn--active" : ""}`}
            onClick={() => handleClick(page)}
            aria-current={page === safeCurrentPage ? 'page' : undefined}
            type="button"
          >
            {page}
          </button>
        ),
      )}

      <button
        className="pagination-btn pagination-arrow"
        disabled={safeCurrentPage === safeTotalPages}
        onClick={() => handleClick(safeCurrentPage + 1)}
        aria-label="Trang sau"
        type="button"
      >
        <FontAwesomeIcon icon={faChevronRight} />
      </button>
    </div>
  );
}
export default Pagination;
