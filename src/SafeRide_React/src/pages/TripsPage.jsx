import { useState } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faClipboardList, faRoute, faSearch } from '@fortawesome/free-solid-svg-icons';
import { AdminLayout } from '../shared/layouts/AdminLayout';
import useAdminSearch from '../shared/hooks/useAdminSearch';
import AdminTripDetailsPage from './AdminTripDetailsPage';
import './TripsPage.css';

function TripsPage() {
  useAdminSearch({
    placeholder: 'Tìm kiếm mã chuyến đi hoặc mã booking...',
  });
  const [tripId, setTripId] = useState('');
  const [bookingId, setBookingId] = useState('');
  const [target, setTarget] = useState(null);
  const [error, setError] = useState('');

  const handleTripLookup = (event) => {
    event.preventDefault();
    const parsed = parseLookupId(tripId);
    if (!parsed) {
      setError('Vui lòng nhập mã chuyến đi hợp lệ.');
      return;
    }

    setError('');
    setTarget({ tripId: parsed });
  };

  const handleBookingLookup = (event) => {
    event.preventDefault();
    const parsed = parseLookupId(bookingId);
    if (!parsed) {
      setError('Vui lòng nhập mã booking hợp lệ.');
      return;
    }

    setError('');
    setTarget({ bookingId: parsed });
  };

  return (
    <AdminLayout>
      {target ? (
        <AdminTripDetailsPage
          tripId={target.tripId}
          bookingId={target.bookingId}
          onBack={() => setTarget(null)}
        />
      ) : (
        <div className="trips-page">
          <header className="trips-page__header">
            <div>
              <h1>Thông tin chuyến đi</h1>
              <p>Tra cứu và xem toàn bộ thông tin của một chuyến đi đã phát sinh trong hệ thống.</p>
            </div>
          </header>

          <section className="trips-lookup-grid">
            <form className="trips-lookup-card" onSubmit={handleTripLookup}>
              <span className="trips-lookup-card__icon">
                <FontAwesomeIcon icon={faRoute} />
              </span>
              <h2>Tìm theo Trip ID</h2>
              <p>Nhập mã chuyến đi, ví dụ: SR-94210 hoặc 94210.</p>
              <label>
                <span>Trip ID</span>
                <input
                  type="text"
                  value={tripId}
                  onChange={(event) => setTripId(event.target.value)}
                  placeholder="SR-94210"
                />
              </label>
              <button type="submit">
                <FontAwesomeIcon icon={faSearch} />
                Xem chuyến đi
              </button>
            </form>

            <form className="trips-lookup-card" onSubmit={handleBookingLookup}>
              <span className="trips-lookup-card__icon trips-lookup-card__icon--booking">
                <FontAwesomeIcon icon={faClipboardList} />
              </span>
              <h2>Tìm theo Booking ID</h2>
              <p>Dùng khi admin đang có mã booking từ Booking Management.</p>
              <label>
                <span>Booking ID</span>
                <input
                  type="text"
                  value={bookingId}
                  onChange={(event) => setBookingId(event.target.value)}
                  placeholder="SR-500"
                />
              </label>
              <button type="submit">
                <FontAwesomeIcon icon={faSearch} />
                Xem chuyến đi
              </button>
            </form>
          </section>

          {error && <div className="trips-page__error" role="alert">{error}</div>}
        </div>
      )}
    </AdminLayout>
  );
}

function parseLookupId(value) {
  const normalized = String(value ?? '')
    .trim()
    .replace(/^#/u, '')
    .replace(/^SR-/iu, '');
  const parsed = Number.parseInt(normalized, 10);

  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

export default TripsPage;
