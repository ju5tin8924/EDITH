/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        background: '#0B0F19',
        surface: {
          DEFAULT: '#131B2E',
          hover: '#1B253D',
          muted: '#0F1626'
        },
        border: {
          DEFAULT: '#1E293B',
          light: '#334155'
        },
        primary: {
          DEFAULT: '#06B6D4', // Cyan
          hover: '#0891B2',
          light: '#22D3EE'
        },
        accent: {
          DEFAULT: '#10B981', // Emerald
          hover: '#059669'
        },
        critical: '#EF4444',
        warning: '#F59E0B'
      }
    },
  },
  plugins: [],
}
