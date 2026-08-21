package trends

import "testing"

func TestEditionOfFollowsTheIdConventionsOfTheTwoGameTypes(t *testing.T) {
	cases := []struct {
		ddragonType, id, want string
	}{
		{"item", "1004", editionModern},
		{"item", "771004", editionClassic},
		{"item", "221011", editionModern}, // Arena variant: another range
		{"item", "7710040", editionModern},
		{"summoner", "SummonerFlash", editionModern},
		{"summoner", "SummonerFlash_Jade", editionClassic},
		{"champion", "Aatrox", ""},
		{"runesReforged", "8000", ""},
	}
	for _, c := range cases {
		if got := editionOf(c.ddragonType, c.id); got != c.want {
			t.Errorf("editionOf(%q, %q) = %q, want %q", c.ddragonType, c.id, got, c.want)
		}
	}
}
