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

	DefaultMinioEndpoint = "http://minio:9000"
	DefaultMinioRegion   = "us-east-1"
	DefaultMinioBucket   = "ddragon"

	// KeyCacheTTL bounds how long a validated (or rejected) API key is served
	// from memory before the database is consulted again.
	KeyCacheTTL = 60 * time.Second
	// TrendsCacheTTL bounds staleness of a computed trends ranking.
	TrendsCacheTTL = 5 * time.Minute
	// NamesCacheTTL bounds staleness of the id -> display-name maps resolved
	// from the Data Dragon datasets stored in MinIO.
	NamesCacheTTL = 30 * time.Minute
	// MeterFlushInterval is the cadence of the batched api_usage upserts.
	MeterFlushInterval = time.Second

	ReadHeaderTimeout = 5 * time.Second
	WriteTimeout      = 30 * time.Second
	IdleTimeout       = 90 * time.Second
	ShutdownTimeout   = 10 * time.Second
)

// Config holds the environment-derived settings.
type Config struct {
	Host           string
	Port           string
	DatabaseURL    string
	MinioEndpoint  string
	MinioRegion    string
	MinioBucket    string
	MinioAccessKey string
	MinioSecretKey string
}

// Load reads configuration from the environment, applying safe defaults that
// match the compose network (postgres/minio service names).
func Load() Config {
	return Config{
		Host:           getenv("HOST", "0.0.0.0"),
		Port:           getenv("PORT", DefaultPort),
		DatabaseURL:    getenv("DATABASE_URL", DefaultDatabaseURL),
		MinioEndpoint:  getenv("MINIO_ENDPOINT", DefaultMinioEndpoint),
		MinioRegion:    getenv("MINIO_REGION", DefaultMinioRegion),
		MinioBucket:    getenv("MINIO_BUCKET", DefaultMinioBucket),
		MinioAccessKey: getenv("MINIO_ACCESS_KEY", ""),
		MinioSecretKey: getenv("MINIO_SECRET_KEY", ""),
	}
}

// Addr returns the host:port listen address.
func (c Config) Addr() string { return c.Host + ":" + c.Port }

// MinioHost returns the endpoint stripped of its scheme (minio-go wants a bare
// host) and whether TLS should be used.
func (c Config) MinioHost() (host string, secure bool) {
	endpoint := c.MinioEndpoint
	switch {
	case strings.HasPrefix(endpoint, "https://"):
		return strings.TrimPrefix(endpoint, "https://"), true
	case strings.HasPrefix(endpoint, "http://"):
		return strings.TrimPrefix(endpoint, "http://"), false
	default:
		return endpoint, false
	}
}

func getenv(key, def string) string {
	if v, ok := os.LookupEnv(key); ok && v != "" {
		return v
	}
	return def
}
