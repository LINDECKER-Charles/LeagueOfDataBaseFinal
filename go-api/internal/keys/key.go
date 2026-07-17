// Package keys handles API key format validation, hashing and the in-memory
// cache of validated keys.
package keys

import (
	"crypto/sha256"
	"encoding/hex"
	"errors"
	"time"
)

const (
	// RawPrefix + SecretHexLength define the only accepted raw key shape:
	// "lodb_" followed by 40 lowercase hex characters.
	RawPrefix       = "lodb_"
	SecretHexLength = 40
)

// ErrMalformed is returned for anything that is not a well-formed raw key.
// Malformed keys are rejected before any database work.
var ErrMalformed = errors.New("malformed api key")

// APIKey is the database identity of a key, as consumed by the middleware.
type APIKey struct {
	ID              int
	UserID          int
	Plan            string
	MonthlyQuota    int64
	CreditsBalance  int64
	RateLimitPerMin int
	Active          bool
	RevokedAt       *time.Time
}

// Usable reports whether the key may authenticate requests at all.
func (k APIKey) Usable() bool { return k.Active && k.RevokedAt == nil }

// Hash validates the raw key shape and returns the SHA-256 hex digest of the
// FULL raw key (prefix included) — the value stored in api_keys.key_hash.
func Hash(raw string) (string, error) {
	if len(raw) != len(RawPrefix)+SecretHexLength || raw[:len(RawPrefix)] != RawPrefix {
		return "", ErrMalformed
	}
	if !isLowerHex(raw[len(RawPrefix):]) {
		return "", ErrMalformed
	}
	sum := sha256.Sum256([]byte(raw))
	return hex.EncodeToString(sum[:]), nil
}

func isLowerHex(s string) bool {
	for _, c := range []byte(s) {
		isDigit := c >= '0' && c <= '9'
		isHexLetter := c >= 'a' && c <= 'f'
		if !isDigit && !isHexLetter {
			return false
		}
	}
	return true
}
