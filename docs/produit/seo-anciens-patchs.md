# Désindexation du site : les pages des anciens patchs

Diagnostic du 14 octobre 2026 et correctif demandé. Le site a perdu presque toute sa
visibilité dans Google depuis le 16 août 2026 ; ni une panne, ni une pénalité, ni une erreur
de réglage n'en sont la cause. Google explore les pages et refuse de les indexer, parce que le
site lui présente des dizaines de milliers de pages quasi identiques : chaque entité, pour
chaque patch épinglé et dans chaque langue.

## 1. Constat (Search Console, relevé le 14 octobre 2026)

**Performances** (propriété `https://league-of-data-base.com/`) :

| Période | Impressions |
|---|---|
| 1ᵉʳ au 15 août | 85 093 au total, 3 945 le 15 août |
| 16 août | 589 |
| 17 août à mi-septembre | ~100 par jour |
| 18 septembre | 26 |
| depuis le 28 septembre | 0 à 14 par jour |

Sur 28 jours : 819 impressions, contre 2 967 sur les 28 jours précédents.

**Statistiques d'exploration** (90 jours) :

- 131 000 demandes. Pics fin juillet (jusqu'à ~24 000 par jour), ~1 000 par jour début
  août, quelques centaines par jour après mi-août.
- Réponses : 200 pour 86 %, 404 pour 10 %, 301 pour 4 %, 5xx sous 1 %.
- État de l'hôte : robots.txt et DNS sans problème. Connectivité du serveur à 0 % d'échec du
  18 juillet à début octobre ; le seul pic est celui des 9 et 10 octobre (panne connue, voir
  le changelog du 10 octobre).

**Actions manuelles** : aucune.

**Indexation des pages** (mise à jour du 9 octobre) : 2 570 pages indexées, contre ~20 000 fin
juillet, en baisse continue depuis mi-août ; 43 000 non indexées.

| Raison | Pages |
|---|---|
| Explorée, actuellement non indexée | 31 201 |
| Autre page avec balise canonique correcte | 10 886 |
| Introuvable (404) | 461 |
| Page avec redirection | 238 |
| Google n'a pas choisi la même URL canonique que l'utilisateur | 75 |
| Exclue par la balise `noindex` | 70 |
| Page en double sans URL canonique sélectionnée par l'utilisateur | 36 |
| Soft 404 | 14 |
| Erreur serveur (5xx) | 2 |
| Bloquée par le fichier robots.txt | 2 |

Le « problème détecté » annoté le 15 août ne porte que sur les 2 pages bloquées par le
robots.txt : ce n'est pas la cause.

**Exemples « Explorée, actuellement non indexée »**, tous explorés le 11 octobre. La nouvelle
stack est touchée comme l'ancienne :

```
https://league-of-data-base.com/cs/11.9.1/items/3854-steel-shoulderguards
https://league-of-data-base.com/en/10.10.4/items/3001-evenshroud
https://league-of-data-base.com/de/11.7.1/items/3108-fiendish-codex
https://league-of-data-base.com/en/11.17.1/items/3108-fiendish-codex
https://league-of-data-base.com/en/10.2.1/items/3003-archangels-staff
https://league-of-data-base.com/en/16.10.1/items/223026-guardian-angel
https://league-of-data-base.com/en/0.152.115/champions/Skarner
https://league-of-data-base.com/10.3.1/champion/Karthus            (ancienne grammaire)
https://league-of-data-base.com/11.22.1/champion/JarvanIV          (ancienne grammaire)
https://league-of-data-base.com/0.152.115/champion/Skarner         (ancienne grammaire)
```

## 2. Diagnostic

« Explorée, actuellement non indexée » veut dire que Google a lu la page et juge qu'elle
n'apporte rien de plus qu'une autre. C'est le cas d'une fiche d'ancien patch face à la même
fiche au dernier patch : même entité, mêmes textes, à quelques chiffres près. Le site
multiplie ces doublons et les pousse à Google :

- **Chaque page d'ancien patch est sa propre canonique.** L'ADR 0005 le prévoit (« patch
  passé (auto-canonique) ») et `src/LoDb.Web/src/app/core/seo/urls/seo-urls-of.ts` met la
  version dans la canonique. Une entité existe donc en autant d'URL canoniques que de patchs
  épinglés, multipliées par 21 langues.
- **Les sitemaps les annoncent.** L'index annonce le dernier patch et les 12 versions
  suivantes de chaque langue (`LoDb__Seo__SitemapVersionWindow`, amendement du 2026-10-11),
  soit ~160 000 URL envoyées, d'après Search Console. Les sitemaps des versions plus anciennes
  restent servis.
- **Les liens internes y mènent aussi** : `/{locale}/patches` (« Historique des patchs », lié
  depuis le pied de chaque page) et le bloc « Dans les patchs précédents » de chaque fiche
  (`features/catalogue/shared/codex/history/previous-patches.ts`, 6 versions).

Google répartit alors son exploration sur un espace immense de pages presque identiques,
n'en garde qu'une poignée, et les fiches du dernier patch, celles qui comptent, se noient
dans la masse. La chute du 16 août s'est produite sur l'ancien site, qui avait déjà une URL
par version ; la nouvelle stack reproduit le même schéma, comme le montrent les exemples.

## 3. Ce qui est demandé

**Objectif** : que Google indexe une page par entité et par langue, celle du dernier patch,
et que les anciens patchs restent consultables par les visiteurs sans lui faire concurrence.

**C'est une décision d'architecture.** Elle revient sur l'ADR 0005 (« patch passé
(auto-canonique) ») et sur son amendement du 2026-10-11 (« leurs pages restent canoniques et
indexables »). Elle s'écrit d'abord en amendement de l'ADR 0005, avec les options pesées, puis
se réalise.

