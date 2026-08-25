import { useMemo, useState } from 'react';
import { AdminLayout } from '../../shared/layouts/AdminLayout';
import useFetch from '../../shared/hooks/useFetch';
import {
  buildQueryPath,
  calculateClaim,
  closeClaim,
  confirmLiabilityAssessment,
  createIdempotencyKey,
  formatVnd,
  fundClaim,
  getAccident,
  recordClaimRecovery,
  reviewMockInsurance,
  saveLiabilityAssessment,
  staffAccidentsPath,
  writeOffClaimAdvance,
} from '../../features/riskProtection/riskProtectionApi';
import '../admin/risk-fund/AdminRiskFundPage.css';

const allocations = [
  ['driverFaultPercentage', 'Lỗi tài xế', 'DRIVER', 'DRIVER_ERROR'],
  ['customerFaultPercentage', 'Lỗi khách hàng', 'CUSTOMER', 'CUSTOMER_INTERFERENCE'],
  ['thirdPartyFaultPercentage', 'Lỗi bên thứ ba', 'THIRD_PARTY', 'THIRD_PARTY_ERROR'],
  ['vehicleFailurePercentage', 'Lỗi phương tiện', 'VEHICLE', 'VEHICLE_MECHANICAL_FAILURE'],
  ['objectiveCausePercentage', 'Khách quan/không lỗi', 'OBJECTIVE', 'UNKNOWN'],
];
const initialAssessment = {
  driverFaultPercentage: 0, customerFaultPercentage: 0, thirdPartyFaultPercentage: 0,
  vehicleFailurePercentage: 0, objectiveCausePercentage: 100,
  driverFaultLevel: 'NO_FAULT', vehicleDefectAwareness: 'UNKNOWN',
  causes: [{ rootCause: 'UNKNOWN', responsibleParty: 'OBJECTIVE', percentage: 100 }], rowVersion: null,
};
const initialSettlement = { totalDamageAmount: '', eligibleDamageAmount: '', requestedInsuranceAmount: '0', requestedRiskFundAmount: '0', isPermanentRiskFundLoss: false, insurancePaymentDestination: 'DIRECT_TO_CLAIMANT' };

