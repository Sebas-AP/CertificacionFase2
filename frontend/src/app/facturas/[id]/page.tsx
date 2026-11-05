"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";
import { ApiError, approveInvoice, explainInvoice, getInvoice, retryInvoiceExtraction, reviewInvoice } from "@/lib/api";
import type { Invoice, InvoiceExplanation, InvoiceReviewRequest } from "@/lib/contracts";
import { ErrorMessage, LoadingState } from "@/components/Feedback";
import { PageHeader } from "@/components/PageHeader";
import { duplicateMatchLabel, formatCurrency, formatDate, formatDateTime, invoiceStatusLabel } from "@/lib/format";

interface ReviewFields {
  invoiceNumber: string;
  issueDate: string;
  currencyCode: string;
  subtotal: string;
  taxAmount: string;
  total: string;
  reason: string;
}

function fieldsFromInvoice(invoice: Invoice): ReviewFields {
  return {
    invoiceNumber: invoice.invoiceNumber ?? "",
    issueDate: invoice.issueDate ?? "",
    currencyCode: invoice.currencyCode ?? "",
    subtotal: invoice.subtotal?.toString() ?? "",
    taxAmount: invoice.taxAmount?.toString() ?? "",
    total: invoice.total?.toString() ?? "",
    reason: "",
  };
}

function toNullableAmount(value: string): number | null {
  if (!value.trim()) return null;
  const amount = Number(value);
  if (!Number.isFinite(amount) || amount < 0 || Math.abs(Math.round(amount * 10_000) - amount * 10_000) > 1e-8) {
    throw new Error("Los importes deben ser números no negativos con hasta cuatro decimales.");
  }
  return amount;
}

