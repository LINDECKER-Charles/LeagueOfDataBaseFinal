# App mobile Android (TWA / APK) — guide complet

> Objectif : distribuer LeagueOfDataBase en application Android (APK sideload,
> optionnellement Play Store) **sans toucher au front**, en réutilisant la PWA existante.

## TL;DR

L'app Android est une **Trusted Web Activity (TWA)** : une coquille native minimale qui
ouvre la prod `https://league-of-data-base.com` en plein écran via la PWA
(`manifest.webmanifest` + `sw.js`). On **ne réécrit rien** : l'APK affiche le site live.
La génération se fait sur [PWABuilder](https://www.pwabuilder.com/) (aucun outil à
installer). Le dépôt est déjà câblé ; il te reste 3 actions manuelles (§ Actions).

---

## 1. Comment ça fonctionne

```
┌─────────────── APK (TWA) ───────────────┐
│  Activité Android + Chrome Custom Tab    │   ← coquille native, ~1–2 Mo
│  plein écran, sans barre d'URL           │
└──────────────────┬───────────────────────┘
                   │  HTTPS (live)
                   ▼
        https://league-of-data-base.com      ← ta prod Symfony (inchangée)
                   │
        manifest.webmanifest  +  sw.js        ← PWA existante (cache network-first)
```

- **TWA vs WebView** : une TWA rend l'origine dans le moteur Chrome du téléphone (mêmes
  perfs/cookies/stockage que le navigateur), et non dans une WebView bridée. C'est la
  voie officielle Google, **acceptée sans friction sur le Play Store** (un simple WebView
  risque le rejet « minimum functionality »).
- **Coquille *online*, pas offline** : le contenu vient de la prod à chaque lancement.
  Le service worker ne met en cache que la **surface publique de consultation**
  (stratégie *network-first*) ; `/api`, `/admin`, auth, builds, profil et le flux SSE
  du loader sont **bypassés** (cf. `sw.js`). L'app exige donc de la connectivité pour
  l'essentiel — compromis assumé pour une encyclopédie (le refaire en SPA embarquée
  serait disproportionné et contraire à la décision archi « SPA rejetée »).
- **Digital Asset Links** : le fichier `/.well-known/assetlinks.json` déclare que ton
  domaine autorise *cet* APK (identifié par son empreinte de signature) à s'ouvrir sans
  barre d'adresse. Sans lui, l'app fonctionne mais affiche une barre type Chrome.

---

## 2. Ce qui a été fait dans le dépôt

| Fichier | Rôle | État |
|---|---|---|
| `app/public/.well-known/assetlinks.json` | Digital Asset Links — lie le domaine à l'APK | **Template** (2 valeurs à remplir) |
| `app/public/pwa/feature-graphic-1024x500.svg` | Visuel de fiche Play Store (1024×500), on-brand Hextech | Fourni, **à rasteriser en PNG** si publication Store |
| `docs/mobile/packaging-apk.md` | Ce guide | — |

**Aucune modification serveur nécessaire** — vérifié sur `docker/nginx/default.conf` :
`root = app/public`, `location / { try_files $uri /index.php... }` sert un fichier
physique tel quel *avant* le front controller ; aucune règle `deny` sur les dotfiles ;
`.json` → `application/json` par défaut ; Caddy edge proxifie sans intercepter
`/.well-known/` (hors `acme-challenge`). Déposer le fichier suffit.

### État des assets PWA (déjà complet)

Rien à générer côté PWA — tout est présent :

- Icônes : `favicon/icon-192.png`, `icon-512.png`, **`icon-maskable-192.png`**,
  **`icon-maskable-512.png`**, `apple-touch-icon.png`.
- Screenshots : `pwa/screen-narrow.png` (786×1704), `pwa/screen-wide.png` (1440×900).
- Manifest : `name`/`short_name`, `display: standalone`, `theme_color`, `shortcuts`,
  `categories` → **installable** (Lighthouse vert).

Le seul manque est le **feature graphic Play Store** (fourni ici en SVG). Il n'entre
**pas** dans le manifest : c'est un asset uploadé à la main dans la Play Console.

---

## 3. Actions manuelles OBLIGATOIRES

### 3.1 — Générer l'APK (PWABuilder)

