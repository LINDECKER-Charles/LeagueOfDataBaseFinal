package api

import (
	"encoding/json"
	"net/http"
	"time"

	"leagueofdatabase/go-api/internal/store"
)

// buildSharePathPrefix matches the site's build sharing route (/b/{token}).
const buildSharePathPrefix = "/b/"

type profileResponse struct {
	Username     string           `json:"username"`
	CreatedAt    time.Time        `json:"created_at"`
	Favorites    profileFavorites `json:"favorites"`
	PublicBuilds int64            `json:"public_builds"`
}

type profileFavorites struct {
	ChampionID *string `json:"champion_id"`
	ItemID     *string `json:"item_id"`
	RuneID     *string `json:"rune_id"`
	SummonerID *string `json:"summoner_id"`
}

// handleProfile serves GET /v1/profiles/{username}. Private and unknown
// profiles both yield 404 so the endpoint does not reveal account existence.
func (s *Server) handleProfile(w http.ResponseWriter, r *http.Request) {
	username := r.PathValue("username")
	profile, err := s.content.ProfileByUsername(r.Context(), username)
	if isNotFound(err) {
		writeNoPublicProfile(w)
		return
	}
	if err != nil {
		s.storeFailure(w, "profile lookup failed", err)
		return
	}
	if !profile.IsPublic {
		writeNoPublicProfile(w)
		return
	}
	buildCount, err := s.content.CountPublicBuilds(r.Context(), profile.ID)
	if err != nil {
		s.storeFailure(w, "build count failed", err)
		return
	}
	writeJSON(w, http.StatusOK, toProfileResponse(profile, buildCount))
}

// toProfileResponse projects a stored profile onto the public contract.
func toProfileResponse(profile store.Profile, buildCount int64) profileResponse {
	return profileResponse{
		Username:  profile.Username,
		CreatedAt: profile.CreatedAt,
		Favorites: profileFavorites{
			ChampionID: profile.FavoriteChampionID,
			ItemID:     profile.FavoriteItemID,
			RuneID:     profile.FavoriteRuneID,
			SummonerID: profile.FavoriteSummonerID,
		},
		PublicBuilds: buildCount,
	}
}

// writeNoPublicProfile is the single 404 of handleProfile: an unknown account
// and a private one must be indistinguishable from the outside.
func writeNoPublicProfile(w http.ResponseWriter) {
	apiError{
		http.StatusNotFound, CodeNotFound, "no public profile for this username",
	}.write(w)
}

type buildItem struct {
	Name        string          `json:"name"`
	Description *string         `json:"description"`
	GameVersion string          `json:"game_version"`
	Runes       json.RawMessage `json:"runes"`
	Steps       json.RawMessage `json:"steps"`
	ShareURL    string          `json:"share_url"`
	CreatedAt   time.Time       `json:"created_at"`
}

type paginationMeta struct {
	Page       int   `json:"page"`
	PerPage    int   `json:"per_page"`
	Total      int64 `json:"total"`
	TotalPages int64 `json:"total_pages"`
}

type buildsResponse struct {
	ChampionID string         `json:"champion_id"`
	Data       []buildItem    `json:"data"`
	Pagination paginationMeta `json:"pagination"`
}

// handleChampionBuilds serves GET /v1/champions/{championId}/builds.
func (s *Server) handleChampionBuilds(w http.ResponseWriter, r *http.Request) {
	paging, err := ParsePagination(r.URL.Query())
	if err != nil {
		apiError{http.StatusBadRequest, CodeInvalid, err.Error()}.write(w)
		return
	}
	championID := r.PathValue("championId")
	builds, total, err := s.content.PublicBuilds(r.Context(), store.BuildsQuery{
		ChampionID: championID,
		Limit:      paging.PerPage,
		Offset:     paging.Offset(),
	})
	if err != nil {
		s.storeFailure(w, "builds query failed", err)
		return
	}
	writeJSON(w, http.StatusOK, buildsResponse{
		ChampionID: championID,
		Data:       toBuildItems(builds, s.siteBaseURL),
		Pagination: paginationMeta{
			Page: paging.Page, PerPage: paging.PerPage,
			Total: total, TotalPages: paging.TotalPages(total),
		},
	})
}

// toBuildItems projects stored builds onto the public contract. share_url is
// absolute: consumers of this API sit on another origin and cannot resolve a
// site-relative path themselves.
func toBuildItems(builds []store.Build, siteBaseURL string) []buildItem {
	items := make([]buildItem, 0, len(builds))
	for _, b := range builds {
		items = append(items, buildItem{
			Name:        b.Name,
			Description: b.Description,
			GameVersion: b.GameVersion,
			Runes:       b.Runes,
			Steps:       b.Steps,
			ShareURL:    siteBaseURL + buildSharePathPrefix + b.ShareToken,
			CreatedAt:   b.CreatedAt,
		})
	}
	return items
}
