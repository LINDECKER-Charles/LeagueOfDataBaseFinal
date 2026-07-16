package fetcher

import (
	"testing"
	"time"
)

func TestAllowed(t *testing.T) {
	f := New([]string{"ddragon.leagueoflegends.com"}, time.Second)
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
