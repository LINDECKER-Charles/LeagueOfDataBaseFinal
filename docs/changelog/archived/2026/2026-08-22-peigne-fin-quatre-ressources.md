---
date: 2026-08-22
type: fix
scope: full-stack
title: Passe au peigne fin des quatre codex (champions, objets, runes, sorts)
summary: Sweep complet des 1080 fiches et des 4 listes — hero Fiddlesticks réparé, portées fantaisistes masquées, titres préservés, objets fantômes retirés, modes de jeu lisibles.
tags: [champions, items, runes, summoners, sweep, quality]
---

## Ce qui change

Un balayage exhaustif des quatre encyclopédies (toutes les fiches et toutes les
listes, icône par icône) a permis de corriger une série de défauts d'affichage :

- **Champions** : le portrait de la fiche Fiddlesticks s'affiche à nouveau (le
  CDN de Riot exige l'orthographe interne « FiddleSticks » sur ce visuel) ; les
  sorts n'affichent plus de portées fantaisistes (« Portée : 4294967295 » sur
  Janna, « 25000 » sur une trentaine de champions — ces valeurs sentinelles sont
  masquées) ; les titres ne sont plus dénaturés (« Épée des Darkin » restait
  « Épée des darkin ») ; après un retour arrière, les statistiques et le curseur
  de niveau restent synchronisés.
- **Objets** : les 4 objets fantômes sans nom et le « Gangplank Placeholder »
  quittent la liste ; les trois améliorations de Gangplank affichent leur vrai
  nom (« Feu à volonté ») au lieu d'un balisage brut ; plus de jetons Riot non
  résolus (`{{ Item_Cooldown }}`, `@Slow@`) dans les descriptions des cartes et
  fiches ; la map « Swarm » est nommée (fin des « Map 33 ») ; « Effigie
  épouvantail » cite « Fiddlesticks » et non « FiddleSticks » ; les catégories
  se lisent « Critical Strike » plutôt que « CriticalStrike ».
- **Sorts d'invocateur** : les modes de jeu affichent des noms lisibles et
  cohérents entre liste et fiche (fin des identifiants internes WIPMODEWIP,
  RUBY_TRIAL_1…) ; les sorts du Roi Poro et du Snow ARURF affichent enfin leur
  mode ; les infobulles des chromas utilisent le libellé de teinte.
- **Runes** : les pages des patchs 7.22 à 8.7 (icônes disparues du CDN)
  s'affichent instantanément après la première visite au lieu de réinterroger le
  CDN à chaque vue ; l'accueil ne préfixe plus les runes d'un « key » parasite.
- **Recherche des listes** : insensible aux accents, comme les pickers
  (« feerique » trouve « Charme féérique »).

## Pourquoi

L'arrivée de LoL Classic a mis en lumière une famille de bugs d'affichage ; le
sweep systématique (hash de chaque icône des 4 listes contre Data Dragon +
scan des 1080 fiches) a confirmé que les icônes étaient justes partout et
débusqué les défauts restants, corrigés ici.

## Technique

- Images champions désormais **indexées par id** comme objets/sorts (projection
  par défaut dans `ResolvesImages::projectImages`) — suppression du contrat
  positionnel et du réalignement par curseur de `ChampionOptionsProjector`.
- Manifeste d'images : une **absence définitive (403/404) est persistée à
  `null`** (`settleChunk`) et résolue en placeholder sans re-fetch ;
  `GoFetcherClient::fetchMany` renvoie `{bytes, absent}` (les échecs transitoires
  ne sont jamais gelés). Corrige la classe « back-catalogue runes ».
- Débris DDragon filtrés dans `ItemManager::paginationCollection` (noms vides,
  « Placeholder », noms balisés réduits via `DdragonText::plainName`) — une seule
  règle pour liste/recherche/pager/compteurs/sitemap/404.
- `DdragonText` (service partagé) nettoie aussi les descriptions JSON-LD ;
  filtres Twig `ucfirst` (non destructif) et `tag_label` ; labels de modes
  centralisés dans `GameModesExtension` (une seule whitelist liste + détail).
- Casse du CDN centralisée : `ChampionSkins::CENTERED_ART_IDS`
  (`Fiddlesticks → FiddleSticks`), hero fourni par le contrôleur.
