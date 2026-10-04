package store

import (
	"context"
	"errors"
	"fmt"
	"io/fs"
	"os"
	"strconv"
	"strings"
	"syscall"
)

const (
	dailyAggregatePrefix = "analytics/daily/"
	dataDir              = "data"
	dataPrefix           = dataDir + "/"
	jsonExtension        = ".json"
)

// Files reads the analytics aggregates and Data Dragon datasets that the PHP
// side writes into the shared storage volume, under the same key layout the
// object store used (data/{version}/{lang}/{type}.json, analytics/daily/...).
// Read-only by contract: PHP is the sole writer and publishes every file
// atomically (temp file + rename), so a read never observes a partial file,
// and go-api mounts the volume read-only.
type Files struct {
	dir  string
	root fs.FS
}

// NewFiles serves the storage rooted at dir. Nothing is touched at
// construction: go-api must start even while the volume is not mounted yet,
// and /healthz reports that state through Ping instead.
func NewFiles(dir string) *Files {
	return &Files{dir: dir, root: os.DirFS(dir)}
}

// Ping reports for /healthz whether the storage root is mounted: it must exist
// and be a directory.
func (f *Files) Ping(ctx context.Context) error {
	if err := ctx.Err(); err != nil {
		return err
	}
	info, err := fs.Stat(f.root, ".")
	if err != nil {
		return err
	}
	if !info.IsDir() {
		return fmt.Errorf("storage root %q is not a directory", f.dir)
	}
	return nil
}

// ReadDaily returns the raw JSON aggregate for a YYYY-MM-DD day, or ErrNotFound
// when that day was never rolled up.
func (f *Files) ReadDaily(ctx context.Context, date string) ([]byte, error) {
	return f.readFile(ctx, dailyAggregatePrefix+date+jsonExtension)
}

// DatasetRef addresses one stored Data Dragon dataset. The three segments are
// the file path itself (data/{version}/{lang}/{type}.json), so they only ever
// make sense together.
type DatasetRef struct {
	Version string
	Lang    string
	Type    string
}

// ReadDataset returns the stored Data Dragon JSON for a dataset address, or
// ErrNotFound when that dataset was never ingested.
func (f *Files) ReadDataset(ctx context.Context, ref DatasetRef) ([]byte, error) {
	// Concatenated on purpose, never path.Join: Join would clean a ".." segment
	// away and turn an escape attempt into a valid path that readFile's
	// fs.ValidPath guard could no longer reject.
	return f.readFile(ctx, dataPrefix+ref.Version+"/"+ref.Lang+"/"+ref.Type+jsonExtension)
}

// readFile maps every "this address holds nothing" outcome to ErrNotFound. A
// name fs.ValidPath rejects (".." or empty segments) is one of them: it must
// neither escape the root nor surface as an I/O failure.
func (f *Files) readFile(ctx context.Context, name string) ([]byte, error) {
	if err := ctx.Err(); err != nil {
		return nil, err
	}
	if !fs.ValidPath(name) {
		return nil, ErrNotFound
	}
	payload, err := fs.ReadFile(f.root, name)
	if isMissing(err) {
		return nil, ErrNotFound
	}
	if err != nil {
		return nil, err
	}
	return payload, nil
}

// isMissing also accepts ENOTDIR: a path crossing a regular file (a/b where a
// is a file) holds nothing either, as the equivalent object key never existed.
func isMissing(err error) bool {
	return errors.Is(err, fs.ErrNotExist) || errors.Is(err, syscall.ENOTDIR)
}

// LatestDataVersion returns the newest version directory under data/
// (numeric-aware ordering), or ErrNotFound when nothing is ingested.
func (f *Files) LatestDataVersion(ctx context.Context) (string, error) {
	if err := ctx.Err(); err != nil {
		return "", err
	}
	entries, err := fs.ReadDir(f.root, dataDir)
	if isMissing(err) {
		return "", ErrNotFound
	}
	if err != nil {
		return "", err
	}
	latest := newestVersionDir(entries)
	if latest == "" {
		return "", ErrNotFound
	}
	return latest, nil
}

// newestVersionDir only considers directories: a stray file under data/ is not
// an ingested version. Returns "" when there is none.
func newestVersionDir(entries []fs.DirEntry) string {
	latest := ""
	for _, entry := range entries {
		if entry.IsDir() && (latest == "" || versionLess(latest, entry.Name())) {
			latest = entry.Name()
		}
	}
	return latest
}

// versionLess orders "15.1.1"-style versions numerically, segment by segment.
func versionLess(a, b string) bool {
	as, bs := strings.Split(a, "."), strings.Split(b, ".")
	for i := 0; i < len(as) && i < len(bs); i++ {
		an, aErr := strconv.Atoi(as[i])
		bn, bErr := strconv.Atoi(bs[i])
		if aErr != nil || bErr != nil {
			if as[i] != bs[i] {
				return as[i] < bs[i]
			}
			continue
		}
		if an != bn {
			return an < bn
		}
	}
	return len(as) < len(bs)
}
