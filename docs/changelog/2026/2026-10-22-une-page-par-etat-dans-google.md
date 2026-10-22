---
date: 2026-10-22
type: feat
scope: full-stack
title: Chaque version d'une fiche a sa page dans Google, et chaque patch la sienne
summary: Les fiches montrent ce que leur patch a changé et tout leur historique, chaque patch a sa page de changements, et l'historique redevient trouvable dans les moteurs de recherche.
tags: [seo, anciens-patchs, patchs, catalogue]
---

## Ce qui change

La fiche d'un champion, d'un objet, d'une rune ou d'un sort d'invocateur raconte désormais son
histoire, patch après patch :

- **« Ce qui a changé dans ce patch »** : sur le patch qui a modifié la fiche, chaque valeur
  avant et après (statistiques, coûts, recharges, recette…), et pour un texte les mots retirés
  et ajoutés. Une entité qui arrive dans le jeu dit « Nouveau dans ce patch », une entité qui
  revient « De retour dans ce patch ».
- **« Inchangé depuis le patch X »** : sur les patchs suivants, tant que rien ne bouge, avec un
  lien vers le patch qui a fait la version affichée.
- **« Historique »** remplace « Dans les patchs précédents » : toutes les versions de la fiche,
  de la plus récente à la plus ancienne, chacune avec ses patchs (« Patchs 11.5.1 à 11.9.1 »,
  « Depuis le patch 16.18.1 ») et ce qu'elle a changé, en un clic. Sur un ancien patch, le bloc
  dit aussi ce qui a suivi : « Changé au patch X » ou « Retiré au patch X ».

Chaque patch a aussi sa **page de changements** (`/fr/changes` pour le dernier patch,
`/fr/16.20.1/changes` pour un ancien) : les champions, objets, runes et sorts que le patch a
modifiés, avec les points touchés, ajoutés, revenus ou retirés, chacun menant à sa fiche.
L'historique des patchs lie la page de changements de chaque patch avant ses quatre listes.

Dans Google et les autres moteurs, chaque version distincte d'une fiche est de nouveau
trouvable, une seule fois, à la page du patch qui l'a apportée : son titre porte les patchs
qu'elle couvre (« Trinity Force (patchs 11.5.1 à 11.9.1) ») et sa description commence par ce
qu'elle a changé. Les copies identiques désignent cette page, et la fiche du dernier patch
garde sa place.

## Pourquoi

Depuis le 14 octobre, seule la fiche du dernier patch était indexée : tout l'historique,
le contenu propre du site, restait invisible dans les moteurs. Avant cela, chaque patch
publiait une copie presque identique de chaque fiche (environ 85 % des pages ne différaient de
la précédente que par leur numéro de patch), et c'est ce qui avait fait décrocher le site en
août. Chaque contenu distinct a maintenant une page, et une seule.

## Technique

- Décision : ADR 0005, ADR 0011 et ADR 0003, amendements du 2026-10-22 ; spécification
  `docs/produit/seo-une-page-par-etat.md` (écarts de l'implémentation : § 11). Le changement
  est en vigueur dans le code ; la prod le sert à la bascule de son déploiement (§ 4), sans
  interrupteur.
- Index des états d'entité en Postgres (`catalog_state_generation`, `catalog_state_version`,
  `catalog_entity_state`), empreinte SHA-256 de la projection affichée de chaque entité
  (`LoDb.Domain/States/`, `FingerprintScheme.Current = 1`), construit par
  `catalog states build` (service `states`, avant la page de maintenance du déploiement),
  étendu à chaque promotion (étape `StateIndex`) et par la tâche `catalog.states.reconcile`.
- API : `canonical` toujours présent (`StateView`), `state`, `history`, `next`,
  `isPresentInLatest` et `changes` sur les quatre fiches ; `GET /api/catalog/{version}/{lang}/changes` ;
  `CatalogMeta.oldestChangesVersion` et `newestChangesVersion` ; anciennes URL menées à la
  canonique de leur état.
  `CanonicalReferences`, `CanonicalReader` et `LoDb__Seo__SitemapVersionWindow` sont retirés.
- Front : blocs de `features/catalogue/shared/codex/state/`, page `features/catalogue/changes/`,
  `isVersionedRoute`, titre `seo.state_range_suffix` et résumé `seo.state.*`, libellés
  `detail.field.*` dans les 21 langues, classe `revisable` d'une fiche épinglée de l'état
  ouvert.
- Sitemaps : un `{version}.xml` par version couverte passée (page de patch, ouvreurs des états
  clos), annoncé s'il n'est pas vide ; IndexNow annonce aussi les pages de patch et les
  ouvreurs que la promotion a clos.
- Précalcul : les classes `archived` et `revisable` stockées, nginx qui garde chaque page selon
  son objet, la file des états clos (`prerender_revision`), la passe « ouvreurs d'abord » ;
  epoch `2` servie à la bascule.
- Remplace, en tout ou en partie, les entrées du 2026-10-11 (historique des patchs), du
  2026-10-12 (pages datées, patch annoncé à sa sortie), du 2026-10-14 (une fiche par entité,
  lien vers le patch actuel), du 2026-10-16 (anciennes adresses), du 2026-10-20 (anciens
  patchs sans attente) et du 2026-10-21 (lien vers le dernier patch).