1. **Décider la règle des pages d'ancien patch**, dans l'amendement :
   - option A : canonique vers l'URL courte de la même entité, dans la même langue (le dernier
     patch). Le signal se concentre, la page reste explorable. Google peut toutefois ignorer
     une canonique entre deux pages qui diffèrent ;
   - option B : `noindex, follow`. C'est explicite, et les liens restent suivis ;
   - cas d'une entité absente du dernier patch (objet retiré) : son ancienne fiche est alors
     un contenu unique. Il faut décider si elle reste indexable, et vers quelle version.

   Recommandation de départ : l'option A pour les entités présentes au dernier patch. Le
   choix final et son pourquoi vont dans l'amendement.
2. **L'appliquer dans le `<head>`** (`core/seo`, `Seo.apply`, jamais une page à la main) :
   canonique, `robots`, `og:url`, et des `hreflang` cohérents (jamais d'alternate vers une
   page canonisée ailleurs). Les données structurées suivent la canonique.
3. **Les sitemaps** : n'annoncer que des URL canoniques. Le défaut de
   `SitemapVersionWindow` passe à `0`. Les sitemaps des anciennes versions cessent de lister
   des URL qui ne sont plus canoniques : décider s'ils répondent vide ou 404, et l'écrire.
4. **Les liens internes** vers les anciens patchs restent pour les visiteurs ; ne rien
   ajouter qui gonfle encore l'exploration.
5. **La documentation** :
   - l'amendement de l'ADR 0005 ;
   - `docs/architecture/pieges.md` (le paragraphe sur l'index des sitemaps) ;
   - `docs/guides/configuration.md` (`LoDb__Seo__SitemapVersionWindow`) ;
   - `docs/produit/seo-indexabilite.md` si besoin ;
   - une entrée de changelog (gain de référencement perceptible).
6. **Les tests**, selon `docs/guides/tests.md` :
   - canonique, `robots` et `hreflang` d'une page de dernier patch, d'une page d'ancien patch
     et d'une entité retirée ;
   - l'index et les sitemaps de version ;
   - un E2E si le `<head>` rendu par le SSR n'est couvert nulle part ailleurs.

**Mesure de l'effet**, à écrire dans l'amendement : dans Search Console, 4 à 8 semaines après
le déploiement, le nombre de pages indexées et les impressions des URL courtes doivent
remonter. « Explorée, actuellement non indexée » doit baisser, tandis qu'« Autre page avec
balise canonique correcte » montera, ce qui est attendu avec l'option A.

**Hors périmètre** : les 301 de l'ancienne grammaire (`docker/nginx/server.d/legacy-redirects.conf`,
corrects), l'enrichissement du contenu des fiches (un chantier à part) et la mesure
d'audience.

**Livraison** : selon `AGENTS.md` (conventions, garde-fous, un commit par changement
cohérent). Le travail aboutit à une pull request vers `dev`, avec une CI verte. Pas de push
sur `dev`, `test` ni `main`.
