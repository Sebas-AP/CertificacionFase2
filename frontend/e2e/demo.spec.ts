import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page, type Route } from "@playwright/test";

const invoiceId = "01234567-89ab-cdef-0123-456789abcdef";

function invoice(status = "needs_validation") {
  return {
    id: invoiceId,
    supplierId: null,
    invoiceNumber: "RC-DEMO-42",
    issueDate: "2026-10-12",
    currencyCode: "MXN",
    subtotal: 1000,
    taxAmount: 160,
    total: 1160,
    status,
    uploadedAtUtc: "2026-10-12T10:00:00Z",
    lines: [],
    reviewHistory: [],
    supplierName: "Proveedor de prueba",
    supplierTaxId: null,
    customerName: null,
    customerTaxId: null,
    dueDate: null,
    purchaseOrderNumber: null,
    extractionStatus: status === "extraction_failed" ? "failed" : "succeeded",
    extractionAttemptCount: 1,
    lastExtractionErrorCode: null,
    extractionCompletedAtUtc: "2026-10-12T10:01:00Z",
    fieldConfidence: {},
    validationWarnings: [],
    duplicateOfInvoiceId: null,
  };
}

function report(overrides: Record<string, unknown> = {}) {
  return {
    fromUtc: "2026-10-01T00:00:00Z",
    toUtc: "2026-11-01T00:00:00Z",
    processedInvoiceCount: 6,
    duplicateInvoiceCount: 1,
    duplicateRate: 0.1,
    approvedTotalsByCurrency: [{ currencyCode: "MXN", totalAmount: 1200 }],
    criticalInventoryItemCount: 2,
    ...overrides,
  };
}

async function mockApi(
  page: Page,
  handler: (route: Route, url: URL) => Promise<boolean>,
) {
  await page.route("http://127.0.0.1:5000/**", async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname === "/health") {
      await route.fulfill({ json: { status: "ok" } });
      return;
    }
    if (await handler(route, url)) return;
    await route.fulfill({
      status: 404,
      contentType: "application/problem+json",
      json: { status: 404, code: "not_found", traceId: "test-correlation" },
    });
  });
}

async function serveReport(page: Page, response: object | number = report()) {
  await mockApi(page, async (route, url) => {
    if (url.pathname !== "/api/reportes") return false;
    if (typeof response === "number") {
      await route.fulfill({
        status: response,
        contentType: "application/problem+json",
        json: { status: response, code: "report_unavailable", traceId: "test-correlation" },
      });
    } else {
      await route.fulfill({ json: response });
    }
    return true;
  });
}

test("opens directly on the dashboard without an authentication screen", async ({ page }) => {
  await serveReport(page);
  await page.goto("/");
  await expect(page).toHaveURL(/\/dashboard$/);
  await expect(page.getByRole("heading", { name: "Dashboard" })).toBeVisible();
  await expect(page.getByText("Facturas procesadas")).toBeVisible();
  await expect(page.getByText("6", { exact: true })).toBeVisible();
  await expect(page.getByRole("link", { name: "Facturas" })).toBeVisible();
  await expect(page.getByRole("heading", { name: /Iniciar sesión|Registro/ })).toHaveCount(0);
});

test("renders a truthful empty dashboard from empty API results", async ({ page }) => {
  await serveReport(page, report({
    processedInvoiceCount: 0,
    duplicateInvoiceCount: 0,
    duplicateRate: 0,
    approvedTotalsByCurrency: [],
    criticalInventoryItemCount: 0,
  }));
  await page.goto("/dashboard");
  await expect(page.getByRole("heading", { name: "Totales aprobados por moneda" })).toBeVisible();
  await expect(page.getByText("No hay totales aprobados para este periodo.")).toBeVisible();
  await expect(page.getByText("No se muestran valores calculados o simulados")).toBeVisible();
});

test("shows a recoverable error instead of mock indicators on a 503", async ({ page }) => {
  await serveReport(page, 503);
  await page.goto("/dashboard");
  await expect(page.locator(".alert-error")).toContainText("El servicio no está disponible");
  await expect(page.getByRole("button", { name: "Intentar de nuevo" })).toBeVisible();
});

test("filters and paginates invoices using the supported server query", async ({ page }) => {
  const requested: string[] = [];
  await mockApi(page, async (route, url) => {
    if (url.pathname !== "/api/facturas") return false;
    requested.push(url.search);
    await route.fulfill({
      json: {
        items: [invoice()],
        page: Number(url.searchParams.get("page") ?? 1),
        pageSize: 25,
        totalItems: 26,
        totalPages: 2,
      },
    });
    return true;
  });
  await page.goto("/facturas");
  await expect(page.getByText("Proveedor de prueba").first()).toBeVisible();
  await page.getByLabel("Estado").selectOption("approved");
  await expect.poll(() => requested.some((query) => query.includes("estado=approved"))).toBe(true);
  await page.getByRole("button", { name: "Siguiente" }).click();
  await expect.poll(() => requested.some((query) => query.includes("page=2") && query.includes("estado=approved"))).toBe(true);
});

