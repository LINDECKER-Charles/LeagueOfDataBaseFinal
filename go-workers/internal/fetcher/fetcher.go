// Package fetcher performs guarded HTTP GET requests against an allow-listed set
// of hosts (Riot Data Dragon), returning raw bytes + metadata.
package fetcher

import (
	"context"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"slices"
	"time"
)

// Result is the outcome of a single fetch.
type Result struct {
	Body        []byte
	ContentType string
	Status      int
}

// Fetcher issues guarded GET requests with a shared, timeout-bounded client.
type Fetcher struct {
	client       *http.Client
	allowedHosts []string
}

// New builds a Fetcher restricted to allowedHosts with the given per-request
// timeout. maxIdlePerHost sizes the keep-alive connection pool.
//
// DDragon's image CDN offers only HTTP/1.1 (no h2 via ALPN — verified), so every
// request rides its own TCP/TLS connection. http.DefaultTransport caps idle
// connections per host at 2, which would force a fresh TLS handshake for all but
// two of each up-to-MaxConcurrency batch wave. Sizing the idle pool to the fetch
// concurrency lets keep-alive connections be reused across waves instead.
func New(allowedHosts []string, timeout time.Duration, maxIdlePerHost int) *Fetcher {
	transport := http.DefaultTransport.(*http.Transport).Clone()
	if maxIdlePerHost > 0 {
		transport.MaxIdleConns = maxIdlePerHost
		transport.MaxIdleConnsPerHost = maxIdlePerHost
	}
	return &Fetcher{
		client:       &http.Client{Timeout: timeout, Transport: transport},
		allowedHosts: allowedHosts,
	}
}

// Allowed enforces the SSRF guard: https scheme and an allow-listed host.
func (f *Fetcher) Allowed(raw string) error {
	u, err := url.Parse(raw)
	if err != nil {
		return fmt.Errorf("invalid url: %w", err)
	}
	if u.Scheme != "https" {
		return fmt.Errorf("scheme %q not allowed", u.Scheme)
	}
	if !slices.Contains(f.allowedHosts, u.Hostname()) {
		return fmt.Errorf("host %q not allowed", u.Hostname())
	}
	return nil
}

// Fetch retrieves the URL, honoring the context and the SSRF allowlist.
func (f *Fetcher) Fetch(ctx context.Context, raw string) (Result, error) {
	if err := f.Allowed(raw); err != nil {
		return Result{}, err
	}
	req, err := http.NewRequestWithContext(ctx, http.MethodGet, raw, nil)
	if err != nil {
		return Result{}, err
	}
	resp, err := f.client.Do(req)
	if err != nil {
		return Result{}, fmt.Errorf("request failed: %w", err)
	}
	defer resp.Body.Close()

	body, err := io.ReadAll(resp.Body)
	if err != nil {
		return Result{}, fmt.Errorf("read body: %w", err)
	}
	return Result{
		Body:        body,
		ContentType: resp.Header.Get("Content-Type"),
		Status:      resp.StatusCode,
	}, nil
}
