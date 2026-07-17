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

func writeJSON(w http.ResponseWriter, status int, v any) {
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.WriteHeader(status)
	_ = json.NewEncoder(w).Encode(v)
}

func writeError(w http.ResponseWriter, status int, code, message string) {
	writeJSON(w, status, errorEnvelope{Error: errorBody{Code: code, Message: message}})
}

// writeInternal hides the underlying failure behind the uniform envelope; the
// real error is expected to be logged by the caller.
func writeInternal(w http.ResponseWriter) {
	writeError(w, http.StatusInternalServerError, CodeInternal, "internal error")
}

// writeUnavailable is used when a dependency (database) is down or the schema
// is not migrated yet: same envelope, 503 status.
func writeUnavailable(w http.ResponseWriter) {
	writeError(w, http.StatusServiceUnavailable, CodeInternal, "service temporarily unavailable")
}
