package api

import (
	"context"
	"encoding/base64"
	"encoding/json"
	"errors"
	"log/slog"
	"net/http"
	"net/url"
	"slices"
	"sync"

	"lodb/go/fetcher/internal/fetcher"
)

// maxLoggedHosts bounds the host sample of a refusal line: a hostile batch must
// not be able to size a log record.
const maxLoggedHosts = 5

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
	results, failures := s.fetchAll(r, urls)
	s.reportBatch(r.Context(), urls, failures)
	writeJSON(w, http.StatusOK, fetchResponse{Results: results})
}

// reportBatch emits AT MOST one line per batch, never one per URL: a cold
// champion list is a few hundred images in a single call, and a line each would
// amplify the hot path into the log store.
//
// The classification is by sentinel (errors.Is), never by matching a message:
// the level of an event must not hinge on how an error happens to be worded.
func (s *Server) reportBatch(ctx context.Context, urls []string, failures []error) {
	var relayed, refused, failed int
	hosts := make([]string, 0, maxLoggedHosts)

	for i, err := range failures {
		switch {
		case err == nil:
			continue
		case errors.Is(err, fetcher.ErrRedirectNotAllowed):
			// An ALLOW-LISTED origin answering a redirect somewhere else. Checked
			// first: it also wraps ErrHostNotAllowed, and it is not the same event.
			relayed++
		case isAllowlistRefusal(err):
			refused++
			hosts = appendHost(hosts, urls[i])
		default:
			failed++
		}
	}

	// Error, not warning: either the caller is building URLs it must never build,
	// or somebody is probing the gateway as an open proxy. Both must light up the
	// dashboard — and the refusal is rendered IN-BAND inside an HTTP 200, so
	// nothing outside the response body would otherwise ever see it.
	if refused > 0 {
		s.log.LogAttrs(ctx, slog.LevelError, "fetch.allowlist.refused",
			slog.Int("refused", refused),
			slog.Int("batch", len(urls)),
			slog.Any("hosts", hosts),
		)
	}
	if relayed > 0 {
		s.log.LogAttrs(ctx, slog.LevelError, "fetch.redirect.refused",
			slog.Int("refused", relayed),
			slog.Int("batch", len(urls)),
		)
	}
	if failed > 0 {
		s.log.LogAttrs(ctx, slog.LevelWarn, "fetch.batch.degraded",
			slog.Int("failed", failed),
			slog.Int("batch", len(urls)),
		)
	}
}

// isAllowlistRefusal reports a URL the caller should never have built: bad
// syntax, wrong scheme, or a host outside the allowlist.
func isAllowlistRefusal(err error) bool {
	return errors.Is(err, fetcher.ErrHostNotAllowed) ||
		errors.Is(err, fetcher.ErrSchemeNotAllowed) ||
		errors.Is(err, fetcher.ErrInvalidURL)
}

// appendHost keeps a bounded, de-duplicated, sanitised sample of refused hosts —
// enough to tell a PHP bug apart from a probe, small enough to stay one line.
func appendHost(hosts []string, raw string) []string {
	if len(hosts) >= maxLoggedHosts {
		return hosts
	}
	u, err := url.Parse(raw)
	if err != nil || u.Hostname() == "" {
		return hosts
	}
	host := safeValue(u.Hostname())
	if slices.Contains(hosts, host) {
		return hosts
	}
	return append(hosts, host)
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
// returns the results in request order, alongside the TYPED failure of each one
// (nil when it succeeded) — the response shape only carries a message string, and
// a level must never be derived from a string.
func (s *Server) fetchAll(r *http.Request, urls []string) ([]fetchItem, []error) {
	results := make([]fetchItem, len(urls))
	failures := make([]error, len(urls))
	sem := make(chan struct{}, s.maxConcurrency)
	var wg sync.WaitGroup

	for i, u := range urls {
		wg.Add(1)
		go func(i int, u string) {
			defer wg.Done()
			sem <- struct{}{}
			defer func() { <-sem }()
			results[i], failures[i] = s.fetchOne(r.Context(), u)
		}(i, u)
	}

	wg.Wait()
	return results, failures
}

// fetchOne never fails the batch: a per-URL error is reported in-band so one bad
// URL cannot sink the whole call. It is returned as well so the caller can
// classify it — the in-band copy is a message, which is not a classification.
func (s *Server) fetchOne(ctx context.Context, url string) (fetchItem, error) {
	item := fetchItem{URL: url}
	res, err := s.fetcher.Fetch(ctx, url)
	if err != nil {
		item.Error = err.Error()
		return item, err
	}
	item.Status = res.Status
	item.ContentType = res.ContentType
	item.BodyBase64 = base64.StdEncoding.EncodeToString(res.Body)
	return item, nil
}
