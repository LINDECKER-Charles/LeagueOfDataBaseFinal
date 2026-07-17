// Package trends turns the per-day analytics aggregates stored in MinIO into
// "most consulted entities" rankings for the public API.
package trends

import (
	"context"
	"encoding/json"
	"sort"
	"strconv"
	"strings"
	"sync"
	"time"
)

const (
	// TopN is the ranking depth exposed by /v1/trends.
	TopN = 25
	// DefaultRangeDays / LongRangeDays are the two supported windows (7d, 30d).
	DefaultRangeDays = 7
	LongRangeDays    = 30
	dateLayout       = "2006-01-02"
)

// APITypes maps the public type segment to the internal analytics/DDragon type
// (the site tracks rune pages under "runesReforged").
var APITypes = map[string]string{
	"champions": "champion",
	"items":     "item",
	"runes":     "runesReforged",
	"summoners": "summoner",
}

// Entry is one ranked entity.
type Entry struct {
	Rank  int    `json:"rank"`
	ID    string `json:"id"`
	Name  string `json:"name,omitempty"`
	Views int64  `json:"views"`
}

// DailyReader supplies the raw JSON aggregate for one YYYY-MM-DD day.
type DailyReader interface {
	ReadDaily(ctx context.Context, date string) ([]byte, error)
}

// NameResolver maps entity ids to display names for a DDragon type.
// Best-effort: an empty map simply omits names from the response.
type NameResolver interface {
	Names(ctx context.Context, ddragonType string) map[string]string
}

type cached struct {
	entries   []Entry
	expiresAt time.Time
}

// Service computes and caches the rankings.
type Service struct {
	reader   DailyReader
	names    NameResolver
	cacheTTL time.Duration
	now      func() time.Time

	mu    sync.Mutex
	cache map[string]cached
}

// New builds the service; now is injectable for tests (nil -> time.Now).
func New(reader DailyReader, names NameResolver, cacheTTL time.Duration, now func() time.Time) *Service {
	if now == nil {
		now = time.Now
	}
	return &Service{reader: reader, names: names, cacheTTL: cacheTTL, now: now, cache: make(map[string]cached)}
}

// Top returns the ranking for a public API type over the last rangeDays days.
// Missing days are skipped silently (the site may not have rolled them up yet).
func (s *Service) Top(ctx context.Context, apiType string, rangeDays int) ([]Entry, bool) {
	ddragonType, ok := APITypes[apiType]
	if !ok {
		return nil, false
	}
	cacheKey := apiType + ":" + strconv.Itoa(rangeDays)
	if entries, hit := s.fromCache(cacheKey); hit {
		return entries, true
	}
	entries := s.rank(ctx, s.mergeDays(ctx, ddragonType, rangeDays), ddragonType)
	s.store(cacheKey, entries)
	return entries, true
}

// mergeDays folds the requested window's per-day entity counters into one map.
func (s *Service) mergeDays(ctx context.Context, ddragonType string, rangeDays int) map[string]int64 {
	views := make(map[string]int64)
	prefix := ddragonType + ":"
	today := s.now().UTC()
	for offset := 0; offset < rangeDays; offset++ {
		payload, err := s.reader.ReadDaily(ctx, today.AddDate(0, 0, -offset).Format(dateLayout))
		if err != nil {
			continue // day absent or unreadable -> skip silently
		}
		var day struct {
			Entities map[string]int64 `json:"entities"`
		}
		if json.Unmarshal(payload, &day) != nil {
			continue
		}
		for key, count := range day.Entities {
			if entity, found := strings.CutPrefix(key, prefix); found && entity != "" {
				views[entity] += count
			}
		}
	}
	return views
}

// rank orders the merged counters, keeps TopN and attaches display names.
func (s *Service) rank(ctx context.Context, views map[string]int64, ddragonType string) []Entry {
	entries := make([]Entry, 0, len(views))
	for id, count := range views {
		entries = append(entries, Entry{ID: id, Views: count})
	}
	sort.Slice(entries, func(i, j int) bool {
		if entries[i].Views != entries[j].Views {
			return entries[i].Views > entries[j].Views
		}
		return entries[i].ID < entries[j].ID
	})
	if len(entries) > TopN {
		entries = entries[:TopN]
	}
	names := s.names.Names(ctx, ddragonType)
	for i := range entries {
		entries[i].Rank = i + 1
		entries[i].Name = names[entries[i].ID]
	}
	return entries
}

func (s *Service) fromCache(key string) ([]Entry, bool) {
	s.mu.Lock()
	defer s.mu.Unlock()
	c, ok := s.cache[key]
	if !ok || s.now().After(c.expiresAt) {
		return nil, false
	}
	return c.entries, true
}

func (s *Service) store(key string, entries []Entry) {
	s.mu.Lock()
	s.cache[key] = cached{entries: entries, expiresAt: s.now().Add(s.cacheTTL)}
	s.mu.Unlock()
}
