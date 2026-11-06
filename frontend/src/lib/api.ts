import type {
  AgentsHealth,
  ChatResponse,
  Invoice,
  InvoiceExplanation,
  InvoicePage,
  InvoiceReviewRequest,
  InvoiceUploadResult,
  ManagementReport,
} from "./contracts";

export interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  code?: string;
  traceId?: string;
  duplicateInvoiceId?: string;
}

const knownErrors: Record<string, string> = {
  pdf_required: "Selecciona un archivo PDF para continuar.",
  file_required: "Selecciona un archivo PDF para continuar.",
  pdf_too_large: "El archivo supera el límite de 20 MiB.",
  pdf_invalid_type: "El archivo debe ser un PDF.",
  invalid_file_extension: "El archivo debe tener extensión .pdf.",
  invalid_content_type: "El tipo de archivo debe ser application/pdf.",
  pdf_invalid_signature: "El contenido no coincide con un archivo PDF válido.",
  invalid_pdf_signature: "El contenido no coincide con un archivo PDF válido.",
  pdf_corrupted: "No se pudo leer el PDF. Verifica el archivo e inténtalo de nuevo.",
  invalid_pdf: "No se pudo leer el PDF. Verifica el archivo e inténtalo de nuevo.",
  empty_file: "El archivo está vacío.",
  pdf_size_mismatch: "No se pudo verificar el tamaño del archivo enviado.",
  pdf_password_protected: "El PDF está protegido con contraseña y no se puede procesar.",
  pdf_page_limit_exceeded: "El PDF supera el máximo de 50 páginas.",
  duplicate_invoice_file: "Este mismo archivo ya había sido cargado.",
  invoice_exact_duplicate: "Este mismo archivo ya había sido cargado.",
  blob_storage_not_configured: "El almacenamiento de facturas no está configurado.",
  blob_upload_failed: "No se pudo conservar el archivo en el almacenamiento.",
  blob_storage_failed: "No se pudo conservar el archivo en el almacenamiento.",
  document_intelligence_not_configured: "La extracción de facturas no está configurada.",
  extraction_failed: "No se pudo extraer la información de la factura.",
  document_intelligence_failed: "No se pudo extraer la información de la factura.",
  document_intelligence_timeout: "La extracción tardó demasiado. Puedes intentarlo de nuevo.",
  extraction_retry_limit: "Se alcanzó el límite de intentos de extracción.",
  invoice_extraction_conflict: "No se puede reintentar la extracción en el estado actual o se alcanzó el límite de intentos.",
  invoice_not_found: "No se encontró la factura solicitada.",
  invoice_document_not_found: "El documento original no está disponible.",
  invoice_pdf_unavailable: "El documento original no está disponible.",
  retention_delete_failed: "No se pudo completar la operación de retención.",
  chat_not_configured: "El asistente de IA todavía no está configurado en este entorno.",
  chat_provider_unavailable: "El asistente de IA no está disponible en este entorno.",
  invoice_agent_not_configured: "El asistente de IA todavía no está configurado en este entorno.",
  invoice_agent_unavailable: "El asistente de facturas no está disponible en este entorno.",
  agent_not_configured: "El asistente de IA todavía no está configurado en este entorno.",
  invalid_report_period: "El periodo seleccionado no es válido.",
  invalid_invoice_query: "Los filtros de facturas no son válidos.",
  invoice_state_conflict: "La factura cambió de estado y no se pudo completar la acción.",
  invalid_invoice_review: "Revisa los campos y formatos del formulario.",
};

export class ApiError extends Error {
  readonly status: number;
  readonly code: string | null;
  readonly traceId: string | null;
  readonly duplicateInvoiceId: string | null;

