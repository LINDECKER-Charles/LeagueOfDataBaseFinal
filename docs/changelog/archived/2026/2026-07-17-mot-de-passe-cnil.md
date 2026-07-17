---
date: 2026-07-17
type: feat
scope: full-stack
title: Mot de passe renforcé avec confirmation et checklist en direct
summary: L'inscription exige un mot de passe solide (recommandation CNIL) avec double saisie et une checklist qui se valide pendant la frappe.
tags: [comptes, inscription, sécurité, mot-de-passe]
---

## Ce qui change

À la création de compte, le mot de passe se saisit désormais deux fois et doit
être vraiment solide : au moins 12 caractères mêlant minuscule, majuscule,
chiffre et caractère spécial. Une checklist s'affiche à côté des champs et se
coche en temps réel pendant que vous tapez — chaque critère s'illumine quand il
est atteint, y compris « les deux champs correspondent ». Les mots de passe trop
courants (« password », « azerty »…) sont refusés.

## Pourquoi

Un compte protège vos favoris et vos builds ; un mot de passe court ou banal se
devine en quelques secondes. Les nouvelles règles suivent la recommandation
officielle de la CNIL (délibération n° 2022-100 du 21 juillet 2022, équivalence
~80 bits d'entropie : 12 caractères sur 4 familles) :
<https://www.cnil.fr/fr/mots-de-passe-une-nouvelle-recommandation-pour-maitriser-sa-securite>
/ <https://www.legifrance.gouv.fr/cnil/id/CNILTEXT000046437451>.

## Détails

- Double saisie du mot de passe avec message clair si les deux champs diffèrent.
- Checklist réactive (12 caractères, minuscule, majuscule, chiffre, spécial,
  correspondance) — critères atteints en turquoise arcanique, manquants en
  neutre ; jamais de rouge agressif pendant la frappe.
- « Caractère spécial » accepte large : toute ponctuation, symbole ou espace.
- Refus des ~1000 mots de passe les plus courants, sans distinction de casse.
- Sans JavaScript, le formulaire garde une aide statique et le serveur reste
  le juge final ; la limitation des tentatives de connexion existait déjà.

## Technique

- Contrainte réutilisable `App\Validator\CnilPassword` + validator : longueur
  ≥ 12 (`mb_strlen`), classes via `\p{Ll}` / `\p{Lu}` / `[0-9]` /
  `[^\p{L}\p{N}]` (jeu « spécial » volontairement large), denylist locale
  embarquée `src/Validator/Resources/common-passwords.txt` (SecLists top-1000,
  normalisée lowercase). Pas de HIBP : l'egress PHP est interdit par
  l'architecture — passerait par le go-fetcher si souhaité un jour.
- `RegistrationFormType` : `plainPassword` en `RepeatedType`, options partagées
  `PasswordFieldOptions` (aussi utilisées par le flux « définir un mot de
  passe » OAuth). Messages traduits depuis le domaine `messages` (le projet ne
  maintient pas de catalogue `validators`), d'où la pré-traduction dans le
  validator et pour `invalid_message`.
- Îlot Vue `password-checklist` (`PasswordChecklist.vue`) branché par sélecteurs
  sur les inputs Twig ; règles clientes dans le module pur
  `assets/vue/security/passwordRules.ts`, parité stricte avec le serveur
  (code points, Unicode), testé vitest + PHPUnit.
- Nouvelles clés i18n `auth.password.*` / `auth.register.password_*` dans les
  21 locales (blocs `auth:`/`profile:` créés dans les 19 locales qui n'avaient
  encore que le fallback anglais).
