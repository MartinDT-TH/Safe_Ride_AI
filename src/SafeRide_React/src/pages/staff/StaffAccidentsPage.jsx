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
  isRiskProtectionConcurrencyConflict,
  reconcilePartyCauses,
  recordClaimRecovery,
  refreshMockInsuranceStatus,
  reviewMockInsurance,
  saveLiabilityAssessment,
  staffAccidentsPath,
  writeOffClaimAdvance,
} from '../../features/riskProtection/riskProtectionApi';
import {
  confirmRiskAction,
  initialCustomerInsurance,
  responsibilityTotal,
  riskProtectionLabel,
  SettlementRecommendation,
  shouldShowSystemInsurance,
} from '../../features/riskProtection/riskProtectionPresentation';
import '../admin/risk-fund/AdminRiskFundPage.css';

const allocations = [
  ['driverFaultPercentage', 'Tài xế', 'DRIVER', 'DRIVER_ERROR'],
  ['customerFaultPercentage', 'Khách hàng', 'CUSTOMER', 'CUSTOMER_INTERFERENCE'],
  ['thirdPartyFaultPercentage', 'Bên thứ ba', 'THIRD_PARTY', 'THIRD_PARTY_ERROR'],
  ['vehicleFailurePercentage', 'Phương tiện', 'VEHICLE', 'VEHICLE_MECHANICAL_FAILURE'],
  ['objectiveCausePercentage', 'Khách quan / không lỗi', 'OBJECTIVE', 'UNKNOWN'],
];
const accidentStatuses = ['REPORTED', 'EVIDENCE_COLLECTION', 'UNDER_REVIEW', 'LIABILITY_PENDING', 'SETTLEMENT', 'CLOSED', 'REJECTED'];
const accidentCategories = ['DRIVER_INJURY', 'CUSTOMER_VEHICLE_DAMAGE', 'THIRD_PARTY_DAMAGE', 'MULTIPLE'];
const driverFaultLevels = ['NO_FAULT', 'ORDINARY_NEGLIGENCE', 'GROSS_NEGLIGENCE', 'INTENTIONAL_MISCONDUCT'];
const awarenessValues = ['UNKNOWN', 'CUSTOMER_KNEW', 'DRIVER_KNEW', 'BOTH_KNEW', 'NEITHER_COULD_REASONABLY_KNOW'];
const rootCauses = ['DRIVER_ERROR', 'CUSTOMER_INTERFERENCE', 'THIRD_PARTY_ERROR', 'VEHICLE_MECHANICAL_FAILURE', 'VEHICLE_PRE_EXISTING_DEFECT', 'ROAD_CONDITION', 'WEATHER', 'FORCE_MAJEURE', 'UNKNOWN'];
const responsibleParties = ['DRIVER', 'CUSTOMER', 'THIRD_PARTY', 'VEHICLE', 'OBJECTIVE'];
const recoverySources = ['DRIVER', 'CUSTOMER', 'THIRD_PARTY', 'INSURANCE'];
const initialAssessment = {
  driverFaultPercentage: 0,
  customerFaultPercentage: 0,
  thirdPartyFaultPercentage: 0,
  vehicleFailurePercentage: 0,
  objectiveCausePercentage: 100,
  driverFaultLevel: 'NO_FAULT',
  vehicleDefectAwareness: 'UNKNOWN',
  causes: [{ rootCause: 'UNKNOWN', responsibleParty: 'OBJECTIVE', percentage: 100 }],
  rowVersion: null,
};
const initialSettlement = {
  totalDamageAmount: '',
  eligibleDamageAmount: '',
};
const initialRecovery = { sourceType: 'DRIVER', payerReference: '', amount: '', paymentReference: '', evidence: null };
const initialWriteOff = { amount: '', reason: '', evidence: null };

