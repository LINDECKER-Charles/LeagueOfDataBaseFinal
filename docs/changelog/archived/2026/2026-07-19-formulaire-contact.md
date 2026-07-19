---
date: 2026-07-19
type: feat
scope: full-stack
title: Formulaire de contact dans le pied de page
summary: Signaler un bug, envoyer un retour, un avis ou une demande commerciale depuis n'importe quelle page.
tags: [contact, footer, admin, email]
---

## Ce qui change

Un bouton « Nous contacter » apparaît dans le pied de page, sur toutes les pages.
Il ouvre un formulaire où l'on choisit un motif (bug, suggestion, avis ou contact
commercial), laisse son e-mail et écrit son message. À l'envoi, une confirmation
s'affiche et la demande part directement vers l'équipe.

## Pourquoi

Jusqu'ici, le seul moyen de nous joindre était un lien e-mail brut. Le formulaire
guide la demande (motif clair) et garantit qu'aucun message ne se perd.

## Détails

- Motifs proposés : bug / anomalie, suggestion, avis, contact commercial.
- Confirmation immédiate après l'envoi ; protection anti-spam transparente.
- Disponible en français et en anglais.

## Technique

- Entité `ContactMessage` (Postgres, données utilisateur) + migration ; lien compte
  optionnel et sécable (`SET NULL`).
- Endpoint `POST /contact` : DTO validé (`ContactSubmission`), CSRF, honeypot,
  rate-limiter `contact_form` (5 / h par IP), redirect-back + flash toaster.
- Notification e-mail via `ContactMailer` : `From` = `MAILER_FROM`, `Reply-To` =
  visiteur, destinataire = **`CONTACT_RECIPIENT`** (nouvelle variable d'env ; vide =
  inbox admin seule, sans e-mail). Ajoutée à compose + `.env*` (examples + prod/staging).
- Boîte de réception `/admin/contacts` (liste, filtre nouveau/traité, marquer
  traité / rouvrir / supprimer), entrée de nav « Messages ».
- Front : `<dialog>` natif dans le footer (ouverture déléguée `fx/enhance.ts`,
  Turbo-safe), primitives `hx-input`/`hx-select`/`hx-btn-gold`, POST classique.
