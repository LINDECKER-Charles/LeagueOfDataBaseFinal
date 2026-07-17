package trends

import (
	"context"
	"errors"
	"os"
	"path/filepath"
	"testing"
	"time"
)

// fixtureReader serves testdata/{date}.json and counts reads (cache assertions).
type fixtureReader struct {
	reads int
}

func (f *fixtureReader) ReadDaily(_ context.Context, date string) ([]byte, error) {
	f.reads++
	payload, err := os.ReadFile(filepath.Join("testdata", date+".json"))
	if err != nil {
		return nil, errors.New("day absent")
	}
	return payload, nil
}

type staticNames map[string]map[string]string

func (n staticNames) Names(_ context.Context, ddragonType string) map[string]string {
	return n[ddragonType]
}

// fixtureNow pins "today" to the newest fixture day.
func fixtureNow() time.Time {
	return time.Date(2026, 7, 17, 12, 0, 0, 0, time.UTC)
}

func newFixtureService(names NameResolver) (*Service, *fixtureReader) {
	reader := &fixtureReader{}
	if names == nil {
		names = staticNames{}
	}
	return New(reader, names, 5*time.Minute, fixtureNow), reader
}

func TestTopMergesDaysAndRanks(t *testing.T) {
	svc, _ := newFixtureService(staticNames{"champion": {"Aatrox": "Aatrox", "Ahri": "Ahri"}})
	entries, ok := svc.Top(context.Background(), "champions", 7)
	if !ok {
		t.Fatal("champions must be a known type")
	}
	want := []Entry{
		{Rank: 1, ID: "Ahri", Name: "Ahri", Views: 55},
		{Rank: 2, ID: "Aatrox", Name: "Aatrox", Views: 51},
		{Rank: 3, ID: "Zed", Views: 10},
	}
	if len(entries) != len(want) {
		t.Fatalf("got %d entries, want %d: %+v", len(entries), len(want), entries)
	}
	for i, e := range entries {
		if e != want[i] {
			t.Errorf("entry %d = %+v, want %+v", i, e, want[i])
		}
	}
}

func TestTopFiltersByRequestedType(t *testing.T) {
	svc, _ := newFixtureService(nil)
	entries, _ := svc.Top(context.Background(), "items", 7)
	if len(entries) != 2 {
		t.Fatalf("expected 2 items, got %+v", entries)
	}
	// Both items total 9 views (1001: 7+2, 3006: 9) — equal views must be
	// tie-broken deterministically by ascending id.
	if entries[0].ID != "1001" || entries[0].Views != 9 || entries[1].ID != "3006" || entries[1].Views != 9 {
		t.Fatalf("tie-break order wrong: %+v", entries)
	}
}

func TestTopMapsPublicRunesToInternalType(t *testing.T) {
	svc, _ := newFixtureService(nil)
	entries, ok := svc.Top(context.Background(), "runes", 7)
	if !ok || len(entries) != 2 {
		t.Fatalf("expected the 2 runesReforged entities, got ok=%v %+v", ok, entries)
	}
}

func TestTopSkipsMissingAndCorruptDaysSilently(t *testing.T) {
	svc, reader := newFixtureService(nil)
	// 7-day window: 2 valid fixtures, 1 corrupt, 4 absent — all must be attempted.
	if _, ok := svc.Top(context.Background(), "summoners", 7); !ok {
		t.Fatal("summoners must be a known type")
	}
	if reader.reads != 7 {
		t.Fatalf("expected 7 day reads, got %d", reader.reads)
	}
}

func TestTopUnknownTypeRejected(t *testing.T) {
	svc, _ := newFixtureService(nil)
	if _, ok := svc.Top(context.Background(), "wards", 7); ok {
		t.Fatal("unknown type must be rejected")
	}
}

func TestTopCachesPerTypeAndRange(t *testing.T) {
	svc, reader := newFixtureService(nil)
	svc.Top(context.Background(), "champions", 7)
	first := reader.reads
	svc.Top(context.Background(), "champions", 7)
	if reader.reads != first {
		t.Fatal("second call must be served from cache")
	}
	svc.Top(context.Background(), "champions", 30)
	if reader.reads == first {
		t.Fatal("a different range must trigger a fresh computation")
	}
}

func TestTopCapsAtTopN(t *testing.T) {
	views := make(map[string]int64, TopN*2)
	for i := 0; i < TopN*2; i++ {
		views[string(rune('a'+i%26))+string(rune('a'+i/26))] = int64(i)
	}
	svc, _ := newFixtureService(nil)
	entries := svc.rank(context.Background(), views, "champion")
	if len(entries) != TopN {
		t.Fatalf("expected %d entries, got %d", TopN, len(entries))
	}
}