test("posts a selected PDF and opens the invoice returned by the API", async ({ page }) => {
  await mockApi(page, async (route, url) => {
    if (url.pathname === "/api/invoices/upload") {
      expect(route.request().method()).toBe("POST");
      await route.fulfill({
        status: 201,
        json: { invoice: invoice(), extractionSucceeded: true, extractionErrorCode: null, extractionErrorMessage: null },
      });
      return true;
    }
    if (url.pathname === `/api/facturas/${invoiceId}`) {
      await route.fulfill({ json: invoice() });
      return true;
    }
    return false;
  });

  await page.goto("/facturas/nueva");
  await page.locator('input[type="file"]').setInputFiles({
    name: "factura-demo.pdf",
    mimeType: "application/pdf",
    buffer: Buffer.from("%PDF-1.4 test fixture"),
  });
  await expect(page.getByText("factura-demo.pdf")).toBeVisible();
  await page.getByRole("button", { name: "Enviar factura" }).click();
  await expect(page).toHaveURL(new RegExp(`/facturas/${invoiceId}$`));
  await expect(page.getByRole("heading", { name: "Factura RC-DEMO-42" })).toBeVisible();
  await expect(page.getByText("Vista previa no disponible")).toBeVisible();
});

test("rejects invalid and oversized files before calling the API", async ({ page }) => {
  let uploadCalls = 0;
  await mockApi(page, async (route, url) => {
    if (url.pathname === "/api/invoices/upload") {
      uploadCalls += 1;
      return true;
    }
    return false;
  });
  await page.goto("/facturas/nueva");
  const input = page.locator('input[type="file"]');
  await input.setInputFiles({
    name: "not-a-pdf.txt",
    mimeType: "text/plain",
    buffer: Buffer.from("not pdf"),
  });
  await expect(page.locator(".field-error")).toContainText("extensión .pdf");
  await input.setInputFiles({
    name: "large.pdf",
    mimeType: "application/pdf",
    buffer: Buffer.alloc(20 * 1024 * 1024 + 1),
  });
  await expect(page.locator(".field-error")).toContainText("máximo de 20 MiB");
  await expect(page.getByRole("button", { name: "Enviar factura" })).toBeDisabled();
  expect(uploadCalls).toBe(0);
});

test("links to an existing invoice when the API reports an exact duplicate", async ({ page }) => {
  await mockApi(page, async (route, url) => {
    if (url.pathname === "/api/invoices/upload") {
      await route.fulfill({
        status: 409,
        contentType: "application/problem+json",
        json: {
          status: 409,
          code: "duplicate_invoice_file",
          duplicateInvoiceId: invoiceId,
          traceId: "duplicate-trace",
        },
      });
      return true;
    }
    return false;
  });
  await page.goto("/facturas/nueva");
  await page.locator('input[type="file"]').setInputFiles({
    name: "duplicada.pdf",
    mimeType: "application/pdf",
    buffer: Buffer.from("%PDF-1.4 test fixture"),
  });
  await page.getByRole("button", { name: "Enviar factura" }).click();
  await expect(page.locator(".alert-error")).toContainText("Este mismo archivo ya había sido cargado");
  await expect(page.getByRole("link", { name: "Abrir la factura existente" })).toHaveAttribute("href", `/facturas/${invoiceId}`);
});

test("shows backend-provided chat replies and only safe external source links", async ({ page }) => {
  await mockApi(page, async (route, url) => {
    if (url.pathname === "/api/chat") {
      await route.fulfill({
        json: {
          reply: "Respuesta de demostración recibida del backend.",
          sources: [
            { title: "Documento ficticio", uri: "https://example.com/fuente" },
            { title: "Enlace no seguro", uri: "javascript:alert(1)" },
          ],
        },
      });
      return true;
    }
    return false;
  });
  await page.goto("/chat");
  await page.getByLabel("Tu pregunta").fill("¿Qué documentos hay disponibles?");
  await page.getByRole("button", { name: "Enviar pregunta" }).click();
  await expect(page.getByText("Respuesta de demostración recibida del backend.")).toBeVisible();
  const source = page.getByRole("link", { name: "Documento ficticio" });
  await expect(source).toHaveAttribute("target", "_blank");
  await expect(source).toHaveAttribute("rel", "noopener noreferrer");
  await expect(page.getByText("Enlace no seguro")).toBeVisible();
  await expect(page.getByText("El backend no devolvió fuentes para esta respuesta.")).toHaveCount(0);
});

