"use client";

import { useCallback, useEffect, useState } from "react";
import { getReport } from "@/lib/api";
import type { ManagementReport } from "@/lib/contracts";
import { customPeriod, formatCurrency, formatPercent, presetPeriod } from "@/lib/format";
import type { ReportPeriod } from "@/lib/format";
import { ErrorMessage } from "@/components/Feedback";

type Preset = "7d" | "30d" | "month" | "custom";

export function ReportPanel({ detailed = false }: { detailed?: boolean }) {
  const [period, setPeriod] = useState<ReportPeriod>(() => presetPeriod("30d"));
  const [preset, setPreset] = useState<Preset>("30d");
  const [fromInput, setFromInput] = useState("");
  const [toInput, setToInput] = useState("");
  const [report, setReport] = useState<ManagementReport | null>(null);
  const [error, setError] = useState<{ message: string; traceId: string | null } | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshKey, setRefreshKey] = useState(0);
  const [periodError, setPeriodError] = useState("");

  const refresh = useCallback(() => setRefreshKey((key) => key + 1), []);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError(null);
    getReport(period.fromUtc, period.toUtc, controller.signal)
      .then(setReport)
      .catch((cause: unknown) => {
        if (controller.signal.aborted) return;
        const apiError = cause instanceof Error ? cause : new Error("No se pudo consultar el reporte.");
        setError({
          message: apiError.message,
          traceId: "traceId" in apiError && typeof apiError.traceId === "string" ? apiError.traceId : null,
        });
        setReport(null);
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });

    return () => controller.abort();
  }, [period, refreshKey]);

  function choosePreset(value: Exclude<Preset, "custom">) {
    setPreset(value);
    setPeriod(presetPeriod(value));
    setPeriodError("");
  }

  function applyCustomPeriod(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const nextPeriod = customPeriod(fromInput, toInput);
    if (!nextPeriod) {
      setPeriodError("Indica ambas fechas y asegúrate de que la fecha inicial sea anterior a la final.");
      return;
    }
    setPeriodError("");
    setPreset("custom");
    setPeriod(nextPeriod);
  }

  return (
    <section className="report-section" aria-labelledby="report-data-title">
      <div className="filter-toolbar">
        <div className="segmented-control" role="group" aria-label="Periodo del reporte">
          <button type="button" className={preset === "7d" ? "segment active" : "segment"} aria-pressed={preset === "7d"} onClick={() => choosePreset("7d")}>7 días</button>
          <button type="button" className={preset === "30d" ? "segment active" : "segment"} aria-pressed={preset === "30d"} onClick={() => choosePreset("30d")}>30 días</button>
          <button type="button" className={preset === "month" ? "segment active" : "segment"} aria-pressed={preset === "month"} onClick={() => choosePreset("month")}>Mes actual</button>
        </div>
        {detailed && (
          <form className="custom-period-form" onSubmit={applyCustomPeriod}>
            <label>
              Desde
              <input type="date" value={fromInput} onChange={(event) => setFromInput(event.target.value)} />
            </label>
            <label>
              Hasta
              <input type="date" value={toInput} onChange={(event) => setToInput(event.target.value)} />
            </label>
            <button type="submit" className="button button-secondary">Aplicar periodo</button>
          </form>
        )}
      </div>
      {periodError && <p className="field-error" role="alert">{periodError}</p>}

      <div className="section-heading">
        <div>
          <h2 id="report-data-title">Indicadores disponibles</h2>
          <p>Periodo: {period.label}. Fechas en UTC; el intervalo incluye la fecha final.</p>
        </div>
        <button type="button" className="button button-secondary" onClick={refresh} disabled={loading}>Actualizar</button>
      </div>

      {loading && <ReportLoading />}
      {!loading && error && <ErrorMessage message={error.message} traceId={error.traceId} retry={refresh} />}
      {!loading && !error && report && (
        <>
          <div className="kpi-grid">
            <article className="kpi-card">
              <span className="kpi-label">Facturas procesadas</span>
              <strong className="kpi-value">{report.processedInvoiceCount.toLocaleString("es-MX")}</strong>
              <span className="kpi-note">Extracción completada en el periodo</span>
            </article>
            <article className="kpi-card">
              <span className="kpi-label">Facturas duplicadas</span>
              <strong className="kpi-value">{report.duplicateInvoiceCount.toLocaleString("es-MX")}</strong>
              <span className="kpi-note">Marcadas como duplicadas por el sistema</span>
            </article>
            <article className="kpi-card">
              <span className="kpi-label">Tasa de duplicados</span>
              <strong className="kpi-value">{formatPercent(report.duplicateRate)}</strong>
              <span className="kpi-note">Duplicadas ÷ facturas cargadas en el periodo</span>
            </article>
            <article className="kpi-card">
              <span className="kpi-label">Inventario crítico</span>
              <strong className="kpi-value">{report.criticalInventoryItemCount.toLocaleString("es-MX")}</strong>
              <span className="kpi-note">Estado actual, independiente del periodo</span>
            </article>
          </div>

          <section className="panel totals-panel" aria-labelledby="totals-title">
            <div className="panel-heading">
              <div>
                <h3 id="totals-title">Totales aprobados por moneda</h3>
                <p>No se combinan monedas diferentes.</p>
              </div>
            </div>
            {report.approvedTotalsByCurrency.length === 0 ? (
              <p className="muted">No hay totales aprobados para este periodo.</p>
            ) : (
              <div className="responsive-table">
                <table>
                  <caption className="sr-only">Totales de facturas aprobadas, agrupados por moneda</caption>
                  <thead><tr><th scope="col">Moneda</th><th scope="col">Total aprobado</th></tr></thead>
                  <tbody>
                    {report.approvedTotalsByCurrency.map((total) => (
                      <tr key={total.currencyCode}>
                        <th scope="row">{total.currencyCode}</th>
                        <td>{formatCurrency(total.totalAmount, total.currencyCode)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
          <p className="data-note">Datos consultados en la API de Río Claro. El backend no ofrece series temporales, comparativos de periodo ni exportación CSV; no se muestran valores calculados o simulados en el navegador.</p>
        </>
      )}
    </section>
  );
}

function ReportLoading() {
  return (
    <div className="report-loading" role="status" aria-live="polite" aria-busy="true">
      <span>Consultando indicadores del servidor…</span>
      <div className="kpi-grid" aria-hidden="true">
        {Array.from({ length: 4 }, (_, index) => (
          <div className="kpi-skeleton" key={index}>
            <span />
            <span />
            <span />
          </div>
        ))}
      </div>
    </div>
  );
}