  constructor(
    message: string,
    status: number,
    code: string | null = null,
    traceId: string | null = null,
    duplicateInvoiceId: string | null = null,
  ) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.code = code;
    this.traceId = traceId;
    this.duplicateInvoiceId = duplicateInvoiceId;
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

export function parseApiError(status: number, body: unknown): ApiError {
  const details = isRecord(body) ? (body as ProblemDetails) : {};
  const code = typeof details.code === "string" ? details.code.toLowerCase() : null;
  const traceId = typeof details.traceId === "string" ? details.traceId : null;
  const duplicateInvoiceId =
    typeof details.duplicateInvoiceId === "string" ? details.duplicateInvoiceId : null;
  const message =
    (code && knownErrors[code]) ||
    (status === 503
      ? "El servicio no está disponible o no está configurado."
      : status === 504
        ? "La solicitud tardó demasiado. Puedes intentarlo de nuevo."
        : status >= 500
          ? "Ocurrió un error del servidor. Inténtalo de nuevo más tarde."
          : "No se pudo completar la solicitud. Revisa los datos e inténtalo de nuevo.");

  return new ApiError(message, status, code, traceId, duplicateInvoiceId);
}

function apiBaseUrl(): string {
  const configured = process.env.NEXT_PUBLIC_API_BASE_URL?.trim();
  if (!configured) {
    throw new ApiError(
      "La API no está configurada. Define NEXT_PUBLIC_API_BASE_URL para conectar la demostración.",
      0,
      "api_not_configured",
    );
  }

  return configured.replace(/\/+$/, "");
}

async function readProblem(response: Response): Promise<unknown> {
  const contentType = response.headers.get("content-type") ?? "";
  if (!contentType.includes("json")) return undefined;

  try {
    return await response.json();
  } catch (error) {
    if (error instanceof SyntaxError) return undefined;
    throw error;
  }
}

export async function apiRequest<T>(
  path: string,
  init: RequestInit = {},
): Promise<T> {
  const headers = new Headers(init.headers);
  headers.set("Accept", "application/json, application/problem+json");
  if (init.body && !(init.body instanceof FormData) && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  try {
    const response = await fetch(`${apiBaseUrl()}${path}`, {
      ...init,
      headers,
      cache: "no-store",
    });

    if (!response.ok) {
      throw parseApiError(response.status, await readProblem(response));
    }

    if (response.status === 204) return undefined as T;
    return (await response.json()) as T;
  } catch (error) {
    if (error instanceof ApiError) throw error;

    // Si es un TypeError (Failed to fetch), significa que el servidor está apagado o la URL es incorrecta
    throw new ApiError(
      "No se pudo establecer conexión con el servidor. Asegúrate de que el backend esté encendido y el puerto sea correcto.",
      503,
      "server_unreachable"
    );
  }
}

export function getReport(
  fromUtc: string,
  toUtc: string,
  signal?: AbortSignal,
): Promise<ManagementReport> {
  const query = new URLSearchParams({ fromUtc, toUtc });
  return apiRequest<ManagementReport>(`/api/reportes?${query}`, { signal });
}

export function getInvoices(
  page: number,
  status: string,
  signal?: AbortSignal,
): Promise<InvoicePage> {
  const query = new URLSearchParams({ page: String(page), pageSize: "25" });
  if (status) query.set("estado", status);
  return apiRequest<InvoicePage>(`/api/facturas?${query}`, { signal });
}

export function getInvoice(id: string, signal?: AbortSignal): Promise<Invoice> {
  return apiRequest<Invoice>(`/api/facturas/${encodeURIComponent(id)}`, { signal });
}

export function uploadInvoice(file: File, signal?: AbortSignal): Promise<InvoiceUploadResult> {
  const body = new FormData();
  body.append("file", file);
  return apiRequest<InvoiceUploadResult>("/api/invoices/upload", { method: "POST", body, signal });
}

export function retryInvoiceExtraction(
  id: string,
  signal?: AbortSignal,
): Promise<InvoiceUploadResult> {
  return apiRequest<InvoiceUploadResult>(`/api/invoices/${encodeURIComponent(id)}/retry-extraction`, {
    method: "POST",
    signal,
  });
}

export function reviewInvoice(
  id: string,
  review: InvoiceReviewRequest,
  signal?: AbortSignal,
): Promise<Invoice> {
  return apiRequest<Invoice>(`/api/facturas/${encodeURIComponent(id)}/revision`, {
    method: "PUT",
    body: JSON.stringify(review),
    signal,
  });
}

export function approveInvoice(id: string, signal?: AbortSignal): Promise<Invoice> {
  return apiRequest<Invoice>(`/api/facturas/${encodeURIComponent(id)}/confirmacion`, {
    method: "POST",
    signal,
  });
}

export function sendChatMessage(
  message: string,
  signal?: AbortSignal,
): Promise<ChatResponse> {
  return apiRequest<ChatResponse>("/api/chat", {
    method: "POST",
    body: JSON.stringify({ message }),
    signal,
  });
}

export function explainInvoice(
  id: string,
  signal?: AbortSignal,
): Promise<InvoiceExplanation> {
  return apiRequest<InvoiceExplanation>(`/api/invoices/${encodeURIComponent(id)}/explain`, {
    method: "POST",
    signal,
  });
}

export function getAgentsHealth(signal?: AbortSignal): Promise<AgentsHealth> {
  return apiRequest<AgentsHealth>("/api/agents/health", { signal });
}

export function getApiHealth(signal?: AbortSignal): Promise<{ status: string }> {
  return apiRequest<{ status: string }>("/health", { signal });
}
