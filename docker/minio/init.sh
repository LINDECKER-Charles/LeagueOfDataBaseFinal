#!/bin/sh
# One-shot MinIO bootstrap: waits for the server, creates the bucket, opens public read
# on the content-addressed blobs (served via nginx /cdn/).
set -e

ALIAS=local
ENDPOINT=http://minio:9000
BUCKET="${MINIO_BUCKET:-ddragon}"

until mc alias set "$ALIAS" "$ENDPOINT" "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD" >/dev/null 2>&1; do
    echo "waiting for minio to become ready..."
    sleep 2
done

mc mb --ignore-existing "$ALIAS/$BUCKET"
# Public download (read-only) — blobs are immutable, non-sensitive CDN assets.
mc anonymous set download "$ALIAS/$BUCKET"

echo "minio: bucket '$BUCKET' ready with public read."