1. Aller sur https://www.pwabuilder.com/ → saisir `https://league-of-data-base.com`.
2. Vérifier le rapport (manifest + SW verts) → **Package for stores** → **Android**.
3. Type : **Signed APK** (sideload) et/ou **AAB** (Play Store).
4. Réglages qui comptent :
   - **Package ID** (`applicationId`) : ex. `com.league_of_data_base.twa`
     (tirets interdits → underscores). ⚠️ **Immuable après publication.**
   - **Signing key** : *Create new* → PWABuilder génère un `signing.keystore`.
   - **Host** : `league-of-data-base.com`.
5. Télécharger le zip : `app-release-signed.apk`, `app-release-bundle.aab`,
   `signing.keystore` (+ alias/mots de passe), et un `assetlinks.json` **déjà rempli**.

### 3.2 — Sécuriser le keystore

🔐 Ranger `signing.keystore` + ses mots de passe dans un **gestionnaire de secrets, hors
Git**. Toute mise à jour de l'APK doit être signée avec **ce même keystore** : le perdre
= nouvelle identité d'app, patch de l'existant impossible.

### 3.3 — Poser le Digital Asset Links

Remplacer le template par le `assetlinks.json` du zip (il contient déjà `package_name`
et `sha256_cert_fingerprints`) dans `app/public/.well-known/assetlinks.json`, déployer,
puis vérifier :

```bash
curl -s https://league-of-data-base.com/.well-known/assetlinks.json
```

> Si tu passes par **Play App Signing**, l'empreinte à mettre ici est celle de la **clé
> de signature d'app de la Play Console**, pas celle du keystore d'upload local.

*(Alternative sans le zip : donne-moi le `package_name` + l'empreinte SHA-256, je remplis
le template et j'ajoute l'entrée `docs/changelog/` avant le commit.)*

---

## 4. Actions manuelles OPTIONNELLES (publication Play Store uniquement)

Inutiles pour un APK sideload.

- **Feature graphic 1024×500** : convertir `feature-graphic-1024x500.svg` → PNG/JPG
  (le Store refuse le SVG). Ex. via navigateur (ouvrir le SVG, capture 1024×500) ou
  `rsvg-convert -w 1024 -h 500 feature-graphic-1024x500.svg -o feature.png`.
  Le SVG est un point de départ éditable — raffine-le si besoin.
- **Icône Store** : `favicon/icon-512.png` (512×512, 32-bit alpha) convient.
- **Screenshots téléphone** : 2 à 8, `pwa/screen-narrow.png` réutilisable ; en ajouter
  1–2 (fiche objet/rune) renforce la fiche.
- **Compte Play Console** : 25 $ une fois, + politique de confidentialité (tu as déjà
  `legal.site_url`), classification de contenu, déclaration data safety.

---

## 5. Distribution

- **Sideload** : partager `app-release-signed.apk` (installation hors store ; activer
  « sources inconnues »). Suffit pour un usage perso/beta, aucun asset Store requis.
- **Play Store** : uploader `app-release-bundle.aab` + la fiche (§4).

---

## 6. Mettre à jour l'app

Le **contenu suit la prod automatiquement** (chargement live) : une évolution du site ne
nécessite **aucun** nouvel APK. Un rebuild n'est requis que si Package ID, icône, ou
réglages TWA changent → rejouer § 3.1 avec le **même keystore** et un `versionCode`
incrémenté.

---

## 7. Limites & trade-offs (à assumer)

- **Online-only** au-delà des pages déjà cachées par le SW (network-first). Pas d'offline
  natif complet.
- **Dépendance à la prod** : l'APK est inutilisable si le domaine est down. « Front-only »
  ne signifie pas « autonome » — le rendu reste server-side (Twig + îlots Vue).
- **iOS** : Apple n'a pas d'équivalent TWA et refuse les simples wrappers web sur l'App
  Store. Hors périmètre ici (Android uniquement).

---

## Checklist

- [ ] APK généré sur PWABuilder (Package ID validé)
- [ ] `signing.keystore` sauvegardé hors Git
- [ ] `assetlinks.json` du zip posé dans `app/public/.well-known/` + déployé
- [ ] `curl .../.well-known/assetlinks.json` renvoie les bonnes valeurs
- [ ] APK testé sur un téléphone (aucune barre d'URL = asset links OK)
- [ ] *(Store)* feature graphic PNG + screenshots + fiche Play Console
- [ ] Entrée `docs/changelog/` créée + commit
