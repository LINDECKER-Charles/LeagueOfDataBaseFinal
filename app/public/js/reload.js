export default function initModalScript() {
/*     document.addEventListener('turbo:load', () => { */
        console.log("Script reload.js chargé ✅");
        document.querySelectorAll("script[data-reload]").forEach(oldScript => {
            const newScript = document.createElement("script");
            newScript.src = oldScript.src;
            newScript.dataset.reload = true;
            oldScript.replaceWith(newScript);
        });
/*     }); */
}
