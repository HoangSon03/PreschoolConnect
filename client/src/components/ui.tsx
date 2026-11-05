import type { ReactNode } from 'react';

export function PageHeader({ title, subtitle, actions }: { title: string; subtitle: string; actions?: ReactNode }) {
  return <header className="page-heading"><div><p className="date-line">{new Intl.DateTimeFormat('vi-VN', { weekday: 'long', day: '2-digit', month: 'long', year: 'numeric' }).format(new Date())}</p><h1>{title}</h1><p className="muted">{subtitle}</p></div><div className="page-actions">{actions}</div></header>;
}

export function Metric({ label, value, unit, note, tone = 'green' }: { label: string; value: string; unit?: string; note: string; tone?: string }) {
  return <article className="metric card"><div className="metric-label">{label}<span className={`metric-dot ${tone}`} aria-hidden="true" /></div><div className="metric-value">{value} <small>{unit}</small></div><p className="muted">{note}</p></article>;
}

export function EmptyState({ title, children }: { title: string; children?: ReactNode }) {
  return <div className="empty-state"><h2>{title}</h2><p className="muted">{children}</p></div>;
}

export function Message({ error, children }: { error?: boolean; children: ReactNode }) {
  return <p className={`message ${error ? 'error' : 'notice'}`} role={error ? 'alert' : 'status'}>{children}</p>;
}
