// Package fetcher performs guarded HTTP GET requests against an allow-listed set
// of hosts (Riot Data Dragon), returning raw bytes + metadata.
package fetcher

import (
	"context"
	"errors"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"slices"
	"time"
)

// Refusal reasons, as sentinels. The caller classifies with errors.Is, never by
// matching a message: the LEVEL of a log line must not depend on the wording of
// an error, and a reworded message would silently reclassify an event.
var (
	ErrInvalidURL       = errors.New("invalid url")
	ErrSchemeNotAllowed = errors.New("scheme not allowed")
	ErrHostNotAllowed   = errors.New("host not allowed")
	// Deliberately distinct from the three above: they say the caller built a bad
	// URL, this one says an ALLOW-LISTED origin is trying to relay us somewhere
	// else. Same guard, entirely different event.
	ErrRedirectNotAllowed = errors.New("redirect target not allowed")
	ErrBodyTooLarge       = errors.New("response exceeds the body cap")
)

// Result is the outcome of a single fetch.
type Result struct {
	Body        []byte
	ContentType string
	Status      int
}

// Options configures a Fetcher. Grouped in a struct rather than passed
// positionally: the knobs are unrelated to one another and a bare argument list
// would not say which is which at the call site.
type Options struct {
	AllowedHosts []string
	Timeout      time.Duration
	// MaxIdlePerHost sizes the keep-alive connection pool.
	MaxIdlePerHost int
	// MaxBodyBytes caps a single response. Zero falls back to DefaultMaxBodyBytes.
	MaxBodyBytes int64
}

// DefaultMaxBodyBytes bounds one upstream response. The largest artifact we
// legitimately fetch is Data Dragon's championFull.json (single-digit MB), so
// this leaves ample headroom while keeping a hostile or truncated upstream from
// growing the process heap without limit.
const DefaultMaxBodyBytes int64 = 32 << 20 // 32 MiB

// Fetcher issues guarded GET requests with a shared, timeout-bounded client.
type Fetcher struct {
	client       *http.Client
	allowedHosts []string
	maxBodyBytes int64
}

// New builds a Fetcher restricted to opts.AllowedHosts.
//
// DDragon's image CDN offers only HTTP/1.1 (no h2 via ALPN — verified), so every
// request rides its own TCP/TLS connection. http.DefaultTransport caps idle
// connections per host at 2, which would force a fresh TLS handshake for all but
// two of each up-to-MaxConcurrency batch wave. Sizing the idle pool to the fetch
// concurrency lets keep-alive connections be reused across waves instead.
func New(opts Options) *Fetcher {
	transport := http.DefaultTransport.(*http.Transport).Clone()
	if opts.MaxIdlePerHost > 0 {
		transport.MaxIdleConns = opts.MaxIdlePerHost
		transport.MaxIdleConnsPerHost = opts.MaxIdlePerHost
	}
	maxBody := opts.MaxBodyBytes
	if maxBody <= 0 {
		maxBody = DefaultMaxBodyBytes
	}
	f := &Fetcher{
		client:       &http.Client{Timeout: opts.Timeout, Transport: transport},
		allowedHosts: opts.AllowedHosts,
		maxBodyBytes: maxBody,
	}
	// Without this, only the FIRST hop is allow-listed: Go follows up to 10
	// redirects on its own, so an allowed host answering 302 would carry the
	// request anywhere. Re-checking every hop closes that SSRF bypass.
	f.client.CheckRedirect = func(req *http.Request, _ []*http.Request) error {
		if err := f.Allowed(req.URL.String()); err != nil {
			return fmt.Errorf("%w: %w", ErrRedirectNotAllowed, err)
		}
		return nil
	}
	return f
}

// Allowed enforces the SSRF guard: https scheme and an allow-listed host.
func (f *Fetcher) Allowed(raw string) error {
	u, err := url.Parse(raw)
	if err != nil {
		return fmt.Errorf("%w: %w", ErrInvalidURL, err)
	}
	if u.Scheme != "https" {
		return fmt.Errorf("%w: %q", ErrSchemeNotAllowed, u.Scheme)
	}
	if !slices.Contains(f.allowedHosts, u.Hostname()) {
		return fmt.Errorf("%w: %q", ErrHostNotAllowed, u.Hostname())
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

	// Read one byte past the cap so an oversized response is detected rather
	// than silently truncated into a corrupt asset.
	body, err := io.ReadAll(io.LimitReader(resp.Body, f.maxBodyBytes+1))
	if err != nil {
		return Result{}, fmt.Errorf("read body: %w", err)
	}
	if int64(len(body)) > f.maxBodyBytes {
		return Result{}, fmt.Errorf("%w of %d bytes", ErrBodyTooLarge, f.maxBodyBytes)
	}
	return Result{
		Body:        body,
		ContentType: resp.Header.Get("Content-Type"),
		Status:      resp.StatusCode,
	}, nil
}
