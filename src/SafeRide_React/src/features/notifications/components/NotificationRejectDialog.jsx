function NotificationRejectDialog({
    notification,
    reason,
    errorMessage,
    isSubmitting,
    onReasonChange,
    onClose,
    onConfirm,
}) {
    if (!notification) {
        return null;
    }

    return (
        <div className="notification-dialog-backdrop" role="presentation">
            <div className="notification-dialog" aria-modal="true" role="dialog">
                <div className="notification-dialog__header">
                    <div>
                        <h3>Từ chối thông báo</h3>
                        <p>{notification.title}</p>
                    </div>
                    <button
                        className="notification-dialog__close"
                        type="button"
                        onClick={onClose}
                        disabled={isSubmitting}
                    >
                        Đóng
                    </button>
                </div>

                <label className="notification-dialog__field">
                    <span>Lý do từ chối</span>
                    <textarea
                        value={reason}
                        onChange={(event) => onReasonChange(event.target.value)}
                        placeholder="Nhập lý do bắt buộc trước khi từ chối thông báo..."
                        rows="4"
                        disabled={isSubmitting}
                    />
                </label>

                {errorMessage && (
                    <div className="notification-dialog__error" role="alert">
                        {errorMessage}
                    </div>
                )}

                <div className="notification-dialog__actions">
                    <button type="button" onClick={onClose} disabled={isSubmitting}>
                        Hủy
                    </button>
                    <button
                        className="notification-dialog__confirm"
                        type="button"
                        onClick={onConfirm}
                        disabled={isSubmitting}
                    >
                        {isSubmitting ? 'Đang lưu...' : 'Xác nhận từ chối'}
                    </button>
                </div>
            </div>
        </div>
    );
}

export default NotificationRejectDialog;
