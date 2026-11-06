"use client";

import type { ReactNode } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";

type IconProps = { size?: number; stroke?: number; className?: string };

function IconBase({
  size = 20,
  stroke = 1.5,
  className,
  children,
}: IconProps & { children: ReactNode }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={stroke}
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
      aria-hidden="true"
    >
      {children}
    </svg>
  );
}

const IconBuilding = (props: IconProps) => <IconBase {...props}><path d="M3 21h18M5 21V7l7-4 7 4v14M9 9h1m4 0h1m-6 4h1m4 0h1m-5 8v-4h2v4" /></IconBase>;
const IconChartInfographic = (props: IconProps) => <IconBase {...props}><path d="M3 3v18h18M7 14l4-4 3 3 6-7M7 18v-2m4 2v-5m4 5v-3m5 3V9" /></IconBase>;
const IconSelector = (props: IconProps) => <IconBase {...props}><path d="m7 10 5-5 5 5M7 14l5 5 5-5" /></IconBase>;
const IconHelpCircle = (props: IconProps) => <IconBase {...props}><circle cx="12" cy="12" r="10" /><path d="M9.1 9a3 3 0 0 1 5.8 1c0 2-3 3-3 3m.1 4h.01" /></IconBase>;
const IconFileText = (props: IconProps) => <IconBase {...props}><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8zM14 2v6h6M8 13h8m-8 4h8" /></IconBase>;
const IconHome = (props: IconProps) => <IconBase {...props}><path d="m3 10 9-7 9 7v10a1 1 0 0 1-1 1h-6v-7h-4v7H4a1 1 0 0 1-1-1z" /></IconBase>;
const IconLogout = (props: IconProps) => <IconBase {...props}><path d="M10 17l5-5-5-5m5 5H3m9-9h7a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-7" /></IconBase>;
const IconPackage = (props: IconProps) => <IconBase {...props}><path d="m12 3 9 5-9 5-9-5 9-5zm-9 5v8l9 5 9-5V8m-9 5v8m-4-16 9 5" /></IconBase>;
const IconSearch = (props: IconProps) => <IconBase {...props}><circle cx="11" cy="11" r="8" /><path d="m21 21-4.3-4.3" /></IconBase>;
const IconBell = (props: IconProps) => <IconBase {...props}><path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9m-8 12a2 2 0 0 0 4 0" /></IconBase>;
const IconSettings2 = (props: IconProps) => <IconBase {...props}><path d="M4 21v-7m0-4V3m8 18v-9m0-4V3m8 18v-5m0-4V3M2 14h4m4-6h4m4 8h4" /></IconBase>;
const IconSparkles = (props: IconProps) => <IconBase {...props}><path d="m12 3 1.9 5.8L20 11l-6.1 2.2L12 19l-1.9-5.8L4 11l6.1-2.2L12 3zm7 12 .9 2.1L22 18l-2.1.9L19 21l-.9-2.1L16 18l2.1-.9L19 15z" /></IconBase>;
const IconUserCircle = (props: IconProps) => <IconBase {...props}><circle cx="12" cy="12" r="10" /><circle cx="12" cy="9" r="3" /><path d="M5.6 19a7 7 0 0 1 12.8 0" /></IconBase>;
const IconUserCog = (props: IconProps) => <IconBase {...props}><circle cx="10" cy="8" r="4" /><path d="M3 21a7 7 0 0 1 11-5.7m5.5-1.3.8 1.2 1.4.3-.1 1.5.8 1.2-1.2.8-.4 1.4-1.5-.1-1.2.8-.8-1.2-1.4-.4.1-1.5-.8-1.2 1.2-.8.4-1.4 1.5.1z" /></IconBase>;

const NAV = [
  { to: "/dashboard", label: "Inicio", icon: IconHome },
  { to: "/facturas", label: "Facturas", icon: IconFileText },
  { to: "/reportes", label: "Reportes", icon: IconChartInfographic },
  { to: "/inventario", label: "Inventario", icon: IconPackage },
  { to: "/chat", label: "Chat de IA", icon: IconSparkles, badge: "NUEVO" },
  { to: "/configuracion", label: "Configuración", icon: IconSettings2 },
  { to: "/perfil", label: "Perfil", icon: IconUserCog },
];

function Sidebar() {
  const pathname = usePathname();

  return (
    <aside className="hidden w-56 shrink-0 flex-col gap-7 bg-[#101E3A] px-4 py-7 lg:flex">
      <div className="flex h-[52px] items-center gap-2.5 rounded-lg bg-[#1D2D4B] p-3">
        <IconBuilding size={20} stroke={1.5} className="text-[#73B9FC]" />
        <span className="flex-1 text-xs font-semibold text-white">
          Sucursal Norte
        </span>
        <IconSelector size={12} className="text-[#A9B7CE]" />
      </div>
      <nav className="flex flex-1 flex-col gap-[7px]">
        <div className="px-3 pb-1.5 text-[10px] font-semibold text-[#A9B7CE]">
          PRINCIPAL
        </div>
        {NAV.map(({ to, label, icon: Icon, badge }) => {
          const isActive = pathname === to;
          return (
            <Link
              key={to}
              href={to}
              className={`flex h-[45px] items-center gap-3 rounded-lg px-[13px] text-[13px] transition-colors ${
                isActive
                  ? "bg-[#2463EB] font-semibold text-white"
                  : "text-[#A9B7CE] hover:bg-white/5 hover:text-white"
              }`}
            >
              <Icon size={18} stroke={1.5} />
              <span className="flex-1">{label}</span>
              {badge && <span className="text-[9px] text-[#73B9FC]">{badge}</span>}
            </Link>
          );
        })}
      </nav>
      <div className="flex flex-col gap-5">
        <div className="flex items-center gap-2.5 px-3 text-xs text-[#A9B7CE]">
          <IconHelpCircle size={17} stroke={1.5} />
          Centro de ayuda
        </div>
        <div className="flex items-center gap-3 border-t border-[#2B3953] px-3 pt-[18px]">
          <IconUserCircle size={22} stroke={1.5} className="shrink-0 text-[#73B9FC]" />
          <span className="flex-1 truncate text-xs font-semibold text-white">
            Tristan Galindo
          </span>
          <IconLogout size={16} className="shrink-0 text-[#A9B7CE]" />
        </div>
      </div>
    </aside>
  );
}

export function AppShell({
  crumb,
  children,
}: {
  crumb: string;
  children: ReactNode;
}) {
  return (
    <div className="flex min-h-screen w-full bg-[#F5F7FB]">
      <Sidebar />
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex h-20 items-center justify-between gap-4 border-b border-[#E6EBF2] bg-white px-4 sm:px-8">
          <div className="text-xs text-[#718096]">
            Espacio de trabajo &nbsp;/&nbsp; {crumb}
          </div>
          <div className="flex items-center gap-5">
            <div className="hidden h-10 w-[258px] items-center gap-2.5 rounded-lg border border-[#E6EBF2] bg-white px-3 sm:flex">
              <IconSearch size={16} className="shrink-0 text-[#718096]" />
              <span className="text-xs text-[#718096]">
                Buscar en tu negocio…
              </span>
            </div>
            <IconBell size={20} stroke={1.5} className="text-[#718096]" />
            <div className="flex size-[34px] items-center justify-center rounded-full bg-[#EDF3FF] text-[11px] font-semibold text-[#2463EB]">
              TG
            </div>
          </div>
        </header>
        <main className="flex flex-col gap-6 p-4 sm:p-8">{children}</main>
      </div>
    </div>
  );
}
