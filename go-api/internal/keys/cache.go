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
	Key     APIKey
	Invalid bool // negative cache: hash known NOT to resolve to a usable key

	mu          sync.Mutex
	monthlyUsed int64
	expiresAt   time.Time
}

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
	e.Key.CreditsBalance = balance
	e.mu.Unlock()
}

// Credits returns the cached prepaid credit balance.
func (e *Entry) Credits() int64 {
	e.mu.Lock()
	defer e.mu.Unlock()
	return e.Key.CreditsBalance
}

// Cache is a TTL map of key hash -> Entry. Both positive and negative lookups
// are cached so a burst of requests with the same (even unknown) key costs a
// single database round-trip per TTL window.
type Cache struct {
	ttl time.Duration
	now func() time.Time

	mu      sync.Mutex
	entries map[string]*Entry
}

// NewCache builds a cache; now is injectable for tests (nil -> time.Now).
func NewCache(ttl time.Duration, now func() time.Time) *Cache {
	if now == nil {
		now = time.Now
	}
	return &Cache{ttl: ttl, now: now, entries: make(map[string]*Entry)}
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

// PutValid caches a usable key with its month-to-date usage.
func (c *Cache) PutValid(hash string, key APIKey, monthlyUsed int64) *Entry {
	entry := &Entry{Key: key, monthlyUsed: monthlyUsed, expiresAt: c.now().Add(c.ttl)}
	c.mu.Lock()
	c.entries[hash] = entry
	c.mu.Unlock()
	return entry
}

// PutInvalid caches a rejection so unknown keys cannot hammer the database.
func (c *Cache) PutInvalid(hash string) {
	entry := &Entry{Invalid: true, expiresAt: c.now().Add(c.ttl)}
	c.mu.Lock()
	c.entries[hash] = entry
	c.mu.Unlock()
}
