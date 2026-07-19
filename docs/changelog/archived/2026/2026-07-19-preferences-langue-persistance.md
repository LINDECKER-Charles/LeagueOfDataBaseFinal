---
date: 2026-07-19
type: fix
scope: back
title: La langue choisie reste mémorisée d'une visite à l'autre
summary: Sélectionner une langue (et une version) depuis le sélecteur en haut la conserve désormais lors des visites suivantes, même après avoir fermé le navigateur, sans devoir cocher « se souvenir ».
tags: [i18n, switcher, preferences]
---

## Ce qui change

Quand tu choisis une langue et une version du jeu dans le sélecteur en haut à droite,
ce choix est maintenant conservé pour tes prochaines visites — y compris après avoir
fermé puis rouvert ton navigateur. Plus besoin de cocher « se souvenir » pour que la
langue tienne. La case sert seulement à mémoriser plus longtemps.

## Pourquoi

La version se retrouvait dans l'adresse de la page, donc elle « collait » toute seule.
La langue, elle, n'était gardée que le temps de la session : dès que celle-ci expirait
(navigateur fermé, longue absence), l'interface repassait à l'anglais par défaut, alors
que la version, elle, semblait tenir. On se retrouvait avec une version mémorisée mais
une langue oubliée.

## Technique

- `HomeController::save()` : le POST `app_setup_save` pose désormais **toujours** le
  cookie signé `lod_prefs` (préférence fonctionnelle patch+langue → exemptée de
  consentement, pas de tracking). La case `remember` ne pilote plus l'on/off mais la
  durée : `PREF_COOKIE_DAYS` (30 j) par défaut, `PREF_COOKIE_DAYS_EXTENDED` (365 j) si
  cochée. Fin du `makeForgetCookie()` sur ce chemin (persistance session-only) qui
  laissait la langue sans porteur durable.
- `ClientManager::makeForgetCookie()` supprimé (plus aucun appelant ; le seul était la
  branche `else` retirée). `getSession()`/`getSelectedLocale()` réhydratent la langue
  depuis `lod_prefs` comme avant — c'est l'écriture du cookie qui manquait par défaut.
- Non touché volontairement : la langue reste hors du segment d'URL (URL versionnée
  langue-invariante, cf. `PageContextResolver`) ; le fallback domaine `.fr → fr / * → en`
  du `LocaleSubscriber` n'intervient plus qu'en l'absence totale de préférence.
