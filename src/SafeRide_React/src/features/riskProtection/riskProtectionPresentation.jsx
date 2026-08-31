import { formatVnd } from './riskProtectionApi';

// This module intentionally keeps the shared labels, pure presentation helpers,
// and their small renderer together so every Risk Protection screen uses one vocabulary.
/* eslint-disable react-refresh/only-export-components */

const labels = {
  REPORTED: 'Đã tiếp nhận',
  EVIDENCE_COLLECTION: 'Đang thu thập bằng chứng',
  UNDER_REVIEW: 'Đang xem xét',
  LIABILITY_PENDING: 'Chờ xác định trách nhiệm',
  SETTLEMENT: 'Đang xử lý quyền lợi',
  CLOSED: 'Đã đóng',
  REJECTED: 'Đã từ chối',
  DRIVER_INJURY: 'Chấn thương tài xế',
  CUSTOMER_VEHICLE_DAMAGE: 'Thiệt hại xe khách hàng',
  THIRD_PARTY_DAMAGE: 'Thiệt hại bên thứ ba',
  MULTIPLE: 'Nhiều loại thiệt hại',
  NO_FAULT: 'Không có lỗi',
  ORDINARY_NEGLIGENCE: 'Sơ suất thông thường',
  GROSS_NEGLIGENCE: 'Sơ suất nghiêm trọng',
  INTENTIONAL_MISCONDUCT: 'Hành vi cố ý',
  DRIVER_ERROR: 'Sai sót của tài xế',
  CUSTOMER_INTERFERENCE: 'Khách hàng can thiệp việc điều khiển xe',
  THIRD_PARTY_ERROR: 'Lỗi từ bên thứ ba',
  VEHICLE_MECHANICAL_FAILURE: 'Sự cố kỹ thuật phương tiện',
  VEHICLE_PRE_EXISTING_DEFECT: 'Khiếm khuyết phương tiện có từ trước',
  ROAD_CONDITION: 'Điều kiện đường sá',
  WEATHER: 'Điều kiện thời tiết',
  FORCE_MAJEURE: 'Sự kiện bất khả kháng',
  UNKNOWN: 'Chưa xác định',
  CUSTOMER_KNEW: 'Khách hàng đã biết',
  DRIVER_KNEW: 'Tài xế đã biết',
  BOTH_KNEW: 'Cả hai bên đã biết',
  NEITHER_COULD_REASONABLY_KNOW: 'Không bên nào có thể phát hiện hợp lý',
  DRAFT: 'Bản nháp',
  PENDING_CONFIRMATION: 'Chờ xác nhận',
  CONFIRMED: 'Đã xác nhận',
  DISPUTED: 'Đang được xem xét lại',
  APPROVED: 'Đã phê duyệt',
  PENDING_FUNDING: 'Chờ cấp kinh phí',
  FUNDED: 'Đã cấp kinh phí',
  RECOVERY_IN_PROGRESS: 'Đang thu hồi',
  SETTLED: 'Đã đối soát',
  NOT_SUBMITTED: 'Chưa gửi bảo hiểm',
  PENDING: 'Đang chờ',
  DIRECT_TO_CLAIMANT: 'Chi trả trực tiếp cho bên nhận bồi thường',
  REIMBURSE_RISK_FUND: 'Hoàn lại Quỹ rủi ro',
  DRIVER: 'Tài xế',
  CUSTOMER: 'Khách hàng',
  THIRD_PARTY: 'Bên thứ ba',
  VEHICLE: 'Phương tiện',
  OBJECTIVE: 'Nguyên nhân khách quan',
  INSURANCE: 'Bảo hiểm',
  PHOTO: 'Ảnh',
  DOCUMENT: 'Tài liệu',
  DRIVER_STATEMENT: 'Tường trình của tài xế',
  CUSTOMER_STATEMENT: 'Tường trình của khách hàng',
  THIRD_PARTY_INFORMATION: 'Thông tin bên thứ ba',
  POLICE_REPORT: 'Biên bản công an',
  OPENING_BALANCE: 'Số dư đầu kỳ',
  CONTRIBUTION: 'Khoản trích vào Quỹ rủi ro',
  CLAIM_ADVANCE: 'Khoản ứng có thể thu hồi',
  CLAIM_PAYOUT: 'Khoản hỗ trợ cuối cùng',
  DRIVER_RECOVERY: 'Thu hồi từ tài xế',
  CUSTOMER_RECOVERY: 'Thu hồi từ khách hàng',
  THIRD_PARTY_RECOVERY: 'Thu hồi từ bên thứ ba',
  INSURANCE_RECOVERY: 'Thu hồi từ bảo hiểm',
  ADJUSTMENT: 'Điều chỉnh có kiểm toán',
  CREDIT: 'Ghi tăng quỹ',
  DEBIT: 'Ghi giảm quỹ',
};

