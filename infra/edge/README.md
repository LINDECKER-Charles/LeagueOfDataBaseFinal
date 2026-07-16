# 🌐 Edge proxy (VPS-global)

Point d'entrée TLS **unique** du VPS, partagé par **tous** les projets qui y sont
déployés. Basé sur [`caddy-docker-proxy`](https://github.com/lucaslorentz/caddy-docker-proxy) :
un seul Caddy possède `80/443`, découvre les routes depuis les **labels** des
conteneurs et gère seul les certificats Let's Encrypt (un store ACME unique).

Caddy lit l'API Docker via un **socket proxy en lecture seule**
([`docker-socket-proxy`](https://github.com/Tecnativa/docker-socket-proxy)) :
le `/var/run/docker.sock` brut n'est jamais monté dans le conteneur exposé.

```
        :80 / :443
   ┌──────── edge (ce stack) ────────┐
   │  caddy ──(RO)──▶ dockerproxy    │
   └───────────────┬─────────────────┘
         réseau externe « edge »
     ┌─────────────┼──────────────┐
 lodb-staging   lodb-prod     futur-projet
   (nginx)       (nginx)        (nginx)
```

## 🚀 Bootstrap

**Automatique via la pipeline** (`_deploy.yml`) : à chaque déploiement, le job
(re)converge le réseau `edge`, copie `infra/edge/{compose.yaml,Caddyfile}` vers
`/opt/edge`, y écrit `.env` depuis le secret `ACME_EMAIL`, et fait `up -d` — le tout
idempotent, avant de monter l'app. Rien à faire à la main sur un VPS neuf.

Prérequis : ports **80 + 443** ouverts, DNS des domaines pointés vers l'IP du VPS
**avant** le premier déploiement (sinon l'émission ACME échoue et Caddy réessaie
une fois le DNS propagé).

Bootstrap **manuel** (fallback / debug hors pipeline) :

```bash
docker network create edge 2>/dev/null || true
mkdir -p /opt/edge && cp infra/edge/compose.yaml infra/edge/Caddyfile /opt/edge/
cp infra/edge/.env.example /opt/edge/.env    # renseigne ACME_EMAIL
docker compose -f /opt/edge/compose.yaml --env-file /opt/edge/.env up -d
```

## ➕ Ajouter un projet (la « méthode »)

Aucune modification ici. Dans le `compose` du nouveau projet, sur le conteneur
public (nginx, front, …) :

```yaml
services:
  nginx:
    networks: [default, edge]
    labels:
      caddy: mon-domaine.com, www.mon-domaine.com   # domaines du projet
      caddy.reverse_proxy: "{{upstreams 80}}"        # port interne du conteneur

networks:
  edge:
    external: true
    name: edge
```

+ un `COMPOSE_PROJECT_NAME` unique par projet (isolation des namespaces Docker).
Caddy détecte le conteneur, émet le certificat et route automatiquement.

## 🔧 Exploitation

```bash
docker compose logs -f caddy                       # certs / challenges ACME
docker compose exec caddy wget -qO- localhost:2019/config/   # config effective
```

> ⚠️ Le volume `caddy_data` contient comptes ACME + certificats : **à conserver**
> (rate limits Let's Encrypt : 5 certs/domaine/semaine). Ne le purge que pour
> repartir d'un état propre, et pas en boucle.
