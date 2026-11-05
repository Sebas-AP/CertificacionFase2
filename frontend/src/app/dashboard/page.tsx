import type { Metadata } from "next";
import { PageHeader } from "@/components/PageHeader";
import { ReportPanel } from "@/components/ReportPanel";

export const metadata: Metadata = { title: "Dashboard" };

export default function DashboardPage() {
  return (
    <>
      <PageHeader
        eyebrow="Resumen gerencial"
        title="Dashboard"
        description="Indicadores disponibles para Río Claro, consultados directamente desde el backend."
        action={{ href: "/facturas/nueva", label: "Subir factura" }}
      />
      <ReportPanel />
    </>
  );
}
