package config

import (
	"errors"
	"fmt"
	"log"
	"net/url"
	"os"
	"slices"
	"strconv"
	"strings"
	"time"
)

// DefaultDDragonBase is the origin the /versions and /languages passthroughs
// read from. It is a single source of truth: the ALLOWED_HOSTS default is
// derived from it, and Validate refuses to start if the two ever diverge.
const DefaultDDragonBase = "https://ddragon.leagueoflegends.com"

// defaultAllowedHost keeps the allowlist default tied to the passthrough origin.
var defaultAllowedHost = hostOf(DefaultDDragonBase)

// Config holds the go-fetcher runtime configuration, sourced from the environment.
type Config struct {
	Host              string
	Port              string
	AllowedHosts      []string
	DDragonBase       string
	RequestTimeout    time.Duration
	MaxConcurrency    int
	MaxURLsPerRequest int
	// MaxResponseBytes caps a single upstream response; 0 means the fetcher default.
	MaxResponseBytes int64
}

// Load reads configuration from the environment, applying safe defaults.
func Load() Config {
	return Config{
		Host:              getenv("HOST", "0.0.0.0"),
		Port:              getenv("PORT", "8085"),
		AllowedHosts:      splitCSV(getenv("ALLOWED_HOSTS", defaultAllowedHost)),
		DDragonBase:       strings.TrimRight(getenv("DDRAGON_BASE", DefaultDDragonBase), "/"),
		RequestTimeout:    getDuration("REQUEST_TIMEOUT", 15*time.Second),
		MaxConcurrency:    getInt("MAX_CONCURRENCY", 16),
		MaxURLsPerRequest: getInt("MAX_URLS_PER_REQUEST", 512),
		MaxResponseBytes:  int64(getInt("MAX_RESPONSE_BYTES", 0)),
	}
}

// Validate rejects a configuration that would leave the gateway running and
// healthy while answering 502 to everything — an empty allowlist blocks every
// fetch, and a passthrough origin outside the allowlist blocks /versions and
// /languages. Both are operator mistakes that must surface at startup.
func (c Config) Validate() error {
	if len(c.AllowedHosts) == 0 {
		return errors.New("ALLOWED_HOSTS must list at least one host")
	}
	host := hostOf(c.DDragonBase)
	if host == "" {
		return fmt.Errorf("DDRAGON_BASE %q is not an absolute URL", c.DDragonBase)
	}
	if !slices.Contains(c.AllowedHosts, host) {
		return fmt.Errorf("DDRAGON_BASE host %q is absent from ALLOWED_HOSTS %v",
			host, c.AllowedHosts)
	}
	return nil
}

// Addr returns the host:port listen address.
func (c Config) Addr() string { return c.Host + ":" + c.Port }

func hostOf(rawURL string) string {
	u, err := url.Parse(rawURL)
	if err != nil {
		return ""
	}
	return u.Hostname()
}

func getenv(key, def string) string {
	if v, ok := os.LookupEnv(key); ok && v != "" {
		return v
	}
	return def
}

func splitCSV(s string) []string {
	parts := strings.Split(s, ",")
	out := make([]string, 0, len(parts))
	for _, p := range parts {
		if p = strings.TrimSpace(p); p != "" {
			out = append(out, p)
		}
	}
	return out
}

// getInt and getDuration fall back to the default on garbage, but say so: a
// silent fallback turns a typo (a forgotten unit, a stray letter) into a
// mysterious production behaviour nobody can trace back to the environment.
func getInt(key string, def int) int {
	raw, ok := os.LookupEnv(key)
	if !ok || strings.TrimSpace(raw) == "" {
		return def
	}
	n, err := strconv.Atoi(strings.TrimSpace(raw))
	if err != nil || n <= 0 {
		log.Printf("config: ignoring %s=%q (expected a positive integer), using %d", key, raw, def)
		return def
	}
	return n
}

func getDuration(key string, def time.Duration) time.Duration {
	raw, ok := os.LookupEnv(key)
	if !ok || strings.TrimSpace(raw) == "" {
		return def
	}
	d, err := time.ParseDuration(strings.TrimSpace(raw))
	if err != nil || d <= 0 {
		log.Printf("config: ignoring %s=%q (expected a positive duration like 15s), using %v",
			key, raw, def)
		return def
	}
	return d
}
