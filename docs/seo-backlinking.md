# Stratégie backlinking — League Of Data Base

Guide opérateur pour construire un profil de liens naturel vers une encyclopédie
League of Legends. Objectif : des liens **gagnés par l'utilité** (outils, données,
partage), pas des liens achetés. Un site de données patch-par-patch a un avantage
structurel : chaque nouveau patch est une occasion de citation.

## 1. Le moteur de liens naturel : les builds partagés (`/b/{token}`)

C'est l'actif le plus précieux du site — chaque build public partagé est un
backlink potentiel posé par un tiers, sans effort marketing.

- **Cartes sociales impeccables** : le lot SEO garantit og:title / og:image /
  description sur `/b/{token}`. Chaque collage de lien dans Discord, Reddit ou un
  forum affiche une carte riche → taux de clic, et le lien reste.
- **À faire ensuite** : bouton « copier le lien » avec un appel visible sur la vue
  build ; un embed compact (image OG générée par build — champion + items) rendrait
  chaque partage plus attractif qu'un lien nu.
- **Mesure** : les référents `discord.com`, `reddit.com`, forums apparaissent déjà
  dans les analytics maison (classification des referers) — suivre leur évolution.

## 2. Communautés LoL : être utile, pas promotionnel

- **Reddit** (r/leagueoflegends, r/summonerschool, r/LeagueOfLegendsMeta,
  subreddits par champion) : ne jamais poster de lien sec. Répondre à de vraies
  questions (« quel est le cooldown de X au patch Y ? ») avec la donnée + le lien
  vers la fiche. Les subreddits par champion acceptent bien les fiches détail
  (lore, skins, chromas).
- **Discord** : serveurs communautaires FR (communautés de streamers, serveurs
  d'entraide). L'embed OG fait le travail ; proposer le site comme référence dans
  les salons « ressources ».
- **Forums et wikis** : les pages « liens utiles » des wikis communautaires et
  forums acceptent les encyclopédies de données. Viser les wikis FR où la
  concurrence anglophone (op.gg, mobafire) est moins citée.

## 3. Annuaires et agrégateurs spécialisés

- Annuaires d'outils LoL et de sites fan (listes « LoL tools », pages GitHub
  *awesome-league*, annuaire de sites fan FR).
- S'inscrire sur les listes de projets utilisant l'API Riot / Data Dragon
  (communauté Riot Developer, showcase HexDocs/CommunityDragon). Le site est un
  cas d'usage DDragon propre — c'est une communauté qui référence volontiers.
- Product Hunt / AlternativeTo (alternative à op.gg orientée encyclopédie
  multilingue) : un lancement soigné = un socle de liens de domaine élevés.

## 4. Créateurs de contenu : leur faire gagner du temps

Les créateurs (YouTube, TikTok, streamers, rédacteurs de guides) ont besoin de
données propres et d'images à jour à chaque patch.

- **Pages faciles à citer** : les fiches détail avec URL canonique stable
  (`/champion/Aatrox`) sont des cibles de citation idéales dans les descriptions
  de vidéos et les guides écrits.
- **Outils gratuits à proposer** : le partage de build est déjà un outil de
  préparation d'écran pour un streamer (« voici mon build du jour » en
  description). Publiciser cet usage.
- **Pages « embed » potentielles** (piste future) : une variante minimaliste d'une
  fiche (icône + stats clés) intégrable en iframe dans un blog ferait de chaque
  guide tiers un backlink. À concevoir avec `X-Frame-Options` assoupli **sur ces
  seules routes** et un lien « voir sur League Of Data Base » dans l'embed.
- **Partenariats FR** : sites fan et médias FR (Breakflip, Millenium ont leurs
  propres bases ; viser plutôt les blogs indépendants et communautés de niche) —
  proposer la donnée multilingue et les chromas (rare ailleurs) comme angle.

## 5. Angles éditoriaux différenciants (linkable assets)

Ce que le site a et que les concurrents n'ont pas — à mettre en avant car c'est
ce qui se cite :

- **21 langues** sur la même URL de données : cible naturelle pour les
  communautés non anglophones (TR, VN, TH, AR…) où l'offre est pauvre.
- **Historique de patchs** : les fiches consultables sur d'anciennes versions
  intéressent les rédacteurs « évolution du champion » et les théoriciens.
- **Chromas** (données CommunityDragon) : quasi introuvables ailleurs sous forme
  propre — les collectionneurs de skins sont une niche très active.

## 6. Ce qu'il ne faut PAS faire

- **Acheter des liens / PBN / échanges massifs** : profil de liens toxique,
  pénalité quasi garantie pour un site jeune.
- **Spammer Reddit/Discord** avec des liens sans contexte : bannissement et
  domaine grillé auprès des modérateurs (les subreddits LoL sont très modérés).
- **Commentaires de blog / forums génériques** avec ancre optimisée : aucun poids,
  signal spam.
- **Contenu dupliqué de Riot** (patch notes recopiées) pour attirer des liens :
  le site est une *base de données*, pas un média — rester sur cette ligne.
- **Widgets avec lien caché obligatoire** : si un embed est proposé, le lien de
  crédit doit être visible et honnête (pratique conforme aux guidelines Google).
- **Ancres sur-optimisées** dans les partenariats (« meilleur site build lol ») :
  préférer la marque (« League Of Data Base ») ou l'URL nue.

## 7. Rythme et mesure

- Cadence réaliste : 1 action communautaire par patch (≈ toutes les 2 semaines),
  alignée sur la mise à jour des données — le moment où le site a un « scoop ».
- Suivi : Search Console (à configurer, cf. `docs/seo-indexabilite.md`) pour les
  backlinks découverts ; analytics maison pour les référents réels.
- Critère de succès à 6 mois : des liens entrants depuis ≥ 3 communautés
  distinctes (Reddit, un wiki/annuaire, un site fan FR) et des partages `/b/`
  hebdomadaires organiques.
