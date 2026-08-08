package ratelimit

import (
	"testing"
	"time"
)

const perMin = 10

func newTestLimiter() (*Limiter, *time.Time) {
	now := time.Unix(1_700_000_000, 0)
	l := New(func() time.Time { return now })
	return l, &now
}

func TestBurstUpToLimitThenDenied(t *testing.T) {
	l, _ := newTestLimiter()
	for i := 0; i < perMin; i++ {
		v := l.Allow(1, perMin)
		if !v.Allowed {
			t.Fatalf("request %d should be allowed", i+1)
		}
		if v.Remaining != perMin-i-1 {
			t.Fatalf("request %d: remaining = %d, want %d", i+1, v.Remaining, perMin-i-1)
		}
	}
	v := l.Allow(1, perMin)
	if v.Allowed {
		t.Fatal("request beyond capacity should be denied")
	}
	if v.Limit != perMin || v.Remaining != 0 {
		t.Fatalf("denied verdict headers wrong: %+v", v)
	}
}

func TestRefillOverTime(t *testing.T) {
	l, now := newTestLimiter()
	for i := 0; i < perMin; i++ {
		l.Allow(1, perMin)
	}
	// 10/min = 1 token per 6 s: after 6 s exactly one request passes.
	*now = now.Add(6 * time.Second)
	if !l.Allow(1, perMin).Allowed {
		t.Fatal("one token should have refilled after 6s")
	}
	if l.Allow(1, perMin).Allowed {
		t.Fatal("second request should still be denied")
	}
}

func TestResetTimestampWhenDenied(t *testing.T) {
	l, now := newTestLimiter()
	for i := 0; i < perMin; i++ {
		l.Allow(1, perMin)
	}
	v := l.Allow(1, perMin)
	// Next token in 6 s (ceil) — reset must point there, not further.
	if want := now.Add(6 * time.Second).Unix(); v.Reset != want {
		t.Fatalf("reset = %d, want %d", v.Reset, want)
	}
}

func TestBucketsAreIndependentPerKey(t *testing.T) {
	l, _ := newTestLimiter()
	for i := 0; i < perMin; i++ {
		l.Allow(1, perMin)
	}
	if !l.Allow(2, perMin).Allowed {
		t.Fatal("key 2 must not share key 1's bucket")
	}
}

func TestZeroLimitAlwaysDenies(t *testing.T) {
	l, _ := newTestLimiter()
	if l.Allow(1, 0).Allowed {
		t.Fatal("a zero rate limit must deny")
	}
}
