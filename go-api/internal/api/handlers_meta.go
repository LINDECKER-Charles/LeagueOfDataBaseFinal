package api

import (
	"context"
	"net/http"

	"leagueofdatabase/go-api/internal/trends"
)

// Supported ?range= values for /v1/trends.
const (
	rangeWeek  = "7d"
	rangeMonth = "30d"
)

type trendsResponse struct {
	Type    string         `json:"type"`
	Range   string         `json:"range"`
	Entries []trends.Entry `json:"entries"`
}

// handleTrends serves GET /v1/trends/{type} with type in champions|items|runes|summoners.
func (s *Server) handleTrends(w http.ResponseWriter, r *http.Request) {
	rangeParam := r.URL.Query().Get("range")
	if rangeParam == "" {
		rangeParam = rangeWeek
	}
	rangeDays, ok := map[string]int{rangeWeek: trends.DefaultRangeDays, rangeMonth: trends.LongRangeDays}[rangeParam]
	if !ok {
		writeError(w, http.StatusBadRequest, CodeInvalid, "range must be 7d or 30d")
		return
	}
	trendType := r.PathValue("type")
	entries, known := s.trends.Top(r.Context(), trendType, rangeDays)
	if !known {
		writeError(w, http.StatusNotFound, CodeNotFound, "type must be one of champions, items, runes, summoners")
		return
	}
	writeJSON(w, http.StatusOK, trendsResponse{Type: trendType, Range: rangeParam, Entries: entries})
}

type usageResponse struct {
	Plan               string `json:"plan"`
	MonthlyQuota       int64  `json:"monthly_quota"`
	UsedThisMonth      int64  `json:"used_this_month"`
	RemainingThisMonth int64  `json:"remaining_this_month"`
	CreditsBalance     int64  `json:"credits_balance"`
	RateLimitPerMin    int    `json:"rate_limit_per_min"`
}

// handleUsage serves GET /v1/usage — authenticated and rate-limited but never
// quota-charged, so an exhausted key can still inspect its consumption.
func (s *Server) handleUsage(w http.ResponseWriter, r *http.Request) {
	entry := entryFromContext(r.Context())
	if entry == nil {
		writeInternal(w)
		return
	}
	snapshot, err := s.auth.UsageSnapshot(r.Context(), entry.Key.ID)
	if err != nil {
		s.log.Error("usage snapshot failed", "error", err, "api_key_id", entry.Key.ID)
		writeUnavailable(w)
		return
	}
	remaining := snapshot.MonthlyQuota - snapshot.UsedThisMonth
	if remaining < 0 {
		remaining = 0
	}
	writeJSON(w, http.StatusOK, usageResponse{
		Plan:               snapshot.Plan,
		MonthlyQuota:       snapshot.MonthlyQuota,
		UsedThisMonth:      snapshot.UsedThisMonth,
		RemainingThisMonth: remaining,
		CreditsBalance:     snapshot.CreditsBalance,
		RateLimitPerMin:    snapshot.RateLimitPerMin,
	})
}

const (
	depOK       = "ok"
	depDegraded = "degraded"
)

type healthResponse struct {
	Status       string            `json:"status"`
	Dependencies map[string]string `json:"dependencies"`
}

// handleHealth serves GET /healthz without auth. Always 200: a degraded
// dependency is reported in the body, not via the status code, so container
// orchestration keeps the API up while it can still serve partially.
func (s *Server) handleHealth(w http.ResponseWriter, r *http.Request) {
	writeJSON(w, http.StatusOK, healthResponse{
		Status: "ok",
		Dependencies: map[string]string{
			"postgres": dependencyState(r.Context(), s.pgPing),
			"minio":    dependencyState(r.Context(), s.s3Ping),
		},
	})
}

func dependencyState(ctx context.Context, p Pinger) string {
	if err := p.Ping(ctx); err != nil {
		return depDegraded
	}
	return depOK
}
