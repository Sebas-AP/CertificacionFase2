import type { Metadata } from "next";
import type { ReactNode } from "react";
import { AppShell } from "@/components/AppShell";
import "./globals.css";

export const metadata: Metadata = {
  title: {
    default: "ClaroGestion AI | Río Claro",
    template: "%s | ClaroGestion AI",
  },
  description: "Demostración de gestión de facturas e información empresarial de Río Claro.",
};

export default function RootLayout({ children }: Readonly<{ children: ReactNode }>) {
  return (
    <html lang="es-MX">
      <body>
        <AppShell>{children}</AppShell>
      </body>
    </html>
  );
}
