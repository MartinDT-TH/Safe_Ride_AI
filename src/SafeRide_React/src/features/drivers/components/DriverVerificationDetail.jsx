import { useState } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faBalanceScale, faArrowLeft, faChevronDown, faClipboardCheck, faFileAlt, faIdBadge, faIdCard, } from '@fortawesome/free-solid-svg-icons';
import './DriverVerificationDetail.css';
const DOCUMENT_TABS = [
    { id: 'citizen-id', label: 'Căn cước công dân', icon: faIdCard, documentType: 'ID_CARD' },
    { id: 'license', label: 'Bằng lái xe', icon: faIdBadge, documentType: 'DRIVING_LICENSE' },
    { id: 'record', label: 'Lý lịch tư pháp', icon: faFileAlt, documentType: 'CRIMINAL_RECORD' },
];
function DriverVerificationDetail({ driver, onBack, onReviewKyc, actionDriverId, }) {
    const [activeDocumentTab, setActiveDocumentTab] = useState('citizen-id');
    const [rejectionReason, setRejectionReason] = useState('');
    const canReviewKyc = driver.status === 'pending_kyc';
    const isActionBusy = actionDriverId === driver.id;
    const activeDocumentType = DOCUMENT_TABS.find((tab) => tab.id === activeDocumentTab)?.documentType ?? 'ID_CARD';
    const activeDocument = driver.documents.find((document) => document.documentType === activeDocumentType);
    return (<section className="driver-verification" id="driver-verification-detail">
      <button type="button" className="verification-back-btn" onClick={onBack}>
        <FontAwesomeIcon icon={faArrowLeft}/>
        Quay lại
      </button>

      <div className="verification-title-row">
        <h1 className="verification-title">Xác minh thông tin Tài xế</h1>
        {canReviewKyc && (<span className="verification-status">
            <FontAwesomeIcon icon={faClipboardCheck}/>
            Chờ duyệt (KYC Pending)
          </span>)}
      </div>

      <div className="verification-grid">
        <div className="verification-side">
          <article className="driver-profile-card">
            <div className="profile-photo-ring">
              <div className={`profile-photo${driver.avatarUrl ? ' profile-photo--image' : ''}`} style={{ backgroundColor: driver.avatar }}>
                {driver.avatarUrl ? (<img src={driver.avatarUrl} alt={driver.name}/>) : (<span>{driver.initials}</span>)}
              </div>
            </div>

            <h2>{driver.name}</h2>
            <p>DRIVER_ID: {driver.driverCode}</p>

            <dl className="profile-meta-list">
              <div>
                <dt>Số điện thoại</dt>
                <dd>{driver.phone}</dd>
              </div>
              <div>
                <dt>Email</dt>
                <dd>{driver.email}</dd>
              </div>
              <div>
                <dt>Ngày đăng ký</dt>
                <dd>{driver.registeredDate}</dd>
              </div>
              <div>
                <dt>Thành phố</dt>
                <dd>{driver.city}</dd>
              </div>
            </dl>
          </article>

          {canReviewKyc && (<article className="verification-decision-card">
              <h2>
                <FontAwesomeIcon icon={faBalanceScale}/>
                Quyết định hồ sơ
              </h2>

              <label className="decision-field">
                <span>Lý do từ chối (nếu có)</span>
                <button type="button" className="decision-select">
                  Chọn lý do...
                  <FontAwesomeIcon icon={faChevronDown}/>
                </button>
              </label>

              <label className="decision-field">
                <textarea placeholder="Ghi chú chi tiết cho tài xế..." value={rejectionReason} onChange={(event) => setRejectionReason(event.target.value)}/>
              </label>

              <div className="decision-actions">
                <button type="button" className="decision-btn decision-btn--reject" disabled={isActionBusy} onClick={() => onReviewKyc(driver, 'Rejected', rejectionReason)}>
                  Từ chối
                </button>
                <button type="button" className="decision-btn decision-btn--approve" disabled={isActionBusy} onClick={() => onReviewKyc(driver, 'Approved')}>
                  Phê duyệt
                </button>
              </div>
            </article>)}
        </div>

        <article className="verification-doc-card">
          <div className="document-tabs">
            {DOCUMENT_TABS.map((tab) => (<button key={tab.id} type="button" className={`document-tab${activeDocumentTab === tab.id ? ' document-tab--active' : ''}`} onClick={() => setActiveDocumentTab(tab.id)}>
                <FontAwesomeIcon icon={tab.icon}/>
                {tab.label}
              </button>))}
          </div>

          <div className="document-content">
            <DocumentPreview document={activeDocument} documentType={activeDocumentType}/>

            <DocumentInfoPanel driver={driver} document={activeDocument} documentType={activeDocumentType}/>
          </div>
        </article>
      </div>
    </section>);
}
function DocumentPreview({ document, documentType, }) {
    if (documentType === 'CRIMINAL_RECORD') {
        return (<div className="document-preview-grid document-preview-grid--single">
        <div>
          <h3>FILE LÝ LỊCH TƯ PHÁP</h3>
          <DocumentImage src={document?.fileUrl} alt="Lý lịch tư pháp" variant="front"/>
        </div>
      </div>);
    }
    const frontLabel = documentType === 'ID_CARD' ? 'MẶT TRƯỚC CCCD' : 'MẶT TRƯỚC BẰNG LÁI';
    const backLabel = documentType === 'ID_CARD' ? 'MẶT SAU CCCD' : 'MẶT SAU BẰNG LÁI';
    return (<div className="document-preview-grid">
      <div>
        <h3>{frontLabel}</h3>
        <DocumentImage src={document?.frontImageUrl} alt={frontLabel.toLocaleLowerCase('vi-VN')} variant="front"/>
      </div>

      <div>
        <h3>{backLabel}</h3>
        <DocumentImage src={document?.backImageUrl} alt={backLabel.toLocaleLowerCase('vi-VN')} variant="back"/>
      </div>
    </div>);
}
function DocumentImage({ src, alt, variant, }) {
    const isPdf = src?.toLowerCase().includes('.pdf');
    return (<div className={`document-image document-image--${variant}`}>
      {src && isPdf ? (<a className="document-file-link" href={src} target="_blank" rel="noreferrer">
          Mở file PDF
        </a>) : src ? (<img src={src} alt={alt}/>) : variant === 'front' ? (<div className="identity-card-mock">
          <span className="identity-emblem"></span>
          <span className="identity-line identity-line--long"></span>
          <span className="identity-line"></span>
        </div>) : (<div className="identity-back-mock">
          <span className="qr-grid"></span>
          <span className="qr-star"></span>
        </div>)}
    </div>);
}
function DocumentInfoPanel({ driver, document, documentType, }) {
    if (documentType === 'DRIVING_LICENSE') {
        return (<dl className="document-info-panel">
        <InfoItem label="Số bằng lái" value={document?.documentNumber ?? 'Chưa cập nhật'}/>
        <InfoItem label="Hạng bằng" value={document?.licenseClass ?? 'Chưa cập nhật'}/>
        <InfoItem label="Ngày cấp" value={formatDocumentDate(document?.issueDate)}/>
        <InfoItem label="Ngày hết hạn" value={formatDocumentDate(document?.expiryDate)}/>
        <InfoItem label="Trạng thái" value={formatKycStatus(document?.kycStatus)} wide/>
      </dl>);
    }
    if (documentType === 'CRIMINAL_RECORD') {
        return (<dl className="document-info-panel">
        <InfoItem label="Mã hồ sơ" value={document?.documentNumber ?? 'Chưa cập nhật'}/>
        <InfoItem label="Ngày nộp" value={formatDocumentDate(document?.createdAt)}/>
        <InfoItem label="Ngày duyệt" value={formatDocumentDate(document?.verifiedAt)}/>
        <InfoItem label="Trạng thái" value={formatKycStatus(document?.kycStatus)}/>
        <InfoItem label="Lý do từ chối" value={document?.rejectionReason ?? 'Không có'} wide/>
      </dl>);
    }
    return (<dl className="document-info-panel">
      <InfoItem label="Số CCCD" value={driver.kyc.citizenId}/>
      <InfoItem label="Họ và Tên" value={driver.kyc.fullName}/>
      <InfoItem label="Ngày sinh" value={driver.kyc.dateOfBirth}/>
      <InfoItem label="Giới tính" value={driver.kyc.gender}/>
      <InfoItem label="Địa chỉ thường trú" value={driver.kyc.address} wide/>
    </dl>);
}
function InfoItem({ label, value, wide = false, }) {
    return (<div className={wide ? 'document-info-panel__wide' : undefined}>
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>);
}
function formatDocumentDate(value) {
    if (!value) {
        return 'Chưa cập nhật';
    }
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
        return value;
    }
    return new Intl.DateTimeFormat('vi-VN', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
    }).format(date);
}
function formatKycStatus(status) {
    switch (status) {
        case 'Approved':
            return 'Đã duyệt';
        case 'Rejected':
            return 'Từ chối';
        case 'Pending':
            return 'Chờ duyệt';
        default:
            return 'Chưa cập nhật';
    }
}
export default DriverVerificationDetail;
