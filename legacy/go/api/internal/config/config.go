// Package config sources the go-api runtime configuration from the environment,
// mirroring the variable names the compose stack already provides to PHP.
package config

import (
	"os"
	"strings"
	"time"
)

// Defaults and operational constants. Durations that shape the service's
// behaviour live here so no call site carries a magic number.
const (
	DefaultPort        = "8090"
	DefaultDatabaseURL = "postgresql://lodb:lodb@postgres:5432/lodb"

	// DefaultStorageDir is where the shared storage volume is mounted (read-only
	// for go-api): the Data Dragon datasets and analytics aggregates PHP writes.
	DefaultStorageDir = "/srv/storage"

	// DefaultPublicSiteURL is the origin third-party API clients must be sent to
	// when a response carries a link into the website (build sharing). It mirrors
	// seo.canonical_host (app/config/packages/seo.yaml); any environment served
	// from another origin has to override it with PUBLIC_SITE_URL.
	DefaultPublicSiteURL = "https://league-of-data-base.com"

	// KeyCacheTTL bounds how long a validated (or rejected) API key is served
	// from memory before the database is consulted again.
	KeyCacheTTL = 60 * time.Second
	// TrendsCacheTTL bounds staleness of a computed trends ranking.
	TrendsCacheTTL = 5 * time.Minute
	// NamesCacheTTL bounds staleness of the id -> display-name maps resolved
	// from the Data Dragon datasets kept on the storage volume.
	NamesCacheTTL = 30 * time.Minute
	// MeterFlushInterval is the cadence of the batched api_usage upserts.
	MeterFlushInterval = time.Second
	// MeterFlushTimeout is the time budget of one api_usage batch write. It is
	// deliberately independent of the cadence above: retuning how often we flush
	// must not silently retune how long Postgres is allowed to answer.
	MeterFlushTimeout = 5 * time.Second

	ReadHeaderTimeout = 5 * time.Second
	WriteTimeout      = 30 * time.Second
	IdleTimeout       = 90 * time.Second
	ShutdownTimeout   = 10 * time.Second
)

// Config holds the environment-derived settings.
type Config struct {
	Host        string
	Port        string
	DatabaseURL string
	// StorageDir is the root of the storage volume (same layout as PHP's).
	StorageDir string
	// PublicSiteURL is the website origin, without a trailing slash.
	PublicSiteURL string
}

// Load reads configuration from the environment, applying safe defaults that
// match the compose stack (postgres service name, storage volume mount point).
func Load() Config {
	return Config{
		Host:        getenv("HOST", "0.0.0.0"),
		Port:        getenv("PORT", DefaultPort),
		DatabaseURL: getenv("DATABASE_URL", DefaultDatabaseURL),
		StorageDir:  getenv("STORAGE_DIR", DefaultStorageDir),
		// Trimmed here so no call site has to guess whether it must add a slash.
		PublicSiteURL: strings.TrimRight(
			getenv("PUBLIC_SITE_URL", DefaultPublicSiteURL), "/",
		),
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
