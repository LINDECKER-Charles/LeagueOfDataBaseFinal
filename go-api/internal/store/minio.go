package store

import (
	"context"
	"io"
	"sort"
	"strconv"
	"strings"
	"time"

	"github.com/minio/minio-go/v7"
	"github.com/minio/minio-go/v7/pkg/credentials"

	"leagueofdatabase/go-api/internal/config"
)

const (
	dailyAggregatePrefix = "analytics/daily/"
	dataPrefix           = "data/"
	minioPingTimeout     = 2 * time.Second
)

// Minio reads the analytics aggregates and Data Dragon datasets that the PHP
// side writes into the content-addressed bucket. Read-only by contract.
type Minio struct {
	client *minio.Client
	bucket string
}

// NewMinio builds the client from the shared MINIO_* configuration.
func NewMinio(cfg config.Config) (*Minio, error) {
	host, secure := cfg.MinioHost()
	client, err := minio.New(host, &minio.Options{
		Creds:  credentials.NewStaticV4(cfg.MinioAccessKey, cfg.MinioSecretKey, ""),
		Secure: secure,
		Region: cfg.MinioRegion,
	})
	if err != nil {
		return nil, err
	}
	return &Minio{client: client, bucket: cfg.MinioBucket}, nil
}

// Ping reports basic reachability for /healthz (bucket existence check).
func (m *Minio) Ping(ctx context.Context) error {
	ctx, cancel := context.WithTimeout(ctx, minioPingTimeout)
	defer cancel()
	_, err := m.client.BucketExists(ctx, m.bucket)
	return err
}

// ReadDaily returns the raw JSON aggregate for a YYYY-MM-DD day, or ErrNotFound
// when that day was never rolled up.
func (m *Minio) ReadDaily(ctx context.Context, date string) ([]byte, error) {
	return m.readObject(ctx, dailyAggregatePrefix+date+".json")
}

// ReadDataset returns the stored Data Dragon JSON for (version, lang, type).
func (m *Minio) ReadDataset(ctx context.Context, version, lang, ddragonType string) ([]byte, error) {
	return m.readObject(ctx, dataPrefix+version+"/"+lang+"/"+ddragonType+".json")
}

func (m *Minio) readObject(ctx context.Context, key string) ([]byte, error) {
	obj, err := m.client.GetObject(ctx, m.bucket, key, minio.GetObjectOptions{})
	if err != nil {
		return nil, err
	}
	defer obj.Close()
	payload, err := io.ReadAll(obj)
	if err != nil {
		if resp := minio.ToErrorResponse(err); resp.Code == "NoSuchKey" {
			return nil, ErrNotFound
		}
		return nil, err
	}
	return payload, nil
}

// LatestDataVersion lists the version directories under data/ and returns the
// newest one (numeric-aware ordering), or ErrNotFound when nothing is ingested.
func (m *Minio) LatestDataVersion(ctx context.Context) (string, error) {
	versions := make([]string, 0, 16)
	opts := minio.ListObjectsOptions{Prefix: dataPrefix} // non-recursive: common prefixes
	for entry := range m.client.ListObjects(ctx, m.bucket, opts) {
		if entry.Err != nil {
			return "", entry.Err
		}
		name := strings.TrimSuffix(strings.TrimPrefix(entry.Key, dataPrefix), "/")
		if name != "" && strings.HasSuffix(entry.Key, "/") {
			versions = append(versions, name)
		}
	}
	if len(versions) == 0 {
		return "", ErrNotFound
	}
	sort.Slice(versions, func(i, j int) bool { return versionLess(versions[i], versions[j]) })
	return versions[len(versions)-1], nil
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
