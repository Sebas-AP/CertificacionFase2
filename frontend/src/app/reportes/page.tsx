"use client";

import { useEffect, useState } from "react";
import {
  IconChartBar,
  IconAlertCircle,
  IconFileText,
  IconRefresh,
} from "@tabler/icons-react";
import { AppShell } from "@/components/AppShell";
import { getReport } from "@/lib/api";
import type { ManagementReport } from "@/lib/contracts";

const panel = "rounded-xl border border-[#E6EBF2] bg-white";

export default function ReportsPage() {
  const [loading, setLoading] = useState(true);
  const [report, setReport] = useState<ManagementReport | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function loadReport() {
      try {
        setLoading(true);
        const now = new Date();
        const firstDayMonth = new Date(now.getFullYear(), now.getMonth(), 1).toISOString();
        const data = await getReport(firstDayMonth, now.toISOString());
        setReport(data);
      } catch (err: unknown) {
        const message = err instanceof Error ? err.message : "No se pudo cargar el reporte gerencial.";
        setError(message);
      } finally {
        setLoading(false);
      }
    }
    void loadReport();
  }, []);

  const handleRefresh = async () => {
    setLoading(true);
    setError(null);
    try {
      const now = new Date();
      const firstDayMonth = new Date(now.getFullYear(), now.getMonth(), 1).toISOString();
      const data = await getReport(firstDayMonth, now.toISOString());
      setReport(data);
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : "Error al actualizar el reporte.";
      setError(message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <AppShell crumb="Reportes">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <h1 className="text-[28px] font-semibold leading-[1.45] text-[#18243B]">
          Reportes Gerenciales
        </h1>
        <button
          onClick={handleRefresh}
          disabled={loading}
          className="flex h-[42px] items-center gap-2 rounded-lg border border-[#E6EBF2] bg-white px-4 text-[13px] font-semibold text-[#18243B] hover:bg-[#F9FAFB] disabled:opacity-50"
        >
          <IconRefresh size={16} stroke={1.5} className={loading ? "animate-spin" : ""} />
          Actualizar datos
        </button>
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        {/* Métricas Principales */}
        <div className={`${panel} p-6 flex flex-col gap-4`}>
          <div className="flex items-center gap-2 text-xs font-semibold text-[#718096] uppercase tracking-wider">
            <IconChartBar size={14} />
            Volumen de Operaciones
          </div>
          <div className="flex flex-col gap-6 mt-2">
            <div className="flex flex-col gap-1">
              <span className="text-xs text-[#718096]">Facturas Procesadas</span>
              <span className="text-3xl font-bold text-[#18243B]">{report?.processedInvoiceCount ?? 0}</span>
            </div>
            <div className="flex flex-col gap-1">
              <span className="text-xs text-[#718096]">Duplicados Detectados</span>
              <span className="text-3xl font-bold text-[#CF4F57]">{report?.duplicateInvoiceCount ?? 0}</span>
            </div>
            <div className="flex flex-col gap-1">
              <span className="text-xs text-[#718096]">Tasa de Duplicidad</span>
              <span className="text-3xl font-bold text-[#18243B]">{report?.duplicateRate.toFixed(2) ?? "0.00"}%</span>
            </div>
          </div>
        </div>

        {/* Análisis Financiero */}
        <div className={`${panel} p-6 flex flex-col gap-4`}>
          <div className="flex items-center gap-2 text-xs font-semibold text-[#718096] uppercase tracking-wider">
            <IconWallet size={14} />
            Resumen Financiero
          </div>
          <div className="flex flex-col gap-6 mt-2">
            {report?.approvedTotalsByCurrency && report.approvedTotalsByCurrency.length > 0 ? (
              report.approvedTotalsByCurrency.map((item, idx) => (
                <div key={idx} className="flex flex-col gap-1">
                  <span className="text-xs text-[#718096]">Total Aprobado ({item.currencyCode})</span>
                  <span className="text-3xl font-bold text-[#2463EB]">{item.totalAmount.toLocaleString()} {item.currencyCode}</span>
                </div>
              ))
            ) : (
              <div className="text-sm text-[#718096] italic">No hay datos financieros disponibles.</div>
            )}
          </div>
        </div>

        {/* Alertas de Inventario */}
        <div className={`${panel} p-6 flex flex-col gap-4`}>
          <div className="flex items-center gap-2 text-xs font-semibold text-[#718096] uppercase tracking-wider">
            <IconAlertCircle size={14} />
            Alertas de Suministro
          </div>
          <div className="flex flex-col gap-6 mt-2">
            <div className="flex flex-col gap-1">
              <span className="text-xs text-[#718096]">Items con Stock Crítico</span>
              <span className="text-3xl font-bold text-[#A86A0B]">{report?.criticalInventoryItemCount ?? 0}</span>
            </div>
            <div className="p-3 rounded-lg bg-[#FFFBF0] border border-[#FDE68A] text-[11px] text-[#854D0E]">
              Sugerencia: Revisa el inventario para evitar roturas de stock en los productos críticos.
            </div>
          </div>
        </div>
      </div>

      {/* Nota de Exportación */}
      <div className={`${panel} p-4 bg-[#F0F7FF] border-[#BBDDFF] flex items-center gap-3`}>
        <IconFileText size={18} className="text-[#2463EB]" />
        <div className="text-xs text-[#2463EB]">
          <strong className="font-semibold">Información de Exportación:</strong> El reporte se genera en tiempo real desde la API. La exportación a CSV no está disponible en la versión actual del contrato.
        </div>
      </div>

      {loading && (
        <div className="fixed inset-0 bg-white/50 backdrop-blur-sm flex items-center justify-center z-50">
          <div className="text-sm font-semibold text-[#18243B]">Actualizando reportes...</div>
        </div>
      )}

      {error && (
        <div className="fixed bottom-4 right-4 p-4 rounded-lg bg-red-50 border border-red-200 text-red-600 text-xs shadow-lg z-50">
          {error}
        </div>
      )}
    </AppShell>
  );
}

type IconProps = { size?: number; stroke?: number; className?: string };

function IconWallet({ size = 18, className = "", ...props }: IconProps) {
  return <IconChartBar size={size} className={className} {...props} />;
}
