package keys

import (
	"sync"
	"time"
)

// Entry is a cached authentication verdict for one key hash. For valid keys it
// also carries the month-to-date request counter, incremented locally so quota
// checks stay off the database between refreshes.
//
// Accepted staleness (documented trade-off): with several concurrent requests,
// or after a restart mid-flush, the local counter can lag the database by a
// small margin, allowing a slight quota overshoot. The counter re-syncs from
// api_usage at every cache refresh (TTL below).
type Entry struct {
	Invalid bool // negative cache: hash known NOT to resolve to a usable key

	// key is written once at construction and never mutated afterwards; the
	// mutable balance lives in creditsBalance below. Keeping it unexported is
	// what makes that invariant enforceable rather than merely documented —
	// callers reach it through the accessors, all of which take the mutex when
	// the value can change.
	key APIKey

	mu             sync.Mutex
	monthlyUsed    int64
	creditsBalance int64
	expiresAt      time.Time
}

// KeyID identifies the API key behind this entry (immutable).
func (e *Entry) KeyID() int { return e.key.ID }

// Usable reports whether the key may serve requests at all (immutable).
func (e *Entry) Usable() bool { return e.key.Usable() }

// RateLimitPerMin is the per-minute allowance of the key (immutable).
func (e *Entry) RateLimitPerMin() int { return e.key.RateLimitPerMin }

// MonthlyQuota is the monthly request allowance of the key (immutable).
func (e *Entry) MonthlyQuota() int64 { return e.key.MonthlyQuota }

// MonthlyUsed returns the locally tracked month-to-date request count.
func (e *Entry) MonthlyUsed() int64 {
	e.mu.Lock()
	defer e.mu.Unlock()
	return e.monthlyUsed
}

// CountRequest adds one request to the local month-to-date counter.
func (e *Entry) CountRequest() {
	e.mu.Lock()
	e.monthlyUsed++
	e.mu.Unlock()
}

// SetCredits updates the cached credit balance after a synchronous decrement.
func (e *Entry) SetCredits(balance int64) {
	e.mu.Lock()
	e.creditsBalance = balance
	e.mu.Unlock()
}

// Credits returns the cached prepaid credit balance.
func (e *Entry) Credits() int64 {
	e.mu.Lock()
	defer e.mu.Unlock()
	return e.creditsBalance
}

// maxEntries bounds the cache. Entries are only ever removed on a *read* of an
// expired key, and a negative entry is by definition never read again — so an
// unauthenticated caller sending random keys would otherwise grow this map
// without limit. Past the ceiling the cache sweeps what has expired and, if it
// is still full of live entries, simply stops caching: the cost is a database
// round-trip, never unbounded memory.
const maxEntries = 50_000

// Cache is a TTL map of key hash -> Entry. Both positive and negative lookups
// are cached so a burst of requests with the same (even unknown) key costs a
// single database round-trip per TTL window.
type Cache struct {
	ttl        time.Duration
	now        func() time.Time
	maxEntries int

	mu      sync.Mutex
	entries map[string]*Entry
}

// NewCache builds a cache; now is injectable for tests (nil -> time.Now).
func NewCache(ttl time.Duration, now func() time.Time) *Cache {
	if now == nil {
		now = time.Now
	}
	return &Cache{ttl: ttl, now: now, maxEntries: maxEntries, entries: make(map[string]*Entry)}
}

// Get returns the live entry for a hash, or nil when absent/expired.
func (c *Cache) Get(hash string) *Entry {
	c.mu.Lock()
	defer c.mu.Unlock()
	entry, ok := c.entries[hash]
	if !ok || c.now().After(entry.expiresAt) {
		delete(c.entries, hash)
		return nil
	}
	return entry
}

// PutValid caches a usable key with its month-to-date usage. The entry is
// returned even when the cache is saturated, so the current request is served
// normally — only the caching is skipped.
func (c *Cache) PutValid(hash string, key APIKey, monthlyUsed int64) *Entry {
	entry := &Entry{
		key:            key,
		monthlyUsed:    monthlyUsed,
		creditsBalance: key.CreditsBalance,
		expiresAt:      c.now().Add(c.ttl),
	}
	c.store(hash, entry)
	return entry
}

// PutInvalid caches a rejection so unknown keys cannot hammer the database.
func (c *Cache) PutInvalid(hash string) {
	c.store(hash, &Entry{Invalid: true, expiresAt: c.now().Add(c.ttl)})
}

// size reports how many entries are currently held.
func (c *Cache) size() int {
	c.mu.Lock()
	defer c.mu.Unlock()
	return len(c.entries)
}

// store inserts under the size ceiling, sweeping expired entries first.
func (c *Cache) store(hash string, entry *Entry) {
	c.mu.Lock()
	defer c.mu.Unlock()

	if _, replacing := c.entries[hash]; !replacing && len(c.entries) >= c.maxEntries {
		c.dropExpiredLocked()
		if len(c.entries) >= c.maxEntries {
			return // saturated with live entries: skip caching rather than grow
		}
	}
	c.entries[hash] = entry
}

// dropExpiredLocked removes every entry past its TTL. Caller holds c.mu.
func (c *Cache) dropExpiredLocked() {
	now := c.now()
	for hash, entry := range c.entries {
		if now.After(entry.expiresAt) {
			delete(c.entries, hash)
		}
	}
}
