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

func TestLoadDerivesTheAllowlistDefaultFromTheDDragonBase(t *testing.T) {
	t.Setenv("ALLOWED_HOSTS", "")
	t.Setenv("DDRAGON_BASE", "")
	cfg := Load()
	if cfg.DDragonBase != DefaultDDragonBase {
		t.Fatalf("DDragonBase = %q, want %q", cfg.DDragonBase, DefaultDDragonBase)
	}
	if err := cfg.Validate(); err != nil {
		t.Fatalf("the shipped defaults must validate, got %v", err)
	}
}

func TestValidateRejectsAnEmptyAllowlist(t *testing.T) {
	t.Setenv("ALLOWED_HOSTS", " , ")
	if err := Load().Validate(); err == nil {
		t.Fatal("an allowlist that filters down to nothing blocks every fetch: it must not start")
	}
}

func TestValidateRejectsAPassthroughOutsideTheAllowlist(t *testing.T) {
	t.Setenv("ALLOWED_HOSTS", "raw.communitydragon.org")
	t.Setenv("DDRAGON_BASE", DefaultDDragonBase)
	if err := Load().Validate(); err == nil {
		t.Fatal("/versions and /languages would 502: the mismatch must fail at startup")
	}
}

func TestValidateAcceptsAMirroredPassthrough(t *testing.T) {
	t.Setenv("ALLOWED_HOSTS", "mirror.test")
	t.Setenv("DDRAGON_BASE", "https://mirror.test/")
	cfg := Load()
	if cfg.DDragonBase != "https://mirror.test" {
		t.Errorf("DDragonBase = %q, want the trailing slash trimmed", cfg.DDragonBase)
	}
	if err := cfg.Validate(); err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
}

func TestInvalidNumbersAndDurationsFallBackToDefaults(t *testing.T) {
	t.Setenv("MAX_URLS_PER_REQUEST", "abc")
	t.Setenv("REQUEST_TIMEOUT", "15") // unit forgotten
	cfg := Load()
	if cfg.MaxURLsPerRequest != 512 {
		t.Errorf("MaxURLsPerRequest = %d, want the 512 default", cfg.MaxURLsPerRequest)
	}
	if cfg.RequestTimeout != 15*time.Second {
		t.Errorf("RequestTimeout = %v, want the 15s default", cfg.RequestTimeout)
	}
}
