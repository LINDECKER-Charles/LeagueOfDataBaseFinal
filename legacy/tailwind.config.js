/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ["./index.html",
    "./src/**/*.{html,js,ts,jsx,tsx}",
    "./src/index.html"
  ],
  theme: {
    extend: {
      colors: {
        test: {
          500: "#c9ad78",
        },
      },
      fontFamily: {
        beaufort: ['Beaufort', 'serif'],
        spiegel: ['Spiegel', 'sans-serif'],
      },
    },
  },
  plugins: [],
};
