import { getHealthResponse } from "../../server/health.ts";

export function GET(): Response {
  return Response.json(getHealthResponse(), {
    status: 200,
    headers: { "Cache-Control": "no-store" },
  });
}
