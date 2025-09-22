package fetcher

import (
	"fmt"
	"io"
	"net/http"
)

// Fetch exécute une requête GET sur une URL et retourne le body en string
func Fetch(url string) (string, error) {
	resp, err := http.Get(url)
	if err != nil {
		return "", fmt.Errorf("erreur requête GET: %w", err)
	}
	defer resp.Body.Close()

	// lecture du body
	body, err := io.ReadAll(resp.Body)
	if err != nil {
		return "", fmt.Errorf("erreur lecture body: %w", err)
	}

	return string(body), nil
}