function StaffAccidentsPage() {
  const [filters, setFilters] = useState({ status: '', category: '', tripId: '', fromUtc: '', toUtc: '' });
  const queuePath = useMemo(() => buildQueryPath(staffAccidentsPath, {
    status: filters.status,
    category: filters.category,
    tripId: filters.tripId,
    fromUtc: toUtc(filters.fromUtc),
    toUtc: toUtc(filters.toUtc),
    limit: 200,
  }), [filters]);
  const queue = useFetch(queuePath);
  const [selectedId, setSelectedId] = useState(null);
  const [accident, setAccident] = useState(null);
  const [claim, setClaim] = useState(null);
  const [assessment, setAssessment] = useState(initialAssessment);
  const [settlement, setSettlement] = useState(initialSettlement);
  const [recovery, setRecovery] = useState(initialRecovery);
  const [writeOff, setWriteOff] = useState(initialWriteOff);
  const [insurance, setInsurance] = useState({ mode: 'recommended', approvedAmount: '', reason: '' });
  const [customerInsurance, setCustomerInsurance] = useState(initialCustomerInsurance);
  const [fundingKey, setFundingKey] = useState(() => createIdempotencyKey('fund'));
  const [recoveryKey, setRecoveryKey] = useState(() => createIdempotencyKey('recovery'));
  const [writeOffKey, setWriteOffKey] = useState(() => createIdempotencyKey('write-off'));
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState('');
  const [feedback, setFeedback] = useState('');
  const [error, setError] = useState('');
  const [recoveryError, setRecoveryError] = useState('');
  const [recoverySuccess, setRecoverySuccess] = useState('');
  const [busy, setBusy] = useState(false);
  const claimId = claim?.id ?? accident?.claim?.id ?? accident?.claimId;
  const currentClaim = claim ?? accident?.claim;
  const showSystemInsurance = shouldShowSystemInsurance(currentClaim);
  const fundingAllowed = ['APPROVED', 'PENDING_FUNDING'].includes(currentClaim?.status);
  const audits = useFetch(claimId ? `/staff/claims/${claimId}/mock-insurance/audits` : null);
  const accidents = Array.isArray(queue.data) ? queue.data : [];
  const total = responsibilityTotal(assessment);
  const causeTotal = assessment.causes.reduce((sum, cause) => sum + Number(cause.percentage), 0);
  const assessmentConfirmed = accident?.liabilityAssessment?.status === 'CONFIRMED';

  const hydrateAccident = (loaded) => {
    setAccident(loaded);
    setClaim(loaded.claim ?? null);
    const current = loaded.liabilityAssessment;
    setAssessment(current ? {
      driverFaultPercentage: current.driverFaultPercentage,
      customerFaultPercentage: current.customerFaultPercentage,
      thirdPartyFaultPercentage: current.thirdPartyFaultPercentage,
      vehicleFailurePercentage: current.vehicleFailurePercentage,
      objectiveCausePercentage: current.objectiveCausePercentage,
      driverFaultLevel: current.driverFaultLevel,
      vehicleDefectAwareness: current.vehicleDefectAwareness,
      causes: current.causes?.length ? current.causes : initialAssessment.causes,
      rowVersion: current.rowVersion,
    } : initialAssessment);
    const currentClaim = loaded.claim;
    setSettlement(currentClaim ? {
      totalDamageAmount: String(currentClaim.totalDamageAmount ?? ''),
      eligibleDamageAmount: String(currentClaim.eligibleDamageAmount ?? ''),
    } : initialSettlement);
    setCustomerInsurance(currentClaim ? {
      appliedAmount: String(currentClaim.customerInsuranceAppliedAmount ?? 0),
      reference: currentClaim.customerInsuranceReference ?? '',
      note: currentClaim.customerInsuranceNote ?? '',
    } : initialCustomerInsurance);
  };

  const openAccident = async (accidentId) => {
    setSelectedId(accidentId);
    setDetailLoading(true);
    setDetailError('');
    setError('');
    setFeedback('');
    try {
      hydrateAccident(await getAccident(accidentId));
    } catch (caught) {
      setDetailError(caught.message);
      setAccident(null);
    } finally {
      setDetailLoading(false);
    }
  };

  const refreshDetail = async () => {
    if (!selectedId) return;
    try {
      hydrateAccident(await getAccident(selectedId));
      setDetailError('');
    } catch (caught) {
      setDetailError(caught.message);
    }
  };

  const run = async (action, message, { refreshAccident = true, onError, onSuccess } = {}) => {
    if (busy) return null;
    setBusy(true);
    setError('');
    setFeedback('');
    try {
      const result = await action();
      if (result?.id) setClaim(result);
      setFeedback(message);
      onSuccess?.(result);
      queue.refetch();
      if (refreshAccident) await refreshDetail();
      if (claimId) audits.refetch();
      return result;
    } catch (caught) {
      if (isRiskProtectionConcurrencyConflict(caught)) {
        await refreshDetail();
        setError('Dữ liệu đã được người khác cập nhật. Hồ sơ mới nhất đã được tải lại để tránh ghi đè.');
      } else {
        setError(caught.message);
      }
      onError?.(caught);
      return null;
    } finally {
      setBusy(false);
    }
  };

  const submitAssessment = async (event, confirm) => {
    event.preventDefault();
    if (confirm && !confirmRiskAction('Xác nhận kết quả trách nhiệm? Sau bước này assessment không thể bị ghi đè trực tiếp.')) return;
    const payload = { ...assessment, rowVersion: assessment.rowVersion || undefined };
    await run(
      () => confirm ? confirmLiabilityAssessment(selectedId, payload) : saveLiabilityAssessment(selectedId, payload),
      confirm ? 'Đã xác nhận trách nhiệm và chuyển hồ sơ sang bước xử lý quyền lợi.' : 'Đã lưu bản nháp đánh giá trách nhiệm.',
    );
  };

  const submitSettlement = async (event) => {
    event.preventDefault();
    const payload = {
      ...Object.fromEntries(Object.entries(settlement).map(([key, value]) => [
        key,
        Number(value),
      ])),
      rowVersion: claim?.rowVersion ?? accident?.claim?.rowVersion,
      submitToInsurance: true,
      customerInsuranceAppliedAmount: Number(customerInsurance.appliedAmount || 0),
      customerInsuranceReference: customerInsurance.reference.trim() || null,
      customerInsuranceNote: customerInsurance.note.trim() || null,
    };
    await run(() => calculateClaim(claimId, payload), 'Máy chủ đã áp dụng bảo hiểm riêng của khách trước, rồi tính quyền lợi bảo hiểm hệ thống SafeRide.');
  };

  const submitFunding = async () => {
    if (!confirmRiskAction('Cấp kinh phí cho toàn bộ khoản Quỹ rủi ro đã được máy chủ xác định? Thao tác này ghi giảm số dư quỹ.')) return;
    const result = await run(
      () => fundClaim(claimId, fundingKey, claim?.rowVersion ?? accident?.claim?.rowVersion),
      'Đã xử lý cấp kinh phí; nếu quỹ chưa đủ, hồ sơ vẫn ở trạng thái chờ cấp kinh phí.',
    );
    if (result) setFundingKey(createIdempotencyKey('fund'));
  };

  const submitRecovery = async (event) => {
    event.preventDefault();
    setRecoveryError('');
    setRecoverySuccess('');
    if (!recovery.evidence) {
      setRecoveryError('Vui lòng chọn chứng từ khoản thu hồi.');
      return;
    }
    if (recovery.evidence.size > 10 * 1024 * 1024) {
      setRecoveryError('Chứng từ không được vượt quá 10 MB.');
      return;
    }
    if (!confirmRiskAction('Ghi nhận khoản tiền đã thực nhận và hoàn vào Quỹ rủi ro? Hãy kiểm tra số tiền, nguồn và bằng chứng.')) return;
    const submittedAmount = Number(recovery.amount);
    const submittedSource = recovery.sourceType;
    const result = await run(() => recordClaimRecovery(claimId, {
      ...recovery,
      amount: submittedAmount,
      rowVersion: claim?.rowVersion ?? accident?.claim?.rowVersion,
      idempotencyKey: recoveryKey,
    }), '', {
      onError: (caught) => setRecoveryError(
        isRiskProtectionConcurrencyConflict(caught)
          ? 'Dữ liệu đã được người khác cập nhật. Hồ sơ mới nhất đã được tải lại để tránh ghi đè.'
          : caught.message),
      onSuccess: () => setRecoverySuccess(
        `Đã ghi nhận ${formatVnd(submittedAmount)} từ ${riskProtectionLabel(submittedSource)} và hoàn lại Quỹ rủi ro.`),
    });
    if (result) {
      setRecovery(initialRecovery);
      setRecoveryKey(createIdempotencyKey('recovery'));
    }
  };

  const submitInsurance = async (event, approve, mode = approve ? 'recommended' : 'reject') => {
    event.preventDefault();
    const current = claim ?? accident?.claim;
    const maximum = Number(current?.maximumApprovableInsuranceAmount ?? current?.insuranceEligibleAmount ?? 0);
    const recommended = Number(current?.recommendedInsuranceApprovalAmount ?? maximum);
    const approvedAmount = mode === 'recommended' ? recommended : mode === 'lower' ? Number(insurance.approvedAmount) : 0;
    if (mode === 'lower' && (!Number.isFinite(approvedAmount) || approvedAmount <= 0 || approvedAmount >= maximum)) {
      setError('Mức duyệt thấp hơn phải lớn hơn 0 và nhỏ hơn mức tối đa được phép.');
      return;
    }
    if ((mode === 'lower' || mode === 'reject') && insurance.reason.trim().length < 10) {
      setError('Vui lòng nhập lý do có ý nghĩa (ít nhất 10 ký tự).');
      return;
    }
    if (!confirmRiskAction(approve
      ? 'Xác nhận kết quả Bảo hiểm hệ thống SafeRide và số tiền được duyệt?'
      : 'Từ chối kết quả Bảo hiểm hệ thống SafeRide này?')) return;
    await run(() => reviewMockInsurance(claimId, approve, {
      approvedAmount,
      reference: undefined,
      reason: insurance.reason,
      rowVersion: claim?.rowVersion ?? accident?.claim?.rowVersion,
    }), approve ? 'Đã phê duyệt Bảo hiểm hệ thống SafeRide.' : 'Đã từ chối Bảo hiểm hệ thống SafeRide.');
  };

  const refreshInsuranceStatus = async () => {
    await run(
      () => refreshMockInsuranceStatus(claimId, claim?.rowVersion ?? accident?.claim?.rowVersion),
      'Đã cập nhật trạng thái từ MockInsuranceProvider của hệ thống SafeRide.',
    );
  };

  const submitWriteOff = async (event) => {
    event.preventDefault();
    if (!confirmRiskAction('Ghi nhận khoản ứng này không thể thu hồi? Thao tác tạo bản ghi đối soát có kiểm toán và không ghi giảm quỹ lần hai.')) return;
    const result = await run(() => writeOffClaimAdvance(claimId, {
      ...writeOff,
      amount: Number(writeOff.amount),
      rowVersion: claim?.rowVersion ?? accident?.claim?.rowVersion,
      idempotencyKey: writeOffKey,
    }), 'Đã ghi nhận khoản ứng không thể thu hồi.');
    if (result) {
      setWriteOff(initialWriteOff);
      setWriteOffKey(createIdempotencyKey('write-off'));
    }
  };

  const closeCurrentClaim = async () => {
    if (!confirmRiskAction('Đóng hồ sơ yêu cầu bảo vệ? Máy chủ chỉ cho phép khi funding, thu hồi, bảo hiểm và đối soát đã cân bằng.')) return;
    await run(() => closeClaim(claimId, claim?.rowVersion ?? accident?.claim?.rowVersion), 'Đã đóng hồ sơ yêu cầu bảo vệ và hồ sơ sự cố.');
  };

  const setAllocation = (key, value) => {
    const next = { ...assessment, [key]: Number(value) };
    if (key === 'driverFaultPercentage') {
      next.driverFaultLevel = Number(value) === 0
        ? 'NO_FAULT'
        : assessment.driverFaultLevel === 'NO_FAULT' ? 'ORDINARY_NEGLIGENCE' : assessment.driverFaultLevel;
    }
    const [, , party, defaultCause] = allocations.find(([allocationKey]) => allocationKey === key);
    next.causes = reconcilePartyCauses(assessment.causes, party, Number(value), defaultCause);
    setAssessment(next);
  };

  const addCause = () => setAssessment({
    ...assessment,
    causes: [...assessment.causes, { rootCause: 'UNKNOWN', responsibleParty: 'OBJECTIVE', percentage: 1 }],
  });

  return (
    <AdminLayout>
      <div className="risk-page">
        <header className="risk-page__header">
          <div>
            <h1>Hồ sơ sự cố & bảo vệ</h1>
            <p>Quy trình theo từng bước; trách nhiệm và nguồn chi trả luôn được xử lý riêng.</p>
          </div>
        </header>
        {(queue.error || detailError || error) && <div className="risk-alert risk-alert--error">{error || detailError || queue.error}</div>}
        {feedback && <div className="risk-alert risk-alert--success">{feedback}</div>}

        <section className="risk-card">
          <div className="risk-card__title"><h2>Hàng đợi tai nạn</h2><button type="button" onClick={queue.refetch}>Tải lại</button></div>
          <div className="risk-filters">
            <Field label="Trạng thái"><select value={filters.status} onChange={(event) => setFilters({ ...filters, status: event.target.value })}><option value="">Tất cả</option>{accidentStatuses.map((value) => <option key={value} value={value}>{riskProtectionLabel(value)}</option>)}</select></Field>
            <Field label="Loại sự cố"><select value={filters.category} onChange={(event) => setFilters({ ...filters, category: event.target.value })}><option value="">Tất cả</option>{accidentCategories.map((value) => <option key={value} value={value}>{riskProtectionLabel(value)}</option>)}</select></Field>
            <Field label="Trip ID"><input type="number" min="1" value={filters.tripId} onChange={(event) => setFilters({ ...filters, tripId: event.target.value })} /></Field>
            <Field label="Từ ngày"><input type="datetime-local" value={filters.fromUtc} onChange={(event) => setFilters({ ...filters, fromUtc: event.target.value })} /></Field>
            <Field label="Đến ngày"><input type="datetime-local" value={filters.toUtc} onChange={(event) => setFilters({ ...filters, toUtc: event.target.value })} /></Field>
          </div>
          <div className="risk-table-wrap">
            <table className="risk-table">
              <thead><tr><th>Mã</th><th>Chuyến</th><th>Loại</th><th>Trạng thái</th><th>Thời điểm</th><th>Yêu cầu bảo vệ</th><th /></tr></thead>
              <tbody>
                {accidents.map((item) => <tr key={item.id}><td>#{item.id}</td><td>#{item.tripId}</td><td>{riskProtectionLabel(item.category)}</td><td><span className="risk-badge">{riskProtectionLabel(item.status)}</span></td><td>{formatDate(item.occurredAtUtc)}</td><td>{item.claimId ? `#${item.claimId} · ${riskProtectionLabel(item.claimStatus)}` : 'Chưa tạo'}</td><td><button type="button" onClick={() => openAccident(item.id)}>Mở hồ sơ</button></td></tr>)}
                {!queue.isLoading && accidents.length === 0 && <tr><td colSpan="7">Không có hồ sơ phù hợp bộ lọc.</td></tr>}
              </tbody>
            </table>
          </div>
        </section>

        {selectedId && <div className="risk-workflow">
          <WorkflowHeading step="1" title="Bằng chứng & thông tin sự cố" description="Đọc hồ sơ và bằng chứng trước khi đánh giá nguyên nhân." />
          <section className="risk-grid">
            <div className="risk-card">
              <div className="risk-card__title"><h2>Hồ sơ #{selectedId}</h2>{detailLoading && <span>Đang tải...</span>}</div>
              {accident && <ul className="risk-detail-list">
                <li><b>Chuyến đi:</b> #{accident.tripId}</li>
                <li><b>Loại sự cố:</b> {riskProtectionLabel(accident.category)}</li>
                <li><b>Trạng thái:</b> {riskProtectionLabel(accident.status)}</li>
                <li><b>Thời điểm:</b> {formatDate(accident.occurredAtUtc)}</li>
                <li><b>Mô tả:</b> {accident.description}</li>
                <li><b>Biên bản công an:</b> {accident.policeReportReference || 'Chưa có'}</li>
              </ul>}
            </div>
            <div className="risk-card">
              <div className="risk-card__title"><h2>Bằng chứng ({accident?.evidence?.length ?? 0})</h2></div>
              <div className="risk-evidence">{accident?.evidence?.length
                ? accident.evidence.map((item) => <a key={item.id} href={item.fileUrl} target="_blank" rel="noreferrer"><b>{riskProtectionLabel(item.evidenceType)}</b> · {item.originalFileName ?? `Tệp #${item.id}`} · {formatDate(item.createdAtUtc)}</a>)
                : <p>Chưa có bằng chứng.</p>}</div>
            </div>
          </section>

          {accident && <form className="risk-card risk-form" onSubmit={(event) => submitAssessment(event, true)}>
            <WorkflowHeading step="2" title="Nguyên nhân" description="Chọn nguyên nhân, mức lỗi tài xế và khả năng nhận biết khiếm khuyết phương tiện." compact />
            <div className="risk-form__columns">
              <Field label="Mức lỗi tài xế"><select value={assessment.driverFaultLevel} onChange={(event) => setAssessment({ ...assessment, driverFaultLevel: event.target.value })}>{driverFaultLevels.map((value) => <option key={value} value={value}>{riskProtectionLabel(value)}</option>)}</select></Field>
              <Field label="Khả năng nhận biết lỗi phương tiện"><select value={assessment.vehicleDefectAwareness} onChange={(event) => setAssessment({ ...assessment, vehicleDefectAwareness: event.target.value })}>{awarenessValues.map((value) => <option key={value} value={value}>{riskProtectionLabel(value)}</option>)}</select></Field>
            </div>
            <div className="risk-card__title"><h3>Nguyên nhân gốc</h3><button type="button" onClick={addCause}>Thêm nguyên nhân</button></div>
            {assessment.causes.map((cause, index) => <CauseRow key={`${cause.responsibleParty}-${index}`} cause={cause} onChange={(next) => setAssessment({ ...assessment, causes: assessment.causes.map((item, position) => position === index ? next : item) })} onRemove={() => setAssessment({ ...assessment, causes: assessment.causes.filter((_, position) => position !== index) })} />)}
            <p className="risk-form__hint">Tổng tỷ lệ nguyên nhân: {causeTotal}%. Mỗi bên phải khớp với tỷ lệ trách nhiệm ở bước 3.</p>

            <WorkflowHeading step="3" title="Phân bổ trách nhiệm" description="Đây là tỷ lệ trách nhiệm, không phải tỷ lệ chi trả. Tổng bắt buộc bằng 100%." compact />
            <div className="risk-form__columns">{allocations.map(([key, label]) => <Field key={key} label={label}><input required type="number" min="0" max="100" value={assessment[key]} onChange={(event) => setAllocation(key, event.target.value)} /></Field>)}</div>
            <div className={`risk-total ${total === 100 && causeTotal === 100 ? 'risk-total--valid' : 'risk-total--invalid'}`}><strong>Tổng trách nhiệm: {total}%</strong><span>Tổng nguyên nhân: {causeTotal}%</span></div>

            <div className="risk-section-title">
              <h3>Kiểm tra trước khi xác nhận trách nhiệm</h3>
              <p>Xác nhận sẽ khóa assessment hiện tại và cho phép máy chủ tính đề xuất xử lý quyền lợi.</p>
            </div>
            <ResponsibilityReview assessment={assessment} />
            <div className="risk-actions">
              <button className="risk-secondary" disabled={busy || assessmentConfirmed} type="button" onClick={(event) => submitAssessment(event, false)}>Lưu bản nháp</button>
              <button disabled={busy || assessmentConfirmed || total !== 100 || causeTotal !== 100} type="submit">Xác nhận trách nhiệm</button>
            </div>
          </form>}

          {accident && <section className="risk-grid risk-grid--settlement">
            <form className="risk-card risk-form" onSubmit={submitSettlement}>
              <WorkflowHeading step="4" title="Nhập thiệt hại" description="Nhập các số liệu thiệt hại thực tế; máy chủ sẽ tự tính toàn bộ phân bổ bảo hiểm và Quỹ rủi ro." compact />
              <div className="risk-form__columns">{Object.keys(settlement).map((key) => <Field key={key} label={claimFieldLabel(key)}><input required type="number" min="0" value={settlement[key]} onChange={(event) => setSettlement({ ...settlement, [key]: event.target.value })} /></Field>)}</div>
              <CustomerInsuranceInput
                claim={currentClaim}
                eligibleDamageAmount={settlement.eligibleDamageAmount}
                value={customerInsurance}
                onChange={setCustomerInsurance}
              />
              <p className="risk-form__hint">Bảo hiểm có thể chi trả, khoản đã duyệt, thiệt hại còn lại và phân bổ Quỹ rủi ro đều do máy chủ trả về.</p>
              {!assessmentConfirmed && <div className="risk-alert risk-alert--info">Cần xác nhận trách nhiệm ở bước 3 trước khi máy chủ có thể tính đề xuất.</div>}
              <button disabled={busy || !claimId || !assessmentConfirmed} type="submit">Yêu cầu máy chủ tính đề xuất</button>
            </form>
            <div className="risk-card"><SettlementRecommendation claim={claim ?? accident.claim} /></div>
          </section>}

          {showSystemInsurance && <SystemInsuranceCard
            claim={currentClaim}
            busy={busy}
            insurance={insurance}
            setInsurance={setInsurance}
            onReview={submitInsurance}
            onRefresh={refreshInsuranceStatus}
            audits={audits.data ?? []}
          />}

          {claimId && <section className="risk-card risk-form">
            <WorkflowHeading step="5" title="Rà soát & thực hiện" description="Kiểm tra đề xuất máy chủ trước khi cấp kinh phí. Thao tác ghi giảm quỹ luôn yêu cầu xác nhận." compact />
            <div>
              <strong>Nguyên nhân đã ghi nhận</strong>
              <ul className="risk-detail-list">
                {assessment.causes.map((cause, index) => <li key={`${cause.responsibleParty}-${cause.rootCause}-${index}`}>{riskProtectionLabel(cause.responsibleParty)} · {riskProtectionLabel(cause.rootCause)} · {Number(cause.percentage)}%</li>)}
              </ul>
            </div>
            <div className="risk-review-grid">
              <ReviewItem label="Trạng thái hồ sơ" value={riskProtectionLabel((claim ?? accident.claim)?.status)} />
              <ReviewItem label="Thiệt hại đủ điều kiện" value={formatVnd((claim ?? accident.claim)?.eligibleDamageAmount)} />
              <ReviewItem label="Bảo hiểm riêng của khách" value={formatVnd((claim ?? accident.claim)?.customerInsuranceAppliedAmount)} />
              <ReviewItem label="Bảo hiểm riêng áp dụng cho khách" value={formatVnd((claim ?? accident.claim)?.customerInsuranceBenefitToCustomer)} />
              <ReviewItem label="Phần bảo hiểm riêng vượt phần khách" value={formatVnd((claim ?? accident.claim)?.customerInsuranceExcessAppliedToOtherLoss)} />
              <ReviewItem label="Bảo hiểm riêng giảm phần tài xế" value={formatVnd((claim ?? accident.claim)?.customerInsuranceBenefitToDriver)} />
              <ReviewItem label="Phần giảm không phân bổ lại lỗi" value={formatVnd((claim ?? accident.claim)?.customerInsuranceUnallocatedCategoryReduction)} />
              <ReviewItem label="Phần khách sau bảo hiểm riêng" value={formatVnd((claim ?? accident.claim)?.customerExposureAfterOwnInsurance)} />
              <ReviewItem label="Phần tài xế sau bảo hiểm riêng" value={formatVnd((claim ?? accident.claim)?.driverExposureBeforeSystemInsurance)} />
              <ReviewItem label="Bảo hiểm hệ thống tối đa" value={formatVnd((claim ?? accident.claim)?.systemInsuranceMaximumAmount)} />
              <ReviewItem label="Bảo hiểm hệ thống đã duyệt" value={formatVnd((claim ?? accident.claim)?.systemInsuranceApprovedAmount)} />
              <ReviewItem label="Quyền lợi hệ thống cho khách" value={formatVnd((claim ?? accident.claim)?.customerSystemInsuranceBenefit)} />
              <ReviewItem label="Quyền lợi hệ thống cho tài xế" value={formatVnd((claim ?? accident.claim)?.driverSystemInsuranceBenefit)} />
              <ReviewItem label="Thiệt hại còn lại sau bảo hiểm" value={formatVnd((claim ?? accident.claim)?.residualUninsuredDamage)} />
              <ReviewItem label="Phần tài xế còn lại" value={formatVnd((claim ?? accident.claim)?.driverAttributableResidualDamage)} />
              <ReviewItem label="Trách nhiệm tài xế" value={formatVnd((claim ?? accident.claim)?.driverLiabilityAmount)} />
              <ReviewItem label="Phần khách hàng còn lại" value={formatVnd((claim ?? accident.claim)?.customerAttributableResidualDamage)} />
              <ReviewItem label="Trách nhiệm khách hàng" value={formatVnd((claim ?? accident.claim)?.customerLiabilityAmount)} />
              <ReviewItem label="Phần bên thứ ba còn lại" value={formatVnd((claim ?? accident.claim)?.thirdPartyAttributableResidualDamage)} />
              <ReviewItem label="Khoản dự kiến thu hồi từ bên thứ ba" value={formatVnd((claim ?? accident.claim)?.thirdPartyLiabilityAmount)} />
              <ReviewItem label="Phần phương tiện/khách quan" value={formatVnd((claim ?? accident.claim)?.vehicleObjectiveResidualAmount)} />
              <ReviewItem label="Khoản ứng từ Quỹ rủi ro" value={formatVnd((claim ?? accident.claim)?.riskFundAdvanceAmount)} />
              <ReviewItem label="Hỗ trợ cuối cùng từ Quỹ rủi ro" value={formatVnd((claim ?? accident.claim)?.riskFundPermanentLossAmount)} />
              <ReviewItem label="Đã thu hồi" value={formatVnd((claim ?? accident.claim)?.recoveredAmount)} />
              <ReviewItem label="Còn phải thu hồi" value={formatVnd((claim ?? accident.claim)?.outstandingRecoveryAmount)} />
              <ReviewItem label="Phần Quỹ rủi ro đang chịu thực tế" value={formatVnd((claim ?? accident.claim)?.actualRecoverableFundExposure)} />
            </div>
            {(claim ?? accident.claim)?.status === 'PENDING_FUNDING' && <div className="risk-alert risk-alert--info">Quỹ hiện chưa đủ để xử lý toàn bộ đề xuất. Hồ sơ vẫn giữ trạng thái chờ cấp kinh phí; không tự động thay đổi phân bổ trách nhiệm.</div>}
            {accident.liabilityAssessment?.status === 'DISPUTED' && <div className="risk-alert risk-alert--info">Kết quả trách nhiệm đang được xem xét lại. Không thực hiện cấp kinh phí cho đến khi máy chủ cho phép.</div>}
            <button disabled={busy || !fundingAllowed} type="button" onClick={submitFunding}>Cấp kinh phí / thử lại cấp kinh phí</button>
          </section>}

          {claimId && <details className="risk-card risk-advanced risk-advanced--panel">
            <summary>Thao tác kế toán nâng cao & kiểm toán</summary>
            <p>Các thao tác dưới đây dùng cho hồ sơ ngoại lệ. Mọi thay đổi vẫn được máy chủ kiểm tra, ghi audit và bảo vệ bằng concurrency token nội bộ.</p>
            <section className="risk-grid">
              <form className="risk-form" onSubmit={submitRecovery}>
                <h3>Ghi nhận khoản thu hồi</h3>
                <p className="risk-form__hint">Chỉ ghi nhận tiền đã thực nhận. Không tự động trừ ví tài xế.</p>
                {recoveryError && <div className="risk-alert risk-alert--error" role="alert">{recoveryError}</div>}
                {recoverySuccess && <div className="risk-alert risk-alert--success" role="status">{recoverySuccess}</div>}
                <Field label="Nguồn thu hồi"><select value={recovery.sourceType} onChange={(event) => setRecovery({ ...recovery, sourceType: event.target.value })}>{recoverySources.map((value) => <option key={value} value={value}>{riskProtectionLabel(value)}</option>)}</select></Field>
                <Field label="Bên thanh toán"><input required value={recovery.payerReference} onChange={(event) => setRecovery({ ...recovery, payerReference: event.target.value })} /></Field>
                <Field label="Số tiền thực nhận"><input required type="number" min="1" value={recovery.amount} onChange={(event) => setRecovery({ ...recovery, amount: event.target.value })} /></Field>
                <Field label="Tham chiếu thanh toán"><input required value={recovery.paymentReference} onChange={(event) => setRecovery({ ...recovery, paymentReference: event.target.value })} /></Field>
                <Field label="Bằng chứng"><input required type="file" accept="image/jpeg,image/png,image/webp,application/pdf" onChange={(event) => setRecovery({ ...recovery, evidence: event.target.files?.[0] ?? null })} /></Field>
                <button disabled={busy} type="submit">Ghi nhận tiền đã thu hồi</button>
              </form>
              <form className="risk-form" onSubmit={submitWriteOff}>
                <h3>Ghi nhận khoản ứng không thể thu hồi</h3>
                <p className="risk-form__hint">Đây là bản ghi đối soát; không ghi giảm Quỹ rủi ro lần thứ hai.</p>
                <Field label="Số tiền"><input required type="number" min="1" value={writeOff.amount} onChange={(event) => setWriteOff({ ...writeOff, amount: event.target.value })} /></Field>
                <Field label="Lý do"><textarea required value={writeOff.reason} onChange={(event) => setWriteOff({ ...writeOff, reason: event.target.value })} /></Field>
                <Field label="Bằng chứng"><input required type="file" accept="image/jpeg,image/png,image/webp,application/pdf" onChange={(event) => setWriteOff({ ...writeOff, evidence: event.target.files?.[0] ?? null })} /></Field>
                <button disabled={busy} type="submit">Ghi nhận không thể thu hồi</button>
              </form>
              <div className="risk-form">
                <h3>Đóng hồ sơ</h3>
                <p className="risk-form__hint">Chỉ thực hiện khi funding, bảo hiểm, thu hồi và write-off đã cân bằng.</p>
                <button disabled={busy} type="button" onClick={closeCurrentClaim}>Đóng hồ sơ đã đối soát</button>
              </div>
            </section>
          </details>}
        </div>}
      </div>
    </AdminLayout>
  );
}

const systemInsuranceReasonLabels = {
  POLICY_LIMIT_ZERO: 'Hạn mức Bảo hiểm Hệ thống trong policy snapshot của chuyến đi bằng 0.',
  NO_REMAINING_COVERED_EXPOSURE: 'Không còn phần thiệt hại thuộc phạm vi Customer/Driver để Bảo hiểm Hệ thống xem xét.',
  CLAIM_RESOLVED: 'Hồ sơ đã được xử lý xong nên không còn khoản Bảo hiểm Hệ thống có thể duyệt.',
  NO_APPROVABLE_SYSTEM_INSURANCE: 'Máy chủ không xác định được khoản Bảo hiểm Hệ thống có thể duyệt cho hồ sơ này.',
};

export function CustomerInsuranceInput({ claim, eligibleDamageAmount, value, onChange }) {
  const enteredEligibleDamage = eligibleDamageAmount === '' || eligibleDamageAmount == null
    ? null
    : Number(eligibleDamageAmount);
  const eligibleDamageCap = Number.isFinite(enteredEligibleDamage)
    ? enteredEligibleDamage
    : claim?.eligibleDamageAmount;
  return <fieldset className="risk-form" aria-label="Bảo hiểm riêng của khách hàng">
    <legend>BẢO HIỂM RIÊNG CỦA KHÁCH HÀNG — KHÔNG BẮT BUỘC</legend>
    <p className="risk-form__hint">Đây là khoản bảo hiểm bên ngoài SafeRide đã xác nhận chi trả. Nếu khách không có hoặc không sử dụng bảo hiểm riêng, để 0.</p>
    <div className="risk-form__columns">
      <Field label="Khoản bảo hiểm riêng đã xác nhận chi trả">
        <input
          required
          aria-label="Khoản bảo hiểm riêng đã xác nhận chi trả"
          type="number"
          min="0"
          max={eligibleDamageCap ?? undefined}
          value={value.appliedAmount}
          onChange={(event) => onChange({ ...value, appliedAmount: event.target.value })}
        />
      </Field>
      <ReviewItem
        label="Phần trách nhiệm ban đầu của khách (máy chủ)"
        value={claim?.customerGrossExposure != null ? formatVnd(claim.customerGrossExposure) : 'Sẽ được máy chủ xác định'}
      />
      <ReviewItem
        label="Tối đa có thể áp dụng"
        value={eligibleDamageCap != null ? formatVnd(eligibleDamageCap) : 'Bằng thiệt hại đủ điều kiện do máy chủ xác định'}
      />
    </div>
    <details>
      <summary>Tham chiếu và ghi chú không bắt buộc</summary>
      <div className="risk-form__columns">
        <Field label="Mã tham chiếu"><input maxLength="200" value={value.reference} onChange={(event) => onChange({ ...value, reference: event.target.value })} /></Field>
        <Field label="Ghi chú"><textarea maxLength="1000" value={value.note} onChange={(event) => onChange({ ...value, note: event.target.value })} /></Field>
      </div>
    </details>
  </fieldset>;
}

export function SystemInsuranceCard({
  claim,
  busy = false,
  insurance,
  setInsurance,
  onReview,
  onRefresh,
  audits = [],
}) {
  const maximum = Number(claim?.maximumApprovableInsuranceAmount ?? claim?.systemInsuranceMaximumAmount ?? 0);
  const recommended = Number(claim?.recommendedInsuranceApprovalAmount ?? maximum);
  const isPendingReview = claim?.insuranceStatus === 'PENDING' && maximum > 0;
  const zeroReason = systemInsuranceReasonLabels[claim?.systemInsuranceEvaluationReason]
    ?? systemInsuranceReasonLabels.NO_APPROVABLE_SYSTEM_INSURANCE;

  return <section className="risk-card risk-form risk-insurance-review" aria-label="Bảo hiểm hệ thống SafeRide">
    <div className="risk-section-title">
      <h3>BẢO HIỂM HỆ THỐNG SAFERIDE</h3>
      <p>Bảo hiểm mặc định của chuyến đi · Nhà cung cấp mô phỏng</p>
    </div>
    <div className="risk-review-grid">
      <ReviewItem label="Nhà cung cấp" value={claim?.systemInsuranceProvider ?? 'MockInsuranceProvider'} />
      <ReviewItem label="Trạng thái" value={riskProtectionLabel(claim?.insuranceStatus)} />
      <ReviewItem label="Thiệt hại đủ điều kiện" value={formatVnd(claim?.eligibleDamageAmount)} />
      <ReviewItem label="Bảo hiểm riêng khách hàng" value={formatVnd(claim?.customerInsuranceAppliedAmount)} />
      <ReviewItem label="Còn lại sau bảo hiểm riêng" value={formatVnd(claim?.remainingLossAfterCustomerInsurance)} />
      <ReviewItem label="Phần Customer/Driver còn lại" value={formatVnd(claim?.systemInsuranceCoveredExposureRemaining)} />
      <ReviewItem label="Giới hạn bảo hiểm hệ thống" value={formatVnd(claim?.systemInsuranceCoverageLimitSnapshot)} />
      <ReviewItem label="Mức tối đa có thể duyệt" value={formatVnd(maximum)} />
      <ReviewItem label="Mức đề xuất" value={formatVnd(recommended)} />
      <ReviewItem label="Mức đã duyệt" value={formatVnd(claim?.systemInsuranceApprovedAmount)} />
      <ReviewItem label="Tham chiếu nhà cung cấp" value={claim?.insuranceReference ?? '—'} />
    </div>
    {maximum <= 0 && <div className="risk-alert risk-alert--info">
      <strong>Bảo hiểm hệ thống hiện không có khoản có thể duyệt cho hồ sơ này.</strong>
      <p>{zeroReason}</p>
    </div>}
    {claim?.insuranceStatus !== 'NOT_SUBMITTED' && claim?.insuranceReference && <button className="risk-secondary" disabled={busy} type="button" onClick={onRefresh}>Cập nhật trạng thái từ nhà cung cấp</button>}
    {isPendingReview && <>
      {insurance.mode === 'lower' && <Field label="Số tiền duyệt thấp hơn"><input required type="number" min="1" max={maximum} value={insurance.approvedAmount} onChange={(event) => setInsurance({ ...insurance, approvedAmount: event.target.value })} /></Field>}
      {(insurance.mode === 'lower' || insurance.mode === 'reject') && <Field label="Lý do (bắt buộc)"><textarea required value={insurance.reason} onChange={(event) => setInsurance({ ...insurance, reason: event.target.value })} /></Field>}
      <div className="risk-actions">
        <button disabled={busy} type="button" onClick={(event) => onReview(event, true, 'recommended')}>Phê duyệt mức đề xuất</button>
        {insurance.mode === 'recommended' && <>
          <button className="risk-secondary" disabled={busy} type="button" onClick={() => setInsurance({ ...insurance, mode: 'lower' })}>Phê duyệt mức thấp hơn</button>
          <button className="risk-secondary" disabled={busy} type="button" onClick={() => setInsurance({ ...insurance, mode: 'reject' })}>Từ chối</button>
        </>}
        {insurance.mode === 'lower' && <>
          <button disabled={busy} type="button" onClick={(event) => onReview(event, true, 'lower')}>Xác nhận mức thấp hơn</button>
          <button className="risk-secondary" disabled={busy} type="button" onClick={() => setInsurance({ ...insurance, mode: 'recommended' })}>Hủy</button>
        </>}
        {insurance.mode === 'reject' && <>
          <button className="risk-secondary" disabled={busy} type="button" onClick={(event) => onReview(event, false, 'reject')}>Xác nhận từ chối</button>
          <button className="risk-secondary" disabled={busy} type="button" onClick={() => setInsurance({ ...insurance, mode: 'recommended' })}>Hủy</button>
        </>}
      </div>
    </>}
    <details><summary>Lịch sử Bảo hiểm hệ thống SafeRide (mô phỏng)</summary>{audits.map((item) => <p key={item.id} className="risk-form__hint">{formatDate(item.createdAtUtc)} · {riskProtectionLabel(item.operation)} · {riskProtectionLabel(item.resultStatus)} · {formatVnd(item.approvedAmount)} · {item.providerReference}</p>)}</details>
  </section>;
}

function WorkflowHeading({ step, title, description, compact = false }) {
  return <div className={`risk-step-heading ${compact ? 'risk-step-heading--compact' : ''}`}><span>{step}</span><div><h2>{title}</h2><p>{description}</p></div></div>;
}

function CauseRow({ cause, onChange, onRemove }) {
  return <div className="risk-cause-row">
    <Field label="Nguyên nhân"><select value={cause.rootCause} onChange={(event) => onChange({ ...cause, rootCause: event.target.value })}>{rootCauses.map((value) => <option key={value} value={value}>{riskProtectionLabel(value)}</option>)}</select></Field>
    <Field label="Bên liên quan"><select value={cause.responsibleParty} onChange={(event) => onChange({ ...cause, responsibleParty: event.target.value })}>{responsibleParties.map((value) => <option key={value} value={value}>{riskProtectionLabel(value)}</option>)}</select></Field>
    <Field label="Tỷ lệ %"><input type="number" min="1" max="100" value={cause.percentage} onChange={(event) => onChange({ ...cause, percentage: Number(event.target.value) })} /></Field>
    <button type="button" onClick={onRemove}>Xóa</button>
  </div>;
}

function ResponsibilityReview({ assessment }) {
  return <div className="risk-review-grid">{allocations.map(([key, label]) => <ReviewItem key={key} label={label} value={`${Number(assessment[key])}%`} />)}<ReviewItem label="Mức lỗi tài xế" value={riskProtectionLabel(assessment.driverFaultLevel)} /></div>;
}

function ReviewItem({ label, value }) {
  return <div className="risk-review-item"><span>{label}</span><strong>{value}</strong></div>;
}

function Field({ label, children }) {
  return <label className="risk-field"><span>{label}</span>{children}</label>;
}

function toUtc(value) {
  return value ? new Date(value).toISOString() : '';
}

function formatDate(value) {
  return value ? new Date(value).toLocaleString('vi-VN') : '—';
}

function claimFieldLabel(key) {
  return ({
    totalDamageAmount: 'Tổng thiệt hại ghi nhận',
    eligibleDamageAmount: 'Thiệt hại đủ điều kiện',
  })[key] ?? key;
}

export default StaffAccidentsPage;