function StaffAccidentsPage() {
  const [filters, setFilters] = useState({ status: '', category: '', tripId: '', fromUtc: '', toUtc: '' });
  const queuePath = useMemo(() => buildQueryPath(staffAccidentsPath, {
    status: filters.status, category: filters.category, tripId: filters.tripId,
    fromUtc: toUtc(filters.fromUtc), toUtc: toUtc(filters.toUtc), limit: 200,
  }), [filters]);
  const queue = useFetch(queuePath);
  const [selectedId, setSelectedId] = useState(null);
  const [accident, setAccident] = useState(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState('');
  const [claim, setClaim] = useState(null);
  const claimId = claim?.id ?? accident?.claim?.id ?? accident?.claimId;
  const audits = useFetch(claimId ? `/staff/claims/${claimId}/mock-insurance/audits` : null);
  const [assessment, setAssessment] = useState(initialAssessment);
  const [settlement, setSettlement] = useState(initialSettlement);
  const [recovery, setRecovery] = useState({ sourceType: 'DRIVER', payerReference: '', amount: '', paymentReference: '', evidence: null });
  const [writeOff, setWriteOff] = useState({ amount: '', reason: '', evidence: null });
  const [insurance, setInsurance] = useState({ approvedAmount: '', reference: '', reason: '', insurancePaymentDestination: 'DIRECT_TO_CLAIMANT' });
  const [feedback, setFeedback] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const accidents = Array.isArray(queue.data) ? queue.data : [];
  const hydrateAccident = (loaded) => {
    setAccident(loaded);
    setClaim(loaded.claim ?? null);
    const current = loaded.liabilityAssessment;
    if (current) {
      setAssessment({
      driverFaultPercentage: current.driverFaultPercentage,
      customerFaultPercentage: current.customerFaultPercentage,
      thirdPartyFaultPercentage: current.thirdPartyFaultPercentage,
      vehicleFailurePercentage: current.vehicleFailurePercentage,
      objectiveCausePercentage: current.objectiveCausePercentage,
      driverFaultLevel: current.driverFaultLevel,
      vehicleDefectAwareness: current.vehicleDefectAwareness,
      causes: current.causes?.length ? current.causes : initialAssessment.causes,
      rowVersion: current.rowVersion,
      });
    }
    else setAssessment(initialAssessment);
    const currentClaim = loaded.claim;
    setSettlement(currentClaim ? {
      totalDamageAmount: String(currentClaim.totalDamageAmount ?? ''),
      eligibleDamageAmount: String(currentClaim.eligibleDamageAmount ?? ''),
      requestedInsuranceAmount: String(currentClaim.insuranceRequestedAmount ?? 0),
      requestedRiskFundAmount: String(Number(currentClaim.riskFundAdvanceAmount ?? 0) + Number(currentClaim.riskFundPermanentLossAmount ?? 0)),
      isPermanentRiskFundLoss: Number(currentClaim.riskFundPermanentLossAmount ?? 0) > 0,
      insurancePaymentDestination: currentClaim.insurancePaymentDestination ?? 'DIRECT_TO_CLAIMANT',
    } : initialSettlement);
  };

  const openAccident = async (accidentId) => {
    setSelectedId(accidentId); setDetailLoading(true); setDetailError(''); setError(''); setFeedback('');
    try { hydrateAccident(await getAccident(accidentId)); }
    catch (caught) { setDetailError(caught.message); setAccident(null); }
    finally { setDetailLoading(false); }
  };

  const refreshDetail = async () => {
    if (!selectedId) return;
    try { hydrateAccident(await getAccident(selectedId)); setDetailError(''); }
    catch (caught) { setDetailError(caught.message); }
  };

  const run = async (action, message, { refreshAccident = true } = {}) => {
    setBusy(true); setError(''); setFeedback('');
    try {
      const result = await action();
      if (result?.id) setClaim(result);
      setFeedback(message); queue.refetch();
      if (refreshAccident) await refreshDetail();
      if (claimId) audits.refetch();
      return result;
    } catch (caught) {
      if (caught.status === 409) {
        await refreshDetail();
        setError('Dữ liệu đã được người khác cập nhật. Hồ sơ mới nhất đã được tải lại.');
      } else {
        setError(caught.message);
      }
      return null;
    } finally { setBusy(false); }
  };

  const submitAssessment = (event, confirm) => {
    event.preventDefault();
    const payload = { ...assessment, rowVersion: assessment.rowVersion || undefined };
    run(
      () => confirm ? confirmLiabilityAssessment(selectedId, payload) : saveLiabilityAssessment(selectedId, payload),
      confirm ? 'Đã xác nhận phân bổ trách nhiệm và cập nhật claim.' : 'Đã lưu bản nháp đánh giá trách nhiệm.',
    );
  };
  const submitSettlement = (event) => {
    event.preventDefault();
    const payload = {
      ...Object.fromEntries(Object.entries(settlement).map(([key, value]) => [key, key === 'isPermanentRiskFundLoss' || key === 'insurancePaymentDestination' ? value : Number(value)])),
      rowVersion: claim?.rowVersion ?? accident?.claim?.rowVersion,
    };
    run(() => calculateClaim(claimId, payload), 'Đã tính lại claim theo policy snapshot.');
  };
  const submitRecovery = (event) => {
    event.preventDefault();
    run(() => recordClaimRecovery(claimId, {
      ...recovery,
      amount: Number(recovery.amount),
      rowVersion: claim?.rowVersion ?? accident?.claim?.rowVersion,
      idempotencyKey: createIdempotencyKey('recovery'),
    }), 'Đã ghi nhận khoản thu hồi thủ công.');
  };
  const submitInsurance = (event, approve) => {
    event.preventDefault();
    run(() => reviewMockInsurance(claimId, approve, {
      approvedAmount: approve ? Number(insurance.approvedAmount) : 0,
      reference: insurance.reference, reason: insurance.reason,
      rowVersion: claim?.rowVersion ?? accident?.claim?.rowVersion,
      insurancePaymentDestination: insurance.insurancePaymentDestination,
    }), approve ? 'Đã phê duyệt kết quả bảo hiểm mô phỏng.' : 'Đã từ chối kết quả bảo hiểm mô phỏng.');
  };

  const setAllocation = (key, value) => {
    const next = { ...assessment, [key]: Number(value) };
    if (key === 'driverFaultPercentage') next.driverFaultLevel = Number(value) === 0 ? 'NO_FAULT' : (assessment.driverFaultLevel === 'NO_FAULT' ? 'ORDINARY_NEGLIGENCE' : assessment.driverFaultLevel);
    next.causes = allocations
      .filter(([allocationKey]) => Number(next[allocationKey]) > 0)
      .map(([allocationKey, , party, defaultCause]) => {
        const existing = assessment.causes.find((cause) => cause.responsibleParty === party);
        return { rootCause: existing?.rootCause ?? defaultCause, responsibleParty: party, percentage: Number(next[allocationKey]) };
      });
    setAssessment(next);
  };

  return (
    <AdminLayout><div className="risk-page">
      <header className="risk-page__header"><div><h1>Tai nạn & Trách nhiệm</h1><p>Điều tra, đánh giá trách nhiệm, bảo hiểm mô phỏng, cấp vốn toàn phần và thu hồi thủ công.</p></div></header>
      {(queue.error || detailError || error) && <div className="risk-alert risk-alert--error">{error || detailError || queue.error}</div>}
      {feedback && <div className="risk-alert risk-alert--success">{feedback}</div>}
      <section className="risk-card">
        <div className="risk-card__title"><h2>Hàng đợi tai nạn</h2><button type="button" onClick={queue.refetch}>Tải lại</button></div>
        <div className="risk-filters">
          <Field label="Trạng thái"><select value={filters.status} onChange={(e) => setFilters({ ...filters, status: e.target.value })}><option value="">Tất cả</option>{['REPORTED','EVIDENCE_COLLECTION','UNDER_REVIEW','LIABILITY_PENDING','SETTLEMENT','CLOSED','REJECTED'].map((x) => <option key={x}>{x}</option>)}</select></Field>
          <Field label="Loại"><select value={filters.category} onChange={(e) => setFilters({ ...filters, category: e.target.value })}><option value="">Tất cả</option>{['DRIVER_INJURY','CUSTOMER_VEHICLE_DAMAGE','THIRD_PARTY_DAMAGE','MULTIPLE'].map((x) => <option key={x}>{x}</option>)}</select></Field>
          <Field label="Trip ID"><input type="number" min="1" value={filters.tripId} onChange={(e) => setFilters({ ...filters, tripId: e.target.value })} /></Field>
          <Field label="Từ ngày"><input type="datetime-local" value={filters.fromUtc} onChange={(e) => setFilters({ ...filters, fromUtc: e.target.value })} /></Field>
          <Field label="Đến ngày"><input type="datetime-local" value={filters.toUtc} onChange={(e) => setFilters({ ...filters, toUtc: e.target.value })} /></Field>
        </div>
        <div className="risk-table-wrap"><table className="risk-table"><thead><tr><th>Mã</th><th>Trip</th><th>Loại</th><th>Trạng thái</th><th>Thời điểm</th><th>Claim</th><th></th></tr></thead><tbody>
          {accidents.map((item) => <tr key={item.id}><td>#{item.id}</td><td>#{item.tripId}</td><td>{item.category}</td><td><span className="risk-badge">{item.status}</span></td><td>{formatDate(item.occurredAtUtc)}</td><td>{item.claimId ? `#${item.claimId} · ${item.claimStatus}` : '—'}</td><td><button type="button" onClick={() => openAccident(item.id)}>Mở hồ sơ</button></td></tr>)}
          {!queue.isLoading && accidents.length === 0 && <tr><td colSpan="7">Không có hồ sơ phù hợp bộ lọc.</td></tr>}
        </tbody></table></div>
      </section>

      {selectedId && <section className="risk-grid">
        <div className="risk-card"><div className="risk-card__title"><h2>Hồ sơ #{selectedId}</h2>{detailLoading && <span>Đang tải...</span>}</div>
          {accident && <><ul className="risk-detail-list"><li><b>Trip:</b> #{accident.tripId}</li><li><b>Loại:</b> {accident.category}</li><li><b>Trạng thái:</b> {accident.status}</li><li><b>Thời điểm:</b> {formatDate(accident.occurredAtUtc)}</li><li><b>Mô tả:</b> {accident.description}</li><li><b>Biên bản công an:</b> {accident.policeReportReference || '—'}</li></ul><hr className="risk-divider"/><h3>Bằng chứng ({accident.evidence?.length ?? 0})</h3><div className="risk-evidence">{accident.evidence?.map((item) => <a key={item.id} href={item.fileUrl} target="_blank" rel="noreferrer">{item.evidenceType}: {item.originalFileName ?? `Tệp #${item.id}`}</a>)}</div></>}
        </div>
        <ClaimSummary claim={claim ?? accident?.claim} />
      </section>}

      {accident && <section className="risk-grid">
        <form className="risk-card risk-form" onSubmit={(event) => submitAssessment(event, true)}>
          <div className="risk-card__title"><h2>Liability assessment</h2><span>Tổng: {allocationTotal(assessment)}%</span></div>
          <div className="risk-form__columns">{allocations.map(([key, label]) => <Field key={key} label={label}><input required type="number" min="0" max="100" value={assessment[key]} onChange={(e) => setAllocation(key, e.target.value)} /></Field>)}</div>
          <div className="risk-form__columns"><Field label="Mức lỗi tài xế"><select value={assessment.driverFaultLevel} onChange={(e) => setAssessment({ ...assessment, driverFaultLevel: e.target.value })}>{['NO_FAULT','ORDINARY_NEGLIGENCE','GROSS_NEGLIGENCE','INTENTIONAL_MISCONDUCT'].map((x) => <option key={x}>{x}</option>)}</select></Field><Field label="Nhận thức về lỗi xe"><select value={assessment.vehicleDefectAwareness} onChange={(e) => setAssessment({ ...assessment, vehicleDefectAwareness: e.target.value })}>{['UNKNOWN','CUSTOMER_KNEW','DRIVER_KNEW','BOTH_KNEW','NEITHER_COULD_REASONABLY_KNOW'].map((x) => <option key={x}>{x}</option>)}</select></Field></div>
          <h3>Nguyên nhân gốc</h3>
          {assessment.causes.map((cause, index) => <CauseRow key={`${cause.responsibleParty}-${index}`} cause={cause} onChange={(next) => setAssessment({ ...assessment, causes: assessment.causes.map((item, position) => position === index ? next : item) })} onRemove={() => setAssessment({ ...assessment, causes: assessment.causes.filter((_, position) => position !== index) })} />)}
          <p className="risk-form__hint">Tổng nguyên nhân: {assessment.causes.reduce((sum, cause) => sum + Number(cause.percentage), 0)}%. Phân bổ theo từng bên phải khớp tỷ lệ ở trên.</p>
          <div className="risk-actions"><button className="risk-secondary" disabled={busy} type="button" onClick={(event) => submitAssessment(event, false)}>Lưu nháp</button><button disabled={busy || allocationTotal(assessment) !== 100} type="submit">Xác nhận assessment</button></div>
        </form>

        <form className="risk-card risk-form" onSubmit={submitSettlement}>
          <div className="risk-card__title"><h2>Tính claim</h2><span>Dùng policy snapshot phía server</span></div>
          <div className="risk-form__columns">{Object.keys(settlement).filter((key) => key !== 'isPermanentRiskFundLoss' && key !== 'insurancePaymentDestination').map((key) => <Field key={key} label={claimFieldLabel(key)}><input required type="number" min="0" value={settlement[key]} onChange={(e) => setSettlement({ ...settlement, [key]: e.target.value })} /></Field>)}</div>
          <Field label="Hạch toán bảo hiểm"><select value={settlement.insurancePaymentDestination} onChange={(e) => setSettlement({ ...settlement, insurancePaymentDestination: e.target.value })}><option value="DIRECT_TO_CLAIMANT">Trả trực tiếp claimant</option><option value="REIMBURSE_RISK_FUND">Hoàn Risk Fund</option></select></Field>
          <label className="risk-check"><input type="checkbox" checked={settlement.isPermanentRiskFundLoss} onChange={(e) => setSettlement({ ...settlement, isPermanentRiskFundLoss: e.target.checked })} /> Khoản Risk Fund là chi phí cuối cùng</label>
          <button disabled={busy || !claimId} type="submit">Tính settlement</button>
          <button disabled={busy || !claimId} type="button" onClick={() => run(() => fundClaim(claimId, createIdempotencyKey('fund'), claim?.rowVersion ?? accident?.claim?.rowVersion), 'Đã xử lý cấp vốn; claim sẽ chờ cấp vốn nếu quỹ chưa đủ.')}>Cấp toàn bộ / thử lại cấp vốn</button>
        </form>
      </section>}

      {claimId && <section className="risk-grid">
        <form className="risk-card risk-form" onSubmit={(event) => submitInsurance(event, true)}>
          <div className="risk-card__title"><h2>Bảo hiểm mô phỏng</h2><span>{(claim ?? accident?.claim)?.insuranceStatus}</span></div>
          <Field label="Số tiền duyệt"><input required type="number" min="0" value={insurance.approvedAmount} onChange={(e) => setInsurance({ ...insurance, approvedAmount: e.target.value })} /></Field>
          <Field label="Mã tham chiếu"><input required value={insurance.reference} onChange={(e) => setInsurance({ ...insurance, reference: e.target.value })} /></Field>
          <Field label="Lý do"><textarea required value={insurance.reason} onChange={(e) => setInsurance({ ...insurance, reason: e.target.value })} /></Field>
          <Field label="Hạch toán"><select value={insurance.insurancePaymentDestination} onChange={(e) => setInsurance({ ...insurance, insurancePaymentDestination: e.target.value })}><option value="DIRECT_TO_CLAIMANT">Trả trực tiếp claimant</option><option value="REIMBURSE_RISK_FUND">Hoàn Risk Fund</option></select></Field>
          <div className="risk-actions"><button disabled={busy} type="submit">Phê duyệt</button><button className="risk-secondary" disabled={busy} type="button" onClick={(event) => submitInsurance(event, false)}>Từ chối</button></div>
          <hr className="risk-divider"/><h3>Audit provider</h3>{(audits.data ?? []).map((item) => <p key={item.id} className="risk-form__hint">{formatDate(item.createdAtUtc)} · {item.operation} · {item.resultStatus} · {formatVnd(item.approvedAmount)} · {item.providerReference}</p>)}
        </form>
        <form className="risk-card risk-form" onSubmit={submitRecovery}>
          <div className="risk-card__title"><h2>Thu hồi thủ công</h2><span>Không trừ DriverWallet</span></div>
          <Field label="Nguồn"><select value={recovery.sourceType} onChange={(e) => setRecovery({ ...recovery, sourceType: e.target.value })}>{['DRIVER','CUSTOMER','THIRD_PARTY','INSURANCE'].map((x) => <option key={x}>{x}</option>)}</select></Field>
          <Field label="Payer"><input required value={recovery.payerReference} onChange={(e) => setRecovery({ ...recovery, payerReference: e.target.value })} /></Field>
          <Field label="Số tiền"><input required type="number" min="1" value={recovery.amount} onChange={(e) => setRecovery({ ...recovery, amount: e.target.value })} /></Field>
          <Field label="Tham chiếu thanh toán"><input required value={recovery.paymentReference} onChange={(e) => setRecovery({ ...recovery, paymentReference: e.target.value })} /></Field>
          <Field label="Bằng chứng tin cậy"><input required type="file" accept="image/jpeg,image/png,image/webp,application/pdf" onChange={(e) => setRecovery({ ...recovery, evidence: e.target.files?.[0] ?? null })} /></Field>
          <button disabled={busy} type="submit">Ghi nhận thu hồi</button>
        </form>
      </section>}
      {claimId && <section className="risk-grid">
        <form className="risk-card risk-form" onSubmit={(event) => { event.preventDefault(); run(() => writeOffClaimAdvance(claimId, { ...writeOff, amount: Number(writeOff.amount), rowVersion: claim?.rowVersion ?? accident?.claim?.rowVersion, idempotencyKey: createIdempotencyKey('write-off') }), 'Đã ghi nhận write-off có audit.'); }}>
          <div className="risk-card__title"><h2>Reconcile / write-off</h2><span>Không debit Risk Fund lần hai</span></div>
          <Field label="Số tiền"><input required type="number" min="1" value={writeOff.amount} onChange={(e) => setWriteOff({ ...writeOff, amount: e.target.value })} /></Field>
          <Field label="Lý do"><textarea required value={writeOff.reason} onChange={(e) => setWriteOff({ ...writeOff, reason: e.target.value })} /></Field>
          <Field label="Bằng chứng"><input required type="file" accept="image/jpeg,image/png,image/webp,application/pdf" onChange={(e) => setWriteOff({ ...writeOff, evidence: e.target.files?.[0] ?? null })} /></Field>
          <button disabled={busy} type="submit">Ghi write-off</button>
        </form>
        <div className="risk-card risk-form"><div className="risk-card__title"><h2>Đóng claim</h2><span>Chỉ khi reconciliation cân bằng</span></div><button disabled={busy} type="button" onClick={() => run(() => closeClaim(claimId, claim?.rowVersion ?? accident?.claim?.rowVersion), 'Đã đóng claim và accident.')}>Đóng claim</button></div>
      </section>}
    </div></AdminLayout>
  );
}

function CauseRow({ cause, onChange, onRemove }) { return <div className="risk-cause-row"><Field label="Nguyên nhân"><select value={cause.rootCause} onChange={(e) => onChange({ ...cause, rootCause: e.target.value })}>{['DRIVER_ERROR','CUSTOMER_INTERFERENCE','THIRD_PARTY_ERROR','VEHICLE_MECHANICAL_FAILURE','VEHICLE_PRE_EXISTING_DEFECT','ROAD_CONDITION','WEATHER','FORCE_MAJEURE','UNKNOWN'].map((x) => <option key={x}>{x}</option>)}</select></Field><Field label="Bên liên quan"><select value={cause.responsibleParty} onChange={(e) => onChange({ ...cause, responsibleParty: e.target.value })}>{['DRIVER','CUSTOMER','THIRD_PARTY','VEHICLE','OBJECTIVE'].map((x) => <option key={x}>{x}</option>)}</select></Field><Field label="Tỷ lệ %"><input type="number" min="1" max="100" value={cause.percentage} onChange={(e) => onChange({ ...cause, percentage: Number(e.target.value) })} /></Field><button type="button" onClick={onRemove}>Xóa</button></div>; }
function ClaimSummary({ claim }) { return <div className="risk-card"><div className="risk-card__title"><h2>Claim</h2><span className="risk-badge">{claim?.status ?? 'CHƯA TẠO'}</span></div>{claim ? <ul className="risk-detail-list"><li><b>Mã:</b> #{claim.id}</li><li><b>Thiệt hại đủ điều kiện:</b> {formatVnd(claim.eligibleDamageAmount)}</li><li><b>Bảo hiểm duyệt:</b> {formatVnd(claim.insuranceApprovedAmount)} ({claim.insuranceStatus})</li><li><b>Bảo hiểm trả claimant:</b> {formatVnd(claim.insurancePaidDirectToClaimant)}</li><li><b>Bảo hiểm hoàn Risk Fund:</b> {formatVnd(claim.insuranceReimbursedToRiskFund)}</li><li><b>Risk Fund ứng có thể thu hồi:</b> {formatVnd(claim.riskFundAdvanceAmount)}</li><li><b>SafeRide payout cuối cùng:</b> {formatVnd(claim.riskFundPermanentLossAmount)}</li><li><b>Advance đã write-off:</b> {formatVnd(claim.writtenOffAdvanceAmount)}</li><li><b>Trách nhiệm tài xế:</b> {formatVnd(claim.driverLiabilityAmount)}</li><li><b>Đã thu hồi:</b> {formatVnd(claim.recoveredAmount)}</li><li><b>Còn phải thu:</b> {formatVnd(claim.outstandingRecoveryAmount)}</li><li><b>Exposure có thể thu hồi:</b> {formatVnd(claim.actualRecoverableFundExposure)}</li><li><b>Reconciled:</b> {claim.isReconciled ? 'Có' : 'Không'}</li></ul> : <p>Hãy xác nhận liability assessment để tạo claim.</p>}</div>; }
function Field({ label, children }) { return <label className="risk-field"><span>{label}</span>{children}</label>; }
function allocationTotal(value) { return allocations.reduce((sum, [key]) => sum + Number(value[key]), 0); }
function toUtc(value) { return value ? new Date(value).toISOString() : ''; }
function formatDate(value) { return value ? new Date(value).toLocaleString('vi-VN') : '—'; }
function claimFieldLabel(key) { return ({ totalDamageAmount: 'Tổng thiệt hại', eligibleDamageAmount: 'Thiệt hại đủ điều kiện', requestedInsuranceAmount: 'Yêu cầu bảo hiểm', requestedRiskFundAmount: 'Yêu cầu Risk Fund' })[key] ?? key; }

export default StaffAccidentsPage;
