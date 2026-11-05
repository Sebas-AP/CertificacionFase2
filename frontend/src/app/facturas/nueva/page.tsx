"use client";

import Link from "next/link";
import { useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { ApiError, uploadInvoice } from "@/lib/api";
import { formatFileSize, validatePdf } from "@/lib/invoices";
import { PageHeader } from "@/components/PageHeader";

export default function NewInvoicePage() {
  const router = useRouter();
  const inputRef = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [fileError, setFileError] = useState("");
  const [requestError, setRequestError] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);
  const [dragging, setDragging] = useState(false);

  function chooseFile(candidate: File | undefined) {
    setRequestError(null);
    setFileError("");
    if (!candidate) {
      setFile(null);
      return;
    }

    const validation = validatePdf(candidate);
    if (!validation.valid) {
      setFile(null);
      setFileError(validation.message);
      return;
    }
    setFile(candidate);
  }

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!file || busy) return;
    setBusy(true);
    setRequestError(null);
    try {
      const result = await uploadInvoice(file);
      router.push(`/facturas/${result.invoice.id}`);
    } catch (cause) {
      if (cause instanceof ApiError) setRequestError(cause);
      else setRequestError(new ApiError("No se pudo completar la carga. Verifica tu conexión e inténtalo de nuevo.", 0));
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <PageHeader
        eyebrow="Facturas"
        title="Subir factura"
        description="Selecciona un PDF para enviarlo a la API de Río Claro. La carga no inicia hasta que confirmes."
      />
      <div className="back-link-row"><Link className="text-link" href="/facturas">← Volver a facturas</Link></div>
      <form className="panel upload-form" onSubmit={submit}>
        <div
          className={dragging ? "drop-zone drop-zone-active" : "drop-zone"}
          onDragOver={(event) => {
            event.preventDefault();
            setDragging(true);
          }}
          onDragLeave={() => setDragging(false)}
          onDrop={(event) => {
            event.preventDefault();
            setDragging(false);
            if (event.dataTransfer.files.length !== 1) {
              setFileError("Selecciona un solo archivo PDF.");
              setFile(null);
              return;
            }
            chooseFile(event.dataTransfer.files[0]);
          }}
        >
          <div className="upload-symbol" aria-hidden="true">PDF</div>
          <h2>Selecciona un archivo PDF</h2>
          <p>Arrastra el archivo aquí o selecciónalo desde tu dispositivo.</p>
          <label className="button button-secondary file-picker">
            Elegir archivo
            <input
              ref={inputRef}
              type="file"
              accept=".pdf,application/pdf"
              onChange={(event) => chooseFile(event.target.files?.[0])}
              aria-describedby="upload-requirements"
            />
          </label>
          <p id="upload-requirements" className="muted">Un archivo · PDF · máximo 20 MiB · hasta 50 páginas (verificado por el servidor)</p>
        </div>

        {file && (
          <div className="selected-file">
            <div>
              <strong>{file.name}</strong>
              <span>{formatFileSize(file.size)}</span>
            </div>
            <button type="button" className="button button-secondary" onClick={() => chooseFile(undefined)} disabled={busy}>Quitar archivo</button>
          </div>
        )}
        {fileError && <p className="field-error" role="alert">{fileError}</p>}
        {requestError && (
          <div className="alert alert-error" role="alert">
            <div>
              <strong>{requestError.status === 409 ? "Archivo duplicado" : "No se pudo cargar el archivo"}</strong>
              <p>{requestError.message}</p>
              {requestError.traceId && <small>Referencia: {requestError.traceId}</small>}
              {requestError.duplicateInvoiceId && (
                <p><Link className="text-link" href={`/facturas/${requestError.duplicateInvoiceId}`}>Abrir la factura existente</Link></p>
              )}
            </div>
          </div>
        )}

        {busy && (
          <p className="loading-state" role="status" aria-live="polite">
            Enviando archivo. El servidor validará el PDF y reportará el resultado real del procesamiento.
          </p>
        )}
        <div className="form-actions">
          <Link className="button button-secondary" href="/facturas">Cancelar</Link>
          <button type="submit" className="button button-primary" disabled={!file || busy}>
            {busy ? "Enviando…" : "Enviar factura"}
          </button>
        </div>
      </form>
    </>
  );
}
