package config

import (
	"os"
	"strconv"
	"strings"
	"time"
)

// Config holds the go-fetcher runtime configuration, sourced from the environment.
type Config struct {
	Host              string
	Port              string
	AllowedHosts      []string
	RequestTimeout    time.Duration
	MaxConcurrency    int
	MaxURLsPerRequest int
}

// Load reads configuration from the environment, applying safe defaults.
func Load() Config {
	return Config{
		Host:              getenv("HOST", "0.0.0.0"),
		Port:              getenv("PORT", "8085"),
		AllowedHosts:      splitCSV(getenv("ALLOWED_HOSTS", "ddragon.leagueoflegends.com")),
		RequestTimeout:    getDuration("REQUEST_TIMEOUT", 15*time.Second),
		MaxConcurrency:    getInt("MAX_CONCURRENCY", 16),
		MaxURLsPerRequest: getInt("MAX_URLS_PER_REQUEST", 512),
	}
}

// Addr returns the host:port listen address.
func (c Config) Addr() string { return c.Host + ":" + c.Port }

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

func getInt(key string, def int) int {
	if v, ok := os.LookupEnv(key); ok {
		if n, err := strconv.Atoi(strings.TrimSpace(v)); err == nil && n > 0 {
			return n
		}
	}
	return def
}

func getDuration(key string, def time.Duration) time.Duration {
	if v, ok := os.LookupEnv(key); ok {
		if d, err := time.ParseDuration(strings.TrimSpace(v)); err == nil && d > 0 {
			return d
		}
	}
	return def
}
