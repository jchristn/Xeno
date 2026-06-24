import { AlertTriangle } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import Modal from './Modal.jsx';

export default function ConfirmModal({
  confirmLabel,
  destructive = false,
  message,
  onCancel,
  onConfirm,
  open,
  title
}) {
  const { t } = useTranslation();

  return (
    <Modal
      actions={(
        <>
          <button className="action-button secondary" onClick={onCancel} type="button">{t('cancel')}</button>
          <button className={`action-button ${destructive ? 'danger' : 'primary'}`} onClick={onConfirm} type="button">
            {confirmLabel || t('confirm')}
          </button>
        </>
      )}
      onClose={onCancel}
      open={open}
      size="small"
      title={title}
    >
      <div className={`confirm-modal-body ${destructive ? 'destructive' : ''}`}>
        <AlertTriangle size={24} />
        <p>{message}</p>
      </div>
    </Modal>
  );
}
