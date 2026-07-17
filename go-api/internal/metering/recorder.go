// Package metering counts billable requests per API key and flushes them to
// api_usage in asynchronous batches, keeping persistence off the hot path.
package metering

import (
	"context"
	"log/slog"
	"time"
)

const (
	// channelCapacity absorbs request bursts between two flushes. If the
	// channel ever fills (database stalled for a long time), events are
	// dropped with a log line rather than blocking API responses —
	// accepted trade-off: metering slightly undercounts in that failure
	// mode instead of taking the API down with the database.
	channelCapacity = 4096
)

// UsageWriter persists a batch of per-key request counts for the current day.
type UsageWriter interface {
	AddUsage(ctx context.Context, requestsByKey map[int]int64) error
}

// Recorder aggregates Record() calls and flushes them on a fixed cadence.
type Recorder struct {
	writer        UsageWriter
	flushInterval time.Duration
	events        chan int
	log           *slog.Logger
}

// New builds a recorder flushing to writer every flushInterval.
func New(writer UsageWriter, flushInterval time.Duration, log *slog.Logger) *Recorder {
	return &Recorder{
		writer:        writer,
		flushInterval: flushInterval,
		events:        make(chan int, channelCapacity),
		log:           log,
	}
}

// Record counts one billable request for the key. Non-blocking by design.
func (r *Recorder) Record(keyID int) {
	select {
	case r.events <- keyID:
	default:
		r.log.Warn("metering buffer full, dropping usage event", "api_key_id", keyID)
	}
}

// Run consumes events until ctx is cancelled, then performs a final flush so a
// graceful shutdown loses nothing that was already buffered.
func (r *Recorder) Run(ctx context.Context) {
	ticker := time.NewTicker(r.flushInterval)
	defer ticker.Stop()

	pending := make(map[int]int64)
	for {
		select {
		case keyID := <-r.events:
			pending[keyID]++
		case <-ticker.C:
			pending = r.flush(pending)
		case <-ctx.Done():
			r.drain(pending)
			return
		}
	}
}

// flush writes the pending counts; on failure they are kept for the next tick
// so transient database errors do not lose usage.
func (r *Recorder) flush(pending map[int]int64) map[int]int64 {
	if len(pending) == 0 {
		return pending
	}
	ctx, cancel := context.WithTimeout(context.Background(), r.flushInterval*2)
	defer cancel()
	if err := r.writer.AddUsage(ctx, pending); err != nil {
		r.log.Error("metering flush failed, will retry", "error", err, "keys", len(pending))
		return pending
	}
	return make(map[int]int64)
}

// drain empties the channel and flushes once, best-effort, at shutdown.
func (r *Recorder) drain(pending map[int]int64) {
	for {
		select {
		case keyID := <-r.events:
			pending[keyID]++
		default:
			r.flush(pending)
			return
		}
	}
}
