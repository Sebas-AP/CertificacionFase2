import type { Metadata } from "next";
import { PageHeader } from "@/components/PageHeader";
import { ReportPanel } from "@/components/ReportPanel";

export const metadata: Metadata = { title: "Reportes" };

export default function ReportsPage() {
  return (
    <>
      <PageHeader
        eyebrow="Consulta de métricas"
        title="Reportes"
        description="Selecciona un periodo para consultar los indicadores agregados disponibles en el servidor."
      />
      <ReportPanel detailed />
      <section className="alert alert-info report-export-note">
        <div>
          <strong>Exportación CSV no disponible</strong>
          <p>El contrato actual no incluye un endpoint de exportación. Para evitar reconstruir o alterar el reporte, esta pantalla no genera un archivo local.</p>
        </div>
      </section>
    </>
  );
}
