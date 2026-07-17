package trends

import (
	"context"
	"encoding/json"
	"strconv"
	"sync"
	"time"
)

// nameResolutionLang is the dataset language used for display names — en_US is
// Data Dragon's universal fallback and is always ingested by the site.
const nameResolutionLang = "en_US"

// runesReforgedType is the only dataset that is a style ARRAY instead of the
// usual {"data": {id: {...}}} envelope.
const runesReforgedType = "runesReforged"

// DatasetReader supplies stored Data Dragon JSON and the newest ingested version.
type DatasetReader interface {
	ReadDataset(ctx context.Context, version, lang, ddragonType string) ([]byte, error)
	LatestDataVersion(ctx context.Context) (string, error)
}

type cachedNames struct {
	names     map[string]string
	expiresAt time.Time
}

// StoreNameResolver resolves entity display names from the datasets the site
// already keeps in MinIO. Purely best-effort: any failure yields an empty map
// (trends then simply omit the "name" field) and is retried after the TTL.
type StoreNameResolver struct {
	reader   DatasetReader
	cacheTTL time.Duration
	now      func() time.Time

	mu    sync.Mutex
	cache map[string]cachedNames
}

// NewStoreNameResolver builds the resolver; now is injectable for tests.
func NewStoreNameResolver(reader DatasetReader, cacheTTL time.Duration, now func() time.Time) *StoreNameResolver {
	if now == nil {
		now = time.Now
	}
	return &StoreNameResolver{reader: reader, cacheTTL: cacheTTL, now: now, cache: make(map[string]cachedNames)}
}

// Names implements NameResolver.
func (r *StoreNameResolver) Names(ctx context.Context, ddragonType string) map[string]string {
	r.mu.Lock()
	c, ok := r.cache[ddragonType]
	r.mu.Unlock()
	if ok && r.now().Before(c.expiresAt) {
		return c.names
	}

	names := r.load(ctx, ddragonType)
	r.mu.Lock()
	r.cache[ddragonType] = cachedNames{names: names, expiresAt: r.now().Add(r.cacheTTL)}
	r.mu.Unlock()
	return names
}

func (r *StoreNameResolver) load(ctx context.Context, ddragonType string) map[string]string {
	version, err := r.reader.LatestDataVersion(ctx)
	if err != nil {
		return map[string]string{}
	}
	payload, err := r.reader.ReadDataset(ctx, version, nameResolutionLang, ddragonType)
	if err != nil {
		return map[string]string{}
	}
	if ddragonType == runesReforgedType {
		return parseRuneNames(payload)
	}
	return parseDataMapNames(payload)
}

// parseDataMapNames handles champion/item/summoner: {"data": {id: {"name": ...}}}.
func parseDataMapNames(payload []byte) map[string]string {
	var doc struct {
		Data map[string]struct {
			Name string `json:"name"`
		} `json:"data"`
	}
	if json.Unmarshal(payload, &doc) != nil {
		return map[string]string{}
	}
	names := make(map[string]string, len(doc.Data))
	for id, entity := range doc.Data {
		if entity.Name != "" {
			names[id] = entity.Name
		}
	}
	return names
}

// parseRuneNames indexes rune styles AND nested runes by both textual key and
// numeric id, since detail pages may be addressed either way.
func parseRuneNames(payload []byte) map[string]string {
	var styles []struct {
		ID    int64  `json:"id"`
		Key   string `json:"key"`
		Name  string `json:"name"`
		Slots []struct {
			Runes []struct {
				ID   int64  `json:"id"`
				Key  string `json:"key"`
				Name string `json:"name"`
			} `json:"runes"`
		} `json:"slots"`
	}
	if json.Unmarshal(payload, &styles) != nil {
		return map[string]string{}
	}
	names := make(map[string]string)
	for _, style := range styles {
		indexName(names, style.ID, style.Key, style.Name)
		for _, slot := range style.Slots {
			for _, perk := range slot.Runes {
				indexName(names, perk.ID, perk.Key, perk.Name)
			}
		}
	}
	return names
}

func indexName(names map[string]string, id int64, key, name string) {
	if name == "" {
		return
	}
	if key != "" {
		names[key] = name
	}
	names[strconv.FormatInt(id, 10)] = name
}
