function NotificationApproveDialog({
    notification,
    errorMessage,
    isSubmitting,
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
                        <h3>Duyệt thông báo</h3>
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

                <div className="notification-dialog__content">
                    <p>
                        Sau khi duyệt, thông báo sẽ được phát hành tới đúng nhóm người dùng đã chọn.
                    </p>
                </div>

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
                        className="notification-dialog__confirm notification-dialog__confirm--approve"
                        type="button"
                        onClick={onConfirm}
                        disabled={isSubmitting}
                    >
                        {isSubmitting ? 'Đang duyệt...' : 'Xác nhận duyệt'}
                    </button>
                </div>
            </div>
        </div>
    );
}

export default NotificationApproveDialog;
