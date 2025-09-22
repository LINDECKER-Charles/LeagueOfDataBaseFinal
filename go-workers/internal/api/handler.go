package api

import (
	"encoding/json"
	"net/http"
	"sync"

	"go-workers/internal/fetcher"
)

// Handler lit un JSON avec des URLs, fait les requêtes en parallèle, renvoie les résultats
func Handler(w http.ResponseWriter, r *http.Request) {
	// 1. décoder le JSON reçu
	var urls []string
	if err := json.NewDecoder(r.Body).Decode(&urls); err != nil {
		http.Error(w, "JSON invalide", http.StatusBadRequest)
		return
	}

	// 2. préparer les résultats
	results := make([]string, len(urls))
	var wg sync.WaitGroup
	wg.Add(len(urls))

	// 3. lancer les fetch en parallèle
	for i, url := range urls {
		go func(i int, url string) {
			defer wg.Done()
			data, err := fetcher.Fetch(url)
			if err != nil {
				results[i] = "Erreur: " + err.Error()
				return
			}
			results[i] = data
		}(i, url)
	}

	// 4. attendre la fin de toutes les goroutines
	wg.Wait()

	// 5. renvoyer la réponse en JSON
	w.Header().Set("Content-Type", "application/json")
	json.NewEncoder(w).Encode(results)
}
