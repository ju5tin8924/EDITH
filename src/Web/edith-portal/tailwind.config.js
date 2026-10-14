/** @type {import('tailwindcss').Config} */
// Investment Indigo AML Terminal — tokens from the Stitch "EDITH Design System" project.
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        background: '#f4f6fc', // canvas
        'canvas-subtle': '#eaedfa',
        surface: {
          DEFAULT: '#ffffff',
          hover: '#eef1fb',
          muted: '#faf8ff'
        },
        border: {
          DEFAULT: '#c5cae9',
          light: '#9fa8da'
        },
        primary: {
          DEFAULT: '#1a237e', // deep navy shell
          hover: '#283593',
          light: '#3949ab',
          container: '#e8eaf6'
        },
        secondary: '#3949ab',
        tertiary: '#5c6bc0',
        ink: '#0d1333',
        muted: '#5c6bc0',
        disabled: '#9fa8da',
        accent: {
          DEFAULT: '#059669', // signal-success
          hover: '#047857'
        },
        critical: '#dc2626',
        warning: '#d97706',
        info: '#2563eb'
      },
      borderRadius: {
        DEFAULT: '0.125rem',
        md: '0.25rem',
        lg: '0.25rem',
        xl: '0.5rem'
      },
      fontFamily: {
        sans: ['Inter', 'system-ui', 'sans-serif'],
        mono: ['"JetBrains Mono"', 'monospace']
      }
    },
  },
  plugins: [],
}
