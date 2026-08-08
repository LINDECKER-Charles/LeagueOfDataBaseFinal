package api

import (
	"encoding/json"
	"net/http"
)

// Stable machine-readable error codes of the public API. The envelope shape
// {"error":{"code","message"}} is part of the contract — never leak raw errors.
const (
	CodeUnauthorized  = "unauthorized"
	CodeForbidden     = "forbidden"
	CodeNotFound      = "not_found"
	CodeRateLimited   = "rate_limited"
	CodeQuotaExceeded = "quota_exceeded"
	CodeInvalid       = "invalid_request"
	CodeInternal      = "internal"
)

type errorBody struct {
	Code    string `json:"code"`
	Message string `json:"message"`
}

type errorEnvelope struct {
	Error errorBody `json:"error"`
}

// apiError is one refusal: the status and the envelope served with it. The
// three parts are a single value because they always travel together — the
// auth pipeline decides on a refusal long before anything renders it.
type apiError struct {
	status  int
	code    string
	message string
}

// write renders the refusal. Single place responsible for the envelope shape.
func (e apiError) write(w http.ResponseWriter) {
	writeJSON(w, e.status, errorEnvelope{Error: errorBody{Code: e.code, Message: e.message}})
}

func writeJSON(w http.ResponseWriter, status int, v any) {
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.WriteHeader(status)
	_ = json.NewEncoder(w).Encode(v)
}

// writeInternal hides the underlying failure behind the uniform envelope; the
// real error is expected to be logged by the caller.
func writeInternal(w http.ResponseWriter) {
	apiError{http.StatusInternalServerError, CodeInternal, "internal error"}.write(w)
}

// writeUnavailable is used when a dependency (database) is down or the schema
// is not migrated yet: same envelope, 503 status.
func writeUnavailable(w http.ResponseWriter) {
	apiError{
		http.StatusServiceUnavailable, CodeInternal, "service temporarily unavailable",
	}.write(w)
}
