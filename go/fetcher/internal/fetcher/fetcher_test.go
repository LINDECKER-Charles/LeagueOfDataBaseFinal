package fetcher

import (
	"context"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
	"time"
)

func TestAllowed(t *testing.T) {
	f := New(Options{
		AllowedHosts:   []string{"ddragon.leagueoflegends.com"},
		Timeout:        time.Second,
		MaxIdlePerHost: 16,
	})
	cases := []struct {
		name    string
		url     string
		wantErr bool
	}{
		{"https allowed host", "https://ddragon.leagueoflegends.com/api/versions.json", false},
		{"http scheme rejected", "http://ddragon.leagueoflegends.com/x", true},
		{"foreign host rejected", "https://evil.example.com/x", true},
		{"ftp scheme rejected", "ftp://ddragon.leagueoflegends.com/x", true},
		{"garbage url rejected", "://nope", true},
	}
	for _, tc := range cases {
		t.Run(tc.name, func(t *testing.T) {
			if err := f.Allowed(tc.url); (err != nil) != tc.wantErr {
				t.Fatalf("Allowed(%q) err=%v, wantErr=%v", tc.url, err, tc.wantErr)
			}
		})
	}
}

// Validating only the first hop would let an allow-listed host bounce the fetch
// anywhere via a 302, so every redirect target goes back through Allowed.
func TestRedirectsAreReCheckedAgainstTheAllowlist(t *testing.T) {
	f := New(Options{AllowedHosts: []string{"ddragon.leagueoflegends.com"}, Timeout: time.Second})
	if f.client.CheckRedirect == nil {
		t.Fatal("client must refuse to follow redirects blindly")
	}

	cases := []struct {
		name    string
		target  string
		wantErr bool
	}{
		{"same allow-listed host", "https://ddragon.leagueoflegends.com/other.json", false},
		{"foreign host", "https://evil.example.com/steal", true},
		{"downgraded to http", "http://ddragon.leagueoflegends.com/x", true},
	}
	for _, tc := range cases {
		t.Run(tc.name, func(t *testing.T) {
			req, err := http.NewRequest(http.MethodGet, tc.target, nil)
			if err != nil {
				t.Fatalf("building request: %v", err)
			}
			if err := f.client.CheckRedirect(req, nil); (err != nil) != tc.wantErr {
				t.Fatalf("CheckRedirect(%q) err=%v, wantErr=%v", tc.target, err, tc.wantErr)
			}
		})
	}
}

func TestFetchRejectsOversizedBody(t *testing.T) {
	const limit = 64
	srv, f := tlsServerAndFetcher(t, strings.Repeat("x", limit+1), limit)
	defer srv.Close()

	if _, err := f.Fetch(context.Background(), srv.URL+"/big"); err == nil {
		t.Fatal("a response above the cap must be refused, not silently truncated")
	}
}

func TestFetchAcceptsBodyAtTheCap(t *testing.T) {
	const limit = 64
	srv, f := tlsServerAndFetcher(t, strings.Repeat("x", limit), limit)
	defer srv.Close()

	res, err := f.Fetch(context.Background(), srv.URL+"/exact")
	if err != nil {
		t.Fatalf("a body of exactly the cap must pass: %v", err)
	}
	if len(res.Body) != limit {
		t.Fatalf("len(body) = %d, want %d", len(res.Body), limit)
	}
}

// Spins a TLS test server (Allowed() requires https) and a Fetcher that trusts
// its throwaway certificate. White-box on purpose: the production constructor
// stays free of any test-only switch.
func tlsServerAndFetcher(t *testing.T, body string, maxBody int64) (*httptest.Server, *Fetcher) {
	t.Helper()
	srv := httptest.NewTLSServer(http.HandlerFunc(func(w http.ResponseWriter, _ *http.Request) {
		if _, err := w.Write([]byte(body)); err != nil {
			t.Errorf("writing test response: %v", err)
		}
	}))

	host := strings.TrimPrefix(srv.URL, "https://")
	f := New(Options{
		AllowedHosts: []string{strings.Split(host, ":")[0]},
		Timeout:      5 * time.Second,
		MaxBodyBytes: maxBody,
	})
	f.client.Transport.(*http.Transport).TLSClientConfig =
		srv.Client().Transport.(*http.Transport).TLSClientConfig

	return srv, f
}
