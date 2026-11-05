"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState } from "react";
import type { ReactNode } from "react";

const navigation = [
  { href: "/dashboard", label: "Dashboard" },
  { href: "/facturas", label: "Facturas" },
  { href: "/chat", label: "Chat empresarial" },
  { href: "/reportes", label: "Reportes" },
  { href: "/estado", label: "Estado del sistema" },
];

function NavigationLinks({ onNavigate }: { onNavigate?: () => void }) {
  const pathname = usePathname();

  return (
    <nav aria-label="Navegación principal">
      <ul className="nav-list">
        {navigation.map((item) => {
          const current =
            pathname === item.href ||
            (item.href === "/facturas" && pathname.startsWith("/facturas/"));
          return (
            <li key={item.href}>
              <Link
                href={item.href}
                aria-current={current ? "page" : undefined}
                className={current ? "nav-link nav-link-current" : "nav-link"}
                onClick={onNavigate}
              >
                {item.label}
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}

export function AppShell({ children }: { children: ReactNode }) {
  const [mobileOpen, setMobileOpen] = useState(false);

  return (
    <>
      <a className="skip-link" href="#main-content">Saltar al contenido</a>
      <div className="app-frame">
        <aside className="sidebar" aria-label="Menú lateral">
          <Link className="brand" href="/dashboard">
            <span className="brand-mark" aria-hidden="true">CG</span>
            <span><strong>ClaroGestion</strong><small>AI · Río Claro</small></span>
          </Link>
          <NavigationLinks />
          <div className="sidebar-foot">
            <span className="mode-dot" aria-hidden="true" />
            <span>Modo demostración</span>
          </div>
        </aside>

        <div className="app-column">
          <header className="topbar">
            <div className="mobile-brand">
              <Link className="brand" href="/dashboard">
                <span className="brand-mark" aria-hidden="true">CG</span>
                <span><strong>ClaroGestion AI</strong><small>Río Claro</small></span>
              </Link>
              <button
                className="menu-toggle"
                type="button"
                aria-expanded={mobileOpen}
                aria-controls="mobile-navigation"
                onClick={() => setMobileOpen((open) => !open)}
              >
                {mobileOpen ? "Cerrar menú" : "Menú"}
              </button>
            </div>
            <div className="org-context">
              <span className="context-label">Organización</span>
              <strong>Río Claro</strong>
            </div>
            <span className="demo-pill">Modo demostración</span>
          </header>
          {mobileOpen && (
            <div className="mobile-navigation" id="mobile-navigation">
              <NavigationLinks onNavigate={() => setMobileOpen(false)} />
              <p className="mobile-demo-note"><span className="mode-dot" aria-hidden="true" />Modo demostración · Río Claro</p>
            </div>
          )}
          <main id="main-content" className="main-content" tabIndex={-1}>
            {children}
          </main>
          <footer className="app-footer">
            <span>ClaroGestion AI</span>
            <span>Demostración académica · No usar con información real</span>
          </footer>
        </div>
      </div>
    </>
  );
}
