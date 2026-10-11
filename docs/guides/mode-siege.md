# Mode siège — runbook préparé, non activé

Deux réponses graduées, **prêtes mais éteintes**, aux abus que les protections ordinaires du
site ne bornent pas : (a) un crawl réparti sur des milliers d'adresses résidentielles,
(b) une attaque volumétrique. Rien dans le dépôt ne les active aujourd'hui : chacune demande
une décision du propriétaire, puis les étapes ci-dessous.

- Où lire les signaux : [`observabilite.md`](observabilite.md) (journal de l'edge, LogsQL) et
  [`metriques.md`](metriques.md) (PromQL, noms des métriques).
- Ce qui borne déjà le coût, étage par étage : `docker/nginx/sites/site.conf` (débits par
  adresse), admission des rendus du SSR (`src/server/admission`), porte des lectures froides
  de l'API, refus d'un User-Agent exact (`docker/nginx/nginx.conf`).
- Les étapes nomment `ENV_STAGING` et `ENV_PROD`. Une fois le
  [déploiement par clé restreinte](deploiement-cle-restreinte.md) en place, c'est le `.env`
  chiffré du poste, posé sur l'hôte par `lodb-env` (§ 7 de ce guide) ; la relance du dernier
  déploiement ne change pas.

## 1. Avant le siège : ce qui protège déjà

| Étage | Protection en place | Ce qu'elle ne voit pas |
|---|---|---|
| nginx | `limit_req` par adresse (pages, lectures publiques de l'API, ancien site, sitemaps) ; refus d'un User-Agent exact | un client réparti sur des centaines d'adresses lentes |
| SSR | couloir des versions épinglées : budget global et part par client ; `503` + `Retry-After` | une fois le budget du couloir épuisé, les lecteurs humains des anciens patchs sont refusés aussi |
| API | porte des lectures froides, budgets d'ingestion à la demande | rien : elle borne le coût, pas le nombre de requêtes |
| Cache | pages des anciens patchs gardées au-delà des déploiements | un balayage d'URL toutes distinctes est toujours un MISS |

Un mode siège ne s'envisage que quand ces étages tiennent le CPU **mais** refusent des
humains (a), ou quand le coût du refus lui-même devient le problème (b).

| Situation | Réponse |
|---|---|
| Pic légitime (sortie de patch) : pages du dernier patch, référents de moteurs ou de réseaux sociaux, statuts 200 | aucune : c'est de la capacité |
| Crawl concentré sur un User-Agent exact | ajouter la chaîne à `$lodb_blocked_agent` (`nginx.conf`), jamais un motif |
| Crawl concentré sur les adresses d'un hébergeur (AS « cloud ») | hors mode siège : budgets séparés par classe de clients (hébergeurs, robots vérifiés, visiteurs), à développer sur ce déclencheur |
| Crawl par proxys résidentiels | **mode (a)**, § 3 |
| Attaque volumétrique | **mode (b)**, § 4 |

## 2. Mesurer

Dans Grafana, *Explore* : source `victorialogs` pour le journal de l'edge, `victoriametrics`
pour les métriques. Hôtes de prod : `league-of-data-base.com`, `league-of-data-base.fr` et
leurs `api.*` ; le staging (`test.*`) partage l'hôte, ne pas le confondre. D'où
`hote:in(…)`, qui compare la valeur entière : un filtre de phrase (`hote:"…"`) prendrait
aussi `test.` et `api.`.

```logsql
# (a) Adresses distinctes par heure sur les pages d'une version épinglée (/{locale}/{version}/…)
stack:edge service:caddy journal:acces schema:https _time:6h
  hote:in("league-of-data-base.com", "league-of-data-base.fr")
  chemin:~"^/[a-z]{2}(-han[st])?/[0-9]"
  | stats by (_time:1h) count_uniq(ip_client) adresses, count() requetes

# (a) Ces adresses par système autonome : un hébergeur qui domine, ou une poussière de FAI ?
stack:edge service:caddy journal:acces schema:https _time:1h
  hote:in("league-of-data-base.com", "league-of-data-base.fr")
  chemin:~"^/[a-z]{2}(-han[st])?/[0-9]"
  | stats by (asn_org) count_uniq(ip_client) adresses, count() requetes
  | sort by (adresses desc) | limit 20

# (a) Volume par adresse : un humain lit quelques pages, un proxy résidentiel aussi
stack:edge service:caddy journal:acces schema:https _time:1h
  hote:in("league-of-data-base.com", "league-of-data-base.fr")
  chemin:~"^/[a-z]{2}(-han[st])?/[0-9]"
  | stats by (ip_client) count() requetes
  | stats count() adresses, quantile(0.5, requetes) mediane, max(requetes) maxi

# (a) et (b) Agents : un seul User-Agent sur des milliers d'adresses signe un outil
stack:edge service:caddy journal:acces schema:https _time:1h
  hote:in("league-of-data-base.com", "league-of-data-base.fr")
  | stats by (agent) count_uniq(ip_client) adresses, count() requetes
  | sort by (adresses desc) | limit 10

# (b) Qui envoie le volume, et ce qu'il reçoit
stack:edge service:caddy journal:acces _time:15m
  hote:in("league-of-data-base.com", "league-of-data-base.fr",
          "api.league-of-data-base.com", "api.league-of-data-base.fr")
  | stats by (asn_org, statut) count() requetes, count_uniq(ip_client) adresses
  | sort by (requetes desc) | limit 20
```

```promql
# (a) Refus du couloir épinglé, par raison. `capacity`, `lane_capacity` et `lane_rate`
# (plafonds globaux) touchent tout le monde ; `client_*` (part d'un client) est le système
# qui marche.
sum by (reason) (rate(lodb_ssr_renders_total{stack="lodb-prod", lane="pinned", outcome="refused"}[5m]))

# (b) Cœurs consommés par l'edge et par le nginx de LoDb
sum(conteneur:cpu_coeurs:moy5m{stack="edge", service="caddy"})
  + sum(conteneur:cpu_coeurs:moy5m{stack="lodb-prod", service="nginx"})

# (b) Part des réponses de LoDb qui sont des refus (429, 503), vue de l'edge
sum(edge:delestage_s:moy5m{hote=~"(api[.])?league-of-data-base[.](com|fr)"})
  / sum(edge:reponses_s:moy5m{hote=~"(api[.])?league-of-data-base[.](com|fr)"})

# (b) Débit reçu par l'hôte (bit/s), puis paquets perdus, hors interfaces internes
8 * sum(rate(node_network_receive_bytes_total{device!~"lo|veth.*|docker.*|br-.*"}[5m]))
sum(rate(node_network_receive_drop_total{device!~"lo|veth.*|docker.*|br-.*"}[5m]))
```

Alertes d'`infra-vps` qui amènent ici : `LodbSsrDelestageEpingle`, `LodbSsrRefuseDesVisiteurs`,
`SiteDelestageMassif`, `SiteTraficAnormal`, `HoteCpuRisqueBridage`, `ConteneurCpuBride`.

## 3. Mode (a) — Anubis devant les seules pages épinglées

### Déclencheur et décision

Les trois signes, tenus **deux heures** (une seule si `LodbSsrRefuseDesVisiteurs` sonne aussi) :

1. plus de **1 000 adresses distinctes par heure** sur les pages épinglées ;
2. **aucun AS d'hébergeur** ne domine : le premier AS porte moins de 20 % des adresses et les
   dix premiers sont des fournisseurs d'accès ; la médiane par adresse reste basse (quelques
   requêtes par heure) ;
3. le couloir épinglé refuse par **plafond global** (`capacity`, `lane_capacity`,
   `lane_rate`), donc des humains.

Décide : le propriétaire du site. Aucun accord d'`infra-vps` n'est nécessaire : tout se passe
dans la stack de LoDb.

### Ce qui change

```
edge ─> nginx ─┬─ /{locale}/{version}/…  auth_request ─> anubis ─(200)─> cache des anciens patchs ─> SSR
               │                          └─(401)─> 307 /.within.website/?redir=…  (défi, une fois)
               ├─ /.within.website/…  ─> anubis (page de défi, vérification)
               └─ tout le reste : inchangé (dernier patch, /api, /v1, /webhooks, /build, /cdn)
```

[Anubis](https://anubis.techaro.lol/) en **mode sous-requête** : nginx lui demande, pour
chaque page épinglée, si le client a déjà prouvé qu'il exécute un navigateur (preuve de
travail, cookie signé lié à son adresse, 7 jours). Sinon, il renvoie le client vers la page
de défi, qui le ramène ensuite à sa page. Les robots des moteurs sont exemptés **par leur
User-Agent et leurs plages d'adresses publiées**, ensemble. nginx pose `X-Real-IP` à partir
de l'adresse qu'il a résolue (seul l'edge peut l'énoncer) : Anubis ne croit rien d'autre.

### Mise en place : une PR, inerte par défaut

Le changement tient dans une PR qui ne fait **rien** tant que le `.env` de l'hôte ne le
demande pas : elle peut être fusionnée à froid, et l'activation se réduit alors à une ligne du
`.env` et un redéploiement.

| Fichier | Changement |
|---|---|
| `docker/anubis/policy.yaml` | la politique ci-dessous |
| `docker/nginx/nginx.conf` | `upstream lodb_anubis { zone lodb_anubis 64k; server lodb-anubis:8923 resolve; }` |
| `docker/nginx/snippets/siege-gate-off.conf`, `siege-gate-anubis.conf` | niveau location : vide ; `auth_request` (ci-dessous) |
| `docker/nginx/snippets/siege-routes-off.conf`, `siege-routes-anubis.conf` | niveau server : vide ; routes d'Anubis (ci-dessous) |
| `docker/nginx/sites/site.conf` | `include …/siege-routes-${LODB_SIEGE}.conf;` dans le server du site, `include …/siege-gate-${LODB_SIEGE}.conf;` dans la location des pages épinglées |
| `docker/nginx/Dockerfile`, `compose.yaml` | `ENV LODB_SIEGE=off` ; `LODB_SIEGE: ${LODB_SIEGE:-off}` pour nginx (jamais en clé nue dans `compose.deploy.yaml`) |
| `compose.siege.yaml` | le service `anubis` (ci-dessous) |
| `tools/deploy/split-env.sh`, `.env.prod.example` | secret `LODB_ANUBIS_KEY` ; ligne `LODB_SIEGE=off` commentée |
| `.github/workflows/_deploy.yml` | `LODB_SIEGE` vaut `off` ou `anubis`, rien d'autre ; `anubis` ajoute `compose.siege.yaml` aux calques et exige le secret non vide |
| `tools/nginx/test`, `tools/compose/test`, `tools/deploy/test` | rendu `off` sans `auth_request`, rendu `anubis` avec, location `^~ /.within.website/` prioritaire sur la garde des fichiers cachés ; calque servi ; cas du job ; `compose-secrets.test.mjs`, qui exige aujourd'hui que chaque clé de `split-env.sh` soit déclarée par `compose.deploy.yaml` et montée par l'API, en exempte `LODB_ANUBIS_KEY`, vérifiée dans le rendu avec `compose.siege.yaml`, montée par `anubis` seul |
| `features/editorial/legal/texts/cookies-{fr,en}.html` | les deux cookies d'Anubis (`lodb-siege-auth-…`, `lodb-siege-cookie-verification-…`), posés seulement pendant un épisode d'abus ; entrée de changelog dans le même commit |

```nginx
# snippets/siege-gate-anubis.conf : la location des pages épinglées demande à Anubis.
auth_request /.within.website/x/cmd/anubis/api/check;
error_page   401 = @lodb_siege_challenge;
# Une location qui déclare son propre error_page répète celui des refus de débit.
error_page   429 @lodb_rate_limited;
```

```nginx
# snippets/siege-routes-anubis.conf : niveau server. `^~` passe avant la garde des
# fichiers cachés (snippets/server-guards.conf), qui refuserait tout chemin en `/.`.
location ^~ /.within.website/ {
    proxy_pass              http://lodb_anubis;
    proxy_set_header        Host              $host;
    proxy_set_header        X-Real-IP         $remote_addr;
    proxy_set_header        X-Forwarded-For   $remote_addr;
    proxy_set_header        X-Forwarded-Proto $lodb_forwarded_proto;
    proxy_pass_request_body off;
    proxy_set_header        Content-Length    "";
    include                 /etc/nginx/snippets/security-headers.conf;
}

location @lodb_siege_challenge {
    absolute_redirect off;
    add_header Cache-Control "no-store" always;
    include    /etc/nginx/snippets/security-headers.conf;
    return     307 /.within.website/?redir=$request_uri;
}
```

```yaml
# docker/anubis/policy.yaml — lu au démarrage d'Anubis (un changement = un redémarrage).
bots:
  # Moteurs reconnus par leur User-Agent ET leurs plages publiées (Google, Bing, Apple…).
  - import: (data)/crawlers/_allow-good.yaml
  # Tous les autres prouvent un navigateur une fois, puis gardent le cookie.
  - name: lodb-pages-epinglees
    path_regex: .*
    action: CHALLENGE
# Mode sous-requête : nginx lirait un DENY en 200 comme une autorisation.
status_codes:
  CHALLENGE: 200
  DENY: 403
honeypot:
  enabled: false
# Une ligne par requête noierait le journal (ADR 0010) : avertissements seulement.
logging:
  sink: stdio
  level: WARN
```

```yaml
# compose.siege.yaml — ajouté par le job quand le .env porte LODB_SIEGE=anubis.
services:
  anubis:
    # Version et empreinte relevées à l'activation : une version d'au moins deux semaines.
    image: ghcr.io/techarohq/anubis:v1.27.0@sha256:<empreinte>
    environment:
      BIND: ":8923"
      TARGET: " "                       # mode sous-requête : nginx sert la page lui-même
      POLICY_FNAME: /etc/anubis/policy.yaml
      COOKIE_PREFIX: lodb-siege
      REDIRECT_DOMAINS: ${CADDY_DOMAINS:?CADDY_DOMAINS is required}
      # Clé de signature stable : un redémarrage ne redemande pas le défi à tout le monde.
      ED25519_PRIVATE_KEY_HEX_FILE: /run/secrets/anubis-key
    secrets:
      - source: LODB_ANUBIS_KEY
        target: anubis-key
    volumes:
      - ./docker/anubis:/etc/anubis:ro
    networks:
      default:
        aliases: [lodb-anubis]
    read_only: true
    restart: unless-stopped
    # Interroge le serveur de métriques d'Anubis (:9090, jamais publié) : forme exec, l'image
    # n'a pas de shell. `up --wait` du déploiement attend ce verdict.
    healthcheck:
      test: ["CMD", "anubis", "--healthcheck"]
      interval: 30s
      timeout: 5s
      retries: 3
      start_period: 10s
    logging:
      driver: json-file
      options: {max-size: "10m", max-file: "3"}
    deploy:
      resources:
        limits: {memory: 128m, cpus: "0.25", pids: 64}

secrets:
  LODB_ANUBIS_KEY:
    file: ./.deploy/secrets/LODB_ANUBIS_KEY
```

Le stockage reste en mémoire (défaut d'Anubis) : il ne tient que les défis en cours ; les
cookies déjà délivrés survivent à un redémarrage grâce à la clé stable.

### Activation

1. **Répétition locale** sur un emplacement ([`developpement.md`](developpement.md)) avec
   `LODB_SIEGE=anubis`, `CADDY_DOMAINS=localhost`, `-f compose.siege.yaml` après
   `compose.override.yaml` et une clé de test dans `.deploy/secrets/LODB_ANUBIS_KEY` : un
   navigateur passe le défi et revient sur sa page ; `curl` reçoit le `307` ; le dernier patch
   et `/api/meta` répondent `200` sans défi. Supprimer l'emplacement et la clé de test après.
2. Clé : `openssl rand -hex 32`, ajoutée en `LODB_ANUBIS_KEY` au `.env` chiffré de
   l'environnement ([`secrets-poste.md`](secrets-poste.md)), avec `LODB_SIEGE=anubis`.
3. Staging d'abord (`ENV_STAGING`, relance du dernier déploiement de `staging`), vérifications
   ci-dessous ; puis `ENV_PROD` et relance du dernier `deploy-prod`. Une relance lit les
   secrets tels qu'ils sont au moment de la relance.

### Vérifier

```bash
host=https://league-of-data-base.com
curl -sI "$host/en/15.1.1/champions" | grep -iE '^(HTTP|location)'   # 307, /.within.website/?redir=…
curl -sI "$host/en/champions" | head -n 1                            # 200 : dernier patch intact
curl -s -o /dev/null -w '%{http_code}\n' "$host/api/meta"            # 200 : API intacte
# Un User-Agent de moteur hors de ses plages est défié comme les autres
curl -sI -A 'Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)' \
  "$host/en/15.1.1/champions" | head -n 1                            # 307
```

Puis : Search Console, *Inspection de l'URL* › *Tester l'URL en direct* sur une page épinglée
(rendue, sans défi). Dans le journal de l'edge, les défis et leurs réussites :

```logsql
stack:edge service:caddy journal:acces hote:in("league-of-data-base.com") _time:1h
  chemin:~"^/[.]within[.]website/"
  | stats by (chemin, statut) count() requetes, count_uniq(ip_client) adresses
```

L'effet attendu : les refus globaux du couloir épinglé retombent, les adresses distinctes
sur les pages épinglées aussi (les clients sans navigateur restent sur la page de défi).

### Effets et limites (assumés pendant le siège)

- Un humain fait un défi de quelques secondes par adresse et par semaine ; un mobile qui
  change d'adresse le refait. JavaScript devient nécessaire sur les pages épinglées.
- Au retour du défi, seul le premier paramètre de la query est gardé (`redir` n'est pas
  encodé) : un filtre multiple d'une liste d'ancien patch est à resélectionner.
- Les aperçus de liens (messageries) d'une page épinglée montrent la page de défi ; un moteur
  absent de la liste vérifiée peut perdre ces pages le temps de l'épisode.
- **Fermé en cas de panne** : si Anubis ne répond pas, les pages épinglées répondent `500`
  (`auth_request` en erreur), le reste du site est intact. `SiteEnPanne` peut sonner :
  retour arrière.
- La page de défi exécute le script d'Anubis sur l'origine du site : image épinglée par
  empreinte, page de défi seule, le temps de l'épisode seulement.
- Le miroir d'analytics ne compte que les requêtes admises.

### Retour arrière

`LODB_SIEGE=off` (ou la ligne retirée) dans `ENV_PROD`, relance du dernier `deploy-prod` :
nginx revient sans `auth_request`, `anubis` part en orphelin. En urgence, sur l'hôte, dans
`$PROD_PATH` : `LODB_SIEGE=off` dans le `.env`, `compose.siege.yaml` retiré de sa ligne
`COMPOSE_FILE`, puis `docker compose up -d --remove-orphans` ; corriger `ENV_PROD` aussitôt,
sinon le déploiement suivant le réactive. Sortie proposée : 24 h sous 300 adresses
distinctes par heure sur les pages épinglées.

## 4. Mode (b) — Cloudflare Free devant les seuls domaines de LoDb

### Déclencheur et décision

L'un des deux, tenu 15 minutes :

- l'edge et le nginx de LoDb consomment **plus de 0,3 cœur** alors que la majorité des
  réponses de LoDb sont des refus (`429`, `503`) : refuser coûte à lui seul ce que le site ne
  peut pas payer, sur un CPU partagé avec les autres projets de l'hôte ;
- **bande passante saturée** : débit reçu en plateau au plafond du port de l'offre, paquets
  perdus en hausse, sonde HTTP externe en échec alors que l'hôte répond en interne.

Décide : le propriétaire du site, avec l'**accord explicite** du propriétaire d'`infra-vps`
(options globales de l'edge, partagé par tous les projets de l'hôte).

### Préparation à froid (à trancher à l'avance)

Le passage par Cloudflare suppose que la zone DNS y soit hébergée. Un changement de serveurs
de noms se propage en **jusqu'à 48 h** : décidé le jour de l'attaque, il arrive trop tard.
Option : héberger dès maintenant les deux zones chez Cloudflare, **tous les enregistrements en
« DNS only »** (nuage gris) : aucun trafic ne passe par Cloudflare, et l'activation tient
alors en quelques minutes. À la migration : recopier **tous** les enregistrements (MX, SPF,
DKIM, DMARC, CAA, `test`, `api.test`), vérifier que les CAA admettent aussi les autorités des
certificats de bord de Cloudflare, désactiver DNSSEC chez le registraire avant le changement
et le réactiver avec le DS de Cloudflare après.

### Activation, dans l'ordre

Deux règles fixent l'ordre. L'adresse réelle du visiteur doit traverser toute la chaîne
**avant** le nuage orange : sinon chaque borne par adresse (nginx, SSR, API) ne verrait que
quelques adresses de Cloudflare et refuserait tout le monde. Et l'origine n'exige le
certificat de Cloudflare **qu'après** : un visiteur encore en direct serait refusé.

Les certificats du site ne changent pas : l'edge garde ceux de Let's Encrypt, que Cloudflare
accepte en *Full (strict)* et que l'edge renouvelle à travers Cloudflare par le défi HTTP (d'où
*Always Use HTTPS* coupé : l'edge redirige lui-même vers HTTPS). Pour un siège de plusieurs
mois, un certificat d'origine Cloudflare (15 ans) retire cette dépendance, au prix d'un bloc
de site par zone et d'un retour arrière qui doit d'abord rendre un certificat public.

1. **Autorité des tirages authentifiés** : une autorité propre (`openssl`), son certificat
   posé dans `/etc/edge/certs/lodb-aop-ca.pem` de l'hôte ; le certificat client qu'elle signe
   est téléversé dans Cloudflare (*SSL/TLS › Origin Server › Authenticated Origin Pulls*,
   niveau **zone**, certificat propre) **dans chacune des deux zones**, `.com` et `.fr` :
   une zone oubliée verrait ses hôtes, dont `api.league-of-data-base.fr`, refusés par
   l'origine à l'étape 7. Jamais le certificat global de Cloudflare.
2. **infra-vps** (accord) : dans le `Caddyfile`, au bloc `servers :443`, à côté de `trace` :
   `trusted_proxies static <plages>` et `client_ip_headers CF-Connecting-IP`, les plages
   relevées le jour même (`curl -s https://api.cloudflare.com/client/v4/ips`, champs
   `ipv4_cidrs` et `ipv6_cidrs`) ; dans le snippet `(umami)`, `header_up X-Forwarded-For
   {client_ip}` sur les deux `reverse_proxy`. `./scripts/validate.sh`, puis déploiement
   d'`infra-vps`. Sans effet pour les autres sites, dont les requêtes n'arrivent pas de ces
   plages.
3. **LoDb, `LODB_CLOUDFLARE=proxy`** dans `ENV_PROD`, déploiement : le calque
   `compose.cloudflare.yaml` (prod seule, ajouté par `_deploy.yml`) transmet l'adresse que
   l'edge a résolue, sans effet tant que le nuage est gris :

   ```yaml
   services:
     nginx:
       labels:
         caddy_0.reverse_proxy.header_up: X-Forwarded-For {client_ip}
         caddy_1.reverse_proxy.header_up: X-Forwarded-For {client_ip}
   ```

4. **Réglages Cloudflare** (tableau ci-dessous), nuage encore gris.
5. **Nuage orange** sur `league-of-data-base.com`, `league-of-data-base.fr` et leurs `api.*`.
   `test` et `api.test` restent gris : le staging ne passe jamais par Cloudflare.
6. **Vérifier** (ci-dessous) dans les dix minutes ; au moindre doute sur l'adresse réelle,
   nuage gris aussitôt.
7. Une heure plus tard au moins, **`LODB_CLOUDFLARE=aop`**, déploiement : le calque
   `compose.cloudflare-aop.yaml` s'ajoute au précédent, et l'edge n'accepte plus pour ces
   hôtes qu'une connexion qui présente le certificat signé par l'autorité de l'étape 1 :

   ```yaml
   services:
     nginx:
       labels:
         caddy_0.tls.client_auth.mode: require_and_verify
         caddy_0.tls.client_auth.trust_pool: file /certs/lodb-aop-ca.pem
         caddy_1.tls.client_auth.mode: require_and_verify
         caddy_1.tls.client_auth.trust_pool: file /certs/lodb-aop-ca.pem
   ```

   Chaque clé de label garde son index (`caddy_N.`) : une clé sans index viderait l'edge de
   ses options globales. Avant ce déploiement : adapter le bloc équivalent avec l'image de
   l'edge (`IMG_CADDY` d'`infra-vps`), `caddy validate --adapter caddyfile`. Le job vérifie
   que `/certs/lodb-aop-ca.pem` existe dans le conteneur de l'edge avant `up`. Effet hors de
   LoDb, couvert par l'accord de l'étape 2 : dès qu'un site exige un certificat client, Caddy
   impose à **tout** le serveur `:443`, donc aux autres projets de l'hôte, qu'une requête
   porte le même nom dans `Host` et dans le SNI (`enabling strict SNI-Host enforcement`,
   relevé par `caddy validate` sur cette image) ; les navigateurs le respectent, un outil
   qui les dissocie est refusé.
8. Politique de confidentialité (`legal/texts/privacy-{fr,en}.html`, § 5 sous-traitants et
   § 6 transferts : Cloudflare, adresse IP traitée, transfert encadré) et politique de
   cookies (`cf_clearance`, `__cf_bm` s'ils apparaissent), avec une entrée de changelog.
   Texte préparé à froid, publié au plus tard avec l'activation.

La PR qui porte les deux calques, les valeurs `off`, `proxy` et `aop` de `LODB_CLOUDFLARE`
(`proxy` et `aop` refusés hors de la prod), le contrôle du fichier de l'autorité et leurs
cas dans `tools/compose/test` et `tools/deploy/test` est, comme celle du mode (a), inerte à
`off` : elle peut être fusionnée à froid.

| Réglage Cloudflare | Valeur |
|---|---|
| *SSL/TLS* | **Full (strict)** ; *Always Use HTTPS* **coupé** ; TLS 1.2 minimum ; *Authenticated Origin Pulls* niveau zone, certificat de l'étape 1 |
| *Cache Rules* : contourner le cache | `http.host in {"api.league-of-data-base.com" "api.league-of-data-base.fr"} or starts_with(http.request.uri.path, "/api/") or starts_with(http.request.uri.path, "/v1/") or starts_with(http.request.uri.path, "/webhooks/") or starts_with(http.request.uri.path, "/admin") or http.request.uri.path contains "/account" or http.request.uri.path eq "/u/api/send" or http.request.uri.path eq "/"` |
| Ce que Cloudflare garde | ses extensions statiques par défaut : `/build/` et `/cdn/blobs/` (`immutable` de l'origine). Le HTML n'est pas éligible : le rendre éligible (respect du `s-maxage`) imposerait une purge à chaque déploiement |
| *Configuration Rules* | *Browser Integrity Check* coupé sur les hôtes `api.*`, `/api/`, `/v1/`, `/webhooks/` : les apps, Stripe et les clients de l'API publique ne sont pas des navigateurs |
| *Security* | *Bot Fight Mode* **coupé** ; *I'm Under Attack* jamais sur toute la zone ; la protection DDoS de couche 7 reste celle par défaut |
| *Speed*, *Scrape Shield* | **coupés** : Rocket Loader, Email Address Obfuscation, Automatic HTTPS Rewrites, Cloudflare Fonts ; *Web Analytics* sans injection automatique, pas de Zaraz |

`/` est contourné parce que sa redirection dépend du cookie de langue et d'`Accept-Language`,
que le cache de Cloudflare ignore. `/api/` ne passe jamais par un cache intermédiaire : la
réponse `426` des apps doit toujours être calculée.

### Vérifier

```bash
curl -sI https://league-of-data-base.com/en/ | grep -iE '^(HTTP|cf-ray|server)'   # cf-ray présent
curl -s -o /dev/null -w '%{http_code}\n' https://api.league-of-data-base.com/healthz  # 200
# Origine en direct, étape 7 faite : poignée de main refusée sans le certificat client
curl -sS --resolve league-of-data-base.com:443:<adresse-du-vps> https://league-of-data-base.com/healthz
```

```logsql
# L'adresse réelle traverse : « Cloudflare » ne doit PAS dominer les systèmes autonomes
stack:edge service:caddy journal:acces hote:in("league-of-data-base.com") _time:10m
  | stats by (asn_org) count_uniq(ip_client) adresses | sort by (adresses desc) | limit 5
```

Puis : un paiement de test passe et son webhook arrive ; l'app Android et le desktop ouvrent
une fiche ; les refus `429` de nginx ne montent pas.

### Effets et limites

- Les HIT de Cloudflare n'atteignent plus l'origine : le journal de l'edge et les métriques de
  Caddy sous-comptent, `SiteTraficAnormal` aussi. La mesure d'audience (`/u/api/send`) n'est
  pas touchée.
- Cloudflare coupe une réponse muette au-delà de 100 s (`524`) : le flux de préchauffage
  doit émettre plus souvent.
- Le contrôle TLS de fin de déploiement passe par Cloudflare : il vérifie le certificat de
  Cloudflare, plus celui de l'edge.

### Retour arrière

L'ordre inverse : l'origine accepte de nouveau tout le monde avant que le moindre visiteur y
revienne en direct.

1. `LODB_CLOUDFLARE=proxy`, déploiement : plus d'authentification du client.
2. Nuage gris sur les quatre hôtes ; attendre deux fois le TTL, puis plus de `cf-ray`.
3. `LODB_CLOUDFLARE=off`, déploiement ; `infra-vps` : retirer `trusted_proxies`,
   `client_ip_headers` et le `header_up` d'Umami.
4. Politique de confidentialité et de cookies remises à jour, entrée de changelog.

## 5. Ce qu'on ne fait pas

| Action | Raison |
|---|---|
| CrowdSec, `caddy-ratelimit` | décision du 2026-10-10 : une image d'edge reconstruite pour tous les projets de l'hôte et un agent à alimenter, pour une limite par adresse qu'un crawl réparti ne déclenche pas |
| Cloudflare en permanence, « par précaution » | décision du 2026-10-10 : seulement sur le déclencheur (b). Héberger le DNS en gris n'est pas un proxy |
| Anubis sur tout le site, ou sur `/api`, `/v1`, `/webhooks` | les apps, l'API publique et Stripe ne passent pas de défi ; le dernier patch est servi par le cache |
| Limite par adresse sur une action authentifiée | ADR 0009 (NAT) ; une limite par compte reste permise |
| `noindex` des anciens patchs pour écarter les robots | politique SEO du 2026-07-18 : ils restent canoniques et indexables |
| Purger le cache de nginx pendant un siège | c'est lui qui sert gratuitement les humains |
| Nuage orange avant la chaîne d'adresse réelle | toutes les bornes par adresse verraient Cloudflare (§ 4, « Activation, dans l'ordre ») |
| Certificat global de Cloudflare pour les tirages authentifiés | commun à tous ses clients : il prouve « Cloudflare », pas « notre zone » |
| Proxy des autres projets de l'hôte, ou du staging | hors périmètre : seuls les hôtes de prod de LoDb |
| Fonctions de Cloudflare qui réécrivent le HTML | la CSP du SSR hache le HTML rendu : un script ajouté ou réécrit est bloqué |

## 6. Historique d'activation

| Mode | Activé | Désactivé | Signaux, effets |
|---|---|---|---|
| | | | |
