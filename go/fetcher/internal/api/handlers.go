package api

import (
	"context"
	"encoding/base64"
	"encoding/json"
	"net/http"
	"sync"
)

func (s *Server) handleHealth(w http.ResponseWriter, _ *http.Request) {
	writeJSON(w, http.StatusOK, map[string]string{"status": "ok"})
}

func (s *Server) handleVersions(w http.ResponseWriter, r *http.Request) {
	s.proxyGet(w, r, s.ddragonBase+"/api/versions.json")
}

func (s *Server) handleLanguages(w http.ResponseWriter, r *http.Request) {
	s.proxyGet(w, r, s.ddragonBase+"/cdn/languages.json")
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

// MaxRequestBodyBytes caps an incoming /fetch payload. This is an inter-service
// contract, not a local tuning knob: the PHP client documents and respects the
// same 1 MiB ceiling (App\Service\Tools\GoFetcherClient).
const MaxRequestBodyBytes int64 = 1 << 20

// handleFetch retrieves many DDragon URLs concurrently (bounded), preserving order.
// Bodies are base64-encoded so binary (images) and text (JSON) share one contract.
func (s *Server) handleFetch(w http.ResponseWriter, r *http.Request) {
	urls, ok := s.acceptedURLs(w, r)
	if !ok {
		return
	}
	writeJSON(w, http.StatusOK, fetchResponse{Results: s.fetchAll(r, urls)})
}

// acceptedURLs decodes and validates the batch payload. It renders the refusal
// itself and reports whether the request may proceed; an empty batch is a valid
// request answered on the spot with an empty result set.
func (s *Server) acceptedURLs(w http.ResponseWriter, r *http.Request) ([]string, bool) {
	var req fetchRequest
	body := http.MaxBytesReader(w, r.Body, MaxRequestBodyBytes)
	if err := json.NewDecoder(body).Decode(&req); err != nil {
		writeError(w, http.StatusBadRequest, "invalid JSON body")
		return nil, false
	}
	if len(req.URLs) == 0 {
		writeJSON(w, http.StatusOK, fetchResponse{Results: []fetchItem{}})
		return nil, false
	}
	if len(req.URLs) > s.maxURLs {
		writeError(w, http.StatusRequestEntityTooLarge, "too many urls")
		return nil, false
	}
	return req.URLs, true
}

// fetchAll runs the batch with at most maxConcurrency in-flight fetches and
// returns the results in request order.
func (s *Server) fetchAll(r *http.Request, urls []string) []fetchItem {
	results := make([]fetchItem, len(urls))
	sem := make(chan struct{}, s.maxConcurrency)
	var wg sync.WaitGroup

	for i, u := range urls {
		wg.Add(1)
		go func(i int, u string) {
			defer wg.Done()
			sem <- struct{}{}
			defer func() { <-sem }()
			results[i] = s.fetchOne(r.Context(), u)
		}(i, u)
	}

	wg.Wait()
	return results
}

// fetchOne never fails: a per-URL error is reported in-band so one bad URL
// cannot sink the whole batch.
func (s *Server) fetchOne(ctx context.Context, url string) fetchItem {
	item := fetchItem{URL: url}
	res, err := s.fetcher.Fetch(ctx, url)
	if err != nil {
		item.Error = err.Error()
		return item
	}
	item.Status = res.Status
	item.ContentType = res.ContentType
	item.BodyBase64 = base64.StdEncoding.EncodeToString(res.Body)
	return item
}
