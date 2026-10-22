# 🚚 Edge proxy partagé — porté par le dépôt d'infrastructure `infra-vps`

> **Mise à jour 2026-08-23 — l'edge n'est plus porté par ce dépôt.** Le proxy TLS
> public du VPS (`caddy-docker-proxy` + `docker-socket-proxy`) et les réseaux Docker
> externes `edge` / `observability` sont désormais déployés par le **dépôt
> d'infrastructure `infra-vps` (privé)**, seul propriétaire de cette couche, **avant**
> tout projet applicatif. Ce guide ne décrit plus comment monter l'edge : il récapitule
> ce que ce projet attend de l'hôte et ce qu'il déclare pour être routé.
> Secrets et flux CI/CD : `docs/guides/github-actions-secrets.md`.

---

## Ce qui a changé

Jusqu'ici, `_deploy.yml` copiait sa propre copie de l'edge sur l'hôte et relançait le
proxy à chaque déploiement. Chaque repo applicatif faisait de même, et les copies ont
divergé : chaque déploiement écrasait l'edge posé par un autre projet. L'edge a donc été
sorti des repos applicatifs.

- Le dossier de l'edge a été **supprimé de ce dépôt** ; la pipeline ne copie plus rien
  sur l'hôte et n'écrit plus la configuration de l'edge.
- Le secret GitHub du **contact Let's Encrypt n'est plus requis** par ce dépôt : il est
  configuré dans `infra-vps`.
- `_deploy.yml` **vérifie** seulement que le réseau `edge` existe et **échoue
  explicitement** sinon (« Docker network 'edge' is missing — deploy infra-vps before
  this project »).
- Le self-heal TLS (smoke test HTTPS + redémarrage de Caddy) est **conservé**, mais il
  résout le conteneur Caddy par ses labels Compose (projet `edge`, service `caddy`),
  sans dépendre de l'emplacement des fichiers d'`infra-vps`.

---

## Prérequis sur l'hôte (dans l'ordre)

1. **Déployer `infra-vps`** : edge + réseaux `edge` et `observability`, plus `sauvegardes`
   sur l'hôte de la prod (le job de déploiement échoue si l'un d'eux manque).
2. Enregistrements DNS (A/AAAA) de `CADDY_DOMAINS` et `API_CADDY_DOMAINS` pointés vers
   l'hôte, ports **80/443** joignables — sinon l'émission ACME échoue (Caddy réessaie
   seul une fois le DNS propagé).
3. Puis seulement les projets applicatifs. Pour ce dépôt : `push dev` → déploie
   `lodb-staging`, merge `test → main` → déploie `lodb-prod`.

---

## ➕ Ajouter un projet au VPS (« la méthode »)

Aucune modification côté edge. Dans le compose du projet, sur son conteneur public
(exemple générique d'`infra-vps`, pas celui de LoDb) :

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

+ un `COMPOSE_PROJECT_NAME` unique par projet. Caddy détecte le conteneur, émet le
certificat et route. La définition de l'edge, l'onboarding détaillé (observabilité
comprise) et les limites mémoire recommandées vivent dans `infra-vps`.

Le bloc réel de LoDb est celui du service `nginx` de `compose.deploy.yaml` : nginx écoute
sur **8080**, et deux labels routent, `caddy_0` (site, `CADDY_DOMAINS`) et `caddy_1`
(sous-domaine `api.`, `API_CADDY_DOMAINS`), chacun avec
`reverse_proxy: "{{upstreams 8080}}"` et
`reverse_proxy.header_up: X-LoDb-Edge ${LODB_EDGE_SECRET}`. Sans cet en-tête, nginx répond
`403` ([`configuration.md`](configuration.md) § 6.7).

---

## Vérification post-déploiement

```bash
# Sur l'hôte
docker network ls | grep -E 'edge|observability|sauvegardes'
docker ps --format '{{.Names}} {{.Status}}' | grep -E '^edge|lodb'

# Depuis n'importe où : HTTP/2 200 et certificat Let's Encrypt valide
curl -sSI https://<domaine>/healthz
```

---

## 🔙 Dépannage

| Symptôme | Cause probable | Action |
|---|---|---|
| Le job s'arrête sur « Docker network 'edge' is missing » | `infra-vps` pas (encore) déployé sur cet hôte | déployer `infra-vps`, relancer le job |
| Site down après push mais edge `Up` | app pas sur `edge`, ou label absent | `docker inspect <nginx> --format '{{json .Config.Labels}}'` ; vérifier `CADDY_DOMAINS` |
| `403` de nginx sur tout le site LoDb (hors `/healthz`) | l'edge n'a pas encore relu les labels de nginx (quelques secondes après un `up`), ou `LODB_EDGE_SECRET` a changé d'un seul côté | attendre ; sinon redéployer : le job réécrit les labels et nginx depuis `.deploy/secrets/LODB_EDGE_SECRET` ([`configuration.md`](configuration.md) § 6.7) |
| « Edge Caddy container not found » pendant le self-heal TLS | le Caddy de l'edge ne tourne pas, ou sous un autre projet Compose que `edge` | `docker ps --filter label=com.docker.compose.project=edge` ; corriger côté `infra-vps` |
| Le job s'arrête sur « COMPOSE_PROJECT_NAME is '…' in the .env; staging expects 'lodb-staging' » (ou `prod`) | `COMPOSE_PROJECT_NAME` absent ou faux dans `ENV_*` : le job refuse plutôt que de laisser staging et prod se remplacer | le poser à `lodb-staging` ou `lodb-prod` dans le secret, redéployer |
| `stat …/compose.deploy.yaml: no such file` | `COMPOSE_FILE` polluée dans le shell | `unset COMPOSE_FILE` (le job l'exporte proprement lui-même) |
