export const invoiceStatusLabels: Record<string, string> = {
  pending_review: "Pendiente de revisión",
  approved: "Aprobada",
  extraction_failed: "Extracción fallida",
  extracted: "Extraída",
  needs_validation: "Requiere validación",
  duplicate: "Duplicada",
};

export function invoiceStatusLabel(status: string): string {
  return invoiceStatusLabels[status] ?? "Estado no reconocido";
}

export function duplicateMatchLabel(matchType: string): string {
  switch (matchType.replace(/[^a-z]/gi, "").toLowerCase()) {
    case "exactfileduplicate":
      return "Este mismo archivo ya había sido cargado.";
    case "exactbusinessduplicate":
      return "Existe otra factura con el mismo proveedor y número de factura.";
    case "probableduplicate":
      return "Existe una factura con datos contables similares.";
    default:
      return "Coincidencia registrada por el backend.";
  }
}

export function formatDate(value: string | null | undefined): string {
  if (!value) return "Sin dato";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "Fecha no válida";
  return new Intl.DateTimeFormat("es-MX", {
    dateStyle: "medium",
    timeZone: "UTC",
  }).format(date);
}

export function formatDateTime(value: string | null | undefined): string {
  if (!value) return "Sin dato";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "Fecha no válida";
  return new Intl.DateTimeFormat("es-MX", {
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    timeZone: "UTC",
    timeZoneName: "short",
  }).format(date);
}

export function formatCurrency(amount: number | null, currency: string | null): string {
  if (amount === null || !currency) return "Sin dato";
  try {
    return new Intl.NumberFormat("es-MX", {
      style: "currency",
      currency,
      currencyDisplay: "code",
    }).format(amount);
  } catch {
    return `${amount.toLocaleString("es-MX")} ${currency}`;
  }
}

export function formatPercent(rate: number): string {
  return new Intl.NumberFormat("es-MX", {
    style: "percent",
    maximumFractionDigits: 1,
  }).format(rate);
}

export interface ReportPeriod {
  fromUtc: string;
  toUtc: string;
  fromDate: string;
  toDateInclusive: string;
  label: string;
}

function dateKey(date: Date): string {
  return date.toISOString().slice(0, 10);
}

export function presetPeriod(preset: "7d" | "30d" | "month"): ReportPeriod {
  const today = new Date();
  const toExclusive = new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), today.getUTCDate() + 1));
  const from = new Date(toExclusive);

  if (preset === "7d") from.setUTCDate(from.getUTCDate() - 7);
  else if (preset === "30d") from.setUTCDate(from.getUTCDate() - 30);
  else {
    from.setTime(Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), 1));
  }

  const fromDate = dateKey(from);
  const toDateInclusive = dateKey(new Date(toExclusive.getTime() - 86_400_000));
  return {
    fromUtc: `${fromDate}T00:00:00.000Z`,
    toUtc: toExclusive.toISOString(),
    fromDate,
    toDateInclusive,
    label: preset === "month" ? "Mes actual (UTC)" : `Últimos ${preset === "7d" ? "7" : "30"} días (UTC)`,
  };
}

export function customPeriod(fromDate: string, toDateInclusive: string): ReportPeriod | null {
  if (!fromDate || !toDateInclusive || fromDate >= toDateInclusive) return null;
  const from = new Date(`${fromDate}T00:00:00.000Z`);
  const toExclusive = new Date(`${toDateInclusive}T00:00:00.000Z`);
  toExclusive.setUTCDate(toExclusive.getUTCDate() + 1);
  if (Number.isNaN(from.getTime()) || Number.isNaN(toExclusive.getTime())) return null;
  return {
    fromUtc: from.toISOString(),
    toUtc: toExclusive.toISOString(),
    fromDate,
    toDateInclusive,
    label: `${fromDate} a ${toDateInclusive} (UTC)`,
  };
}