export default function InvoiceDetailPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const [invoice, setInvoice] = useState<Invoice | null>(null);
  const [fields, setFields] = useState<ReviewFields | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [editing, setEditing] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);
  const [formError, setFormError] = useState("");
  const [notice, setNotice] = useState("");
  const [explanation, setExplanation] = useState<InvoiceExplanation | null>(null);

  async function loadInvoice(signal?: AbortSignal) {
    setLoading(true);
    setError(null);
    try {
      const next = await getInvoice(id, signal);
      setInvoice(next);
      setFields(fieldsFromInvoice(next));
    } catch (cause) {
      if (signal?.aborted) return;
      setError(cause instanceof ApiError ? cause : new ApiError("No se pudo consultar la factura.", 0));
    } finally {
      if (!signal?.aborted) setLoading(false);
    }
  }

  useEffect(() => {
    const controller = new AbortController();
    void loadInvoice(controller.signal);
    return () => controller.abort();
  }, [id]);

  const canEdit = invoice && ["pending_review", "extracted", "needs_validation"].includes(invoice.status);
  const canApprove = invoice?.status === "pending_review" &&
    Boolean(invoice.invoiceNumber && invoice.issueDate && invoice.currencyCode && invoice.total !== null);
  const extractionFailed = invoice?.status === "extraction_failed";

  async function saveReview(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!fields || busy) return;
    setBusy(true);
    setFormError("");
    setNotice("");
    try {
      const review: InvoiceReviewRequest = {
        invoiceNumber: fields.invoiceNumber.trim() || null,
        issueDate: fields.issueDate || null,
        currencyCode: fields.currencyCode.trim().toUpperCase() || null,
        subtotal: toNullableAmount(fields.subtotal),
        taxAmount: toNullableAmount(fields.taxAmount),
        total: toNullableAmount(fields.total),
        reason: fields.reason.trim() || null,
      };
      if (review.currencyCode && !/^[A-Z]{3}$/.test(review.currencyCode)) {
        throw new Error("La moneda debe usar un código de tres letras.");
      }
      const updated = await reviewInvoice(id, review);
      setInvoice(updated);
      setFields(fieldsFromInvoice(updated));
      setEditing(false);
      setNotice("Los campos de revisión se guardaron en el backend.");
    } catch (cause) {
      setFormError(cause instanceof Error ? cause.message : "No se pudo guardar la revisión.");
    } finally {
      setBusy(false);
    }
  }

  async function approve() {
    if (!canApprove || busy) return;
    if (!window.confirm("¿Confirmas la aprobación de esta factura?")) return;
    setBusy(true);
    setFormError("");
    setNotice("");
    try {
      const updated = await approveInvoice(id);
      setInvoice(updated);
      setFields(fieldsFromInvoice(updated));
      setNotice("La factura fue aprobada por el backend.");
    } catch (cause) {
      setFormError(cause instanceof Error ? cause.message : "No se pudo aprobar la factura.");
    } finally {
      setBusy(false);
    }
  }

  async function retryExtraction() {
    if (!extractionFailed || busy) return;
    if (!window.confirm("¿Solicitas un nuevo intento de extracción para esta factura?")) return;
    setBusy(true);
    setFormError("");
    setNotice("");
    try {
      const result = await retryInvoiceExtraction(id);
      setInvoice(result.invoice);
      setFields(fieldsFromInvoice(result.invoice));
      setNotice(result.extractionSucceeded
        ? "El backend completó el nuevo intento de extracción."
        : "El backend conservó la factura, pero la extracción todavía no se completó.");
    } catch (cause) {
      setFormError(cause instanceof Error ? cause.message : "No se pudo solicitar la extracción.");
      await loadInvoice();
    } finally {
      setBusy(false);
    }
  }

  async function requestExplanation() {
    setBusy(true);
    setFormError("");
    setExplanation(null);
    try {
      setExplanation(await explainInvoice(id));
    } catch (cause) {
      setFormError(cause instanceof Error ? cause.message : "No se pudo solicitar una explicación.");
    } finally {
      setBusy(false);
    }
  }

  if (loading) return <LoadingState label="Consultando detalle de la factura…" />;
  if (error || !invoice || !fields) {
    return (
      <>
        <PageHeader eyebrow="Facturas" title="Detalle de factura" description="No fue posible cargar el registro." />
        <ErrorMessage message={error?.message ?? "No se encontró la factura."} traceId={error?.traceId} retry={() => void loadInvoice()} />
        <Link className="text-link back-link-row" href="/facturas">Volver a facturas</Link>
      </>
    );
  }

  return (
    <>
      <PageHeader
        eyebrow="Facturas"
        title={invoice.invoiceNumber ? `Factura ${invoice.invoiceNumber}` : "Detalle de factura"}
        description={`${invoice.supplierName || "Proveedor sin identificar"} · Cargada ${formatDateTime(invoice.uploadedAtUtc)}`}
      />
      <div className="back-link-row"><Link className="text-link" href="/facturas">← Volver a facturas</Link></div>

      <section className="invoice-detail-header panel">
        <div>
          <span className={`status-badge status-${invoice.status}`}>{invoiceStatusLabel(invoice.status)}</span>
          <p className="detail-subtitle">ID de factura: <code>{invoice.id}</code></p>
        </div>
        <div className="detail-actions">
          {canEdit && !editing && <button className="button button-secondary" type="button" onClick={() => { setEditing(true); setFormError(""); }}>Revisar datos</button>}
          {canApprove && <button className="button button-primary" type="button" onClick={() => void approve()} disabled={busy}>Aprobar factura</button>}
          {extractionFailed && <button className="button button-secondary" type="button" onClick={() => void retryExtraction()} disabled={busy}>{busy ? "Procesando…" : "Reintentar extracción"}</button>}
          <button className="button button-secondary" type="button" onClick={() => void requestExplanation()} disabled={busy}>{busy ? "Consultando…" : "Solicitar explicación con IA"}</button>
        </div>
      </section>

      {notice && <p className="alert alert-success" role="status">{notice}</p>}
      {formError && <ErrorMessage message={formError} />}

      <div className="detail-grid">
        <section className="panel document-panel">
          <div className="panel-heading">
            <div><h2>Documento original</h2><p>Vista previa protegida</p></div>
          </div>
          <div className="document-unavailable">
            <span className="document-icon" aria-hidden="true">PDF</span>
            <h3>Vista previa no disponible</h3>
            <p>El backend no expone un endpoint seguro de descarga o visualización. El archivo no se solicita directamente a Azure Storage.</p>
          </div>
        </section>

        <section className="panel extracted-panel">
          <div className="panel-heading">
            <div><h2>Datos de la factura</h2><p>Valores registrados por el servidor</p></div>
            {invoice.extractionStatus && <span className="subtle-pill">Extracción: {invoice.extractionStatus}</span>}
          </div>

          {editing ? (
            <form className="review-form" onSubmit={saveReview}>
              <div className="form-grid">
                <label>Número de factura<input maxLength={100} value={fields.invoiceNumber} onChange={(event) => setFields({ ...fields, invoiceNumber: event.target.value })} /></label>
                <label>Fecha de emisión<input type="date" value={fields.issueDate} onChange={(event) => setFields({ ...fields, issueDate: event.target.value })} /></label>
                <label>Moneda<input maxLength={3} minLength={3} value={fields.currencyCode} onChange={(event) => setFields({ ...fields, currencyCode: event.target.value.toUpperCase() })} placeholder="MXN" /></label>
                <label>Subtotal<input type="number" min="0" step="0.0001" value={fields.subtotal} onChange={(event) => setFields({ ...fields, subtotal: event.target.value })} /></label>
                <label>Impuestos<input type="number" min="0" step="0.0001" value={fields.taxAmount} onChange={(event) => setFields({ ...fields, taxAmount: event.target.value })} /></label>
                <label>Total<input type="number" min="0" step="0.0001" value={fields.total} onChange={(event) => setFields({ ...fields, total: event.target.value })} /></label>
              </div>
              <label>Motivo de revisión (opcional)<textarea maxLength={500} rows={3} value={fields.reason} onChange={(event) => setFields({ ...fields, reason: event.target.value })} /></label>
              <div className="form-actions">
                <button className="button button-secondary" type="button" disabled={busy} onClick={() => {
                  if (window.confirm("¿Descartar los cambios de revisión?")) {
                    setFields(fieldsFromInvoice(invoice));
                    setEditing(false);
                  }
                }}>Descartar cambios</button>
                <button className="button button-primary" type="submit" disabled={busy}>{busy ? "Guardando…" : "Guardar revisión"}</button>
              </div>
            </form>
          ) : (
            <dl className="detail-list">
              <div><dt>Proveedor</dt><dd>{invoice.supplierName || "Sin dato"}</dd></div>
              <div><dt>Número de factura</dt><dd>{invoice.invoiceNumber || "Sin dato"}</dd></div>
              <div><dt>Fecha de emisión</dt><dd>{formatDate(invoice.issueDate)}</dd></div>
              <div><dt>Fecha de vencimiento</dt><dd>{formatDate(invoice.dueDate)}</dd></div>
              <div><dt>Moneda</dt><dd>{invoice.currencyCode || "Sin dato"}</dd></div>
              <div><dt>Subtotal</dt><dd>{formatCurrency(invoice.subtotal, invoice.currencyCode)}</dd></div>
              <div><dt>Impuestos</dt><dd>{formatCurrency(invoice.taxAmount, invoice.currencyCode)}</dd></div>
              <div><dt>Total</dt><dd><strong>{formatCurrency(invoice.total, invoice.currencyCode)}</strong></dd></div>
              <div><dt>Orden de compra</dt><dd>{invoice.purchaseOrderNumber || "Sin dato"}</dd></div>
              <div><dt>Intentos de extracción</dt><dd>{invoice.extractionAttemptCount}</dd></div>
            </dl>
          )}
        </section>
      </div>

      {invoice.validationWarnings && invoice.validationWarnings.length > 0 && (
        <section className="panel" aria-labelledby="warnings-title">
          <h2 id="warnings-title">Advertencias de validación</h2>
          <ul className="warning-list">{invoice.validationWarnings.map((warning, index) => <li key={`${index}-${warning}`}>{warning}</li>)}</ul>
        </section>
      )}

      {invoice.fieldConfidence && Object.entries(invoice.fieldConfidence).length > 0 && (
        <section className="panel" aria-labelledby="confidence-title">
          <h2 id="confidence-title">Confianza de extracción</h2>
          <p className="muted">Los valores inferiores a 80% se marcan para revisión como referencia visual; no confirman un error ni sustituyen las advertencias del servidor.</p>
          <ul className="confidence-list">
            {Object.entries(invoice.fieldConfidence).map(([field, confidence]) => (
              <li key={field}>
                <span>{field}</span>
                {confidence === null ? <span>Sin medición</span> : <span>{new Intl.NumberFormat("es-MX", { style: "percent", maximumFractionDigits: 0 }).format(confidence)}{confidence < 0.8 && <strong className="review-required">Requiere revisión</strong>}</span>}
              </li>
            ))}
          </ul>
        </section>
      )}

      {invoice.duplicateOfInvoiceId && (
        <section className="alert alert-warning" role="note">
          <div>
            <strong>Factura marcada como duplicada</strong>
            <p>El backend vinculó este registro con otra factura. Esto requiere revisión humana y no constituye una acusación de fraude.</p>
            <Link className="text-link" href={`/facturas/${invoice.duplicateOfInvoiceId}`}>Consultar la factura relacionada</Link>
          </div>
        </section>
      )}

      {explanation && (
        <section className="panel" aria-labelledby="explanation-title">
          <h2 id="explanation-title">Explicación del agente de facturas</h2>
          <p className="preserve-lines">{explanation.explanation}</p>
          {explanation.warnings.length > 0 && <ul className="warning-list">{explanation.warnings.map((warning, index) => <li key={`${index}-${warning}`}>{warning}</li>)}</ul>}
          {explanation.duplicateMatches.length > 0 && (
            <div className="responsive-table">
              <table>
                <caption>Coincidencias proporcionadas por el agente</caption>
                <thead><tr><th scope="col">Factura candidata</th><th scope="col">Coincidencia</th><th scope="col">Reglas</th><th scope="col">Total</th></tr></thead>
                <tbody>{explanation.duplicateMatches.map((match) => (
                  <tr key={match.invoiceId}>
                    <td><Link className="text-link" href={`/facturas/${match.invoiceId}`}>{match.invoiceNumber || match.invoiceId}</Link></td>
                    <td>{duplicateMatchLabel(match.matchType)}{match.score !== null && ` Puntuación: ${match.score}`}</td>
                    <td>{match.matchedRules.join(", ") || "Sin reglas listadas"}</td>
                    <td>{formatCurrency(match.total, match.currencyCode)}</td>
                  </tr>
                ))}</tbody>
              </table>
            </div>
          )}
        </section>
      )}

      {invoice.lines.length > 0 && (
        <section className="panel" aria-labelledby="lines-title">
          <h2 id="lines-title">Conceptos</h2>
          <div className="responsive-table">
            <table>
              <caption>Conceptos extraídos de la factura</caption>
              <thead><tr><th scope="col">Descripción</th><th scope="col">Cantidad</th><th scope="col">Precio unitario</th><th scope="col">Importe</th></tr></thead>
              <tbody>{invoice.lines.map((line) => (
                <tr key={line.id}><td>{line.description}</td><td>{line.quantity}</td><td>{formatCurrency(line.unitPrice, invoice.currencyCode)}</td><td>{formatCurrency(line.lineTotal, invoice.currencyCode)}</td></tr>
              ))}</tbody>
            </table>
          </div>
        </section>
      )}

      <section className="panel" aria-labelledby="history-title">
        <h2 id="history-title">Historial de revisión</h2>
        {invoice.reviewHistory.length === 0 ? <p className="muted">La API no reporta acciones de revisión para esta factura.</p> : (
          <ol className="timeline">
            {invoice.reviewHistory.map((entry, index) => (
              <li key={`${entry.occurredAtUtc}-${index}`}>
                <span className="timeline-dot" aria-hidden="true" />
                <div><strong>{entry.action}</strong><time dateTime={entry.occurredAtUtc}>{formatDateTime(entry.occurredAtUtc)}</time></div>
              </li>
            ))}
          </ol>
        )}
      </section>

      <p className="data-note">Las decisiones de revisión de duplicados no están disponibles en el contrato REST. La interfaz no ofrece acciones que el backend no admita.</p>
    </>
  );
}
