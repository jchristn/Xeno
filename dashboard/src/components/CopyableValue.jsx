import CopyButton from './CopyButton.jsx';

export default function CopyableValue({ label, value }) {
  const text = String(value ?? '');
  return (
    <span className="copyable-value">
      <code>{text}</code>
      <CopyButton label={label} value={text} />
    </span>
  );
}
