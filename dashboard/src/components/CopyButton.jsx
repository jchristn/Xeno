import { useEffect, useState } from 'react';
import { Check, Copy } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { copyToClipboard } from '../utils/clipboard.js';

export default function CopyButton({ className = '', label, value }) {
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    if (!copied) return undefined;
    const timer = window.setTimeout(() => setCopied(false), 1400);
    return () => window.clearTimeout(timer);
  }, [copied]);

  async function handleCopy(event) {
    event.stopPropagation();
    const success = await copyToClipboard(value);
    if (success) setCopied(true);
  }

  const title = copied ? t('copied') : (label || t('copy'));

  return (
    <button
      aria-label={title}
      className={`copy-button ${copied ? 'copied' : ''} ${className}`.trim()}
      onClick={handleCopy}
      title={title}
      type="button"
    >
      {copied ? <Check size={14} /> : <Copy size={14} />}
    </button>
  );
}
