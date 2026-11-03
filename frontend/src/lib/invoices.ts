export const MAX_PDF_SIZE_BYTES = 20 * 1024 * 1024;
export const MAX_PDF_PAGES = 50;

export type PdfValidation = { valid: true } | { valid: false; message: string };

export function validatePdf(file: Pick<File, "name" | "size" | "type">): PdfValidation {
  if (!file.name.toLowerCase().endsWith(".pdf")) {
    return { valid: false, message: "El archivo debe tener extensión .pdf." };
  }
  if (file.type && file.type.toLowerCase() !== "application/pdf") {
    return { valid: false, message: "El tipo de archivo no coincide con PDF." };
  }
  if (file.size <= 0) {
    return { valid: false, message: "El archivo está vacío." };
  }
  if (file.size > MAX_PDF_SIZE_BYTES) {
    return { valid: false, message: "El archivo supera el máximo de 20 MiB." };
  }
  return { valid: true };
}

export function formatFileSize(bytes: number): string {
  if (bytes < 1024 * 1024) return `${Math.max(1, Math.round(bytes / 1024))} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}
