package metering

import (
	"context"
	"errors"
	"log/slog"
	"testing"
	"time"
)

// recordingWriter captures the batch it received and the deadline it was given.
type recordingWriter struct {
	err      error
	batch    map[int]int64
	budget   time.Duration
	hadLimit bool
}

func (w *recordingWriter) AddUsage(ctx context.Context, requestsByKey map[int]int64) error {
	deadline, ok := ctx.Deadline()
	w.hadLimit = ok
	if ok {
		w.budget = time.Until(deadline)
	}
	w.batch = make(map[int]int64, len(requestsByKey))
	for keyID, count := range requestsByKey {
		w.batch[keyID] = count
	}
	return w.err
}

func newTestRecorder(writer UsageWriter, opts Options) *Recorder {
	return New(writer, opts, slog.New(slog.DiscardHandler))
}

// The write budget must not be derived from the flush cadence: speeding up the
// cadence would otherwise silently shrink how long Postgres is allowed to answer.
func TestFlushBudgetIsIndependentOfTheFlushCadence(t *testing.T) {
	writer := &recordingWriter{}
	rec := newTestRecorder(writer, Options{
		FlushInterval: 200 * time.Millisecond,
		FlushTimeout:  5 * time.Second,
	})

	if left := rec.flush(map[int]int64{7: 3}); len(left) != 0 {
		t.Fatalf("a successful flush must clear the pending batch, got %v", left)
	}
	if !writer.hadLimit {
		t.Fatal("the batch write must carry a deadline")
	}
	if writer.budget < 4*time.Second {
		t.Fatalf("write budget = %v, want ~5s despite the 200ms cadence", writer.budget)
	}
	if writer.batch[7] != 3 {
		t.Fatalf("batch = %v, want key 7 with 3 requests", writer.batch)
	}
}

func TestFlushKeepsPendingWhenTheWriteFails(t *testing.T) {
	writer := &recordingWriter{err: errors.New("database down")}
	rec := newTestRecorder(writer, Options{FlushInterval: time.Second, FlushTimeout: time.Second})

	pending := rec.flush(map[int]int64{4: 2})
	if pending[4] != 2 {
		t.Fatalf("pending = %v, want the batch kept for the next tick", pending)
	}
}

func TestRunFlushesBufferedEventsAtShutdown(t *testing.T) {
	writer := &recordingWriter{}
	rec := newTestRecorder(writer, Options{FlushInterval: time.Hour, FlushTimeout: time.Second})
	rec.Record(11)
	rec.Record(11)
	rec.Record(12)

	ctx, cancel := context.WithCancel(context.Background())
	done := make(chan struct{})
	go func() { defer close(done); rec.Run(ctx) }()
	cancel()
	<-done

	if writer.batch[11] != 2 || writer.batch[12] != 1 {
		t.Fatalf("final flush = %v, want {11:2, 12:1}", writer.batch)
	}
}
