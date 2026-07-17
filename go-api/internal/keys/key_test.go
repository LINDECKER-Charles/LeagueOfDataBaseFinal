package keys

import (
	"crypto/sha256"
	"encoding/hex"
	"errors"
	"strings"
	"testing"
	"time"
)

const validSecret = "0123456789abcdef0123456789abcdef01234567"

func TestHashAcceptsWellFormedKey(t *testing.T) {
	raw := RawPrefix + validSecret
	got, err := Hash(raw)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	sum := sha256.Sum256([]byte(raw))
	if want := hex.EncodeToString(sum[:]); got != want {
		t.Fatalf("hash mismatch: got %s want %s", got, want)
	}
}

func TestHashRejectsMalformedKeys(t *testing.T) {
	cases := map[string]string{
		"empty":          "",
		"wrong prefix":   "lodx_" + validSecret,
		"no prefix":      validSecret,
		"too short":      RawPrefix + validSecret[:39],
		"too long":       RawPrefix + validSecret + "a",
		"uppercase hex":  RawPrefix + strings.ToUpper(validSecret),
		"non-hex secret": RawPrefix + strings.Repeat("g", SecretHexLength),
	}
	for name, raw := range cases {
		if _, err := Hash(raw); !errors.Is(err, ErrMalformed) {
			t.Errorf("%s: expected ErrMalformed, got %v", name, err)
		}
	}
}

func TestCacheExpiresEntries(t *testing.T) {
	now := time.Unix(1_000_000, 0)
	clock := func() time.Time { return now }
	cache := NewCache(60*time.Second, clock)

	cache.PutValid("h1", APIKey{ID: 1, Active: true}, 5)
	if entry := cache.Get("h1"); entry == nil || entry.MonthlyUsed() != 5 {
		t.Fatal("expected live entry with initial usage")
	}
	now = now.Add(61 * time.Second)
	if cache.Get("h1") != nil {
		t.Fatal("expected entry to expire after TTL")
	}
}

func TestCacheNegativeEntriesAndCounters(t *testing.T) {
	cache := NewCache(time.Minute, nil)
	cache.PutInvalid("bad")
	entry := cache.Get("bad")
	if entry == nil || !entry.Invalid {
		t.Fatal("expected negative entry")
	}

	valid := cache.PutValid("good", APIKey{ID: 2, Active: true, CreditsBalance: 3}, 0)
	valid.CountRequest()
	valid.CountRequest()
	valid.SetCredits(2)
	if valid.MonthlyUsed() != 2 || valid.Credits() != 2 {
		t.Fatalf("counter mismatch: used=%d credits=%d", valid.MonthlyUsed(), valid.Credits())
	}
}

func TestUsable(t *testing.T) {
	revoked := time.Now()
	cases := []struct {
		key  APIKey
		want bool
	}{
		{APIKey{Active: true}, true},
		{APIKey{Active: false}, false},
		{APIKey{Active: true, RevokedAt: &revoked}, false},
	}
	for i, c := range cases {
		if c.key.Usable() != c.want {
			t.Errorf("case %d: Usable() = %v, want %v", i, c.key.Usable(), c.want)
		}
	}
}
