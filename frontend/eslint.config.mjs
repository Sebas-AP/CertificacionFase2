import tseslint from "typescript-eslint";

export default tseslint.config(
  ...tseslint.configs.recommended,
  {
    ignores: [".next/**", "node_modules/**", "next-env.d.ts", "test-results/**", "playwright-report/**"],
  },
);
