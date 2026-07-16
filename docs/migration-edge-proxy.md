# 🚚 Migration — edge proxy partagé (VPS mono-hôte multi-projets)

Passage de l'ancien stack combiné `lodb` (Caddy embarqué, collision staging/prod)
à l'architecture cible : **un edge proxy global** au VPS (`caddy-docker-proxy` +
`docker-socket-proxy`) qui détient `80/443` et route par labels, chaque environnement
étant un **projet Compose distinct** (`lodb-staging` / `lodb-prod`).

> **L'edge est auto-bootstrappé par la pipeline** (`_deploy.yml`) : réseau `edge`,
> fichiers `/opt/edge` (copiés du repo) et proxy sont (re)convergés à chaque déploiement,
> avant l'app. Rien à monter à la main. La seule action one-shot ci-dessous est de
> **libérer les ports 80/443** encore tenus par le legacy `lodb`.
> Contexte : `infra/edge/README.md`, `docs/github-actions-secrets.md`.

---

## 1. One-shot sur le VPS — démonter le legacy `lodb`

Tant que l'ancien Caddy (projet `lodb`) tourne, il tient `80/443` et l'edge ne peut
pas démarrer. À faire **une fois** :

```bash
# Démonte l'ancien stack combiné (par nom de projet, sans fichier compose requis)
docker compose -p lodb down --remove-orphans

# Purge le volume ACME pollué (mélange test.* + apex). NE PAS utiliser -v global (garde minio).
docker volume rm lodb_caddy_data lodb_caddy_config 2>/dev/null || true

# Vérifie que plus rien ne tient 80/443
ss -ltnp | grep -E ':80|:443' || echo "80/443 libres ✅"
```

> Si tu avais commencé un `/opt/edge` à la main, tu peux le laisser : la pipeline
> écrase `compose.yaml` / `Caddyfile` et (ré)écrit `.env` depuis le secret `ACME_EMAIL`.

---

## 2. Secrets GitHub (Settings → Secrets and variables → Actions)

| Secret | Valeur |
|---|---|
| `ACME_EMAIL` | **nouveau** — contact Let's Encrypt de l'edge (ex. `charles.lindecker@outlook.fr`) |
| `ENV_STAGING` | contenu de `.env.staging` — inclut `COMPOSE_PROJECT_NAME=lodb-staging`, **sans** `ACME_EMAIL` |
| `ENV_PROD` | contenu de `.env.prod` — inclut `COMPOSE_PROJECT_NAME=lodb-prod`, **sans** `ACME_EMAIL`, `APP_SECRET` régénéré (aucun `$`) |

---

## 3. DNS

Enregistrements **A/AAAA** vers l'IP du VPS :

- `test.league-of-data-base.com` (staging)
- `league-of-data-base.com` (prod)
- `league-of-data-base.fr` (prod) ← **à ajouter**

Le cert `.fr` s'émettra une fois le DNS propagé ; `.com` et `test.*` partent
immédiatement, Caddy réessaie `.fr` seul. DNS `.fr` non prêt ≠ bloquant pour le reste.

---

## 4. Push → tout se déroule automatiquement

- `push dev` → pipeline → bootstrap edge (si absent) + déploie `lodb-staging` (`test.*`)
- merge `test → main` → `push main` → bootstrap edge (si absent) + déploie `lodb-prod` (`.fr`/`.com`)

Chaque stack rejoint `edge` ; Caddy lit les labels `caddy:` sur nginx, émet les certs
et route. Déploiements suivants : 100 % auto, aucune action.

---

## 5. Vérification post-déploiement

```bash
# Sur le VPS
docker compose -f /opt/edge/compose.yaml logs -f caddy    # "certificate obtained" par domaine
docker ps --format '{{.Names}}\t{{.Status}}' | grep -E 'edge|lodb'

# Depuis n'importe où
curl -sSI https://test.league-of-data-base.com/healthz
curl -sSI https://league-of-data-base.com/healthz
curl -sSI https://league-of-data-base.fr/healthz          # une fois DNS .fr propagé
```

Attendu : `HTTP/2 200` sur chaque domaine, certificat Let's Encrypt valide.

---

## ➕ Ajouter un futur projet au VPS (« la méthode »)

Aucune modif sur l'edge. Dans le compose du nouveau projet, sur son conteneur public :

```yaml
services:
  nginx:
    networks: [default, edge]
    labels:
      caddy: mon-domaine.com, www.mon-domaine.com
      caddy.reverse_proxy: "{{upstreams 80}}"
networks:
  edge:
    external: true
    name: edge
```

+ un `COMPOSE_PROJECT_NAME` unique. Caddy détecte, émet le cert, route. Fin.

> ⚠️ **DRY inter-repos** : l'edge est aujourd'hui bootstrappé depuis `infra/edge/`
> de CE repo. Si d'autres projets (autres repos) bootstrappent aussi l'edge depuis
> leur propre copie, garde une définition **identique** partout — ou, mieux à terme,
> extrais l'edge dans son propre repo/déploiement pour qu'une seule source fasse foi.

---

## 🔙 Dépannage

| Symptôme | Cause probable | Action |
|---|---|---|
| edge ne démarre pas, `bind: address already in use` | legacy `lodb` (ou autre) tient 80/443 | `docker compose -p lodb down` ; `ss -ltnp \| grep -E ':80\|:443'` |
| Site down après push mais edge `Up` | app pas sur `edge`, ou label absent | `docker inspect <nginx> --format '{{json .Config.Labels}}'` ; vérifier `CADDY_DOMAINS` |
| Cert `.fr` en échec | DNS `.fr` pas propagé | attendre ; `dig +short league-of-data-base.fr` |
| Collision réapparaît | `COMPOSE_PROJECT_NAME` manquant dans un `ENV_*` | l'ajouter au secret, redéployer |
| `stat …/compose.deploy.yaml: no such file` | `COMPOSE_FILE` polluée dans le shell | `unset COMPOSE_FILE` (le job l'exporte proprement lui-même) |

---

## 📎 Annexe — bootstrap edge manuel (fallback hors pipeline)

Normalement inutile (la pipeline s'en charge). Utile pour tester l'edge avant tout
push, ou en debug. Depuis un checkout du repo sur le VPS :

```bash
docker network create edge 2>/dev/null || true
mkdir -p /opt/edge && cp infra/edge/compose.yaml infra/edge/Caddyfile /opt/edge/
printf 'ACME_EMAIL=%s\n' 'charles.lindecker@outlook.fr' > /opt/edge/.env
docker compose -f /opt/edge/compose.yaml --env-file /opt/edge/.env up -d
```
