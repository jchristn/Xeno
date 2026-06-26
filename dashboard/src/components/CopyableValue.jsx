import CopyButton from './CopyButton.jsx';

export default function CopyableValue({ label, tooltip, value }) {
  const text = String(value ?? '');
  return (
    <span className="copyable-value">
      <code title={tooltip || label}>{text}</code>
      <CopyButton label={label} tooltip={tooltip} value={text} />
    </span>
  );
}
