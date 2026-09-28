package trends

import (
	"regexp"
	"strings"
)

// Editions of the entities that exist in both games since League of Legends
// Classic shipped through Data Dragon: the classic twin of an item / summoner
// spell shares its display name with the current one, so a ranking that only
// carries names is ambiguous without this field.
const (
	editionModern  = "modern"
	editionClassic = "classic"
)

// classicItemID is Riot's classic item range — the "77"-prefixed six-digit ids
// (771004 is the classic twin of 1004). Mirrors App\Service\API\Edition\ItemEditionRule.
var classicItemID = regexp.MustCompile(`^77\d{4}$`)

// classicSpellSuffix marks the classic summoner spells (SummonerFlash_Jade).
// Deliberate approximation of the authoritative PHP rule (a spell whose modes
// contain JADE — App\Service\API\Edition\SummonerEditionRule): the ranking only
// holds ids, and on current data the suffix and the mode are 1:1. Would Riot
// ever break that convention, this field degrades to a wrong label while the
// site itself stays correct.
const classicSpellSuffix = "_Jade"

// editionOf returns the edition of an entity id for the two-game types, and
// "" for the others (champions, runes) so the field is omitted.
func editionOf(ddragonType, id string) string {
	switch ddragonType {
	case "item":
		return pick(classicItemID.MatchString(id))
	case "summoner":
		return pick(strings.HasSuffix(id, classicSpellSuffix))
	default:
		return ""
	}
}

func pick(classic bool) string {
	if classic {
		return editionClassic
	}
	return editionModern
}
