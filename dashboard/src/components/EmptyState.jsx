import { Inbox } from 'lucide-react';

export default function EmptyState({ actions, message, title }) {
  return (
    <div className="empty-state-panel">
      <div className="empty-state-icon">
        <Inbox size={22} />
      </div>
      <div>
        <h3>{title}</h3>
        {message ? <p>{message}</p> : null}
      </div>
      {actions ? <div className="empty-state-actions">{actions}</div> : null}
    </div>
  );
}
