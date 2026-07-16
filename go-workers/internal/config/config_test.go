package config

import (
	"testing"
	"time"
)

func TestLoadDefaults(t *testing.T) {
	t.Setenv("HOST", "")
	t.Setenv("PORT", "")
	t.Setenv("ALLOWED_HOSTS", "")
	cfg := Load()
	if cfg.Addr() != "0.0.0.0:8085" {
		t.Errorf("Addr = %q, want 0.0.0.0:8085", cfg.Addr())
	}
	if len(cfg.AllowedHosts) != 1 || cfg.AllowedHosts[0] != "ddragon.leagueoflegends.com" {
		t.Errorf("AllowedHosts = %v", cfg.AllowedHosts)
	}
}

func TestLoadFromEnv(t *testing.T) {
	t.Setenv("HOST", "127.0.0.1")
	t.Setenv("PORT", "9090")
	t.Setenv("ALLOWED_HOSTS", "a.com, b.com ,")
	t.Setenv("MAX_CONCURRENCY", "32")
	t.Setenv("REQUEST_TIMEOUT", "5s")
	cfg := Load()
	if cfg.Addr() != "127.0.0.1:9090" {
		t.Errorf("Addr = %q", cfg.Addr())
	}
	if len(cfg.AllowedHosts) != 2 {
		t.Errorf("AllowedHosts = %v, want 2 entries", cfg.AllowedHosts)
	}
	if cfg.MaxConcurrency != 32 {
		t.Errorf("MaxConcurrency = %d, want 32", cfg.MaxConcurrency)
	}
	if cfg.RequestTimeout != 5*time.Second {
		t.Errorf("RequestTimeout = %v, want 5s", cfg.RequestTimeout)
	}
}
