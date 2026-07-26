import { useMemo } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import {
  faArrowLeft,
  faCar,
  faCheck,
  faClock,
  faHeadset,
  faLocationDot,
  faMoneyBillWave,
  faMotorcycle,
  faPrint,
  faRoute,
  faRotateRight,
  faShieldAlt,
  faStar,
  faUser,
} from '@fortawesome/free-solid-svg-icons';
import useFetch from '../shared/hooks/useFetch';
import {
  getAdminTripDetailsPath,
  mapAdminTripDetails,
} from '../features/trips/adminTripDetailsApi';
import './AdminTripDetailsPage.css';

const MAP_IMAGE_URL =
  'https://lh3.googleusercontent.com/aida-public/AB6AXuA5REMJp1npzA32WbyFv-tUcgnsrafEVE-2ifiiXy59zaAUZymKqos5EZIxmgGzkcCHfCdqae7BDRX4cb8qCNUwtVSQWCPiNLWBsEm3whtHactE7ix1H_xnfW-Dl8MN1lT_NcnAHQ5-iPd0Iht0okt6rpMV5nomFkmToolRQUUCpTkdSd4Mjn_YzMOoMiVkHkVoMP3FmT-5JY5cT5ggppeFuvd_G0s4V0uQj21a4T9P1_ovZIUximvupoWPoivrF0IczdfgBAqDdZo';

