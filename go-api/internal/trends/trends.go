// Package trends turns the per-day analytics aggregates stored in MinIO into
// "most consulted entities" rankings for the public API.
package trends

import (
	"context"
	"encoding/json"
	"log/slog"
	"sort"
	"strconv"
	"strings"
	"sync"
	"time"
)

const (
	// TopN is the ranking depth exposed by /v1/trends.
	TopN       = 25
	dateLayout = "2006-01-02"
)

// rangeWindow binds a public ?range= label to the window length it stands for.
type rangeWindow struct {
	label string
	days  int
}

// supportedRanges is the single owner of the windows /v1/trends accepts, listed
// in the order the API advertises them. The first entry is the default applied
// when the caller omits ?range=.
var supportedRanges = []rangeWindow{{label: "7d", days: 7}, {label: "30d", days: 30}}

// DefaultRange is the ?range= label used when the caller omits the parameter.
func DefaultRange() string { return supportedRanges[0].label }

// RangeDays resolves a ?range= label to its window length in days.
func RangeDays(label string) (int, bool) {
	for _, window := range supportedRanges {
		if window.label == label {
			return window.days, true
		}
	}
	return 0, false
}

// SupportedRanges lists the accepted ?range= labels, in advertised order, so
// callers can build error messages that cannot drift from the real windows.
func SupportedRanges() []string {
	labels := make([]string, 0, len(supportedRanges))
	for _, window := range supportedRanges {
		labels = append(labels, window.label)
	}
	return labels
}

// apiTypes maps the public type segment to the internal analytics/DDragon type
// (the site tracks rune pages under "runesReforged"). Unexported on purpose: it
// is routing state, and an exported package-level map would let any consumer
// rewrite it at runtime. Read it through Top or SupportedTypes.
var apiTypes = map[string]string{
	"champions": "champion",
	"items":     "item",
	"runes":     "runesReforged",
	"summoners": "summoner",
}

// SupportedTypes lists the public /v1/trends type segments, sorted so error
// messages and documentation stay stable.
func SupportedTypes() []string {
	types := make([]string, 0, len(apiTypes))
	for apiType := range apiTypes {
		types = append(types, apiType)
	}
	sort.Strings(types)
	return types
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
	log      *slog.Logger
	now      func() time.Time

	mu    sync.Mutex
	cache map[string]cached
}

// Options groups the Service dependencies. Grouped in a struct rather than
// passed positionally: the knobs are unrelated and a bare argument list would
// not say which is which at the call site.
type Options struct {
	Reader   DailyReader
	Names    NameResolver
	CacheTTL time.Duration
	// Log receives the aggregate-contract warnings (nil -> discarded).
	Log *slog.Logger
	// Now is injectable for tests (nil -> time.Now).
	Now func() time.Time
}

// New builds the service from its dependencies.
func New(opts Options) *Service {
	if opts.Now == nil {
		opts.Now = time.Now
	}
	if opts.Log == nil {
		opts.Log = slog.New(slog.DiscardHandler)
	}
	return &Service{
		reader:   opts.Reader,
		names:    opts.Names,
		cacheTTL: opts.CacheTTL,
		log:      opts.Log,
		now:      opts.Now,
		cache:    make(map[string]cached),
	}
}

// Top returns the ranking for a public API type over the last rangeDays days.
// Missing days are skipped (the site may not have rolled them up yet); a window
// where *nothing* could be decoded is reported, since that is the only signal
// distinguishing "no rollup yet" from a broken aggregate contract.
func (s *Service) Top(ctx context.Context, apiType string, rangeDays int) ([]Entry, bool) {
	ddragonType, ok := apiTypes[apiType]
	if !ok {
		return nil, false
	}
	cacheKey := apiType + ":" + strconv.Itoa(rangeDays)
	if entries, hit := s.fromCache(cacheKey); hit {
		return entries, true
	}
	views, decodedDays := s.mergeDays(ctx, ddragonType, rangeDays)
	if decodedDays == 0 {
		s.log.Warn("no analytics aggregate could be decoded",
			"type", apiType, "range_days", rangeDays)
	}
	entries := s.rank(ctx, views, ddragonType)
	s.store(cacheKey, entries)
	return entries, true
}

// mergeDays folds the requested window's per-day entity counters into one map,
// and reports how many days actually decoded.
//
// Wire contract, owned by the PHP writer: each daily rollup carries an
// "entities" object keyed "{ddragonType}:{entityId}" — see
// App\Service\Analytics\AnalyticsAggregator (encoder) and DailyAggregateStore
// (object path). Changing either side without the other yields empty rankings.
func (s *Service) mergeDays(
	ctx context.Context,
	ddragonType string,
	rangeDays int,
) (views map[string]int64, decodedDays int) {
	views = make(map[string]int64)
	prefix := ddragonType + ":"
	today := s.now().UTC()
	for offset := 0; offset < rangeDays; offset++ {
		payload, err := s.reader.ReadDaily(ctx, today.AddDate(0, 0, -offset).Format(dateLayout))
		if err != nil {
			continue // day absent or unreadable
		}
		var day struct {
			Entities map[string]int64 `json:"entities"`
		}
		if json.Unmarshal(payload, &day) != nil {
			continue
		}
		decodedDays++
		for key, count := range day.Entities {
			if entity, found := strings.CutPrefix(key, prefix); found && entity != "" {
				views[entity] += count
			}
		}
	}
	return views, decodedDays
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
