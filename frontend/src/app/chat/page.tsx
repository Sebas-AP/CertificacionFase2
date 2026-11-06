"use client";

import { useRef, useState } from "react";
import {
  IconSend,
  IconRobot,
  IconUser,
  IconLink,
  IconAlertCircle,
  IconRefresh,
  IconSparkles,
} from "@tabler/icons-react";
import { AppShell } from "@/components/AppShell";
import { ApiError, sendChatMessage } from "@/lib/api";
import type { ChatResponse } from "@/lib/contracts";

interface ChatMessage {
  id: string;
  role: "user" | "assistant";
  text: string;
  sources?: ChatResponse["sources"];
  createdAt: Date;
}

function safeSourceUri(value: string | null): string | null {
  if (!value) return null;
  try {
    const url = new URL(value);
    return url.protocol === "https:" || url.protocol === "http:" ? url.toString() : null;
  } catch {
    return null;
  }
}

const panel = "rounded-xl border border-[#E6EBF2] bg-white";

export default function ChatPage() {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [draft, setDraft] = useState("");
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);
  const messageRef = useRef<HTMLTextAreaElement>(null);

  async function send() {
    const message = draft.trim();
    if (!message || pending || message.length > 4000) return;
    setError(null);
    setDraft("");
    setPending(true);

    const userMessage: ChatMessage = {
      id: crypto.randomUUID(),
      role: "user",
      text: message,
      createdAt: new Date(),
    };

    setMessages((current) => [...current, userMessage]);

    try {
      const response = await sendChatMessage(message);
      setMessages((current) => [
        ...current,
        {
          id: crypto.randomUUID(),
          role: "assistant",
          text: response.reply,
          sources: response.sources,
          createdAt: new Date(),
        },
      ]);
    } catch (cause) {
      setError(cause instanceof ApiError ? cause : new ApiError("No se pudo conectar con el servicio de chat.", 0));
      setDraft(message);
    } finally {
      setPending(false);
    }
  }

  return (
    <AppShell crumb="Chat de IA">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <h1 className="text-[28px] font-semibold leading-[1.45] text-[#18243B]">Asistente de Río Claro</h1>
        <div className="flex items-center gap-2 text-xs text-[#718096]">
          <IconRobot size={16} />
          <span>IA basada en contexto empresarial</span>
        </div>
      </div>

      <div className={`${panel} flex h-[calc(100vh-250px)] flex-col overflow-hidden`}>
        <div className="flex items-center justify-between border-b border-[#E6EBF2] bg-[#FAFBFD] px-6 py-4">
          <div className="flex items-center gap-3">
            <div className="flex size-8 items-center justify-center rounded-full bg-[#2463EB] text-white font-bold text-xs">
              AI
            </div>
            <div>
              <div className="text-sm font-semibold text-[#18243B]">Asistente Inteligente</div>
              <div className="text-[10px] text-[#718096]">En línea · Consultas autorizadas</div>
            </div>
          </div>
          <button
            onClick={() => setMessages([])}
            className="text-xs text-[#718096] hover:text-red-500 transition-colors"
          >
            Limpiar chat
          </button>
        </div>

        <div className="flex-1 overflow-y-auto p-6 space-y-6 bg-[#FBFBFC]">
          {messages.length === 0 && (
            <div className="flex h-full flex-col items-center justify-center text-center gap-3">
              <div className="rounded-full bg-[#EDF3FF] p-4 text-[#2463EB]">
                <IconSparkles size={32} stroke={1.5} />
              </div>
              <div className="max-w-sm">
                <h3 className="text-sm font-semibold text-[#18243B]">¿En qué puedo ayudarte hoy?</h3>
                <p className="text-xs text-[#718096] mt-1">
                  Pregunta sobre facturas, reportes o el estado del inventario.
                  El asistente utiliza la información real de tu negocio.
                </p>
              </div>
            </div>
          )}

          {messages.map((message) => (
            <div key={message.id} className={`flex ${message.role === "user" ? "justify-end" : "justify-start"}`}>
              <div className={`flex max-w-[80%] gap-3 ${message.role === "user" ? "flex-row-reverse" : "flex-row"}`}>
                <div
                  className={`flex shrink-0 size-8 items-center justify-center rounded-full text-white text-xs font-bold ${
                    message.role === "user" ? "bg-[#718096]" : "bg-[#2463EB]"
                  }`}
                >
                  {message.role === "user" ? <IconUser size={14} /> : <IconRobot size={14} />}
                </div>

                <div className={`flex flex-col gap-1 ${message.role === "user" ? "items-end" : "items-start"}`}>
                  <div
                    className={`px-4 py-3 rounded-2xl text-xs leading-relaxed ${
                      message.role === "user"
                        ? "bg-[#2463EB] text-white rounded-tr-none"
                        : "bg-white border border-[#E6EBF2] text-[#18243B] rounded-tl-none shadow-sm"
                    }`}
                  >
                    {message.text}
                  </div>
                  <time className="text-[10px] text-[#A9B7CE] px-1">
                    {new Intl.DateTimeFormat("es-MX", { timeStyle: "short" }).format(message.createdAt)}
                  </time>

                  {message.role === "assistant" && message.sources && message.sources.length > 0 && (
                    <div className="mt-2 flex flex-col gap-2">
                      <span className="text-[10px] font-semibold text-[#718096] uppercase tracking-wider">Fuentes:</span>
                      <div className="flex flex-wrap gap-2">
                        {message.sources.map((source, idx) => {
                          const href = safeSourceUri(source.uri);
                          return (
                            <a
                              key={idx}
                              href={href || "#"}
                              target={href ? "_blank" : undefined}
                              rel="noopener noreferrer"
                              className="flex items-center gap-1 rounded-md bg-white border border-[#E6EBF2] px-2 py-1 text-[10px] text-[#2463EB] hover:bg-[#F5F7FB] transition-colors"
                            >
                              <IconLink size={10} />
                              {source.title}
                            </a>
                          );
                        })}
                      </div>
                    </div>
                  )}
                </div>
              </div>
            </div>
          ))}

          {pending && (
            <div className="flex justify-start">
              <div className="flex gap-3">
                <div className="flex size-8 items-center justify-center rounded-full bg-[#2463EB] text-white">
                  <IconRobot size={14} />
                </div>
                <div className="px-4 py-3 rounded-2xl rounded-tl-none bg-white border border-[#E6EBF2] text-xs text-[#718096] italic shadow-sm">
                  El asistente está pensando...
                </div>
              </div>
            </div>
          )}
        </div>

        <div className="border-t border-[#E6EBF2] bg-white p-4">
          {error && (
            <div className="mb-4 flex items-center gap-2 rounded-lg bg-red-50 p-3 text-xs text-red-600 border border-red-100">
              <IconAlertCircle size={16} stroke={1.5} />
              <span>{error.message}</span>
              <button onClick={() => setError(null)} className="ml-auto text-red-400 hover:text-red-600">
                ×
              </button>
            </div>
          )}

          <form
            className="flex items-end gap-3"
            onSubmit={(event) => {
              event.preventDefault();
              void send();
            }}
          >
            <div className="relative flex-1">
              <textarea
                ref={messageRef}
                rows={1}
                maxLength={4000}
                value={draft}
                onChange={(event) => setDraft(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === "Enter" && !event.shiftKey) {
                    event.preventDefault();
                    void send();
                  }
                }}
                placeholder="Escribe tu consulta sobre la empresa..."
                disabled={pending}
                className="w-full resize-none rounded-xl border border-[#E6EBF2] bg-[#F9FAFB] p-3 pr-12 text-xs outline-none focus:border-[#2463EB] focus:ring-1 focus:ring-[#2463EB] transition-all"
                style={{ height: "auto", minHeight: "44px", maxHeight: "200px" }}
              />
              <div className="absolute right-3 bottom-2 text-[10px] text-[#A9B7CE]">{draft.length} / 4000</div>
            </div>
            <button
              type="submit"
              disabled={pending || !draft.trim()}
              className="flex h-[44px] w-[44px] items-center justify-center rounded-xl bg-[#2463EB] text-white disabled:bg-[#B9C8E2] transition-colors shadow-sm hover:bg-[#1d54c9]"
            >
              {pending ? <IconRefresh size={20} className="animate-spin" /> : <IconSend size={20} />}
            </button>
          </form>
        </div>
      </div>
    </AppShell>
  );
}
