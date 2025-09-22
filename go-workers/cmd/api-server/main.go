package main

import (
	"fmt"
	"log"
	"net/http"
	"go-workers/internal/fetcher"
	"go-workers/internal/api"
)


/* https://ddragon.leagueoflegends.com/api/versions.json */


func main() {
	// Route simple qui renvoie du texte
	http.HandleFunc("/process", func(w http.ResponseWriter, r *http.Request) {
		fmt.Fprintln(w, "✅ Hello depuis ton serveur Go !")
	})

	http.HandleFunc("/versions", func(w http.ResponseWriter, r *http.Request) {
		data, err := fetcher.Fetch("https://ddragon.leagueoflegends.com/api/versions.json")
		if err != nil {
			http.Error(w, fmt.Sprintf("Erreur fetch: %v", err), http.StatusInternalServerError)
			return
		}

		w.Header().Set("Content-Type", "application/json")
		w.Write([]byte(data))
	})


	http.HandleFunc("/multi-fetch", func(w http.ResponseWriter, r *http.Request) {
		// n'accepte que POST
		if r.Method != http.MethodPost {
			http.Error(w, "Méthode non autorisée", http.StatusMethodNotAllowed)
			return
		}
		// appelle ton Handler
		api.Handler(w, r)
	})

	port := "127.0.0.1:8085"
	fmt.Println("🚀 API Server listening on", port)
	if err := http.ListenAndServe(port, nil); err != nil {
		log.Fatal(err)
	}
}
