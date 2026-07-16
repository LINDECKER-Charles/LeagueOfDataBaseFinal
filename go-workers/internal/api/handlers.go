package api

import (
	"encoding/base64"
	"encoding/json"
	"net/http"
	"sync"
)

func (s *Server) handleHealth(w http.ResponseWriter, _ *http.Request) {
	writeJSON(w, http.StatusOK, map[string]string{"status": "ok"})
}

func (s *Server) handleVersions(w http.ResponseWriter, r *http.Request) {
	s.proxyGet(w, r, ddragonBase+"/api/versions.json")
}

func (s *Server) handleLanguages(w http.ResponseWriter, r *http.Request) {
	s.proxyGet(w, r, ddragonBase+"/cdn/languages.json")
}

// proxyGet fetches a single DDragon URL and streams the body through unchanged.
func (s *Server) proxyGet(w http.ResponseWriter, r *http.Request, url string) {
	res, err := s.fetcher.Fetch(r.Context(), url)
	if err != nil {
		writeError(w, http.StatusBadGateway, err.Error())
		return
	}
	ct := res.ContentType
	if ct == "" {
		ct = "application/json"
	}
	w.Header().Set("Content-Type", ct)
	w.WriteHeader(res.Status)
	_, _ = w.Write(res.Body)
}

type fetchRequest struct {
	URLs []string `json:"urls"`
}

type fetchItem struct {
	URL         string `json:"url"`
	Status      int    `json:"status"`
	ContentType string `json:"content_type,omitempty"`
	BodyBase64  string `json:"body_base64,omitempty"`
	Error       string `json:"error,omitempty"`
}

type fetchResponse struct {
	Results []fetchItem `json:"results"`
}

// handleFetch retrieves many DDragon URLs concurrently (bounded), preserving order.
// Bodies are base64-encoded so binary (images) and text (JSON) share one contract.
func (s *Server) handleFetch(w http.ResponseWriter, r *http.Request) {
	var req fetchRequest
	if err := json.NewDecoder(http.MaxBytesReader(w, r.Body, 1<<20)).Decode(&req); err != nil {
		writeError(w, http.StatusBadRequest, "invalid JSON body")
		return
	}
	if len(req.URLs) == 0 {
		writeJSON(w, http.StatusOK, fetchResponse{Results: []fetchItem{}})
		return
	}
	if len(req.URLs) > s.maxURLs {
		writeError(w, http.StatusRequestEntityTooLarge, "too many urls")
		return
	}

	results := make([]fetchItem, len(req.URLs))
	sem := make(chan struct{}, s.maxConcurrency)
	var wg sync.WaitGroup

	for i, u := range req.URLs {
		wg.Add(1)
		go func(i int, u string) {
			defer wg.Done()
			sem <- struct{}{}
			defer func() { <-sem }()

			item := fetchItem{URL: u}
			res, err := s.fetcher.Fetch(r.Context(), u)
			if err != nil {
				item.Error = err.Error()
			} else {
				item.Status = res.Status
				item.ContentType = res.ContentType
				item.BodyBase64 = base64.StdEncoding.EncodeToString(res.Body)
			}
			results[i] = item
		}(i, u)
	}

	wg.Wait()
	writeJSON(w, http.StatusOK, fetchResponse{Results: results})
}
