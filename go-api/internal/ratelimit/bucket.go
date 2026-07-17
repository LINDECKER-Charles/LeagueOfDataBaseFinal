// Package ratelimit implements a per-key in-memory token bucket sized by the
// key's rate_limit_per_min. Single-instance by design: the API runs as one
// container, so no distributed coordination is needed.
package ratelimit

import (
	"math"
	"sync"
	"time"
)

const secondsPerMinute = 60.0

// Verdict carries everything the middleware needs for the X-RateLimit-* headers.
type Verdict struct {
	Allowed   bool
	Limit     int
	Remaining int
	// Reset is when the bucket is full again (allowed) or when the next
	// token becomes available (denied), as a Unix timestamp.
	Reset int64
}

type bucket struct {
	tokens   float64
	lastFill time.Time
}

// Limiter keeps one token bucket per API key id.
type Limiter struct {
	now func() time.Time

	mu      sync.Mutex
	buckets map[int]*bucket
}

// New builds a limiter; now is injectable for tests (nil -> time.Now).
func New(now func() time.Time) *Limiter {
	if now == nil {
		now = time.Now
	}
	return &Limiter{now: now, buckets: make(map[int]*bucket)}
}

// Allow consumes one token from the key's bucket (capacity = perMin, refill
// rate = perMin per minute) and reports the verdict.
func (l *Limiter) Allow(keyID, perMin int) Verdict {
	if perMin <= 0 {
		return Verdict{Allowed: false, Limit: 0, Remaining: 0, Reset: l.now().Unix()}
	}
	l.mu.Lock()
	defer l.mu.Unlock()

	now := l.now()
	b := l.refilled(keyID, perMin, now)
	if b.tokens < 1 {
		return Verdict{Allowed: false, Limit: perMin, Remaining: 0, Reset: l.resetAt(b, perMin, 1, now)}
	}
	b.tokens--
	return Verdict{
		Allowed:   true,
		Limit:     perMin,
		Remaining: int(b.tokens),
		Reset:     l.resetAt(b, perMin, float64(perMin), now),
	}
}

// refilled returns the key's bucket topped up for the elapsed time.
func (l *Limiter) refilled(keyID, perMin int, now time.Time) *bucket {
	b, ok := l.buckets[keyID]
	if !ok {
		b = &bucket{tokens: float64(perMin), lastFill: now}
		l.buckets[keyID] = b
		return b
	}
	refillPerSecond := float64(perMin) / secondsPerMinute
	b.tokens = math.Min(float64(perMin), b.tokens+now.Sub(b.lastFill).Seconds()*refillPerSecond)
	b.lastFill = now
	return b
}

// resetAt computes when the bucket will hold `target` tokens.
func (l *Limiter) resetAt(b *bucket, perMin int, target float64, now time.Time) int64 {
	missing := target - b.tokens
	if missing <= 0 {
		return now.Unix()
	}
	refillPerSecond := float64(perMin) / secondsPerMinute
	return now.Add(time.Duration(math.Ceil(missing/refillPerSecond)) * time.Second).Unix()
}
