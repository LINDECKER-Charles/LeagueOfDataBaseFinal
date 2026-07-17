package trends

import (
	"context"
	"errors"
	"testing"
	"time"
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

func (f *fakeDatasets) ReadDataset(_ context.Context, _, _, ddragonType string) ([]byte, error) {
	f.calls++
	payload, ok := f.payloads[ddragonType]
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