test("explains an unconfigured chat without substituting a local answer", async ({ page }) => {
  await mockApi(page, async (route, url) => {
    if (url.pathname === "/api/chat") {
      await route.fulfill({
        status: 503,
        contentType: "application/problem+json",
        json: { status: 503, code: "chat_not_configured", traceId: "chat-trace" },
      });
      return true;
    }
    return false;
  });
  await page.goto("/chat");
  await page.getByLabel("Tu pregunta").fill("¿Cuál es el estado de mis facturas?");
  await page.getByRole("button", { name: "Enviar pregunta" }).click();
  await expect(page.locator(".alert-error")).toContainText("El asistente de IA todavía no está configurado en este entorno.");
  await expect(page.getByRole("link", { name: "Consultar estado del sistema" })).toBeVisible();
  await expect(page.getByText(/Respuesta:/)).toHaveCount(0);
});

test("reports extraction retry failure from the backend without success-shaped fallback", async ({ page }) => {
  await mockApi(page, async (route, url) => {
    if (url.pathname === `/api/facturas/${invoiceId}`) {
      await route.fulfill({ json: invoice("extraction_failed") });
      return true;
    }
    if (url.pathname === `/api/invoices/${invoiceId}/retry-extraction`) {
      await route.fulfill({
        status: 503,
        contentType: "application/problem+json",
        json: { status: 503, code: "document_intelligence_not_configured", traceId: "extract-trace" },
      });
      return true;
    }
    return false;
  });
  page.on("dialog", (dialog) => dialog.accept());
  await page.goto(`/facturas/${invoiceId}`);
  await page.getByRole("button", { name: "Reintentar extracción" }).click();
  await expect(page.locator(".alert-error")).toContainText("La extracción de facturas no está configurada");
  await expect(page.getByRole("button", { name: "Reintentar extracción" })).toBeEnabled();
});

test("saves corrected invoice fields through the review contract", async ({ page }) => {
  await mockApi(page, async (route, url) => {
    if (url.pathname === `/api/facturas/${invoiceId}`) {
      await route.fulfill({ json: invoice() });
      return true;
    }
    if (url.pathname === `/api/facturas/${invoiceId}/revision`) {
      expect(route.request().method()).toBe("PUT");
      const body = route.request().postDataJSON() as { invoiceNumber: string; reason: string };
      expect(body.invoiceNumber).toBe("RC-DEMO-42-CORREGIDA");
      expect(body.reason).toBe("Corrección de demostración");
      await route.fulfill({ json: { ...invoice(), invoiceNumber: body.invoiceNumber } });
      return true;
    }
    return false;
  });
  await page.goto(`/facturas/${invoiceId}`);
  await page.getByRole("button", { name: "Revisar datos" }).click();
  await page.getByLabel("Número de factura").fill("RC-DEMO-42-CORREGIDA");
  await page.getByLabel("Motivo de revisión (opcional)").fill("Corrección de demostración");
  await page.getByRole("button", { name: "Guardar revisión" }).click();
  await expect(page.getByRole("heading", { name: "Factura RC-DEMO-42-CORREGIDA" })).toBeVisible();
  await expect(page.getByRole("status")).toContainText("se guardaron en el backend");
});

test("approves only after explicit confirmation and a successful backend response", async ({ page }) => {
  await mockApi(page, async (route, url) => {
    if (url.pathname === `/api/facturas/${invoiceId}`) {
      await route.fulfill({ json: invoice("pending_review") });
      return true;
    }
    if (url.pathname === `/api/facturas/${invoiceId}/confirmacion`) {
      expect(route.request().method()).toBe("POST");
      await route.fulfill({ json: { ...invoice("pending_review"), status: "approved" } });
      return true;
    }
    return false;
  });
  page.on("dialog", (dialog) => dialog.accept());
  await page.goto(`/facturas/${invoiceId}`);
  await page.getByRole("button", { name: "Aprobar factura" }).click();
  await expect(page.getByText("Aprobada", { exact: true })).toBeVisible();
  await expect(page.getByRole("status")).toContainText("fue aprobada por el backend");
});

test("shows API and Foundry agent configuration status", async ({ page }) => {
  await mockApi(page, async (route, url) => {
    if (url.pathname === "/api/agents/health") {
      await route.fulfill({
        json: {
          businessAgent: { status: "NotConfigured" },
          invoiceAgent: { status: "NotConfigured" },
          modelDeployment: null,
          checkedAtUtc: "2026-10-12T10:00:00Z",
        },
      });
      return true;
    }
    return false;
  });
  await page.goto("/estado");
  await expect(page.getByText("API Río Claro")).toBeVisible();
  await expect(page.getByText("Agente empresarial")).toBeVisible();
  await expect(page.getByText("No configurado").first()).toBeVisible();
  await expect(page.getByText("Los agentes Foundry no están configurados o desplegados para este entorno.")).toBeVisible();
});

