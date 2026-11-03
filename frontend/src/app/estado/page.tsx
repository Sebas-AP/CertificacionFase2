"use client";

import { useCallback, useEffect, useState } from "react";
import { ErrorMessage, LoadingState } from "@/components/Feedback";
import { PageHeader } from "@/components/PageHeader";
import { getAgentsHealth, getApiHealth } from "@/lib/api";
import type { AgentsHealth } from "@/lib/contracts";
import { formatDateTime } from "@/lib/format";

type CheckStatus = "loading" | "healthy" | "unhealthy";
interface SystemState {
  api: CheckStatus;
  agents: AgentsHealth | null;
  apiError: string | null;
  agentsError: string | null;
}

function statusLabel(status: string): string {
  switch (status.toLowerCase()) {
    case "healthy": return "Disponible";
    case "degraded": return "Degradado";
    case "notconfigured": return "No configurado";
    case "unhealthy": return "No disponible";
    default: return "Estado no reconocido";
  }
}

function statusClass(status: string): string {
  switch (status.toLowerCase()) {
    case "healthy": return "status-healthy";
    case "degraded": return "status-degraded";
    case "notconfigured": return "status-not-configured";
    default: return "status-unhealthy";
  }
}

export default function SystemStatusPage() {
  const [state, setState] = useState<SystemState>({
    api: "loading",
    agents: null,
    apiError: null,
    agentsError: null,
  });
  const [refreshKey, setRefreshKey] = useState(0);
  const refresh = useCallback(() => setRefreshKey((key) => key + 1), []);

  useEffect(() => {
    const controller = new AbortController();
    setState({ api: "loading", agents: null, apiError: null, agentsError: null });
    void Promise.allSettled([
      getApiHealth(controller.signal),
      getAgentsHealth(controller.signal),
    ]).then(([apiResult, agentResult]) => {
      if (controller.signal.aborted) return;
      setState({
        api: apiResult.status === "fulfilled" && apiResult.value.status === "ok" ? "healthy" : "unhealthy",
        agents: agentResult.status === "fulfilled" ? agentResult.value : null,
        apiError: apiResult.status === "rejected" && apiResult.reason instanceof Error ? apiResult.reason.message : null,
        agentsError: agentResult.status === "rejected" && agentResult.reason instanceof Error ? agentResult.reason.message : null,
      });
    });
    return () => controller.abort();
  }, [refreshKey]);

  const isLoading = state.api === "loading" && state.agents === null && !state.apiError && !state.agentsError;

  return (
    <>
      <PageHeader
        eyebrow="Disponibilidad"
        title="Estado del sistema"
        description="Consulta el estado básico de la API y la configuración de los agentes. No se muestran endpoints internos ni credenciales."
      />
      <div className="section-heading">
        <p className="muted">Verificación en vivo · no consulta Azure Storage ni Document Intelligence desde el navegador.</p>
        <button className="button button-secondary" type="button" onClick={refresh} disabled={isLoading}>Actualizar estado</button>
      </div>

      {isLoading && <LoadingState label="Consultando servicios…" />}
      {state.apiError && <ErrorMessage message={state.apiError} />}
      {state.agentsError && <ErrorMessage message={state.agentsError} />}
      {!isLoading && !state.apiError && !state.agentsError && (
        <>
          <section className="panel" aria-labelledby="system-status-title">
            <h2 id="system-status-title">Servicios</h2>
            <div className="system-status-list">
              <StatusRow name="API Río Claro" status={state.api === "healthy" ? "Healthy" : "Unhealthy"} />
              {state.agents && (
                <>
                  <StatusRow name="Agente empresarial" status={state.agents.businessAgent.status} />
                  <StatusRow name="Agente de facturas" status={state.agents.invoiceAgent.status} />
                </>
              )}
            </div>
            {state.agents && <p className="muted status-checked">Verificado: {formatDateTime(state.agents.checkedAtUtc)}</p>}
          </section>
          {state.agents && (
            <section className="panel">
              <h2>Configuración de IA</h2>
              {state.agents.businessAgent.status.toLowerCase() === "notconfigured" ||
              state.agents.invoiceAgent.status.toLowerCase() === "notconfigured" ? (
                <p className="alert alert-info" role="status">Los agentes Foundry no están configurados o desplegados para este entorno.</p>
              ) : (
                <p className="muted">La API informa disponibilidad de los agentes. Esta pantalla no muestra nombres de versiones ni detalles operativos.</p>
              )}
            </section>
          )}
        </>
      )}
    </>
  );
}

function StatusRow({ name, status }: { name: string; status: string }) {
  return (
    <div className="system-status-row">
      <span>{name}</span>
      <span className={`status-badge ${statusClass(status)}`}>
        <span className="status-dot" aria-hidden="true" />
        {statusLabel(status)}
      </span>
    </div>
  );
}
