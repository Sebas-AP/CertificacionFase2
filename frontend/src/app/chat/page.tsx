"use client";

import Link from "next/link";
import { useRef, useState } from "react";
import { ApiError, sendChatMessage } from "@/lib/api";
import type { ChatResponse } from "@/lib/contracts";
import { PageHeader } from "@/components/PageHeader";

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
      setMessages((current) => [...current, {
        id: crypto.randomUUID(),
        role: "assistant",
        text: response.reply,
        sources: response.sources,
        createdAt: new Date(),
      }]);
    } catch (cause) {
      setError(cause instanceof ApiError ? cause : new ApiError("No se pudo conectar con el servicio de chat.", 0));
      setMessages((current) => current.filter((entry) => entry.id !== userMessage.id));
      setDraft(message);
      messageRef.current?.focus();
    } finally {
      setPending(false);
    }
  }

  return (
    <>
      <PageHeader
        eyebrow="Consulta con contexto autorizado"
        title="Chat empresarial"
        description="Pregunta sobre la información disponible para Río Claro. Las respuestas y fuentes dependen del servicio configurado en el backend."
      />
      <section className="chat-panel panel" aria-label="Conversación empresarial">
        <div className="chat-intro">
          <div className="chat-avatar" aria-hidden="true">AI</div>
          <div>
            <h2>Asistente de Río Claro</h2>
            <p>Las conversaciones no se guardan y no se envía un identificador de conversación.</p>
          </div>
        </div>
        <div className="chat-messages" role="log" aria-label="Mensajes de la conversación" aria-live="polite" aria-relevant="additions">
          {messages.length === 0 && (
            <div className="chat-empty">
              <h3>¿Qué información necesitas consultar?</h3>
              <p>Escribe una pregunta. El asistente solo responderá si el backend y su agente están disponibles.</p>
            </div>
          )}
          {messages.map((message) => (
            <article key={message.id} className={`chat-message chat-${message.role}`}>
              <div className="message-label">{message.role === "user" ? "Tú" : "Asistente"}</div>
              <p className="preserve-lines">{message.text}</p>
              <time dateTime={message.createdAt.toISOString()}>{new Intl.DateTimeFormat("es-MX", { timeStyle: "short" }).format(message.createdAt)}</time>
              {message.role === "assistant" && (
                message.sources && message.sources.length > 0 ? (
                  <div className="message-sources">
                    <strong>Fuentes proporcionadas por el backend</strong>
                    <ul>{message.sources.map((source, index) => {
                      const href = safeSourceUri(source.uri);
                      return <li key={`${source.title}-${index}`}>{href
                        ? <a href={href} target="_blank" rel="noopener noreferrer">{source.title}</a>
                        : <span>{source.title}</span>}</li>;
                    })}</ul>
                  </div>
                ) : <p className="source-note">El backend no devolvió fuentes para esta respuesta.</p>
              )}
            </article>
          ))}
          {pending && <p className="loading-state" role="status">El asistente está procesando la consulta…</p>}
        </div>

        {error && (
          <div className="alert alert-error" role="alert">
            <div>
              <strong>{error.status === 503 || error.code?.includes("not_configured")
                ? "El asistente de IA todavía no está configurado en este entorno."
                : "No se pudo enviar la consulta."}</strong>
              {error.status !== 503 && <p>{error.message}</p>}
              {error.traceId && <small>Referencia: {error.traceId}</small>}
              {(error.status === 503 || error.code?.includes("not_configured")) && (
                <p><Link className="text-link" href="/estado">Consultar estado del sistema</Link></p>
              )}
            </div>
            <button className="button button-secondary" type="button" onClick={() => void send()} disabled={pending || !draft.trim()}>Volver a intentar</button>
          </div>
        )}

        <form className="chat-composer" onSubmit={(event) => { event.preventDefault(); void send(); }}>
          <label htmlFor="chat-message">Tu pregunta</label>
          <textarea
            id="chat-message"
            ref={messageRef}
            rows={3}
            maxLength={4000}
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === "Enter" && !event.shiftKey) {
                event.preventDefault();
                void send();
              }
            }}
            placeholder="Escribe una pregunta sobre la información empresarial…"
            disabled={pending}
          />
          <div className="composer-footer">
            <span>{draft.length.toLocaleString("es-MX")} / 4,000 · Enter para enviar · Shift+Enter para salto de línea</span>
            <button className="button button-primary" type="submit" disabled={pending || !draft.trim() || draft.length > 4000}>{pending ? "Enviando…" : "Enviar pregunta"}</button>
          </div>
        </form>
      </section>
    </>
  );
}
