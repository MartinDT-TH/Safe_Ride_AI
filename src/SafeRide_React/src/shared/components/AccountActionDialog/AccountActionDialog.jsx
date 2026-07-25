import { useEffect, useId, useState } from 'react';
import { createPortal } from 'react-dom';
import Button from '../Button/Button';
import './AccountActionDialog.css';

const UNLOCK_CONFIRM_TEXT = 'CONFIRM';

function AccountActionDialog({
    isOpen,
    mode,
    accountType,
    accountName,
    currentReason,
    isSubmitting = false,
    errorMessage = null,
    onClose,
    onConfirm,
}) {
    const titleId = useId();
    const fieldId = useId();
    const descriptionId = useId();
    const [reason, setReason] = useState('');
    const [unlockText, setUnlockText] = useState('');
    const [validationMessage, setValidationMessage] = useState(null);

    useEffect(() => {
        if (!isOpen || typeof document === 'undefined') {
            return undefined;
        }

        const originalOverflow = document.body.style.overflow;
        document.body.style.overflow = 'hidden';

        return () => {
            document.body.style.overflow = originalOverflow;
        };
    }, [isOpen]);

    useEffect(() => {
        if (!isOpen || typeof document === 'undefined') {
            return undefined;
        }

        const handleKeyDown = (event) => {
            if (event.key === 'Escape' && !isSubmitting) {
                onClose?.();
            }
        };

        document.addEventListener('keydown', handleKeyDown);
        return () => {
            document.removeEventListener('keydown', handleKeyDown);
        };
    }, [isOpen, isSubmitting, onClose]);

    if (!isOpen || typeof document === 'undefined') {
        return null;
    }

    const isLockMode = mode === 'lock';
    const normalizedAccountType = accountType === 'driver' ? 't\u00e0i x\u1ebf' : 'kh\u00e1ch h\u00e0ng';
    const trimmedReason = reason.trim();
    const isUnlockConfirmed = unlockText === UNLOCK_CONFIRM_TEXT;
    const submitDisabled = isSubmitting || (isLockMode ? trimmedReason.length === 0 : !isUnlockConfirmed);
    const title = isLockMode
        ? 'X\u00e1c nh\u1eadn kh\u00f3a t\u00e0i kho\u1ea3n'
        : 'X\u00e1c nh\u1eadn m\u1edf kh\u00f3a t\u00e0i kho\u1ea3n';
    const description = isLockMode
        ? `T\u00e0i kho\u1ea3n ${normalizedAccountType} n\u00e0y s\u1ebd b\u1ecb kh\u00f3a ngay sau khi b\u1ea1n x\u00e1c nh\u1eadn.`
        : `Nh\u1eadp ch\u00ednh x\u00e1c ${UNLOCK_CONFIRM_TEXT} \u0111\u1ec3 x\u00e1c nh\u1eadn m\u1edf kh\u00f3a t\u00e0i kho\u1ea3n ${normalizedAccountType}.`;
    const submitLabel = isSubmitting
        ? '\u0110ang x\u1eed l\u00fd...'
        : isLockMode
            ? 'Kh\u00f3a t\u00e0i kho\u1ea3n'
            : 'M\u1edf kh\u00f3a t\u00e0i kho\u1ea3n';
    const helperText = isLockMode
        ? 'L\u00fd do kh\u00f3a l\u00e0 b\u1eaft bu\u1ed9c.'
        : `Nh\u1eadp ch\u00ednh x\u00e1c ${UNLOCK_CONFIRM_TEXT} \u0111\u1ec3 b\u1eadt n\u00fat m\u1edf kh\u00f3a.`;
    const accountLabel = 'T\u00e0i kho\u1ea3n';
    const currentReasonLabel = 'L\u00fd do kh\u00f3a hi\u1ec7n t\u1ea1i';
    const lockReasonLabel = 'L\u00fd do kh\u00f3a';
    const lockReasonPlaceholder = 'Nh\u1eadp l\u00fd do kh\u00f3a t\u00e0i kho\u1ea3n';
    const unlockConfirmLabel = 'Nh\u1eadp CONFIRM \u0111\u1ec3 x\u00e1c nh\u1eadn';
    const cancelLabel = 'H\u1ee7y';

    const handleBackdropClick = (event) => {
        if (event.target === event.currentTarget && !isSubmitting) {
            onClose?.();
        }
    };

    const handleSubmit = (event) => {
        event.preventDefault();

        if (isLockMode) {
            if (!trimmedReason) {
                setValidationMessage('Vui l\u00f2ng nh\u1eadp l\u00fd do kh\u00f3a t\u00e0i kho\u1ea3n.');
                return;
            }

            onConfirm?.({ reason: trimmedReason });
            return;
        }

        if (!isUnlockConfirmed) {
            setValidationMessage(`Vui l\u00f2ng nh\u1eadp ch\u00ednh x\u00e1c ${UNLOCK_CONFIRM_TEXT} \u0111\u1ec3 m\u1edf kh\u00f3a t\u00e0i kho\u1ea3n.`);
            return;
        }

        onConfirm?.();
    };

    const handleReasonChange = (event) => {
        setReason(event.target.value);
        if (validationMessage) {
            setValidationMessage(null);
        }
    };

    const handleUnlockTextChange = (event) => {
        setUnlockText(event.target.value);
        if (validationMessage) {
            setValidationMessage(null);
        }
    };

    return createPortal(
        <div className="account-action-dialog-backdrop" onClick={handleBackdropClick}>
            <div
                className="account-action-dialog"
                role="dialog"
                aria-modal="true"
                aria-labelledby={titleId}
                aria-describedby={descriptionId}
            >
                <div className="account-action-dialog__header">
                    <h2 id={titleId}>{title}</h2>
                    <p id={descriptionId}>{description}</p>
                </div>

                <div className="account-action-dialog__target">
                    <span className="account-action-dialog__target-label">{accountLabel}</span>
                    <strong>{accountName ?? 'Ch\u01b0a x\u00e1c \u0111\u1ecbnh'}</strong>
                </div>

                {!isLockMode && currentReason && (
                    <div className="account-action-dialog__reason-note">
                        <span>{currentReasonLabel}</span>
                        <strong>{currentReason}</strong>
                    </div>
                )}

                <form className="account-action-dialog__form" onSubmit={handleSubmit}>
                    {isLockMode ? (
                        <label className="account-action-dialog__field" htmlFor={fieldId}>
                            <span>{lockReasonLabel}</span>
                            <textarea
                                id={fieldId}
                                value={reason}
                                onChange={handleReasonChange}
                                placeholder={lockReasonPlaceholder}
                                autoFocus
                                disabled={isSubmitting}
                            />
                        </label>
                    ) : (
                        <label className="account-action-dialog__field" htmlFor={fieldId}>
                            <span>{unlockConfirmLabel}</span>
                            <input
                                id={fieldId}
                                type="text"
                                value={unlockText}
                                onChange={handleUnlockTextChange}
                                placeholder={UNLOCK_CONFIRM_TEXT}
                                autoFocus
                                disabled={isSubmitting}
                            />
                        </label>
                    )}

                    <p className="account-action-dialog__helper">{helperText}</p>

                    {(validationMessage || errorMessage) && (
                        <div className="account-action-dialog__error" role="alert">
                            {validationMessage ?? errorMessage}
                        </div>
                    )}

                    <div className="account-action-dialog__actions">
                        <Button
                            type="button"
                            variant="outline"
                            className="account-action-dialog__button"
                            onClick={onClose}
                            disabled={isSubmitting}
                        >
                            {cancelLabel}
                        </Button>
                        <Button
                            type="submit"
                            variant="primary"
                            className={`account-action-dialog__button ${isLockMode ? 'account-action-dialog__submit--danger' : ''}`.trim()}
                            disabled={submitDisabled}
                        >
                            {submitLabel}
                        </Button>
                    </div>
                </form>
            </div>
        </div>,
        document.body,
    );
}

export default AccountActionDialog;
