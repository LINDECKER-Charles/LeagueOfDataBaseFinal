package api

import (
	"errors"
	"net/url"
	"testing"
)

func TestParsePaginationDefaults(t *testing.T) {
	p, err := ParsePagination(url.Values{})
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if p.Page != 1 || p.PerPage != DefaultPerPage {
		t.Fatalf("defaults wrong: %+v", p)
	}
	if p.Offset() != 0 {
		t.Fatalf("offset = %d, want 0", p.Offset())
	}
}

func TestParsePaginationClampsPerPage(t *testing.T) {
	p, err := ParsePagination(url.Values{"per_page": {"999"}, "page": {"3"}})
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if p.PerPage != MaxPerPage {
		t.Fatalf("per_page = %d, want cap %d", p.PerPage, MaxPerPage)
	}
	if p.Offset() != 2*MaxPerPage {
		t.Fatalf("offset = %d, want %d", p.Offset(), 2*MaxPerPage)
	}
}

func TestParsePaginationRejectsGarbage(t *testing.T) {
	for _, values := range []url.Values{
		{"page": {"0"}},
		{"page": {"-2"}},
		{"page": {"abc"}},
		{"per_page": {"0"}},
		{"per_page": {"x"}},
	} {
		if _, err := ParsePagination(values); !errors.Is(err, ErrInvalidPagination) {
			t.Errorf("%v: expected ErrInvalidPagination, got %v", values, err)
		}
	}
}

func TestTotalPages(t *testing.T) {
	p := Pagination{Page: 1, PerPage: 20}
	cases := map[int64]int64{0: 1, 1: 1, 20: 1, 21: 2, 100: 5}
	for total, want := range cases {
		if got := p.TotalPages(total); got != want {
			t.Errorf("TotalPages(%d) = %d, want %d", total, got, want)
		}
	}
}
