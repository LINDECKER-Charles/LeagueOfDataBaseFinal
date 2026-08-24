---
date: 2026-08-23
type: devops
scope: fetcher
title: La passerelle Data Dragon signale enfin ses refus
summary: Une requete refusee par la passerelle etait renvoyee dans une reponse « tout va bien » ; elle allume desormais le tableau de bord.
tags: [observabilite, securite, ssrf, logs]
---

## Ce qui change

La passerelle qui va chercher les données Riot signale maintenant clairement ce
qu'elle refuse. Jusqu'ici, une adresse hors de sa liste blanche — soit un bug de
génération d'URL de notre côté, soit une tentative d'utiliser la passerelle comme
relais — était refusée correctement mais **annoncée dans une réponse HTTP 200** :
le refus était bien appliqué, simplement invisible.

Aucun changement de comportement pour le joueur.

## Pourquoi

Une protection qui fonctionne sans jamais rien dire est une protection dont on ne
sait pas si elle a servi.

## Technique

- `log/slog` en JSON sur **`os.Stdout`** (le service écrivait tout sur stderr, ce qui
  rendait un filtre `stream:stderr` inutilisable pour distinguer une erreur du trafic
  normal), plus `slog.SetDefault` pour capturer aussi le paquet `log` de la stdlib.
- **Sentinelles d'erreur** (`ErrInvalidURL`, `ErrSchemeNotAllowed`, `ErrHostNotAllowed`,
  `ErrRedirectNotAllowed`, `ErrBodyTooLarge`) classées par `errors.Is` : le niveau d'un
  événement ne doit jamais dépendre d'un matching de chaîne. `ErrRedirectNotAllowed` est
  volontairement distinct — un hôte autorisé qui tente de nous relayer ailleurs n'est pas
  le même événement qu'une URL mal construite — et il enveloppe `ErrHostNotAllowed`, donc
  il est testé en premier.
- Refus d'allowlist en `Error`, clé `fetch.allowlist.refused`. **Une ligne de synthèse
  par lot**, jamais une par URL : une liste de champions froide, c'est quelques centaines
  d'images en un seul appel.
- `srv.ErrorLog` sur les deux services : sans lui, un panic de handler sort en texte hors
  du flux JSON et une stack trace devient autant d'événements que de lignes.
- `/healthz` exclu des access-logs des deux services : 11 520 lignes/jour et par stack
  (sonde toutes les 15 s), conservées 90 jours — le double avec le staging.
- Durée **numérique en millisecondes** (`time.Duration.String()` rend `"1.481s"` puis
  `"532ms"`, inexploitable dans un graphe), et chemin **assaini** avant journalisation :
  un saut de ligne y injecterait des enregistrements entiers, sans niveau ni clé.
- `go-api` journalisait ses **5xx en `INFO`** : le niveau suit maintenant le statut, et la
  ligne d'accès porte `api_key_id` — l'identifiant interne, **jamais la clé**, qui est un
  identifiant d'authentification.