export const initialCustomerInsurance = { appliedAmount: '0', reference: '', note: '' };

const evaluatedClaimStatuses = new Set([
  'APPROVED',
  'PENDING_FUNDING',
  'FUNDED',
  'RECOVERY_IN_PROGRESS',
  'SETTLED',
  'CLOSED',
]);

export function shouldShowSystemInsurance(claim) {
  if (!claim) return false;
  return claim.insuranceStatus !== 'NOT_SUBMITTED'
    || evaluatedClaimStatuses.has(claim.status)
    || Number(claim.totalDamageAmount) > 0
    || Number(claim.eligibleDamageAmount) > 0;
}

export function riskProtectionLabel(value) {
  if (!value) return '—';
  return labels[value] ?? String(value).toLowerCase().split('_')
    .map((part) => `${part.charAt(0).toUpperCase()}${part.slice(1)}`)
    .join(' ');
}

export function responsibilityTotal(assessment) {
  return [
    'driverFaultPercentage',
    'customerFaultPercentage',
    'thirdPartyFaultPercentage',
    'vehicleFailurePercentage',
    'objectiveCausePercentage',
  ].reduce((sum, key) => sum + Number(assessment?.[key] ?? 0), 0);
}

export function buildSettlementRecommendation(claim) {
  if (!claim) return [];
  return [
    ['Thiệt hại đủ điều kiện', Number(claim.eligibleDamageAmount ?? 0), 'base'],
    ['Phần lỗi gộp của khách', Number(claim.customerGrossExposure ?? 0), 'base'],
    ['Bảo hiểm riêng của khách', Number(claim.customerInsuranceAppliedAmount ?? 0), 'offset'],
    ['Phần khách sau bảo hiểm riêng', Number(claim.customerExposureAfterOwnInsurance ?? 0), 'base'],
    ['Bảo hiểm hệ thống SafeRide đã duyệt', Number(claim.systemInsuranceApprovedAmount ?? claim.insuranceApprovedAmount ?? 0), 'offset'],
    ['Quyền lợi hệ thống cho khách', Number(claim.customerSystemInsuranceBenefit ?? 0), 'offset'],
    ['Quyền lợi hệ thống cho tài xế', Number(claim.driverSystemInsuranceBenefit ?? 0), 'offset'],
    ['Thiệt hại còn lại sau hai lớp bảo hiểm', Number(claim.residualUninsuredDamage ?? 0), 'base'],
    ['Phần tài xế trước rate/cap', Number(claim.driverRemainingExposureBeforeRateCap ?? claim.driverAttributableResidualDamage ?? 0), 'offset'],
    ['Nghĩa vụ tài xế sau rate/cap', Number(claim.driverLiabilityAmount ?? 0), 'offset'],
    ['Phần khách hàng', Number(claim.customerAttributableResidualDamage ?? claim.customerLiabilityAmount ?? 0), 'offset'],
    ['Phần bên thứ ba', Number(claim.thirdPartyAttributableResidualDamage ?? claim.thirdPartyLiabilityAmount ?? 0), 'offset'],
    ['Phần phương tiện/khách quan', Number(claim.vehicleObjectiveResidualAmount ?? 0), 'fund'],
    ['Quỹ rủi ro · khoản ứng có thể thu hồi', Number(claim.riskFundAdvanceAmount ?? 0), 'fund'],
    ['Quỹ rủi ro · hỗ trợ cuối cùng', Number(claim.riskFundPermanentLossAmount ?? 0), 'fund'],
  ];
}

export function SettlementRecommendation({ claim }) {
  if (!claim) {
    return <p className="risk-form__hint">Lưu và xác nhận trách nhiệm để máy chủ tạo đề xuất xử lý.</p>;
  }
  return (
    <div className="risk-recommendation" data-testid="server-settlement-recommendation">
      <div className="risk-card__title">
        <h3>Đề xuất từ máy chủ</h3>
        <span className="risk-badge">{riskProtectionLabel(claim.status)}</span>
      </div>
      <p className="risk-form__hint">Các khoản dưới đây do máy chủ tính theo fault đã duyệt và policy snapshot của chuyến đi; giao diện không tự phân bổ tiền bảo hiểm.</p>
      <dl>
        {buildSettlementRecommendation(claim).map(([label, amount, kind]) => (
          <div key={label} className={`risk-recommendation__row risk-recommendation__row--${kind}`}>
            <dt>{label}</dt>
            <dd>{kind === 'offset' && amount > 0 ? '− ' : ''}{formatVnd(amount)}</dd>
          </div>
        ))}
      </dl>
    </div>
  );
}

export function confirmRiskAction(message, confirmAction = globalThis.confirm) {
  return typeof confirmAction === 'function' && confirmAction(message);
}
