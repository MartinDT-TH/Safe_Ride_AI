import './ActionFeedback.css';

function ActionFeedback({ message, variant = 'success' }) {
    if (!message) {
        return null;
    }

    return (
        <div className={`action-feedback action-feedback--${variant}`} role={variant === 'error' ? 'alert' : 'status'}>
            <span>{message}</span>
        </div>
    );
}

export default ActionFeedback;
