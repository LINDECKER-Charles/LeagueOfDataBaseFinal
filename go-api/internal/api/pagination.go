package api

import (
	"errors"
	"net/url"
	"strconv"
)

const (
	// DefaultPerPage / MaxPerPage bound the page size of collection endpoints.
	DefaultPerPage = 20
	MaxPerPage     = 50
	firstPage      = 1
)

// ErrInvalidPagination flags query values that are not positive integers.
var ErrInvalidPagination = errors.New("page and per_page must be positive integers")

// Pagination is a validated (page, per_page) pair.
type Pagination struct {
	Page    int
	PerPage int
}

// Offset converts the page number into a SQL offset.
func (p Pagination) Offset() int { return (p.Page - firstPage) * p.PerPage }

// TotalPages derives the page count for a collection size (minimum 1).
func (p Pagination) TotalPages(total int64) int64 {
	if total <= 0 {
		return firstPage
	}
	perPage := int64(p.PerPage)
	return (total + perPage - 1) / perPage
}

// ParsePagination reads ?page=&per_page= with defaults; per_page above the cap
// is clamped (lenient) while non-numeric or non-positive input is rejected.
func ParsePagination(query url.Values) (Pagination, error) {
	page, err := positiveInt(query.Get("page"), firstPage)
	if err != nil {
		return Pagination{}, ErrInvalidPagination
	}
	perPage, err := positiveInt(query.Get("per_page"), DefaultPerPage)
	if err != nil {
		return Pagination{}, ErrInvalidPagination
	}
	if perPage > MaxPerPage {
		perPage = MaxPerPage
	}
	return Pagination{Page: page, PerPage: perPage}, nil
}

func positiveInt(raw string, def int) (int, error) {
	if raw == "" {
		return def, nil
	}
	n, err := strconv.Atoi(raw)
	if err != nil || n < 1 {
		return 0, ErrInvalidPagination
	}
	return n, nil
}
