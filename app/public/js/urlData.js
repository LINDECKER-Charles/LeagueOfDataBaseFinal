export default function initModalScript(lang, ver) {
    document.addEventListener('turbo:load', () => {
        console.log("Script urlData.js chargé ✅");
        const params = new URLSearchParams(window.location.search);
        params.set('version', ver);
        params.set('lang', lang);
        window.history.replaceState({}, '', `${window.location.pathname}?${params}`);
    });
}
/*     <script type="module">
      import initAddonScript from '{{ asset("js/reload.js") }}';
      initAddonScript();
    </script> */
