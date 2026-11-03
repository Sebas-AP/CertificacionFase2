import assert from "node:assert/strict";
import test from "node:test";
import { parseApiError } from "./api.ts";
import { MAX_PDF_SIZE_BYTES, validatePdf } from "./invoices.ts";
import { customPeriod, duplicateMatchLabel, formatPercent, invoiceStatusLabel, presetPeriod } from "./format.ts";

test("maps known API errors without exposing backend detail", () => {
  const error = parseApiError(503, {
    code: "chat_not_configured",
    traceId: "demo-correlation",
    detail: "internal provider details",
  });

  assert.equal(error.message, "El asistente de IA todavía no está configurado en este entorno.");
  assert.equal(error.traceId, "demo-correlation");
  assert.equal(error.message.includes("internal provider details"), false);
  assert.equal(parseApiError(400, { code: "invalid_pdf_signature" }).message, "El contenido no coincide con un archivo PDF válido.");
});

test("uses a safe generic message for unknown server errors", () => {
  const error = parseApiError(500, { detail: "stack trace or sensitive provider information" });
  assert.match(error.message, /error del servidor/i);
  assert.equal(error.message.includes("stack trace"), false);
});

test("validates PDF extension, MIME and exact size boundary", () => {
  assert.deepEqual(validatePdf({ name: "factura.pdf", size: MAX_PDF_SIZE_BYTES, type: "application/pdf" }), {
    valid: true,
  });
  assert.equal(validatePdf({ name: "factura.exe", size: 100, type: "application/pdf" }).valid, false);
  assert.equal(validatePdf({ name: "factura.pdf", size: 100, type: "text/plain" }).valid, false);
  assert.equal(
    validatePdf({ name: "factura.pdf", size: MAX_PDF_SIZE_BYTES + 1, type: "application/pdf" }).valid,
    false,
  );
});

test("rejects incomplete or reversed custom report periods", () => {
  assert.equal(customPeriod("", "2026-10-10"), null);
  assert.equal(customPeriod("2026-10-11", "2026-10-10"), null);
  assert.equal(customPeriod("2026-10-01", "2026-10-31")?.toUtc, "2026-11-01T00:00:00.000Z");
});

test("labels backend invoice states and formats ratio values as percentages", () => {
  assert.equal(invoiceStatusLabel("extraction_failed"), "Extracción fallida");
  assert.equal(invoiceStatusLabel("unknown"), "Estado no reconocido");
  assert.equal(formatPercent(0.125).replace(/\s/g, ""), "12.5%");
  assert.equal(duplicateMatchLabel("ExactBusinessDuplicate"), "Existe otra factura con el mismo proveedor y número de factura.");
});

test("creates a seven-day UTC interval ending at the next UTC midnight", () => {
  const period = presetPeriod("7d");
  const difference = Date.parse(period.toUtc) - Date.parse(period.fromUtc);
  assert.equal(difference, 7 * 24 * 60 * 60 * 1000);
});
