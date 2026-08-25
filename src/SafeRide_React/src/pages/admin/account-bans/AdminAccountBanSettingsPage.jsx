import { useEffect, useMemo, useState } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import {
  faBan,
  faCheckCircle,
  faClock,
  faExclamationCircle,
  faRotateRight,
  faSave,
  faShieldAlt,
  faStarHalfAlt,
} from '@fortawesome/free-solid-svg-icons';
import { AdminLayout } from '../../../shared/layouts/AdminLayout';
import {
  createAccountBanFormValues,
  getAccountBanConfiguration,
  toAccountBanPayload,
  updateAccountBanConfiguration,
  validateAccountBanValues,
} from '../../../features/admin/accountBans/accountBanConfigurationApi';
import './AdminAccountBanSettingsPage.css';

function AdminAccountBanSettingsPage() {
  const [configuration, setConfiguration] = useState(null);
  const [formValues, setFormValues] = useState(() => createAccountBanFormValues());
  const [formErrors, setFormErrors] = useState({});
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState('');
  const [successMessage, setSuccessMessage] = useState('');

  useEffect(() => {
    let isMounted = true;

    getAccountBanConfiguration()
      .then((nextConfiguration) => {
        if (!isMounted) return;
        setConfiguration(nextConfiguration);
        setFormValues(createAccountBanFormValues(nextConfiguration));
      })
      .catch((caughtError) => {
        if (!isMounted) return;
        setError(caughtError.message || 'Không thể tải cấu hình khóa tự động.');
      })
      .finally(() => {
        if (isMounted) setIsLoading(false);
      });

    return () => {
      isMounted = false;
    };
  }, []);

  useEffect(() => {
    if (!successMessage) return undefined;
    const timeoutId = window.setTimeout(() => setSuccessMessage(''), 4000);
    return () => window.clearTimeout(timeoutId);
  }, [successMessage]);

  const previewText = useMemo(() => {
    const payload = toAccountBanPayload(formValues);
    if (!Number.isFinite(payload.negativeFeedbackThreshold) ||
        !Number.isFinite(payload.negativeRatingMaxScore) ||
        !Number.isFinite(payload.temporaryBanDurationDays) ||
        !Number.isFinite(payload.maximumTemporaryBans)) {
      return 'Nhập đầy đủ giá trị hợp lệ để xem quy tắc đang cấu hình.';
    }

    return `${payload.negativeFeedbackThreshold} đánh giá từ ${payload.negativeRatingMaxScore} sao trở xuống sẽ khóa tạm thời ${payload.temporaryBanDurationDays} ngày; sau ${payload.maximumTemporaryBans} lần khóa tạm thời, lần vi phạm tiếp theo sẽ khóa vĩnh viễn.`;
  }, [formValues]);

  const handleChange = (name, value) => {
    setFormValues((current) => ({ ...current, [name]: value }));
    setFormErrors((current) => ({ ...current, [name]: undefined, form: undefined }));
    setError('');
    setSuccessMessage('');
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    const validationErrors = validateAccountBanValues(formValues);
    if (Object.keys(validationErrors).length > 0) {
      setFormErrors(validationErrors);
      return;
    }

    const confirmed = window.confirm('Lưu cấu hình khóa tài khoản tự động? Quy tắc mới sẽ áp dụng cho các đánh giá tiếp theo.');
    if (!confirmed) {
      return;
    }

    setIsSaving(true);
    setFormErrors({});
    setError('');
    setSuccessMessage('');

    try {
      const nextConfiguration = await updateAccountBanConfiguration(
        toAccountBanPayload(formValues),
      );
      setConfiguration(nextConfiguration);
      setFormValues(createAccountBanFormValues(nextConfiguration));
      setSuccessMessage('Đã lưu cấu hình khóa tài khoản tự động.');
    } catch (caughtError) {
      setFormErrors({
        form: caughtError.message || 'Không thể lưu cấu hình khóa tự động.',
      });
    } finally {
      setIsSaving(false);
    }
  };

  const resetForm = () => {
    if (!configuration || isSaving) return;
    setFormValues(createAccountBanFormValues(configuration));
    setFormErrors({});
    setError('');
    setSuccessMessage('');
  };

  return (
    <AdminLayout>
      <div className="admin-account-ban-page">
        <header className="admin-account-ban-header">
          <div>
            <h1>Cấu hình khóa tài khoản tự động</h1>
            <p>Điều chỉnh quy tắc xử lý tài khoản nhận nhiều phản hồi tiêu cực.</p>
          </div>
          <span className={`admin-account-ban-status${configuration?.isEnabled ? ' admin-account-ban-status--active' : ''}`}>
            <FontAwesomeIcon icon={configuration?.isEnabled ? faCheckCircle : faBan} />
            {configuration?.isEnabled ? 'Đang bật' : 'Đang tắt'}
          </span>
        </header>

        {isLoading && (
          <div className="admin-account-ban-state">
            <FontAwesomeIcon icon={faRotateRight} spin />
            Đang tải cấu hình...
          </div>
        )}

        {!isLoading && error && (
          <div className="admin-account-ban-feedback admin-account-ban-feedback--error">
            <FontAwesomeIcon icon={faExclamationCircle} />
            {error}
          </div>
        )}

        {!isLoading && successMessage && (
          <div className="admin-account-ban-feedback admin-account-ban-feedback--success">
            <FontAwesomeIcon icon={faCheckCircle} />
            {successMessage}
          </div>
        )}

        {!isLoading && configuration && (
          <>
            <section className="admin-account-ban-summary" aria-label="Giá trị cấu hình hiện tại">
              <SummaryCard icon={faShieldAlt} label="Ngưỡng phản hồi" value={`${configuration.negativeFeedbackThreshold} lần`} tone="teal" />
              <SummaryCard icon={faStarHalfAlt} label="Điểm tiêu cực" value={`<= ${configuration.negativeRatingMaxScore} sao`} tone="amber" />
              <SummaryCard icon={faClock} label="Khóa tạm thời" value={`${configuration.temporaryBanDurationDays} ngày`} tone="blue" />
              <SummaryCard icon={faBan} label="Tối đa khóa tạm" value={`${configuration.maximumTemporaryBans} lần`} tone="red" />
            </section>

            <section className="admin-account-ban-panel">
              <div className="admin-account-ban-panel-header">
                <div>
                  <h2>Thiết lập quy tắc</h2>
                  <span>Cập nhật gần nhất: {configuration.updatedAtLabel}</span>
                </div>
              </div>

              {formErrors.form && (
                <div className="admin-account-ban-form-alert">
                  <FontAwesomeIcon icon={faExclamationCircle} />
                  {formErrors.form}
                </div>
              )}

              <form className="admin-account-ban-form" onSubmit={handleSubmit}>
                <label className="admin-account-ban-toggle">
                  <input
                    type="checkbox"
                    checked={formValues.isEnabled}
                    onChange={(event) => handleChange('isEnabled', event.target.checked)}
                  />
                  <span>Bật quy tắc khóa tự động</span>
                </label>

                <div className="admin-account-ban-grid">
                  <NumberField
                    label="Ngưỡng phản hồi tiêu cực"
                    name="negativeFeedbackThreshold"
                    value={formValues.negativeFeedbackThreshold}
                    error={formErrors.negativeFeedbackThreshold}
                    onChange={handleChange}
                  />
                  <NumberField
                    label="Điểm đánh giá được tính là tiêu cực"
                    name="negativeRatingMaxScore"
                    value={formValues.negativeRatingMaxScore}
                    min="1"
                    max="5"
                    error={formErrors.negativeRatingMaxScore}
                    onChange={handleChange}
                  />
                  <NumberField
                    label="Thời gian khóa tạm thời"
                    name="temporaryBanDurationDays"
                    value={formValues.temporaryBanDurationDays}
                    suffix="ngày"
                    error={formErrors.temporaryBanDurationDays}
                    onChange={handleChange}
                  />
                  <NumberField
                    label="Số lần khóa tạm thời tối đa"
                    name="maximumTemporaryBans"
                    value={formValues.maximumTemporaryBans}
                    suffix="lần"
                    error={formErrors.maximumTemporaryBans}
                    onChange={handleChange}
                  />
                </div>

                <div className="admin-account-ban-preview">
                  <strong>Quy tắc áp dụng</strong>
                  <p>{previewText}</p>
                </div>

                <div className="admin-account-ban-actions">
                  <button type="button" onClick={resetForm} disabled={isSaving}>
                    <FontAwesomeIcon icon={faRotateRight} />
                    Khôi phục
                  </button>
                  <button type="submit" disabled={isSaving}>
                    <FontAwesomeIcon icon={faSave} />
                    {isSaving ? 'Đang lưu...' : 'Lưu cấu hình'}
                  </button>
                </div>
              </form>
            </section>
          </>
        )}
      </div>
    </AdminLayout>
  );
}

function SummaryCard({ icon, label, value, tone }) {
  return (
    <article className="admin-account-ban-summary-card">
      <div>
        <span>{label}</span>
        <strong>{value}</strong>
      </div>
      <span className={`admin-account-ban-summary-icon admin-account-ban-summary-icon--${tone}`}>
        <FontAwesomeIcon icon={icon} />
      </span>
    </article>
  );
}

function NumberField({
  label,
  name,
  value,
  error,
  onChange,
  min = '1',
  max,
  suffix,
}) {
  return (
    <label className="admin-account-ban-field">
      <span>{label}</span>
      <div className="admin-account-ban-input-wrap">
        <input
          type="number"
          min={min}
          max={max}
          step="1"
          value={value}
          onChange={(event) => onChange(name, event.target.value)}
        />
        {suffix && <em>{suffix}</em>}
      </div>
      {error && <small>{error}</small>}
    </label>
  );
}

export default AdminAccountBanSettingsPage;
