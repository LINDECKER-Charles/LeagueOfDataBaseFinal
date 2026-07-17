package store

import (
	"context"
	"encoding/json"
	"errors"
	"time"

	"github.com/jackc/pgx/v5"
)

// Profile is the public slice of a users row exposed by /v1/profiles.
type Profile struct {
	ID                 int
	Username           string
	IsPublic           bool
	FavoriteChampionID *string
	FavoriteItemID     *string
	FavoriteRuneID     *string
	FavoriteSummonerID *string
	CreatedAt          time.Time
}

// Build is the public slice of a builds row exposed by /v1/champions/{id}/builds.
type Build struct {
	Name        string
	Description *string
	GameVersion string
	Runes       json.RawMessage
	Steps       json.RawMessage
	ShareToken  string
	CreatedAt   time.Time
}

// ProfileByUsername resolves a user case-insensitively (mirrors the site's
// LOWER(username) unique index, so the lookup uses that index).
func (p *Postgres) ProfileByUsername(ctx context.Context, username string) (Profile, error) {
	var pr Profile
	err := p.pool.QueryRow(ctx,
		`SELECT id, username, is_public_profile, favorite_champion_id, favorite_item_id,
		        favorite_rune_id, favorite_summoner_id, created_at
		   FROM users WHERE LOWER(username) = LOWER($1)`, username,
	).Scan(&pr.ID, &pr.Username, &pr.IsPublic, &pr.FavoriteChampionID, &pr.FavoriteItemID,
		&pr.FavoriteRuneID, &pr.FavoriteSummonerID, &pr.CreatedAt)
	if errors.Is(err, pgx.ErrNoRows) {
		return Profile{}, ErrNotFound
	}
	return pr, err
}

// CountPublicBuilds counts the user's publicly shared builds.
func (p *Postgres) CountPublicBuilds(ctx context.Context, userID int) (int64, error) {
	var count int64
	err := p.pool.QueryRow(ctx,
		`SELECT COUNT(*) FROM builds WHERE owner_id = $1 AND is_public`, userID,
	).Scan(&count)
	return count, err
}

// PublicBuilds pages through the public builds of one champion, newest first.
func (p *Postgres) PublicBuilds(ctx context.Context, championID string, limit, offset int) ([]Build, int64, error) {
	var total int64
	err := p.pool.QueryRow(ctx,
		`SELECT COUNT(*) FROM builds WHERE champion_id = $1 AND is_public`, championID,
	).Scan(&total)
	if err != nil {
		return nil, 0, err
	}

	rows, err := p.pool.Query(ctx,
		`SELECT name, description, game_version, runes, steps, share_token, created_at
		   FROM builds WHERE champion_id = $1 AND is_public
		  ORDER BY created_at DESC, id DESC
		  LIMIT $2 OFFSET $3`, championID, limit, offset)
	if err != nil {
		return nil, 0, err
	}
	defer rows.Close()

	builds := make([]Build, 0, limit)
	for rows.Next() {
		var b Build
		if err := rows.Scan(&b.Name, &b.Description, &b.GameVersion, &b.Runes,
			&b.Steps, &b.ShareToken, &b.CreatedAt); err != nil {
			return nil, 0, err
		}
		builds = append(builds, b)
	}
	return builds, total, rows.Err()
}
