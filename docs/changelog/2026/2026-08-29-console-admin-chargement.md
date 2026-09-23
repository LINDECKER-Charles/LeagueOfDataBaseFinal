---
date: 2026-08-29
type: perf
scope: back
title: Console d'administration : chargement nettement plus rapide
summary: Les panneaux de la console s'affichent en parallèle, et les rapports lourds ne recalculent plus ce qui n'a pas changé.
tags: [admin, performance]
---

## Ce qui change

La console d'administration s'affiche beaucoup plus vite. Les sections d'une page
(compteurs, trafic, stockage) arrivent maintenant réellement en même temps au lieu
de se mettre en file d'attente les unes derrière les autres, et les rapports les
plus coûteux ne refont plus le travail déjà fait.

## Pourquoi

Chaque section de la console était bien chargée séparément, mais elles se
bloquaient mutuellement : une page à trois sections mettait trois fois plus de
temps qu'une page à une seule. Par-dessus, la page de surveillance recalculait
l'inventaire complet du stockage juste pour afficher un voyant « MinIO
opérationnel ».

## Détails

- Les sections d'une page se chargent en parallèle, quel que soit leur nombre.
- Le voyant MinIO répond en quelques millisecondes au lieu d'attendre l'inventaire
  du bucket.
- Le rapport de stockage se recalcule deux fois plus vite ; les volumes de trafic
  des journées passées ne sont plus relus à chaque affichage.

## Technique

Quatre causes, mesurées en local (bucket de 7 968 objets, 264 manifestes) :

- **Verrou de session.** Le handler de session natif de PHP garde un verrou
  exclusif pendant toute la requête : les fragments `/admin/panel/{panel}`, tous
  authentifiés, se sérialisaient (escalier mesuré de +0,55 s par requête
  concurrente — 6 requêtes parallèles : 2,35 s → 5,18 s). `AdminPanelController`
  ferme désormais la session avant le rendu (fragment en lecture seule,
  authentification déjà résolue) ; le firewall la rouvre au `kernel.response` pour
  réécrire le token. Après : 6 requêtes parallèles à plat, 2,53–2,67 s.
- **Sonde MinIO.** `ServiceHealthProbe::minio()` appelait `StorageAnalyticsService::report()`,
  donc un listing profond en O(objets) du bucket entier, pour un simple voyant de
  santé. Remplacé par un listing superficiel de la racine arrêté à la première
  entrée ; les volumes ne sont affichés que si le rapport est déjà mémorisé
  (`cachedTotals()`, ne calcule jamais). Mesuré à froid, avant/après :
  `probe->all()` 617 ms → 26 ms, `monitoring->report()` 856 ms → 139 ms.
- **Lecture des manifestes.** Le ratio de déduplication lit chaque manifeste
  (264 lectures objet séquentielles). Le résultat est une fonction pure de leur
  contenu : il est mémorisé sous l'empreinte du jeu de manifestes vue par le scan
  (nombre + poids cumulé + écriture la plus récente), donc réinvalidée dès qu'un
  ingest touche un manifeste — y compris derrière « Rafraîchir », qui reste correct
  sans repayer la lecture. Recalcul du rapport : 600 ms → 284 ms.
- **Agrégats analytics.** La fenêtre était assemblée avec une lecture objet par
  jour à chaque recalcul (30 jours par défaut, 90 au maximum). Les journées closes
  sont immuables : elles sont mémorisées individuellement (7 j), seule la journée
  courante reste vive. Une journée non encore consolidée ET sans NDJSON local n'est
  pas figée (TTL court), pour qu'une indisponibilité passagère du stockage objet ne
  s'incruste pas une semaine.

TTL du rapport de stockage porté de 120 s à 600 s (le bouton « Rafraîchir » reste
la porte de sortie).

Non traité, hors périmètre : les ~3 s de latence par requête observées en `dev`
sont l'overhead profiler/`debug=true` documenté dans `CLAUDE.md`, identique sur
toutes les pages du site (une 404 anonyme met le même temps) — pas une
caractéristique de la console.
