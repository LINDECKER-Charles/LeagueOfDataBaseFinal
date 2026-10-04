console.log('>>> tailwind.config.js CHARGE');
// tailwind.config.js
/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "../templates/**/*.html.twig",           // ← super important en Symfony
    "../assets/**/*.{js,ts,jsx,tsx,vue}",    // ← si tu génères des classes côté JS
    "../src/**/*.{php,twig,js,ts}",          // ← si tu rends côté PHP/Twig ailleurs
  ],
  theme: {
    extend: {
      colors: {
        test: {
          500: "#c9ad78",
        },
        gold: {
          DEFAULT: "#c9ad78"
        },
        black: {
          DEFAULT: "#111111"
        },
        blue: {
          200: "#00a0ba",
          850: "#162033",
          950: "#0a1428",
        },
        white:{
          DEFAULT: "#FAFAFA"
        },
        grey:{
          DEFAULT: "#292929",
          700: "#444444"
        }

      },
      fontFamily: {
        beaufort: ['Beaufort', 'serif'],
        spiegel: ['Spiegel', 'sans-serif'],
      },
    },
  },
  plugins: [],
};
