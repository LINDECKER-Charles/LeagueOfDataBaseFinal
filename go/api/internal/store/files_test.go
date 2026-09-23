package store

import (
	"errors"
	"os"
	"path/filepath"
	"testing"
)

const storedChampions = `{"data":{"Ahri":{"name":"Ahri"}}}`

// writeStored lays a file out under root with the permissions PHP publishes.
func writeStored(t *testing.T, root, name, content string) {
	t.Helper()
	full := filepath.Join(root, filepath.FromSlash(name))
	if err := os.MkdirAll(filepath.Dir(full), 0o755); err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(full, []byte(content), 0o644); err != nil {
		t.Fatal(err)
	}
}

func TestReadDatasetServesTheStoredJSON(t *testing.T) {
	root := t.TempDir()
	writeStored(t, root, "data/15.10.1/en_US/champion.json", storedChampions)

	ref := DatasetRef{Version: "15.10.1", Lang: "en_US", Type: "champion"}
	payload, err := NewFiles(root).ReadDataset(t.Context(), ref)
	if err != nil || string(payload) != storedChampions {
		t.Fatalf("ReadDataset = %q, %v", payload, err)
	}
}

// Every address that holds no dataset inside the root is a plain "not found",
// including one crafted to climb out of it towards a file that does exist.
func TestReadDatasetReportsUnresolvableAddressesAsNotFound(t *testing.T) {
	parent := t.TempDir()
	root := filepath.Join(parent, "storage")
	writeStored(t, root, "data/15.10.1/en_US/champion.json", storedChampions)
	writeStored(t, root, "data/15.9.1", "a stray file, not a version directory")
	writeStored(t, parent, "secret.json", `{"leaked":true}`)

	cases := map[string]DatasetRef{
		"never ingested":         {Version: "15.10.1", Lang: "fr_FR", Type: "champion"},
		"escaping the root":      {Version: "..", Lang: "..", Type: "secret"},
		"empty segment":          {Version: "", Lang: "en_US", Type: "champion"},
		"crossing a stored file": {Version: "15.9.1", Lang: "en_US", Type: "champion"},
	}
	files := NewFiles(root)
	for name, ref := range cases {
		t.Run(name, func(t *testing.T) {
			payload, err := files.ReadDataset(t.Context(), ref)
			if !errors.Is(err, ErrNotFound) {
				t.Fatalf("ReadDataset = %q, %v; want ErrNotFound", payload, err)
			}
		})
	}
}

func TestReadDailyServesARolledUpDay(t *testing.T) {
	root := t.TempDir()
	writeStored(t, root, "analytics/daily/2026-07-15.json", `{"entities":{}}`)

	payload, err := NewFiles(root).ReadDaily(t.Context(), "2026-07-15")
	if err != nil || string(payload) != `{"entities":{}}` {
		t.Fatalf("ReadDaily = %q, %v", payload, err)
	}
}

func TestReadDailyReportsAMissingDayAsNotFound(t *testing.T) {
	root := t.TempDir()
	writeStored(t, root, "analytics/daily/2026-07-15.json", `{"entities":{}}`)

	payload, err := NewFiles(root).ReadDaily(t.Context(), "2026-07-16")
	if !errors.Is(err, ErrNotFound) {
		t.Fatalf("ReadDaily = %q, %v; want ErrNotFound", payload, err)
	}
}

// 15.10.1 must win over 15.9.1 (a lexical sort gets it wrong), and a stray file
// under data/ is never mistaken for a version even when its name sorts higher.
func TestLatestDataVersionPicksTheNewestVersionDirectory(t *testing.T) {
	root := t.TempDir()
	writeStored(t, root, "data/15.9.1/en_US/champion.json", storedChampions)
	writeStored(t, root, "data/15.10.1/en_US/champion.json", storedChampions)
	writeStored(t, root, "data/99.1.1", "a stray file")

	version, err := NewFiles(root).LatestDataVersion(t.Context())
	if err != nil || version != "15.10.1" {
		t.Fatalf("LatestDataVersion = %q, %v; want 15.10.1", version, err)
	}
}

func TestLatestDataVersionWithoutAnyVersionIsNotFound(t *testing.T) {
	withStrayFile := t.TempDir()
	writeStored(t, withStrayFile, "data/readme.txt", "no version here")

	roots := map[string]string{"no data directory": t.TempDir(), "only files": withStrayFile}
	for name, root := range roots {
		t.Run(name, func(t *testing.T) {
			version, err := NewFiles(root).LatestDataVersion(t.Context())
			if !errors.Is(err, ErrNotFound) {
				t.Fatalf("LatestDataVersion = %q, %v; want ErrNotFound", version, err)
			}
		})
	}
}

// The volume may be mounted after go-api started: construction must not
// require it, and Ping must follow its actual state.
func TestPingFollowsTheStorageRootState(t *testing.T) {
	root := filepath.Join(t.TempDir(), "storage")
	files := NewFiles(root)
	if err := files.Ping(t.Context()); err == nil {
		t.Fatal("Ping must fail while the root does not exist")
	}

	if err := os.Mkdir(root, 0o755); err != nil {
		t.Fatal(err)
	}
	if err := files.Ping(t.Context()); err != nil {
		t.Fatalf("Ping on a mounted root = %v, want nil", err)
	}
}

func TestPingRejectsARootThatIsNotADirectory(t *testing.T) {
	parent := t.TempDir()
	writeStored(t, parent, "storage", "a regular file")

	if err := NewFiles(filepath.Join(parent, "storage")).Ping(t.Context()); err == nil {
		t.Fatal("Ping must fail when the root is a regular file")
	}
}
