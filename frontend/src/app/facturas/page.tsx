"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { ErrorMessage, EmptyState, LoadingState } from "@/components/Feedback";
import { PageHeader } from "@/components/PageHeader";
import { getInvoices } from "@/lib/api";
import type { InvoicePage } from "@/lib/contracts";
import { formatCurrency, formatDate, invoiceStatusLabel } from "@/lib/format";

const statuses = [
  ["", "Todos los estados"],
  ["pending_review", "Pendiente de revisión"],
  ["needs_validation", "Requiere validación"],
  ["extracted", "Extraída"],
  ["approved", "Aprobada"],
  ["extraction_failed", "Extracción fallida"],
  ["duplicate", "Duplicada"],
] as const;

export default function InvoicesPage() {
  const [status, setStatus] = useState("");
  const [page, setPage] = useState(1);
  const [data, setData] = useState<InvoicePage | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<{ message: string; traceId: string | null } | null>(null);
  const [refreshKey, setRefreshKey] = useState(0);

  const refresh = useCallback(() => setRefreshKey((key) => key + 1), []);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError(null);
    getInvoices(page, status, controller.signal)
      .then(setData)
      .catch((cause: unknown) => {
        if (controller.signal.aborted) return;
        const apiError = cause instanceof Error ? cause : new Error("No se pudo consultar el historial.");
        setError({
          message: apiError.message,
          traceId: "traceId" in apiError && typeof apiError.traceId === "string" ? apiError.traceId : null,
        });
        setData(null);
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [page, status, refreshKey]);

  return (
    <>
      <PageHeader
        eyebrow="Procesamiento de documentos"
        title="Facturas"
        description="Consulta el historial y abre una factura para revisar la información extraída."
        action={{ href: "/facturas/nueva", label: "Subir factura" }}
      />
      <section className="panel">
        <div className="filter-toolbar">
          <label className="field-label filter-select">
            Estado
            <select
              value={status}
              onChange={(event) => {
                setStatus(event.target.value);
                setPage(1);
              }}
            >
              {statuses.map(([value, label]) => <option key={value || "all"} value={value}>{label}</option>)}
            </select>
          </label>
          <p className="muted filter-help">La API permite filtrar por estado y paginar. La búsqueda, proveedor y rango de fechas no están disponibles en el contrato actual.</p>
        </div>

        {loading && <LoadingState label="Consultando facturas…" />}
        {!loading && error && <ErrorMessage message={error.message} traceId={error.traceId} retry={refresh} />}
        {!loading && !error && data && data.items.length === 0 && (
          <EmptyState title="No hay facturas para mostrar">
            <p>No se encontraron registros para el estado seleccionado. Las facturas aparecen aquí cuando se guardan en la API.</p>
            <Link className="button button-primary" href="/facturas/nueva">Subir una factura</Link>
          </EmptyState>
        )}
        {!loading && !error && data && data.items.length > 0 && (
          <>
            <p className="result-count">{data.totalItems} {data.totalItems === 1 ? "factura" : "facturas"} · Página {data.page} de {Math.max(data.totalPages, 1)}</p>
            <div className="responsive-table invoice-desktop-table">
              <table>
                <caption className="sr-only">Facturas registradas en Río Claro</caption>
                <thead>
                  <tr>
                    <th scope="col">Proveedor</th>
                    <th scope="col">N.º de factura</th>
                    <th scope="col">Fecha</th>
                    <th scope="col">Total</th>
                    <th scope="col">Estado</th>
                    <th scope="col">Acciones</th>
                  </tr>
                </thead>
                <tbody>
                  {data.items.map((invoice) => (
                    <tr key={invoice.id}>
                      <td>{invoice.supplierName || "Proveedor sin identificar"}</td>
                      <td>{invoice.invoiceNumber || "Sin número"}</td>
                      <td>{formatDate(invoice.issueDate)}</td>
                      <td>{formatCurrency(invoice.total, invoice.currencyCode)}</td>
                      <td><span className={`status-badge status-${invoice.status}`}>{invoiceStatusLabel(invoice.status)}</span></td>
                      <td><Link className="text-link" href={`/facturas/${invoice.id}`}>Ver detalle</Link></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <ul className="invoice-mobile-list" aria-label="Facturas">
              {data.items.map((invoice) => (
                <li className="invoice-mobile-card" key={invoice.id}>
                  <div className="invoice-card-heading">
                    <strong>{invoice.supplierName || "Proveedor sin identificar"}</strong>
                    <span className={`status-badge status-${invoice.status}`}>{invoiceStatusLabel(invoice.status)}</span>
                  </div>
                  <dl className="compact-details">
                    <div><dt>N.º de factura</dt><dd>{invoice.invoiceNumber || "Sin número"}</dd></div>
                    <div><dt>Fecha</dt><dd>{formatDate(invoice.issueDate)}</dd></div>
                    <div><dt>Total</dt><dd>{formatCurrency(invoice.total, invoice.currencyCode)}</dd></div>
                  </dl>
                  <Link className="button button-secondary full-width" href={`/facturas/${invoice.id}`}>Ver detalle</Link>
                </li>
              ))}
            </ul>
            <div className="pagination">
              <button className="button button-secondary" type="button" onClick={() => setPage((value) => Math.max(1, value - 1))} disabled={page <= 1 || loading}>Anterior</button>
              <span aria-live="polite">Página {data.page} de {Math.max(data.totalPages, 1)}</span>
              <button className="button button-secondary" type="button" onClick={() => setPage((value) => value + 1)} disabled={page >= data.totalPages || loading}>Siguiente</button>
            </div>
          </>
        )}
      </section>
    </>
  );
}
