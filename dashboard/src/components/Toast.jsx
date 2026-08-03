import { useEffect, useState } from 'react';
import { X } from 'lucide-react';

let toasts = [];
let nextId = 1;
const listeners = new Set();

function emit() {
  listeners.forEach((listener) => listener(toasts));
}

export function dismissToast(id) {
  toasts = toasts.filter((toast) => toast.id !== id);
  emit();
}

export function showToast(message, options = {}) {
  const id = nextId;
  nextId += 1;
  const duration = typeof options.duration === 'number' ? options.duration : 6000;
  toasts = [...toasts, { id, message, type: options.type || 'info' }];
  emit();
  if (duration > 0) {
    window.setTimeout(() => dismissToast(id), duration);
  }
  return id;
}

export function ToastContainer() {
  const [items, setItems] = useState(toasts);

  useEffect(() => {
    const listener = (next) => setItems(next);
    listeners.add(listener);
    setItems(toasts);
    return () => {
      listeners.delete(listener);
    };
  }, []);

  if (items.length === 0) return null;

  return (
    <div className="toast-stack" aria-live="polite" role="status">
      {items.map((toast) => (
        <div className={`toast toast-${toast.type}`} key={toast.id}>
          <span className="toast-message">{toast.message}</span>
          <button
            className="toast-dismiss"
            type="button"
            aria-label="Dismiss"
            onClick={() => dismissToast(toast.id)}
          >
            <X size={14} />
          </button>
        </div>
      ))}
    </div>
  );
}
