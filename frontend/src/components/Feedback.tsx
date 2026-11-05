import Link from "next/link";
import type { ReactNode } from "react";

export function ErrorMessage({
  message,
  traceId,
  retry,
}: {
  message: string;
  traceId?: string | null;
  retry?: () => void;
}) {
  return (
    <div className="alert alert-error" role="alert">
      <div>
        <strong>No se pudo completar la solicitud</strong>
        <p>{message}</p>
        {traceId && <small>Referencia: {traceId}</small>}
      </div>
      {retry && <button className="button button-secondary" type="button" onClick={retry}>Intentar de nuevo</button>}
    </div>
  );
}

export function EmptyState({
  title,
  children,
}: {
  title: string;
  children: ReactNode;
}) {
  return (
    <div className="empty-state">
      <span className="empty-icon" aria-hidden="true">—</span>
      <h2>{title}</h2>
      <div>{children}</div>
    </div>
  );
}

export function ConfigurationMessage({
  children,
  href,
}: {
  children: ReactNode;
  href?: string;
}) {
  return (
    <div className="alert alert-info" role="status">
      <div>
        <strong>No configurado</strong>
        <p>{children}</p>
      </div>
      {href && <Link className="button button-secondary" href={href}>Consultar estado del sistema</Link>}
    </div>
  );
}

export function LoadingState({ label = "Cargando información…" }: { label?: string }) {
  return <p className="loading-state" role="status" aria-live="polite">{label}</p>;
}
