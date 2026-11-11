import { createPortal } from 'react-dom';

// A tooltip drawn on the document body, so a card's overflow cannot clip it. It stays inside the window.
export default function ChartTooltip({ tip }) {
  if (!tip) return null;
  const left = Math.max(8, Math.min(tip.x + 12, window.innerWidth - 248));
  const top = Math.max(8, Math.min(tip.y + 12, window.innerHeight - 40 - (tip.lines.length * 18)));
  return createPortal(
    <div className="usage-chart-tooltip" role="tooltip" style={{ left, top }}>
      {tip.title ? <strong>{tip.title}</strong> : null}
      {tip.lines.map((line) => (
        <div className="usage-chart-tooltip-line" key={line.label}>
          {line.color ? <i style={{ background: line.color }} /> : null}
          <span>{line.label}</span>
          <b>{line.value}</b>
        </div>
      ))}
    </div>,
    document.body
  );
}
