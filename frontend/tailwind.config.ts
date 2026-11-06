import type { Config } from "tailwindcss";

const config: Config = {
  content: [
    "./src/pages/**/*.{js,ts,jsx,tsx,mdx}",
    "./src/components/**/*.{js,ts,jsx,tsx,mdx}",
    "./src/app/**/*.{js,ts,jsx,tsx,mdx}",
  ],
  theme: {
    extend: {
      colors: {
        // Palette family from DESIGN.md: "Calm navy-and-blue business console"
        brand: {
          navy: "#101E3A",      // Base brand color (Sidebar)
          action: "#2463EB",    // Primary action blue
          sky: "#73B9FC",       // Accent sky blue
          slate: {
            dark: "#18243B",    // Strong text / Headings
            medium: "#718096",  // Secondary text / Muted
            light: "#E6EBF2",   // Borders / Subtle backgrounds
          },
        },
      },
      fontFamily: {
        sans: ["Inter Variable", "Inter", "sans-serif"],
      },
      // Shape language from DESIGN.md
      borderRadius: {
        'control': '8px',    // 8px controls
        'tile': '12px',      // 12px icon tiles
        'xl': '12px',       // General panels
      },
      lineHeight: {
        'relaxed': '1.45',   // 145% line height for UI text
      },
      fontSize: {
        'ui-xs': '11px',
        'ui-sm': '13px',
        'ui-md': '14px',
        'heading-lg': ['32px', { lineHeight: '1.45', fontWeight: '600' }],
        'heading-xl': ['48px', { lineHeight: '1.45', fontWeight: '600' }],
      },
    },
  },
  plugins: [],
};

export default config;
