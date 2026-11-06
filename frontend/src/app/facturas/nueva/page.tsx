"use client";

import Link from "next/link";
import { useRef, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import {
  IconCloudUpload,
  IconFileText,
  IconX,
  IconAlertCircle,
  IconCircleCheck,
  IconArrowLeft,
  IconRefresh,
} from "@tabler/icons-react";
import { ApiError, uploadInvoice } from "@/lib/api";
import { AppShell } from "@/components/AppShell";

function formatFileSize(bytes: number): string {
  if (bytes === 0) return "0 Bytes";
  const k = 1024;
  const sizes = ["Bytes", "KB", "MB", "GB"];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return `${(bytes / Math.pow(k, i)).toFixed(2)} ${sizes[i]}`;
}

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

    if (candidate.type !== "application/pdf" && !candidate.name.endsWith(".pdf")) {
      setFile(null);
      setFileError("El archivo debe ser un formato PDF.");
      return;
    }
    if (candidate.size > 20 * 1024 * 1024) {
      setFile(null);
      setFileError("El archivo supera el límite de 20 MiB.");
      return;
    }
    setFile(candidate);
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!file || busy) return;
    setBusy(true);
    setRequestError(null);

    try {
      const result = await uploadInvoice(file);
      router.push(`/facturas/${result.invoice.id}`);
    } catch (cause) {
      if (cause instanceof ApiError) {
        setRequestError(cause);
      } else {
        setRequestError(new ApiError("No se pudo completar la carga. Verifica tu conexión e inténtalo de nuevo.", 0));
      }
    } finally {
      setBusy(false);
    }
  }

  const panel = "rounded-xl border border-[#E6EBF2] bg-white";

  return (
    <AppShell crumb="Nueva Factura">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <h1 className="text-[28px] font-semibold leading-[1.45] text-[#18243B]">Subir nueva factura</h1>
        <Link
          href="/facturas"
          className="flex h-[42px] items-center gap-2 rounded-lg border border-[#E6EBF2] bg-white px-4 text-[13px] font-semibold text-[#18243B] hover:bg-[#F9FAFB]"
        >
          <IconArrowLeft size={16} stroke={1.5} />
          Volver al listado
        </Link>
      </div>

      <div className="max-w-3xl mx-auto w-full mt-6">
        <form className={`${panel} p-8 flex flex-col gap-8`} onSubmit={submit}>
          <div className="flex flex-col gap-2">
            <h2 className="text-lg font-semibold text-[#18243B]">Carga de documento</h2>
            <p className="text-xs text-[#718096]">
              Sube la factura en formato PDF. Nuestro sistema de IA extraerá la información automáticamente.
            </p>
          </div>

          <div
            className={`relative group cursor-pointer border-2 border-dashed rounded-2xl p-12 flex flex-col items-center justify-center gap-4 transition-all ${
              dragging
                ? "border-[#2463EB] bg-[#EDF3FF] scale-[1.01]"
                : "border-[#E6EBF2] bg-[#FAFBFD] hover:border-[#B9C8E2] hover:bg-[#F5F7FB]"
            }`}
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
            onClick={() => inputRef.current?.click()}
          >
            <div
              className={`flex size-16 items-center justify-center rounded-full transition-colors ${
                file ? "bg-[#E8F7F0] text-[#13856A]" : "bg-white text-[#718096] shadow-sm"
              }`}
            >
              {file ? <IconCircleCheck size={32} stroke={1.5} /> : <IconCloudUpload size={32} stroke={1.5} />}
            </div>
            <div className="text-center">
              <div className="text-sm font-semibold text-[#18243B]">
                {file ? "Archivo seleccionado" : "Arrastra tu PDF aquí o haz clic para buscar"}
              </div>
              <div className="text-xs text-[#718096] mt-1">Límite: 20 MiB · Formato: .pdf</div>
            </div>
            <input
              ref={inputRef}
              type="file"
              accept=".pdf,application/pdf"
              className="hidden"
              onChange={(event) => chooseFile(event.target.files?.[0])}
            />
          </div>

          {file && (
            <div className="flex items-center justify-between p-4 rounded-xl bg-[#F9FAFB] border border-[#E6EBF2]">
              <div className="flex items-center gap-3">
                <div className="flex size-8 items-center justify-center rounded-lg bg-white border border-[#E6EBF2] text-[#2463EB]">
                  <IconFileText size={16} stroke={1.5} />
                </div>
                <div className="flex flex-col">
                  <span className="text-xs font-semibold text-[#18243B] truncate max-w-[200px]">{file.name}</span>
                  <span className="text-[10px] text-[#718096]">{formatFileSize(file.size)}</span>
                </div>
              </div>
              <button
                type="button"
                onClick={(e) => {
                  e.stopPropagation();
                  chooseFile(undefined);
                }}
                disabled={busy}
                className="p-2 text-[#718096] hover:text-red-500 transition-colors"
              >
                <IconX size={18} stroke={1.5} />
              </button>
            </div>
          )}

          {fileError && (
            <div className="flex items-center gap-2 p-3 rounded-lg bg-red-50 border border-red-100 text-xs text-red-600">
              <IconAlertCircle size={14} stroke={1.5} />
              {fileError}
            </div>
          )}

          {requestError && (
            <div className="flex flex-col gap-2 p-4 rounded-xl bg-red-50 border border-red-100 text-xs text-red-600">
              <div className="flex items-start gap-2">
                <IconAlertCircle size={16} stroke={1.5} className="shrink-0 mt-0.5" />
                <div className="flex flex-col gap-1">
                  <strong className="font-semibold">
                    {requestError.status === 409 ? "Archivo duplicado" : "Error en la carga"}
                  </strong>
                  <p>{requestError.message}</p>
                </div>
              </div>
              {requestError.duplicateInvoiceId && (
                <Link
                  href={`/facturas/${requestError.duplicateInvoiceId}`}
                  className="text-[#2463EB] font-semibold hover:underline mt-1"
                >
                  Abrir la factura existente →
                </Link>
              )}
            </div>
          )}

          {busy && (
            <div className="flex items-center gap-3 p-4 rounded-xl bg-[#EDF3FF] border border-[#BBDDFF] text-xs text-[#2463EB]">
              <IconRefresh size={16} className="animate-spin" />
              <span>Enviando archivo al servidor... La IA está procesando la información.</span>
            </div>
          )}

          <div className="flex items-center justify-end gap-4 pt-4 border-t border-[#E6EBF2]">
            <Link href="/facturas" className="text-xs font-semibold text-[#718096] hover:text-[#18243B]">
              Cancelar
            </Link>
            <button
              type="submit"
              disabled={!file || busy}
              className="flex h-[42px] items-center gap-2 rounded-lg bg-[#2463EB] px-6 text-[13px] font-semibold text-white hover:bg-[#1d54c9] disabled:bg-[#B9C8E2] transition-all shadow-sm"
            >
              {busy ? "Enviando..." : "Confirmar y Enviar"}
            </button>
          </div>
        </form>
      </div>
    </AppShell>
  );
}
