import { useMemo, useState } from 'react';
import { AdminLayout } from '../../../shared/layouts/AdminLayout';
import useFetch from '../../../shared/hooks/useFetch';
import {
  buildQueryPath,
  createIdempotencyKey,
  createOpeningBalance,
  createRiskFundAdjustment,
  createRiskProtectionConfiguration,
  exportRiskFundTransactions,
  formatVnd,
  riskFundDashboardPath,
  riskFundTransactionsPath,
  riskProtectionConfigurationPath,
  riskProtectionVersionsPath,
} from '../../../features/riskProtection/riskProtectionApi';
import {
  confirmRiskAction,
  riskProtectionLabel,
} from '../../../features/riskProtection/riskProtectionPresentation';
import './AdminRiskFundPage.css';

const initialMutation = { amount: '', direction: 'CREDIT', reason: '', externalReference: '', evidenceUrl: '' };
const initialPolicy = {
  effectiveFromUtc: '',
  basePlatformCommissionRate: '',
  riskReserveRate: '',
  defaultProtectionLimit: '20000000',
  driverOrdinaryNegligenceRate: '0.20',
  driverOrdinaryNegligenceCap: '2000000',
  driverGrossNegligenceRate: '0.50',
  driverGrossNegligenceCap: '5000000',
  mockInsuranceCoverageLimit: '10000000',
  claimAutoApprovalThreshold: '2000000',
  riskFundEnabled: true,
  changeReason: '',
};
const transactionTypes = [
  'OPENING_BALANCE',
  'CONTRIBUTION',
  'CLAIM_ADVANCE',
  'CLAIM_PAYOUT',
  'DRIVER_RECOVERY',
  'CUSTOMER_RECOVERY',
  'THIRD_PARTY_RECOVERY',
  'INSURANCE_RECOVERY',
  'ADJUSTMENT',
];

