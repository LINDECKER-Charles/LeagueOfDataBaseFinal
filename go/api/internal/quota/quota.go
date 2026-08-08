// Package quota decides how a request is paid for: monthly plan first, then
// prepaid credits, otherwise denied. Pure logic — persistence lives elsewhere.
package quota

// Decision is the outcome of a quota evaluation.
type Decision int

const (
	// AllowPlan: the request fits in the key's monthly quota.
	AllowPlan Decision = iota
	// AllowCredits: monthly quota is exhausted but prepaid credits remain;
	// the caller must decrement one credit synchronously before proceeding.
	AllowCredits
	// Deny: neither quota nor credits can pay for the request.
	Deny
)

// Evaluate applies the plan-then-credits policy for one incoming request.
// monthlyUsed may slightly lag reality (cached counter) — see keys.Entry.
func Evaluate(monthlyUsed, monthlyQuota, creditsBalance int64) Decision {
	if monthlyUsed < monthlyQuota {
		return AllowPlan
	}
	if creditsBalance > 0 {
		return AllowCredits
	}
	return Deny
}
