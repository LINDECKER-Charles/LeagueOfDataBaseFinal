export default function initAddonScript() {
    console.log("Script ajaxSummoner.js chargé ✅");
    const searchInput = document.querySelector('input[name="q"]');
    const resultsContainer = document.createElement('div');
    resultsContainer.className = "absolute z-50 bg-blue-850 border border-grey-700 rounded-lg mt-1 w-full max-h-60 overflow-y-auto";
    searchInput.parentNode.appendChild(resultsContainer);

    let debounceTimer;
    let lastResults = [];

    function renderResults(data) {
        resultsContainer.innerHTML = '';

        if (!data.length) {
            resultsContainer.innerHTML = `<div class="px-4 py-2 text-blue-200/70">Aucun résultat</div>`;
            return;
        }

        data.forEach(item => {
            const el = document.createElement('a');
            el.href = `/summoner/${encodeURIComponent(item.id)}`;
            el.className = "flex flex-col px-4 py-2 hover:bg-blue-900 transition";

            el.innerHTML = `
                <div class="flex items-center gap-3">
                    <img src="/${item.image}" alt="${item.name}" class="h-8 w-8 rounded object-contain border border-grey-700">
                    <span class="text-white text-sm">${item.name}</span>
                </div>
                <span class="ml-11 text-[11px] text-blue-200/60">${item.id}</span>
            `;

            resultsContainer.appendChild(el);
        });
    }

    searchInput.addEventListener('input', () => {
        const query = searchInput.value.trim();

        clearTimeout(debounceTimer);
        debounceTimer = setTimeout(() => {
            if (query.length < 2) {
                resultsContainer.innerHTML = '';
                lastResults = [];
                return;
            }

            fetch(`/api/summoners/search/${encodeURIComponent(query)}`)
                .then(res => res.json())
                .then(data => {
                    lastResults = data;
                    renderResults(data);
                })
                .catch(err => {
                    console.error("Erreur API:", err);
                    resultsContainer.innerHTML = `<div class="px-4 py-2 text-red-400">Erreur de recherche</div>`;
                });
        }, 300);
    });

    // Masquer les résultats quand l'input perd le focus
    searchInput.addEventListener('blur', () => {
        setTimeout(() => {
            resultsContainer.innerHTML = '';
        }, 150);
    });

    // Réafficher les résultats si on reclique dans l'input
    searchInput.addEventListener('focus', () => {
        if (lastResults.length && searchInput.value.trim().length >= 2) {
            renderResults(lastResults);
        }
    });
}