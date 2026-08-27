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
import './AdminRiskFundPage.css';

const initialMutation = { amount: '', direction: 'CREDIT', reason: '', externalReference: '', evidenceUrl: '' };
const initialPolicy = {
  effectiveFromUtc: '', basePlatformCommissionRate: '', riskReserveRate: '',
  defaultProtectionLimit: '20000000', driverOrdinaryNegligenceRate: '0.20',
  driverOrdinaryNegligenceCap: '2000000', driverGrossNegligenceRate: '0.50',
  driverGrossNegligenceCap: '5000000', mockInsuranceCoverageLimit: '10000000',
  claimAutoApprovalThreshold: '2000000', riskFundEnabled: true, changeReason: '',
};
const transactionTypes = [
  'OPENING_BALANCE', 'CONTRIBUTION', 'CLAIM_ADVANCE', 'CLAIM_PAYOUT',
  'DRIVER_RECOVERY', 'CUSTOMER_RECOVERY', 'THIRD_PARTY_RECOVERY',
  'INSURANCE_RECOVERY', 'ADJUSTMENT',
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
  const [mode, setMode] = useState('opening');
  const [feedback, setFeedback] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [policy, setPolicy] = useState(initialPolicy);

  const copyCurrentPolicy = () => {
    const current = configuration.data;
    if (!current) return;
    setPolicy((value) => ({
      ...value,
      basePlatformCommissionRate: String(current.basePlatformCommissionRate),
      riskReserveRate: String(current.riskReserveRate),
      defaultProtectionLimit: String(current.defaultProtectionLimit),
      driverOrdinaryNegligenceRate: String(current.driverOrdinaryNegligenceRate),
      driverOrdinaryNegligenceCap: String(current.driverOrdinaryNegligenceCap),
      driverGrossNegligenceRate: String(current.driverGrossNegligenceRate),
      driverGrossNegligenceCap: String(current.driverGrossNegligenceCap),
      mockInsuranceCoverageLimit: String(current.mockInsuranceCoverageLimit),
      claimAutoApprovalThreshold: String(current.claimAutoApprovalThreshold),
      riskFundEnabled: current.riskFundEnabled,
    }));
  };

  const refresh = () => { dashboard.refetch(); ledger.refetch(); configuration.refetch(); policyVersions.refetch(); };

  const downloadLedger = async () => {
    setBusy(true); setError('');
    try {
      const { blob, fileName } = await exportRiskFundTransactions({
        type: filters.type,
        fromUtc: toUtc(filters.fromUtc),
        toUtc: toUtc(filters.toUtc),
      });
      downloadBlob(blob, fileName ?? `risk-fund-${new Date().toISOString().slice(0, 10)}.csv`);
    } catch (caught) { setError(caught.message); } finally { setBusy(false); }
  };

  const submitMutation = async (event) => {
    event.preventDefault(); setBusy(true); setError(''); setFeedback('');
    try {
      const payload = { ...mutation, amount: Number(mutation.amount), idempotencyKey: createIdempotencyKey(mode) };
      if (mode === 'opening') await createOpeningBalance({ ...payload, direction: 'CREDIT' });
      else await createRiskFundAdjustment(payload);
      setFeedback(mode === 'opening' ? 'Đã ghi nhận số dư đầu kỳ.' : 'Đã ghi nhận điều chỉnh có kiểm toán.');
      setMutation(initialMutation); refresh();
    } catch (caught) { setError(caught.message); } finally { setBusy(false); }
  };

  const submitPolicy = async (event) => {
    event.preventDefault(); setBusy(true); setError(''); setFeedback('');
    try {
      const payload = Object.fromEntries(Object.entries(policy).map(([key, value]) => {
        if (key === 'riskFundEnabled' || key === 'effectiveFromUtc' || key === 'changeReason') return [key, value];
        return [key, Number(value)];
      }));
      payload.effectiveFromUtc = new Date(policy.effectiveFromUtc).toISOString();
      await createRiskProtectionConfiguration(payload);
      setFeedback('Đã tạo phiên bản chính sách mới; phiên bản cũ vẫn giữ nguyên để đối soát.');
      setPolicy((value) => ({ ...value, effectiveFromUtc: '', changeReason: '' }));
      refresh();
    } catch (caught) { setError(caught.message); } finally { setBusy(false); }
  };

  const transactions = Array.isArray(ledger.data) ? ledger.data : [];
  const stats = dashboard.data ?? {};
  const currentPolicy = configuration.data;

  return (
    <AdminLayout><div className="risk-page">
      <header className="risk-page__header"><div><h1>SafeRide Risk Fund</h1><p>Sổ cái bất biến, số dư, thu hồi và cấu hình bảo vệ theo phiên bản.</p></div></header>
      {(dashboard.error || ledger.error || error) && <div className="risk-alert risk-alert--error">{error || dashboard.error || ledger.error}</div>}
      {feedback && <div className="risk-alert risk-alert--success">{feedback}</div>}
      <section className="risk-stats">
        <Stat label="Số dư hiện tại" value={formatVnd(stats.currentBalance)} />
        <Stat label="Tổng đóng góp" value={formatVnd(stats.totalContributions)} />
        <Stat label="Đã ứng" value={formatVnd(stats.claimAdvances)} />
        <Stat label="Đã chi cuối cùng" value={formatVnd(stats.claimPayouts)} />
        <Stat label="Đã thu hồi" value={formatVnd(stats.totalRecoveries)} />
        <Stat label="Còn phải thu" value={formatVnd(stats.outstandingRecoveries)} />
        <Stat label="Điều chỉnh ghi có" value={formatVnd(stats.adjustmentCredits)} />
        <Stat label="Điều chỉnh ghi nợ" value={formatVnd(stats.adjustmentDebits)} />
        <Stat label="Dư nợ Risk Fund" value={formatVnd(stats.outstandingExposure)} />
        <Stat label="Chờ điều tra" value={stats.pendingInvestigationClaims ?? 0} />
        <Stat label="Chờ cấp vốn" value={stats.pendingFundingClaims ?? 0} />
      </section>
      <section className="risk-grid">
        <form className="risk-card risk-form" onSubmit={submitMutation}>
          <div className="risk-card__title"><h2>Giao dịch quản trị</h2><select value={mode} onChange={(e) => setMode(e.target.value)}><option value="opening">Số dư đầu kỳ</option><option value="adjustment">Điều chỉnh</option></select></div>
          {mode === 'adjustment' && <Field label="Hướng"><select value={mutation.direction} onChange={(e) => setMutation({ ...mutation, direction: e.target.value })}><option value="CREDIT">Ghi có</option><option value="DEBIT">Ghi nợ</option></select></Field>}
          <Field label="Số tiền"><input required type="number" min="1" value={mutation.amount} onChange={(e) => setMutation({ ...mutation, amount: e.target.value })} /></Field>
          <Field label="Lý do kiểm toán"><textarea required value={mutation.reason} onChange={(e) => setMutation({ ...mutation, reason: e.target.value })} /></Field>
          <Field label="Tham chiếu thanh toán"><input required value={mutation.externalReference} onChange={(e) => setMutation({ ...mutation, externalReference: e.target.value })} /></Field>
          <Field label="URL bằng chứng"><input required type="url" value={mutation.evidenceUrl} onChange={(e) => setMutation({ ...mutation, evidenceUrl: e.target.value })} /></Field>
          <button disabled={busy} type="submit">{busy ? 'Đang lưu...' : 'Ghi vào sổ cái'}</button>
        </form>
        <form className="risk-card risk-form" onSubmit={submitPolicy}>
          <div className="risk-card__title"><h2>Phiên bản chính sách mới</h2><button type="button" disabled={!currentPolicy} onClick={copyCurrentPolicy}>{currentPolicy ? `Sao chép #${currentPolicy.id}` : 'Chưa cấu hình'}</button></div>
          <Field label="Hiệu lực từ (giờ địa phương)"><input required type="datetime-local" value={policy.effectiveFromUtc} onChange={(e) => setPolicy({ ...policy, effectiveFromUtc: e.target.value })} /></Field>
          <div className="risk-form__columns">
            <NumberField label="Tỷ lệ commission" name="basePlatformCommissionRate" value={policy} setValue={setPolicy} max="1" step="0.01" />
            <NumberField label="Tỷ lệ trích quỹ" name="riskReserveRate" value={policy} setValue={setPolicy} max="1" step="0.01" />
            <NumberField label="Hạn mức bảo vệ" name="defaultProtectionLimit" value={policy} setValue={setPolicy} />
            <NumberField label="Tỷ lệ lỗi thông thường" name="driverOrdinaryNegligenceRate" value={policy} setValue={setPolicy} max="1" step="0.01" />
            <NumberField label="Trần lỗi thông thường" name="driverOrdinaryNegligenceCap" value={policy} setValue={setPolicy} />
            <NumberField label="Tỷ lệ lỗi nghiêm trọng" name="driverGrossNegligenceRate" value={policy} setValue={setPolicy} max="1" step="0.01" />
            <NumberField label="Trần lỗi nghiêm trọng" name="driverGrossNegligenceCap" value={policy} setValue={setPolicy} />
            <NumberField label="Hạn mức insurer mô phỏng" name="mockInsuranceCoverageLimit" value={policy} setValue={setPolicy} />
            <NumberField label="Ngưỡng insurer tự duyệt" name="claimAutoApprovalThreshold" value={policy} setValue={setPolicy} />
          </div>
          <Field label="Lý do thay đổi"><textarea required value={policy.changeReason} onChange={(e) => setPolicy({ ...policy, changeReason: e.target.value })} /></Field>
          <label className="risk-check"><input type="checkbox" checked={policy.riskFundEnabled} onChange={(e) => setPolicy({ ...policy, riskFundEnabled: e.target.checked })} /> Bật rollout Risk Fund</label>
          <button disabled={busy} type="submit">Tạo phiên bản</button>
        </form>
      </section>
      <section className="risk-card">
        <div className="risk-card__title"><h2>Lịch sử phiên bản chính sách</h2><button type="button" onClick={policyVersions.refetch}>Tải lại</button></div>
        <div className="risk-table-wrap"><table className="risk-table"><thead><tr><th>Phiên bản</th><th>Hiệu lực</th><th>Commission</th><th>Trích quỹ</th><th>Risk Fund</th><th>Lý do</th></tr></thead><tbody>
          {(Array.isArray(policyVersions.data) ? policyVersions.data : []).map((item) => <tr key={item.id}><td>#{item.id}</td><td>{formatDate(item.effectiveFromUtc)}</td><td>{Number(item.basePlatformCommissionRate) * 100}%</td><td>{Number(item.riskReserveRate) * 100}%</td><td>{item.riskFundEnabled ? 'Bật' : 'Tắt'}</td><td>{item.changeReason}</td></tr>)}
          {!policyVersions.isLoading && (!Array.isArray(policyVersions.data) || policyVersions.data.length === 0) && <tr><td colSpan="6">Chưa có phiên bản chính sách.</td></tr>}
        </tbody></table></div>
      </section>
      <section className="risk-card">
        <div className="risk-card__title"><h2>Sổ cái</h2><div className="risk-actions"><button type="button" onClick={ledger.refetch}>Tải lại</button><button type="button" disabled={busy} onClick={downloadLedger}>Xuất CSV đầy đủ</button></div></div>
        <div className="risk-filters">
          <Field label="Loại"><select value={filters.type} onChange={(e) => setFilters({ ...filters, type: e.target.value })}><option value="">Tất cả</option>{transactionTypes.map((type) => <option key={type}>{type}</option>)}</select></Field>
          <Field label="Từ ngày"><input type="datetime-local" value={filters.fromUtc} onChange={(e) => setFilters({ ...filters, fromUtc: e.target.value })} /></Field>
          <Field label="Đến ngày"><input type="datetime-local" value={filters.toUtc} onChange={(e) => setFilters({ ...filters, toUtc: e.target.value })} /></Field>
        </div>
        <div className="risk-table-wrap"><table className="risk-table"><thead><tr><th>Thời gian</th><th>Loại</th><th>Hướng</th><th>Số tiền</th><th>Số dư trước</th><th>Số dư sau</th><th>Trip/Claim</th><th>Tham chiếu</th><th>Lý do</th></tr></thead><tbody>
          {transactions.map((item) => <tr key={item.id}><td>{formatDate(item.createdAtUtc)}</td><td>{item.transactionType}</td><td>{item.direction}</td><td>{formatVnd(item.amount)}</td><td>{formatVnd(item.balanceBefore)}</td><td>{formatVnd(item.balanceAfter)}</td><td>{item.tripId ? `Trip #${item.tripId}` : item.protectionClaimId ? `Claim #${item.protectionClaimId}` : '—'}</td><td>{item.externalReference ?? `#${item.id}`}</td><td>{item.reason}</td></tr>)}
          {!ledger.isLoading && transactions.length === 0 && <tr><td colSpan="9">Chưa có giao dịch phù hợp bộ lọc.</td></tr>}
        </tbody></table></div>
      </section>
    </div></AdminLayout>
  );
}

function toUtc(value) { return value ? new Date(value).toISOString() : ''; }
function formatDate(value) { return value ? new Date(value).toLocaleString('vi-VN') : '—'; }
function Stat({ label, value }) { return <article><span>{label}</span><strong>{value}</strong></article>; }
function Field({ label, children }) { return <label className="risk-field"><span>{label}</span>{children}</label>; }
function NumberField({ label, name, value, setValue, max, step = '1' }) { return <Field label={label}><input required type="number" min="0" max={max} step={step} value={value[name]} onChange={(e) => setValue({ ...value, [name]: e.target.value })} /></Field>; }
function downloadBlob(blob, fileName) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a'); link.href = url; link.download = fileName; link.click();
  URL.revokeObjectURL(url);
}

export default AdminRiskFundPage;
