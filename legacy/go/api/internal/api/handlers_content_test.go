package api

import (
	"context"
	"encoding/json"
	"errors"
	"log/slog"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

	"lodb/go/api/internal/store"
)

func publicProfileAuth() *stubAuth { return &stubAuth{key: usableKey()} }

// A database outage must read as a dependency outage (503), never as an
// application bug (500): supervision and clients tell those two apart.
func TestProfileReportsAStoreOutageAsUnavailable(t *testing.T) {
	handler := testServer(publicProfileAuth(), &stubContent{profileErr: errDatabaseDown},
		&countingMeter{})
	rec := authedGet(handler, "/v1/profiles/someone")
	assertStatus(t, rec, http.StatusServiceUnavailable)
}

func TestBuildCountOutageIsReportedAsUnavailable(t *testing.T) {
	content := &stubContent{
		profile:  store.Profile{ID: 1, Username: "someone", IsPublic: true},
		countErr: errDatabaseDown,
	}
	rec := authedGet(testServer(publicProfileAuth(), content, &countingMeter{}),
		"/v1/profiles/someone")
	assertStatus(t, rec, http.StatusServiceUnavailable)
}

func TestBuildsQueryOutageIsReportedAsUnavailable(t *testing.T) {
	handler := testServer(publicProfileAuth(), &stubContent{buildsErr: errDatabaseDown},
		&countingMeter{})
	rec := authedGet(handler, "/v1/champions/Ahri/builds")
	assertStatus(t, rec, http.StatusServiceUnavailable)
}

// Unknown and private accounts must be indistinguishable from the outside.
func TestProfileHidesUnknownAndPrivateAccountsAlike(t *testing.T) {
	unknown := authedGet(
		testServer(publicProfileAuth(), &stubContent{profileErr: store.ErrNotFound},
			&countingMeter{}),
		"/v1/profiles/ghost",
	)
	private := authedGet(
		testServer(publicProfileAuth(),
			&stubContent{profile: store.Profile{ID: 2, Username: "hidden"}}, &countingMeter{}),
		"/v1/profiles/hidden",
	)
	assertStatus(t, unknown, http.StatusNotFound)
	assertStatus(t, private, http.StatusNotFound)
	if unknown.Body.String() != private.Body.String() {
		t.Fatalf("responses differ: %q vs %q", unknown.Body.String(), private.Body.String())
	}
}

// share_url must be resolvable by a third-party client on another origin.
func TestBuildShareURLIsAbsolute(t *testing.T) {
	content := &stubContent{
		builds: []store.Build{{Name: "Poke Ahri", ShareToken: "tok123"}},
		total:  1,
	}
	rec := authedGet(testServer(publicProfileAuth(), content, &countingMeter{}),
		"/v1/champions/Ahri/builds")
	assertStatus(t, rec, http.StatusOK)

	var body struct {
		Data []struct {
			ShareURL string `json:"share_url"`
		} `json:"data"`
	}
	if err := json.Unmarshal(rec.Body.Bytes(), &body); err != nil {
		t.Fatal(err)
	}
	if want := testSiteBaseURL + "/b/tok123"; body.Data[0].ShareURL != want {
		t.Fatalf("share_url = %q, want %q", body.Data[0].ShareURL, want)
	}
}

// The advertised windows and types must come from the trends package itself, so
// the messages cannot drift from what the service accepts.
func TestTrendsRejectsUnsupportedRangeAndType(t *testing.T) {
	handler := testServer(publicProfileAuth(), &stubContent{}, &countingMeter{})

	badRange := authedGet(handler, "/v1/trends/champions?range=90d")
	assertStatus(t, badRange, http.StatusBadRequest)
	if !strings.Contains(badRange.Body.String(), "range must be 7d or 30d") {
		t.Fatalf("unexpected message: %s", badRange.Body.String())
	}

	badType := authedGet(handler, "/v1/trends/wards")
	assertStatus(t, badType, http.StatusNotFound)
	if !strings.Contains(badType.Body.String(), "champions, items, runes, summoners") {
		t.Fatalf("unexpected message: %s", badType.Body.String())
	}
}

type downPinger struct{}

func (downPinger) Ping(context.Context) error { return errors.New("not mounted") }

// An unmounted storage volume is reported under "storage" while the probe stays
// 200: the API keeps serving what Postgres alone can answer.
func TestHealthReportsAnUnreachableStorageAsDegraded(t *testing.T) {
	handler := NewServer(Deps{
		PGPing:      okPinger{},
		StoragePing: downPinger{},
		Log:         slog.New(slog.DiscardHandler),
	})
	rec := httptest.NewRecorder()
	handler.ServeHTTP(rec, httptest.NewRequest(http.MethodGet, healthPath, nil))

	assertStatus(t, rec, http.StatusOK)
	var body healthResponse
	if err := json.Unmarshal(rec.Body.Bytes(), &body); err != nil {
		t.Fatal(err)
	}
	want := map[string]string{"postgres": depOK, "storage": depDegraded}
	if len(body.Dependencies) != len(want) ||
		body.Dependencies["postgres"] != want["postgres"] ||
		body.Dependencies["storage"] != want["storage"] {
		t.Fatalf("dependencies = %v, want %v", body.Dependencies, want)
	}
}
