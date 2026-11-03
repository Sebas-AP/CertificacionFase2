# ClaroGestion AI

Demostración académica para Río Claro: revisión de facturas, indicadores
gerenciales y consulta empresarial. La interfaz Next.js consume exclusivamente
la API REST de `backend/`; no conecta el navegador directamente con Azure.

## Requisitos

- Node.js 22.6 o posterior (el proyecto se validó con Node.js 26).
- npm.
- .NET SDK 8 para ejecutar el backend.

## Ejecutar la aplicación

1. Configura y ejecuta el backend siguiendo [backend/README.md](./backend/README.md).
   La API local documentada escucha en `http://localhost:5000` salvo que
   Kestrel indique otro puerto.
2. Desde `frontend/`, instala dependencias y configura el origen de API:

   ```sh
   npm ci
   cp .env.example .env.local
   npm run dev
   ```

3. Abre `http://localhost:3000`. La raíz redirige directamente al dashboard;
   no hay login, registro ni selección de organización.

`NEXT_PUBLIC_API_BASE_URL` es una URL pública del backend, no un secreto. No
configures claves, tokens ni credenciales de Azure en el frontend.

## Demo y límites conocidos

- La demo usa la identidad fija y la organización Río Claro que asigna el
  backend. No debe utilizarse con datos reales ni exponerse como aplicación
  productiva.
- Facturas, revisión, reportes, chat y estado consumen endpoints reales. Si la
  API o una integración no está disponible, la interfaz informa el error o el
  estado de configuración; no sustituye respuestas por fixtures.
- Foundry todavía puede no estar configurado. En ese caso el chat y la
  explicación de facturas muestran el estado devuelto por la API.
- El contrato actual no incluye descarga/preview seguro de PDF, búsqueda y
  filtrado de facturas por fechas/proveedor, revisión de coincidencias de
  duplicados, series temporales ni exportación CSV. La interfaz lo hace
  explícito y no simula esas operaciones.
- El backend crea solo la organización y el usuario de demostración; no
  agrega facturas ni indicadores ficticios.

## Validaciones

Desde `frontend/`:

```sh
npm run lint
npm run typecheck
npm test
npm run test:e2e
npm run build
```

La primera ejecución E2E requiere Chromium; instálalo con
`npx playwright install chromium` desde `frontend/`. Las pruebas usan
respuestas ficticias interceptadas por Playwright y no requieren Azure ni el
backend.

Consulta [frontend/FRONTEND-IMPLEMENTATION-STATUS.md](./frontend/FRONTEND-IMPLEMENTATION-STATUS.md)
para los contratos conectados, limitaciones y resultados de validación.