test("exposes keyboard navigation and has no serious Axe violations on primary screens", async ({ page }) => {
  await mockApi(page, async (route, url) => {
    if (url.pathname === "/api/reportes") {
      await route.fulfill({ json: report() });
      return true;
    }
    if (url.pathname === "/api/facturas") {
      await route.fulfill({ json: { items: [invoice()], page: 1, pageSize: 25, totalItems: 1, totalPages: 1 } });
      return true;
    }
    if (url.pathname === `/api/facturas/${invoiceId}`) {
      await route.fulfill({ json: invoice() });
      return true;
    }
    if (url.pathname === "/api/agents/health") {
      await route.fulfill({
        json: {
          businessAgent: { status: "NotConfigured" },
          invoiceAgent: { status: "NotConfigured" },
          modelDeployment: null,
          checkedAtUtc: "2026-10-12T10:00:00Z",
        },
      });
      return true;
    }
    return false;
  });
  await page.goto("/dashboard");
  await page.keyboard.press("Tab");
  await expect(page.locator(".skip-link")).toBeFocused();
  await page.keyboard.press("Enter");
  await expect(page.locator("#main-content")).toBeFocused();

  for (const path of ["/dashboard", "/facturas", "/facturas/nueva", `/facturas/${invoiceId}`, "/chat", "/reportes", "/estado"]) {
    await page.goto(path);
    await expect(page.locator("main h1")).toBeVisible();
    const results = await new AxeBuilder({ page }).analyze();
    expect(
      results.violations.filter((violation) => ["critical", "serious"].includes(violation.impact ?? "")),
      `Axe violations in ${path}`,
    ).toEqual([]);
  }
});

test("keeps dashboard, invoice, chat, and report screens free of global horizontal overflow", async ({ page }) => {
  await mockApi(page, async (route, url) => {
    if (url.pathname === "/api/reportes") {
      await route.fulfill({ json: report() });
      return true;
    }
    if (url.pathname === "/api/facturas") {
      await route.fulfill({ json: { items: [invoice()], page: 1, pageSize: 25, totalItems: 1, totalPages: 1 } });
      return true;
    }
    if (url.pathname === `/api/facturas/${invoiceId}`) {
      await route.fulfill({ json: invoice() });
      return true;
    }
    return false;
  });
  for (const viewport of [
    { width: 360, height: 800 },
    { width: 768, height: 1024 },
    { width: 1024, height: 768 },
    { width: 1440, height: 900 },
  ]) {
    await page.setViewportSize(viewport);
    for (const path of ["/dashboard", "/facturas", `/facturas/${invoiceId}`, "/chat", "/reportes"]) {
      await page.goto(path);
      await expect(page.locator("main h1")).toBeVisible();
      const dimensions = await page.evaluate(() => ({
        documentWidth: document.documentElement.scrollWidth,
        viewportWidth: document.documentElement.clientWidth,
        overflowElements: Array.from(document.querySelectorAll("body *"))
          .filter((element) => element.getBoundingClientRect().right > document.documentElement.clientWidth + 1)
          .slice(0, 8)
          .map((element) => `${element.tagName.toLowerCase()}.${String(element.className)} right=${Math.round(element.getBoundingClientRect().right)}`),
      }));
      expect(
        dimensions.documentWidth,
        `${path} overflows at ${viewport.width}px: ${dimensions.overflowElements.join(", ")}`,
      ).toBeLessThanOrEqual(dimensions.viewportWidth);
    }
  }
});

test("opens and closes the mobile navigation with its accessible control", async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 800 });
  await serveReport(page);
  await page.goto("/dashboard");
  const menuButton = page.getByRole("button", { name: "Menú" });
  await expect(menuButton).toBeVisible();
  await menuButton.click();
  await expect(page.getByRole("button", { name: "Cerrar menú" })).toHaveAttribute("aria-expanded", "true");
  await expect(page.getByRole("navigation", { name: "Navegación principal" }).getByRole("link", { name: "Chat empresarial" })).toBeVisible();
  await expect(page.getByText("Modo demostración · Río Claro")).toBeVisible();
  const results = await new AxeBuilder({ page }).analyze();
  expect(results.violations.filter((violation) => ["critical", "serious"].includes(violation.impact ?? ""))).toEqual([]);
  await page.getByRole("button", { name: "Cerrar menú" }).click();
  await expect(page.getByRole("button", { name: "Menú" })).toHaveAttribute("aria-expanded", "false");
});
