package quota

import "testing"

func TestEvaluate(t *testing.T) {
	cases := []struct {
		name    string
		used    int64
		quota   int64
		credits int64
		want    Decision
	}{
		{"fresh key uses plan", 0, 500, 0, AllowPlan},
		{"last plan request", 499, 500, 0, AllowPlan},
		{"quota full, no credits", 500, 500, 0, Deny},
		{"quota full, credits available", 500, 500, 1, AllowCredits},
		{"quota overshot, credits available", 900, 500, 10, AllowCredits},
		{"quota full, credits drained", 500, 500, 0, Deny},
		{"zero quota plan goes straight to credits", 0, 0, 5, AllowCredits},
		{"zero quota, zero credits", 0, 0, 0, Deny},
	}
	for _, c := range cases {
		if got := Evaluate(c.used, c.quota, c.credits); got != c.want {
			t.Errorf("%s: Evaluate(%d,%d,%d) = %v, want %v", c.name, c.used, c.quota, c.credits, got, c.want)
		}
	}
}
