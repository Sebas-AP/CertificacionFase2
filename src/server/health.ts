export type HealthResponse = {
  status: "ok";
};

export function getHealthResponse(): HealthResponse {
  return { status: "ok" };
}
