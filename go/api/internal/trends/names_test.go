package trends

import (
	"context"
	"errors"
	"os"
	"path/filepath"
	"testing"
	"time"

	"lodb/go/api/internal/store"
)

type fakeDatasets struct {
	version  string
	payloads map[string][]byte // ddragonType -> JSON
	calls    int
}

func (f *fakeDatasets) LatestDataVersion(context.Context) (string, error) {
	if f.version == "" {
		return "", errors.New("nothing ingested")
	}
	return f.version, nil
}

func (f *fakeDatasets) ReadDataset(_ context.Context, ref store.DatasetRef) ([]byte, error) {
	f.calls++
	payload, ok := f.payloads[ref.Type]
	if !ok {
		return nil, errors.New("absent")
	}
	return payload, nil
}

func TestNamesFromDataMapDataset(t *testing.T) {
	reader := &fakeDatasets{version: "15.1.1", payloads: map[string][]byte{
		"champion": []byte(`{"data":{"Aatrox":{"name":"Aatrox"},"MonkeyKing":{"name":"Wukong"}}}`),
	}}
	resolver := NewStoreNameResolver(reader, time.Minute, nil)
	names := resolver.Names(context.Background(), "champion")
	if names["MonkeyKing"] != "Wukong" || names["Aatrox"] != "Aatrox" {
		t.Fatalf("unexpected names: %v", names)
	}
}

func TestNamesFromRunesDatasetIndexesKeyAndID(t *testing.T) {
	reader := &fakeDatasets{version: "15.1.1", payloads: map[string][]byte{
		"runesReforged": []byte(`[{"id":8100,"key":"Domination","name":"Domination",
			"slots":[{"runes":[{"id":8112,"key":"Electrocute","name":"Electrocute"}]}]}]`),
	}}
	resolver := NewStoreNameResolver(reader, time.Minute, nil)
	names := resolver.Names(context.Background(), "runesReforged")
	for _, key := range []string{"Domination", "8100", "Electrocute", "8112"} {
		if names[key] == "" {
			t.Errorf("missing name for %q in %v", key, names)
		}
	}
}

func TestNamesDegradeToEmptyOnFailure(t *testing.T) {
	resolver := NewStoreNameResolver(&fakeDatasets{}, time.Minute, nil)
	if names := resolver.Names(context.Background(), "champion"); len(names) != 0 {
		t.Fatalf("expected empty map when nothing is ingested, got %v", names)
	}
}

// The Data Dragon dataset shape is walked here AND on the PHP side
// (App\Service\Picker\RuneOptionsProjector, App\Service\Build\RuneTreeIndex).
// The duplication is accepted — go-api never ingests — so these two tests are
// the contract anchor: they run against untrimmed upstream-shaped payloads, so
// a schema change breaks them here instead of silently emptying the rankings.
func loadContractFixture(t *testing.T, name string) []byte {
	t.Helper()
	payload, err := os.ReadFile(filepath.Join("testdata", name))
	if err != nil {
		t.Fatalf("fixture %s: %v", name, err)
	}
	return payload
}

func TestRunesDatasetContractIsHonoured(t *testing.T) {
	reader := &fakeDatasets{version: "15.1.1", payloads: map[string][]byte{
		"runesReforged": loadContractFixture(t, "runesReforged.contract.json"),
	}}
	names := NewStoreNameResolver(reader, time.Minute, nil).
		Names(context.Background(), "runesReforged")

	want := map[string]string{
		"Precision":      "Precision",        // style, by key
		"8000":           "Precision",        // style, by id
		"PressTheAttack": "Press the Attack", // perk in slot 0, by key
		"8005":           "Press the Attack",
		"AbsorbLife":     "Absorb Life", // perk in a later slot: every slot is walked
		"8112":           "Electrocute", // second style is walked too
	}
	for key, wanted := range want {
		if names[key] != wanted {
			t.Errorf("names[%q] = %q, want %q", key, names[key], wanted)
		}
	}
}

func TestDataMapDatasetContractIsHonoured(t *testing.T) {
	reader := &fakeDatasets{version: "15.1.1", payloads: map[string][]byte{
		"champion": loadContractFixture(t, "champion.contract.json"),
	}}
	names := NewStoreNameResolver(reader, time.Minute, nil).
		Names(context.Background(), "champion")

	// The dataset id is the map key, not the "name" — MonkeyKing/Wukong is the
	// canonical proof that the two must not be conflated.
	if names["MonkeyKing"] != "Wukong" || names["Aatrox"] != "Aatrox" {
		t.Fatalf("unexpected names: %v", names)
	}
}

func TestNamesAreCachedPerType(t *testing.T) {
	reader := &fakeDatasets{version: "15.1.1", payloads: map[string][]byte{
		"champion": []byte(`{"data":{"Aatrox":{"name":"Aatrox"}}}`),
	}}
	resolver := NewStoreNameResolver(reader, time.Minute, nil)
	resolver.Names(context.Background(), "champion")
	resolver.Names(context.Background(), "champion")
	if reader.calls != 1 {
		t.Fatalf("expected a single dataset read, got %d", reader.calls)
	}
}
