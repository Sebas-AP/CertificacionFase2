"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import {
  IconFileText,
  IconPlus,
  IconFilter,
  IconChevronLeft,
  IconChevronRight,
} from "@tabler/icons-react";
import { AppShell } from "@/components/AppShell";
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

const panel = "rounded-xl border border-[#E6EBF2] bg-white";

export default function InvoicesPage() {
  const [status, setStatus] = useState("");
  const [page, setPage] = useState(1);
  const [data, setData] = useState<InvoicePage | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
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
        const message = cause instanceof Error ? cause.message : "No se pudo consultar el historial de facturas.";
        setError(message);
        setData(null);
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [page, status, refreshKey]);

  return (
    <AppShell crumb="Facturas">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <h1 className="text-[28px] font-semibold leading-[1.45] text-[#18243B]">
          Historial de Facturas
        </h1>
        <Link
          href="/facturas/nueva"
          className="flex h-[42px] items-center gap-2 rounded-lg border border-[#2463EB] bg-[#2463EB] px-4 text-[13px] font-semibold text-white hover:bg-[#1d54c9]"
        >
          <IconPlus size={16} stroke={1.5} />
          Subir factura
        </Link>
      </div>

      <div className={`${panel} flex flex-col overflow-hidden`}>
        {/* Toolbar de Filtros */}
        <div className="flex items-center justify-between border-b border-[#E6EBF2] bg-[#FAFBFD] px-6 py-4">
          <div className="flex items-center gap-3">
            <div className="flex items-center gap-2 rounded-lg border border-[#E6EBF2] bg-white px-3 py-2 text-xs text-[#718096]">
              <IconFilter size={14} stroke={1.5} />
              <span className="font-medium">Filtrar por estado:</span>
              <select
                className="bg-transparent outline-none font-semibold text-[#18243B]"
                value={status}
                onChange={(event) => {
                  setStatus(event.target.value);
                  setPage(1);
                }}
              >
                {statuses.map(([value, label]) => (
                  <option key={value || "all"} value={value}>{label}</option>
                ))}
              </select>
            </div>
          </div>
          <div className="hidden text-xs text-[#718096] sm:block">
            Mostrando {data?.items.length ?? 0} de {data?.totalItems ?? 0} facturas
          </div>
        </div>

        {/* Contenido Principal */}
        <div className="p-0">
          {loading ? (
            <div className="flex h-64 items-center justify-center text-sm text-[#718096]">
              Cargando facturas...
            </div>
          ) : error ? (
            <div className="flex h-64 flex-col items-center justify-center gap-3 text-center">
              <div className="text-sm text-red-500 font-medium">{error}</div>
              <button
                onClick={refresh}
                className="text-xs font-semibold text-[#2463EB] hover:underline"
              >
                Intentar de nuevo
              </button>
            </div>
          ) : data && data.items.length === 0 ? (
            <div className="flex h-64 flex-col items-center justify-center gap-4 text-center">
              <IconFileText size={48} stroke={1} className="text-[#E6EBF2]" />
              <div className="flex flex-col gap-1">
                <div className="text-sm font-semibold text-[#18243B]">No hay facturas disponibles</div>
                <div className="text-xs text-[#718096]">No se encontraron registros con el filtro seleccionado.</div>
              </div>
              <Link
                href="/facturas/nueva"
                className="rounded-lg bg-[#2463EB] px-4 py-2 text-xs font-semibold text-white hover:bg-[#1d54c9]"
              >
                Subir la primera factura
              </Link>
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-left border-collapse">
                <thead>
                  <tr className="bg-[#FAFBFD] text-[11px] font-semibold text-[#718096] uppercase tracking-wider">
                    <th className="px-6 py-4 font-semibold">Proveedor</th>
                    <th className="px-6 py-4 font-semibold">N.º Factura</th>
                    <th className="px-6 py-4 font-semibold">Fecha</th>
                    <th className="px-6 py-4 font-semibold">Total</th>
                    <th className="px-6 py-4 font-semibold">Estado</th>
                    <th className="px-6 py-4 font-semibold text-right">Acciones</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#E6EBF2]">
                  {data?.items.map((invoice) => (
                    <tr key={invoice.id} className="hover:bg-[#F9FAFB] transition-colors">
                      <td className="px-6 py-4 text-xs font-semibold text-[#18243B]">
                        {invoice.supplierName || "Proveedor sin identificar"}
                      </td>
                      <td className="px-6 py-4 text-xs text-[#718096]">
                        {invoice.invoiceNumber || "Sin número"}
                      </td>
                      <td className="px-6 py-4 text-xs text-[#718096]">
                        {formatDate(invoice.issueDate)}
                      </td>
                      <td className="px-6 py-4 text-xs font-medium text-[#18243B]">
                        {formatCurrency(invoice.total, invoice.currencyCode)}
                      </td>
                      <td className="px-6 py-4">
                        <span className={`inline-block rounded-md px-2 py-1 text-[10px] font-bold uppercase tracking-tight ${
                          invoice.status === "approved"
                            ? "bg-[#E8F7F0] text-[#13856A]"
                            : invoice.status === "duplicate"
                            ? "bg-[#FFF5DD] text-[#A86A0B]"
                            : "bg-[#F1F5F9] text-[#64748B]"
                        }`}>
                          {invoiceStatusLabel(invoice.status)}
                        </span>
                      </td>
                      <td className="px-6 py-4 text-right">
                        <Link
                          href={`/facturas/${invoice.id}`}
                          className="text-xs font-semibold text-[#2463EB] hover:text-[#1d54c9] transition-colors"
                        >
                          Ver detalle →
                        </Link>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>

        {/* Paginación */}
        {data && data.items.length > 0 && (
          <div className="flex items-center justify-between border-t border-[#E6EBF2] bg-white px-6 py-4">
            <div className="text-xs text-[#718096]">
              Página <span className="font-semibold text-[#18243B]">{data.page}</span> de {Math.max(data.totalPages, 1)}
            </div>
            <div className="flex gap-2">
              <button
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page <= 1 || loading}
                className="flex h-8 w-8 items-center justify-center rounded-md border border-[#E6EBF2] text-[#718096] disabled:opacity-50 hover:bg-[#F9FAFB]"
              >
                <IconChevronLeft size={16} stroke={1.5} />
              </button>
              <button
                onClick={() => setPage((p) => p + 1)}
                disabled={page >= (data.totalPages || 1) || loading}
                className="flex h-8 w-8 items-center justify-center rounded-md border border-[#E6EBF2] text-[#718096] disabled:opacity-50 hover:bg-[#F9FAFB]"
              >
                <IconChevronRight size={16} stroke={1.5} />
              </button>
            </div>
          </div>
        )}
      </div>
    </AppShell>
  );
}