function AdminRiskFundPage() {
  const [filters, setFilters] = useState({ type: '', fromUtc: '', toUtc: '' });
  const transactionPath = useMemo(() => buildQueryPath(riskFundTransactionsPath, {
    type: filters.type,
    fromUtc: toUtc(filters.fromUtc),
    toUtc: toUtc(filters.toUtc),
  }), [filters]);
  const dashboard = useFetch(riskFundDashboardPath);
  const ledger = useFetch(transactionPath);
  const configuration = useFetch(riskProtectionConfigurationPath);
  const policyVersions = useFetch(riskProtectionVersionsPath);
  const [mutation, setMutation] = useState(initialMutation);
  const [mutationKey, setMutationKey] = useState(() => createIdempotencyKey('opening'));
  const [mode, setMode] = useState('opening');
  const [feedback, setFeedback] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [policy, setPolicy] = useState(initialPolicy);
  const transactions = Array.isArray(ledger.data) ? ledger.data : [];
  const stats = dashboard.data ?? {};
  const currentPolicy = configuration.data;

  const refresh = () => {
    dashboard.refetch();
    ledger.refetch();
    configuration.refetch();
    policyVersions.refetch();
  };

  const changeMode = (nextMode) => {
    setMode(nextMode);
    setMutation(initialMutation);
    setMutationKey(createIdempotencyKey(nextMode));
  };

  const copyCurrentPolicy = () => {
    if (!currentPolicy) return;
    setPolicy((value) => ({
      ...value,
      basePlatformCommissionRate: String(currentPolicy.basePlatformCommissionRate),
      riskReserveRate: String(currentPolicy.riskReserveRate),
      defaultProtectionLimit: String(currentPolicy.defaultProtectionLimit),
      driverOrdinaryNegligenceRate: String(currentPolicy.driverOrdinaryNegligenceRate),
      driverOrdinaryNegligenceCap: String(currentPolicy.driverOrdinaryNegligenceCap),
      driverGrossNegligenceRate: String(currentPolicy.driverGrossNegligenceRate),
      driverGrossNegligenceCap: String(currentPolicy.driverGrossNegligenceCap),
      mockInsuranceCoverageLimit: String(currentPolicy.mockInsuranceCoverageLimit),
      claimAutoApprovalThreshold: String(currentPolicy.claimAutoApprovalThreshold),
      riskFundEnabled: currentPolicy.riskFundEnabled,
    }));
  };

  const downloadLedger = async () => {
    setBusy(true);
    setError('');
    try {
      const { blob, fileName } = await exportRiskFundTransactions({
        type: filters.type,
        fromUtc: toUtc(filters.fromUtc),
        toUtc: toUtc(filters.toUtc),
      });
      downloadBlob(blob, fileName ?? `risk-fund-${new Date().toISOString().slice(0, 10)}.csv`);
    } catch (caught) {
      setError(caught.message);
    } finally {
      setBusy(false);
    }
  };

  const submitMutation = async (event) => {
    event.preventDefault();
    const impact = mode === 'opening'
      ? 'Xác nhận ghi số dư đầu kỳ? Chỉ được tạo một lần trước giao dịch đầu tiên và sẽ trở thành lịch sử sổ cái.'
      : `Xác nhận ${mutation.direction === 'DEBIT' ? 'ghi giảm' : 'ghi tăng'} Quỹ rủi ro ${formatVnd(mutation.amount)}? Thao tác này được kiểm toán và không thể sửa bản ghi sổ cái.`;
    if (!confirmRiskAction(impact)) return;
    setBusy(true);
    setError('');
    setFeedback('');
    try {
      const payload = { ...mutation, amount: Number(mutation.amount), idempotencyKey: mutationKey };
      if (mode === 'opening') await createOpeningBalance({ ...payload, direction: 'CREDIT' });
      else await createRiskFundAdjustment(payload);
      setFeedback(mode === 'opening' ? 'Đã ghi nhận số dư đầu kỳ.' : 'Đã ghi nhận điều chỉnh có kiểm toán.');
      setMutation(initialMutation);
      setMutationKey(createIdempotencyKey(mode));
      refresh();
    } catch (caught) {
      setError(caught.message);
    } finally {
      setBusy(false);
    }
  };

  const submitPolicy = async (event) => {
    event.preventDefault();
    if (!confirmRiskAction('Tạo phiên bản chính sách mới? Phiên bản lịch sử không bị sửa và chuyến đi hiện có tiếp tục dùng snapshot cũ.')) return;
    setBusy(true);
    setError('');
    setFeedback('');
    try {
      const payload = Object.fromEntries(Object.entries(policy).map(([key, value]) => {
        if (key === 'riskFundEnabled' || key === 'effectiveFromUtc' || key === 'changeReason') return [key, value];
        return [key, Number(value)];
      }));
      payload.effectiveFromUtc = new Date(policy.effectiveFromUtc).toISOString();
      await createRiskProtectionConfiguration(payload);
      setFeedback('Đã tạo phiên bản chính sách mới; các phiên bản trước vẫn giữ nguyên để đối soát.');
      setPolicy((value) => ({ ...value, effectiveFromUtc: '', changeReason: '' }));
      refresh();
    } catch (caught) {
      setError(caught.message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <AdminLayout>
      <div className="risk-page">
        <header className="risk-page__header">
          <div>
            <h1>Quỹ rủi ro SafeRide</h1>
            <p>Theo dõi nguồn quỹ, các khoản ứng, thu hồi và chính sách bảo vệ theo phiên bản.</p>
          </div>
        </header>
        {(dashboard.error || ledger.error || error) && <div className="risk-alert risk-alert--error">{error || dashboard.error || ledger.error}</div>}
        {feedback && <div className="risk-alert risk-alert--success">{feedback}</div>}

        <section aria-labelledby="risk-overview-title">
          <div className="risk-section-title"><div><h2 id="risk-overview-title">Tổng quan</h2><p>Tình hình thanh khoản và hồ sơ cần xử lý.</p></div><button type="button" onClick={refresh}>Làm mới</button></div>
          <div className="risk-stats">
            <Stat label="Số dư hiện tại" value={formatVnd(stats.currentBalance)} emphasize />
            <Stat label="Khoản trích vào quỹ" value={formatVnd(stats.totalContributions)} />
            <Stat label="Khoản ứng có thể thu hồi" value={formatVnd(stats.claimAdvances)} />
            <Stat label="Khoản hỗ trợ cuối cùng" value={formatVnd(stats.claimPayouts)} />
            <Stat label="Đã thu hồi" value={formatVnd(stats.totalRecoveries)} />
            <Stat label="Còn phải thu" value={formatVnd(stats.outstandingRecoveries)} />
            <Stat label="Dư nợ quỹ đang chịu" value={formatVnd(stats.outstandingExposure)} />
            <Stat label="Chờ điều tra" value={stats.pendingInvestigationClaims ?? 0} />
            <Stat label="Chờ cấp kinh phí" value={stats.pendingFundingClaims ?? 0} />
          </div>
        </section>

        <section className="risk-card" aria-labelledby="risk-ledger-title">
          <div className="risk-card__title"><div><h2 id="risk-ledger-title">Sổ cái</h2><p className="risk-form__hint">Lịch sử bất biến của mọi khoản vào và ra Quỹ rủi ro.</p></div><div className="risk-actions"><button type="button" onClick={ledger.refetch}>Tải lại</button><button type="button" disabled={busy} onClick={downloadLedger}>Xuất CSV đầy đủ</button></div></div>
          <div className="risk-filters">
            <Field label="Loại giao dịch"><select value={filters.type} onChange={(event) => setFilters({ ...filters, type: event.target.value })}><option value="">Tất cả</option>{transactionTypes.map((type) => <option key={type} value={type}>{riskProtectionLabel(type)}</option>)}</select></Field>
            <Field label="Từ ngày"><input type="datetime-local" value={filters.fromUtc} onChange={(event) => setFilters({ ...filters, fromUtc: event.target.value })} /></Field>
            <Field label="Đến ngày"><input type="datetime-local" value={filters.toUtc} onChange={(event) => setFilters({ ...filters, toUtc: event.target.value })} /></Field>
          </div>
          <div className="risk-table-wrap">
            <table className="risk-table">
              <thead><tr><th>Thời gian</th><th>Loại</th><th>Hướng</th><th>Số tiền</th><th>Số dư trước</th><th>Số dư sau</th><th>Liên kết</th><th>Tham chiếu</th><th>Lý do</th></tr></thead>
              <tbody>
                {transactions.map((item) => <tr key={item.id}><td>{formatDate(item.createdAtUtc)}</td><td>{riskProtectionLabel(item.transactionType)}</td><td>{riskProtectionLabel(item.direction)}</td><td>{formatVnd(item.amount)}</td><td>{formatVnd(item.balanceBefore)}</td><td>{formatVnd(item.balanceAfter)}</td><td>{item.tripId ? `Chuyến #${item.tripId}` : item.protectionClaimId ? `Hồ sơ #${item.protectionClaimId}` : '—'}</td><td>{item.externalReference ?? `#${item.id}`}</td><td>{item.reason}</td></tr>)}
                {!ledger.isLoading && transactions.length === 0 && <tr><td colSpan="9">Chưa có giao dịch phù hợp bộ lọc.</td></tr>}
              </tbody>
            </table>
          </div>
        </section>

        <section aria-labelledby="risk-policy-title">
          <div className="risk-section-title"><div><h2 id="risk-policy-title">Cài đặt chính sách</h2><p>Phiên bản hiện hành và lịch sử bất biến phục vụ đối soát snapshot.</p></div></div>
          {currentPolicy ? <div className="risk-card risk-policy-current">
            <div className="risk-card__title"><h3>Phiên bản đang áp dụng #{currentPolicy.id}</h3><span className="risk-badge">{currentPolicy.riskFundEnabled ? 'Đang bật' : 'Đang tắt'}</span></div>
            <div className="risk-review-grid">
              <ReviewItem label="Hiệu lực từ" value={formatDate(currentPolicy.effectiveFromUtc)} />
              <ReviewItem label="Tỷ lệ trích quỹ" value={`${Number(currentPolicy.riskReserveRate) * 100}%`} />
              <ReviewItem label="Hạn mức bảo vệ mặc định" value={formatVnd(currentPolicy.defaultProtectionLimit)} />
              <ReviewItem label="Ngưỡng Bảo hiểm hệ thống tự duyệt" value={formatVnd(currentPolicy.claimAutoApprovalThreshold)} />
            </div>
          </div> : <div className="risk-card">Chưa tải được chính sách hiện hành.</div>}
          <div className="risk-card">
            <div className="risk-card__title"><h3>Lịch sử phiên bản</h3><button type="button" onClick={policyVersions.refetch}>Tải lại</button></div>
            <div className="risk-table-wrap"><table className="risk-table"><thead><tr><th>Phiên bản</th><th>Hiệu lực</th><th>Phí nền tảng</th><th>Trích quỹ</th><th>Quỹ rủi ro</th><th>Lý do</th></tr></thead><tbody>
              {(Array.isArray(policyVersions.data) ? policyVersions.data : []).map((item) => <tr key={item.id}><td>#{item.id}</td><td>{formatDate(item.effectiveFromUtc)}</td><td>{Number(item.basePlatformCommissionRate) * 100}%</td><td>{Number(item.riskReserveRate) * 100}%</td><td>{item.riskFundEnabled ? 'Bật' : 'Tắt'}</td><td>{item.changeReason}</td></tr>)}
              {!policyVersions.isLoading && (!Array.isArray(policyVersions.data) || policyVersions.data.length === 0) && <tr><td colSpan="6">Chưa có phiên bản chính sách.</td></tr>}
            </tbody></table></div>
          </div>
        </section>

        <details className="risk-card risk-advanced risk-advanced--panel">
          <summary>Thao tác quản trị nâng cao & có kiểm toán</summary>
          <p>Chỉ dùng khi thiết lập ban đầu, sửa sai có chứng từ hoặc ban hành chính sách mới. Mọi thao tác yêu cầu xác nhận rõ tác động.</p>
          <section className="risk-grid">
            <form className="risk-form" onSubmit={submitMutation}>
              <div className="risk-card__title"><h3>Số dư đầu kỳ / điều chỉnh</h3><select value={mode} onChange={(event) => changeMode(event.target.value)}><option value="opening">Thiết lập số dư đầu kỳ</option><option value="adjustment">Điều chỉnh có kiểm toán</option></select></div>
              {mode === 'adjustment' && <Field label="Tác động đến số dư"><select value={mutation.direction} onChange={(event) => setMutation({ ...mutation, direction: event.target.value })}><option value="CREDIT">{riskProtectionLabel('CREDIT')}</option><option value="DEBIT">{riskProtectionLabel('DEBIT')}</option></select></Field>}
              <Field label="Số tiền"><input required type="number" min="1" value={mutation.amount} onChange={(event) => setMutation({ ...mutation, amount: event.target.value })} /></Field>
              <Field label="Lý do kiểm toán"><textarea required value={mutation.reason} onChange={(event) => setMutation({ ...mutation, reason: event.target.value })} /></Field>
              <Field label="Tham chiếu bên ngoài"><input required value={mutation.externalReference} onChange={(event) => setMutation({ ...mutation, externalReference: event.target.value })} /></Field>
              <Field label="URL bằng chứng kiểm toán"><input required type="url" value={mutation.evidenceUrl} onChange={(event) => setMutation({ ...mutation, evidenceUrl: event.target.value })} /></Field>
              <button disabled={busy} type="submit">{busy ? 'Đang lưu...' : 'Xác nhận ghi vào sổ cái'}</button>
            </form>
            <form className="risk-form" onSubmit={submitPolicy}>
              <div className="risk-card__title"><h3>Tạo phiên bản chính sách mới</h3><button type="button" disabled={!currentPolicy} onClick={copyCurrentPolicy}>{currentPolicy ? `Sao chép phiên bản #${currentPolicy.id}` : 'Chưa có cấu hình'}</button></div>
              <Field label="Hiệu lực từ (giờ địa phương)"><input required type="datetime-local" value={policy.effectiveFromUtc} onChange={(event) => setPolicy({ ...policy, effectiveFromUtc: event.target.value })} /></Field>
              <div className="risk-form__columns">
                <NumberField label="Tỷ lệ phí nền tảng" name="basePlatformCommissionRate" value={policy} setValue={setPolicy} max="1" step="0.01" />
                <NumberField label="Tỷ lệ trích Quỹ rủi ro" name="riskReserveRate" value={policy} setValue={setPolicy} max="1" step="0.01" />
                <NumberField label="Hạn mức bảo vệ mặc định" name="defaultProtectionLimit" value={policy} setValue={setPolicy} />
                <NumberField label="Tỷ lệ sơ suất thông thường" name="driverOrdinaryNegligenceRate" value={policy} setValue={setPolicy} max="1" step="0.01" />
                <NumberField label="Trần sơ suất thông thường" name="driverOrdinaryNegligenceCap" value={policy} setValue={setPolicy} />
                <NumberField label="Tỷ lệ sơ suất nghiêm trọng" name="driverGrossNegligenceRate" value={policy} setValue={setPolicy} max="1" step="0.01" />
                <NumberField label="Trần sơ suất nghiêm trọng" name="driverGrossNegligenceCap" value={policy} setValue={setPolicy} />
                <NumberField label="Hạn mức Bảo hiểm hệ thống SafeRide" name="mockInsuranceCoverageLimit" value={policy} setValue={setPolicy} />
                <NumberField label="Ngưỡng Bảo hiểm hệ thống tự duyệt" name="claimAutoApprovalThreshold" value={policy} setValue={setPolicy} />
              </div>
              <Field label="Lý do ban hành phiên bản"><textarea required value={policy.changeReason} onChange={(event) => setPolicy({ ...policy, changeReason: event.target.value })} /></Field>
              <label className="risk-check"><input type="checkbox" checked={policy.riskFundEnabled} onChange={(event) => setPolicy({ ...policy, riskFundEnabled: event.target.checked })} /> Cho phép Risk Protection dùng Quỹ rủi ro</label>
              <button disabled={busy} type="submit">Tạo phiên bản mới</button>
            </form>
          </section>
        </details>
      </div>
    </AdminLayout>
  );
}

function toUtc(value) {
  return value ? new Date(value).toISOString() : '';
}

function formatDate(value) {
  return value ? new Date(value).toLocaleString('vi-VN') : '—';
}

function Stat({ label, value, emphasize = false }) {
  return <article className={emphasize ? 'risk-stat--emphasize' : ''}><span>{label}</span><strong>{value}</strong></article>;
}

function ReviewItem({ label, value }) {
  return <div className="risk-review-item"><span>{label}</span><strong>{value}</strong></div>;
}

function Field({ label, children }) {
  return <label className="risk-field"><span>{label}</span>{children}</label>;
}

function NumberField({ label, name, value, setValue, max, step = '1' }) {
  return <Field label={label}><input required type="number" min="0" max={max} step={step} value={value[name]} onChange={(event) => setValue({ ...value, [name]: event.target.value })} /></Field>;
}

function downloadBlob(blob, fileName) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}

export default AdminRiskFundPage;
