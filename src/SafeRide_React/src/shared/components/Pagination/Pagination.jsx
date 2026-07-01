import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faChevronLeft, faChevronRight } from '@fortawesome/free-solid-svg-icons';
import './Pagination.css';
function Pagination({ currentPage, totalPages, onPageChange }) {
    const handleClick = (page) => {
        if (page >= 1 && page <= totalPages && page !== currentPage) {
            onPageChange?.(page);
        }
    };
    /** Build an array of page numbers with ellipsis */
    const getPages = () => {
        if (totalPages <= 5) {
            return Array.from({ length: totalPages }, (_, i) => i + 1);
        }
        const pages = [1, 2, 3];
        if (currentPage > 4) {
            pages.push('...');
        }
        if (currentPage > 3 && currentPage < totalPages - 1) {
            pages.push(currentPage);
        }
        if (currentPage < totalPages - 2) {
            pages.push('...');
        }
        pages.push(totalPages);
        return pages;
    };
    return (<div className="pagination" id="pagination">
      <button className="pagination-btn pagination-arrow" disabled={currentPage === 1} onClick={() => handleClick(currentPage - 1)} aria-label="Trang trước" type="button">
        <FontAwesomeIcon icon={faChevronLeft}/>
      </button>

      {getPages().map((page, i) => page === '...' ? (<span key={`dots-${i}`} className="pagination-dots">...</span>) : (<button key={page} className={`pagination-btn${page === currentPage ? ' pagination-btn--active' : ''}`} onClick={() => handleClick(page)} type="button">
            {page}
          </button>))}

      <button className="pagination-btn pagination-arrow" disabled={currentPage === totalPages} onClick={() => handleClick(currentPage + 1)} aria-label="Trang sau" type="button">
        <FontAwesomeIcon icon={faChevronRight}/>
      </button>
    </div>);
}
export default Pagination;
