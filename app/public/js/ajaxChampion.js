export default function initAddonScript() {
    console.log("Script ajaxChampion.js chargé ✅");

    const searchInput = document.querySelector('input[name="q"]');
    if (!searchInput) return;

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

        data.forEach(champion => {
            const el = document.createElement('a');
            el.href = `/champion_redirect/${encodeURIComponent(champion.id)}`;
            el.className = "flex flex-col px-4 py-2 hover:bg-blue-900 transition";
            console.log(champion);
            el.innerHTML = `
                <div class="flex items-center gap-3">
                    ${champion.image
                        ? `<img src="/${champion.image}" alt="${champion.name}" class="h-8 w-8 rounded object-cover border border-grey-700">`
                        : `<div class="h-8 w-8 rounded bg-blue-950 border border-grey-700 grid place-items-center text-blue-200/70 text-xs">${champion.name.slice(0,2).toUpperCase()}</div>`
                    }
                    <span class="text-white text-sm">${champion.name}</span>
                </div>
                <span class="ml-11 text-[11px] text-blue-200/60">${champion.id}</span>
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

            fetch(`/api/champions/search/${encodeURIComponent(query)}`)
                .then(res => res.json())
                .then(data => {
                    if (!Array.isArray(data)) throw new Error("Réponse API inattendue");
                    lastResults = data;
                    renderResults(data);
                })
                .catch(err => {
                    console.error("Erreur API Champions:", err);
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