function AdminTripDetailsPage({ tripId, bookingId, onBack }) {
  const path = useMemo(
    () => getAdminTripDetailsPath({ tripId, bookingId }),
    [bookingId, tripId],
  );
  const { data: trip, isLoading, error, refetch } = useFetch(path, {
    select: mapAdminTripDetails,
  });

  if (!path) {
    return (
      <TripDetailFrame onBack={onBack}>
        <section className="admin-trip-state admin-trip-state--empty">
          <strong>Chưa chọn chuyến đi</strong>
          <p>Chọn một chuyến đi từ Booking Management hoặc nhập mã chuyến trong trang Chuyến đi.</p>
        </section>
      </TripDetailFrame>
    );
  }

  if (isLoading && !trip) {
    return (
      <TripDetailFrame onBack={onBack}>
        <section className="admin-trip-state">
          <span className="admin-trip-state__spinner" aria-hidden="true" />
          <strong>Đang tải thông tin chuyến đi...</strong>
          <p>SafeRide đang lấy dữ liệu chuyến đi, thanh toán và đánh giá liên quan.</p>
        </section>
      </TripDetailFrame>
    );
  }

  if (error && !trip) {
    return (
      <TripDetailFrame onBack={onBack}>
        <section className="admin-trip-state admin-trip-state--error" role="alert">
          <strong>Không thể tải thông tin chuyến đi</strong>
          <p>{error}</p>
          <button type="button" onClick={refetch}>
            <FontAwesomeIcon icon={faRotateRight} />
            Thử lại
          </button>
        </section>
      </TripDetailFrame>
    );
  }

  if (!trip) {
    return (
      <TripDetailFrame onBack={onBack}>
        <section className="admin-trip-state admin-trip-state--empty">
          <strong>Không có dữ liệu chuyến đi</strong>
          <p>Chuyến đi được chọn chưa có dữ liệu chi tiết để hiển thị.</p>
        </section>
      </TripDetailFrame>
    );
  }

  return (
    <TripDetailFrame trip={trip} onBack={onBack}>
      {error && (
        <div className="admin-trip-inline-error" role="alert">
          <span>{error}</span>
          <button type="button" onClick={refetch}>Thử lại</button>
        </div>
      )}

      <header className="admin-trip-header">
        <div>
          <h1>Trip #{trip.tripCode}</h1>
          <div className="admin-trip-header__meta">
            <StatusPill label={trip.tripStatusLabel} variant={trip.tripStatusVariant} />
            <span>{trip.completedAtSummaryLabel}</span>
          </div>
        </div>
        <div className="admin-trip-header__actions">
          <button type="button" onClick={() => window.print()}>
            <FontAwesomeIcon icon={faPrint} />
            In hóa đơn
          </button>
          <button type="button" className="admin-trip-header__primary" onClick={refetch}>
            <FontAwesomeIcon icon={faRotateRight} />
            Làm mới
          </button>
          <button type="button" className="admin-trip-header__primary">
            <FontAwesomeIcon icon={faHeadset} />
            Liên hệ hỗ trợ
          </button>
        </div>
      </header>

      <div className="admin-trip-grid">
        <div className="admin-trip-main-column">
          <section className="admin-trip-card admin-trip-map-card">
            <div className="admin-trip-map">
              <img src={MAP_IMAGE_URL} alt="Bản đồ lộ trình chuyến đi" />
              <div className="admin-trip-map__chip admin-trip-map__chip--pickup">
                <FontAwesomeIcon icon={faLocationDot} />
                <span>Điểm đón: {trip.pickup.address}</span>
              </div>
              <div className="admin-trip-map__chip admin-trip-map__chip--destination">
                <FontAwesomeIcon icon={faLocationDot} />
                <span>Điểm đến: {trip.destination.address}</span>
              </div>
            </div>
            <div className="admin-trip-route-summary">
              <RoutePoint
                variant="pickup"
                title="Điểm đón"
                address={trip.pickup.address}
                coordinate={trip.pickup.coordinateLabel}
              />
              <RoutePoint
                variant="destination"
                title="Điểm đến"
                address={trip.destination.address}
                coordinate={trip.destination.coordinateLabel}
              />
            </div>
          </section>

          <section className="admin-trip-card admin-trip-payment">
            <h2>Chi tiết thanh toán</h2>
            <div className="admin-trip-payment__table">
              <table>
                <thead>
                  <tr>
                    <th>Hạng mục</th>
                    <th>Số tiền</th>
                  </tr>
                </thead>
                <tbody>
                  {trip.fare.paymentRows.map((row) => (
                    <tr
                      key={row.id}
                      className={row.total ? 'admin-trip-payment__total' : ''}
                    >
                      <td className={row.tone === 'discount' ? 'admin-trip-payment__discount' : ''}>
                        {row.label}
                      </td>
                      <td className={row.tone === 'discount' ? 'admin-trip-payment__discount' : ''}>
                        {row.amountLabel}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="admin-trip-payment__method">
              <span>
                <FontAwesomeIcon icon={faMoneyBillWave} />
                Phương thức thanh toán
              </span>
              <strong>{trip.payment?.paymentMethodLabel ?? 'Chưa phát sinh'}</strong>
              <StatusPill
                label={trip.payment?.paymentStatusLabel ?? 'Chưa phát sinh'}
                variant={trip.payment?.paymentStatusVariant ?? 'gray'}
              />
            </div>
          </section>
        </div>

        <aside className="admin-trip-side-column">
          <section className="admin-trip-participants">
            <ParticipantCard
              role="Khách hàng"
              accent="primary"
              icon={faUser}
              person={trip.customer}
            />
            <ParticipantCard
              role="Tài xế"
              accent="secondary"
              icon={faCar}
              person={trip.driver}
              rating={trip.driver.averageRatingLabel}
              vehicle={`${trip.vehicle.brandModel} - ${trip.vehicle.plateNumber}`}
              tags={[trip.vehicle.vehicleTypeLabel, trip.serviceName]}
            />
          </section>

          <section className="admin-trip-card admin-trip-timeline-card">
            <div className="admin-trip-card__heading">
              <h2>Tiến trình & An toàn</h2>
              <span className={`admin-trip-safety admin-trip-safety--${trip.route.safetyTone}`}>
                <FontAwesomeIcon icon={faShieldAlt} />
                {trip.route.safetyLabel}
              </span>
            </div>
            <div className="admin-trip-timeline">
              {trip.timeline.items.map((item) => (
                <TimelineItem key={item.id} item={item} />
              ))}
            </div>
            <div className="admin-trip-safety-note">
              <div>
                <span>
                  <FontAwesomeIcon icon={faShieldAlt} />
                </span>
                <div>
                  <strong>Giám sát an toàn</strong>
                  <small>Bảo mật bởi SafeGuard AI</small>
                </div>
              </div>
              <p>{trip.route.safetyNote}</p>
            </div>
          </section>
        </aside>
      </div>

      <section className="admin-trip-info-grid">
        <InfoField label="Trip ID" value={`#${trip.tripCode}`} />
        <InfoField label="Booking ID" value={`#${trip.bookingCode}`} />
        <InfoField label="Trạng thái booking" value={trip.bookingStatusLabel} />
        <InfoField label="Loại booking" value={trip.bookingTypeLabel} />
        <InfoField label="Dịch vụ" value={trip.serviceName} />
        <InfoField label="Thời gian đặt" value={trip.timeline.bookingCreatedAtLabel} />
        <InfoField label="Bắt đầu chuyến" value={trip.timeline.startedAtLabel} />
        <InfoField label="Kết thúc chuyến" value={trip.timeline.completedAtLabel} />
        <InfoField label="Giá ước tính" value={trip.fare.estimatedFareLabel} />
        <InfoField label="Giá cuối cùng" value={trip.fare.finalFareLabel} />
        <InfoField label="Khuyến mãi" value={formatPromotions(trip.promotions)} />
        <InfoField label="Ngày tạo" value={trip.createdAtLabel} />
        <InfoField label="Cập nhật cuối" value={trip.lastUpdatedAtLabel} />
        <InfoField label="Ghi chú chuyến đi" value={trip.tripNotes} wide />
        <InfoField
          label="Đánh giá & phản hồi"
          value={trip.rating ? `${trip.rating.ratingScore}/5 - ${trip.rating.comment || 'Không có nhận xét'}` : 'Chưa có đánh giá'}
          wide
        />
      </section>

      <section className="admin-trip-insights">
        <InsightCard
          icon={faClock}
          label="Thời gian di chuyển"
          value={trip.route.durationLabel}
        />
        <InsightCard
          icon={faRoute}
          label="Khoảng cách"
          value={trip.route.distanceLabel}
        />
        <InsightCard
          icon={trip.vehicle.vehicleType === 'Motorbike' ? faMotorcycle : faCar}
          label="Phương tiện"
          value={trip.vehicle.vehicleTypeLabel}
        />
      </section>
    </TripDetailFrame>
  );
}

function TripDetailFrame({ children, trip, onBack }) {
  return (
    <div className="admin-trip-detail-page">
      <nav className="admin-trip-breadcrumb" aria-label="Breadcrumb">
        {onBack ? (
          <button type="button" onClick={onBack}>
            <FontAwesomeIcon icon={faArrowLeft} />
            Chuyến đi
          </button>
        ) : (
          <span>Chuyến đi</span>
        )}
        <span>/</span>
        <strong>{trip ? `Chi tiết chuyến đi #${trip.tripCode}` : 'Chi tiết chuyến đi'}</strong>
      </nav>
      {children}
    </div>
  );
}

function StatusPill({ label, variant }) {
  return (
    <span className={`admin-trip-status admin-trip-status--${variant}`}>
      {variant === 'green' && <FontAwesomeIcon icon={faCheck} />}
      {label}
    </span>
  );
}

function RoutePoint({ title, address, coordinate, variant }) {
  return (
    <article className={`admin-trip-route-point admin-trip-route-point--${variant}`}>
      <div className="admin-trip-route-point__marker">
        <FontAwesomeIcon icon={faLocationDot} />
      </div>
      <div>
        <span>{title}</span>
        <strong>{address}</strong>
        <small>{coordinate}</small>
      </div>
    </article>
  );
}

function ParticipantCard({ role, icon, person, rating, vehicle, tags = [], accent }) {
  return (
    <article className={`admin-trip-card admin-trip-person admin-trip-person--${accent}`}>
      <div className="admin-trip-person__top">
        <span>{role}</span>
        <div className="admin-trip-person__rating">
          <FontAwesomeIcon icon={faStar} />
          {rating ?? 'N/A'}
        </div>
      </div>
      <div className="admin-trip-person__body">
        <Avatar person={person} icon={icon} />
        <div>
          <h3>{person.name}</h3>
          <p>{person.phone}</p>
          <p>{person.email}</p>
          {vehicle && <strong>{vehicle}</strong>}
          {tags.length > 0 && (
            <div className="admin-trip-person__tags">
              {tags.map((tag) => <span key={tag}>{tag}</span>)}
            </div>
          )}
        </div>
      </div>
    </article>
  );
}

function Avatar({ person, icon }) {
  if (person.avatarUrl) {
    return <img className="admin-trip-avatar" src={person.avatarUrl} alt={person.name} />;
  }

  return (
    <span className="admin-trip-avatar admin-trip-avatar--fallback">
      {person.initials || <FontAwesomeIcon icon={icon} />}
    </span>
  );
}

function TimelineItem({ item }) {
  return (
    <div className={`admin-trip-timeline__item${item.done ? ' admin-trip-timeline__item--done' : ''}${item.terminal ? ' admin-trip-timeline__item--terminal' : ''}`}>
      <span className="admin-trip-timeline__dot">
        {item.terminal && item.done ? <FontAwesomeIcon icon={faCheck} /> : null}
      </span>
      <div>
        <strong>{item.title}</strong>
        <p>{item.time} - {item.detail}</p>
      </div>
    </div>
  );
}

function InfoField({ label, value, wide = false }) {
  return (
    <article className={`admin-trip-info-field${wide ? ' admin-trip-info-field--wide' : ''}`}>
      <span>{label}</span>
      <strong>{value}</strong>
    </article>
  );
}

function InsightCard({ icon, label, value }) {
  return (
    <article className="admin-trip-insight-card">
      <span>
        <FontAwesomeIcon icon={icon} />
      </span>
      <div>
        <small>{label}</small>
        <strong>{value}</strong>
      </div>
    </article>
  );
}

function formatPromotions(promotions) {
  if (!promotions.length) {
    return 'Không áp dụng';
  }

  return promotions
    .map((promotion) => `${promotion.promotionCode} (${promotion.discountAmountLabel})`)
    .join(', ');
}

export default AdminTripDetailsPage;
