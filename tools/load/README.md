# Banc de charge k6

Mesure ce que coûtent à l'hôte les charges que les bornes du coût doivent contenir (admission
des rendus du SSR, porte froide des catalogues, budget d'ingestion, débits de nginx), et ce
qu'un visiteur humain obtient pendant ce temps. Chaque scénario tourne sur un emplacement local
dont les bornes et les quotas sont ceux d'un hôte servi ; le résumé rend un verdict par
scénario.

```bash
# Emplacement 1, les cinq scénarios, 3 min chacun après 30 s de chauffe (~35 min avec la
# construction et l'ingestion du dernier patch) ; supprime l'emplacement (down -v) à la fin
bash tools/load/run.sh
# Un sous-ensemble, plus court
LODB_BENCH_SCENARIOS="S1 S3" LODB_BENCH_DURATION=1m bash tools/load/run.sh
# Tests de l'outil (compose-bench demande la CLI Docker seulement)
node --test 'tools/load/test/*.test.mjs'
```

Rien à installer : k6 tourne dans son image officielle, épinglée par version et empreinte
(`K6_IMAGE` de `run.sh`), sur le réseau de l'emplacement. Il faut Docker, `curl` et Node (le
résumé). Pour changer de version, relever l'empreinte de l'index multi-plateforme :
`docker buildx imagetools inspect grafana/k6:<version>` (ligne `Digest`).

## Jamais sur le staging

Le staging partage les 4 vCPU de la prod : un banc qui le sature ralentit la prod. Le banc ne
vise qu'un emplacement local (`run.sh` refuse tout projet autre que `lodb-dev-e<n>`, car il le
supprime à la fin), et toujours avec son calque.

## Le calque `compose.bench.yaml`

Appliqué **après** `compose.override.yaml`, jamais sans lui :

```bash
LODB_HTTP_PORT=18180 LODB_API_PORT=18181 LODB_SSR_PORT=18182 LODB_PG_PORT=15532 \
LODB_MAIL_PORT=18125 docker compose -p lodb-dev-e1 -f compose.yaml \
  -f compose.override.yaml -f tools/load/compose.bench.yaml up -d --build --wait
```

- Il rend aux bornes que `compose.override.yaml` relève pour l'E2E leur valeur d'hôte servi :
  l'entrée de `compose.yaml` pour le SSR et nginx (le défaut du code, celui de l'image pour
  nginx), aucune valeur pour l'API (le défaut du code, comme sur un hôte). Exportée dans le
  shell qui lance le banc, une variable atteint quand même la stack : c'est ainsi qu'on mesure
  une borne proposée (`LODB_CURRENT_RENDERS_PER_MINUTE=120 bash tools/load/run.sh`).
- **Tout nouveau relèvement de `compose.override.yaml` doit être annulé ici** :
  `test/compose-bench.test.mjs` échoue tant qu'une clé relevée garde sa valeur relevée.
  Seuls `ASPNETCORE_ENVIRONMENT` (documents OpenAPI) et le puits de courriels (`LoDb__Mail__*`)
  restent, ce ne sont pas des bornes.
- Il pose les limites mémoire de `compose.deploy.yaml` et les quotas CPU prévus pour l'hôte
  (`cpus` / `cpu_shares` / `pids`) : web-ssr 1,0 / 1024 / 256, api 1,25 / 1024 / 512,
  postgres 0,5 / 1024 / 256, nginx 0,25 / 2048 / 256. Le test vérifie qu'ils suivent ceux de
  `compose.deploy.yaml` dès que celui-ci en pose.
- Il fixe le sous-réseau de l'emplacement (`LODB_BENCH_SUBNET`, `10.213.7.0/24`) et le donne à
  `LODB_EDGE_CIDR` : nginx croit l'`X-Forwarded-For` du générateur comme celui de l'edge sur un
  hôte, et chaque requête porte l'adresse du client qu'elle représente.

## Scénarios

S1 reproduit le crawl du 2026-10-10 (237 adresses, 23 requêtes/s en tout) ; S4 garde ce débit
sur les URL de l'ancien site. Chaque requête d'un scénario est calculée de son rang
(`lib/targets.mjs`) : un passage est reproductible, et aucune URL ne revient avant que toutes
les autres soient passées. Les adresses viennent des plages de documentation (RFC 5737). k6 ne
suit pas les redirections : un scénario mesure la réponse à l'URL qu'il modélise.

| Scénario | Modèle | Débit | Adresses |
|---|---|---:|---:|
| S1 | Pages uniques des patchs archivés, listes et fiches, toutes locales | 23/s | 237 |
| S2 | Recherches `?q=` aléatoires sur les listes du patch courant | 20/s | 1 |
| S3 | `/api/catalog` balayé : patchs archivés × langues de Data Dragon | 10/s | 237 |
| S4 | Ancien site : chemins versionnés, `?version=`, `?lang=`, entrées inconnues | 23/s | 237 |
| S5 | Boucle de `/warm-up` (flux SSE du chargeur de patch) sur les patchs archivés | 2/s | 237 |

**Sonde humaine**, à côté de chaque scénario, une page par seconde depuis sa propre adresse :
en alternance une liste du dernier patch que le cache de nginx garde (HIT attendu) et une
fiche du dernier patch que personne n'a demandée (MISS attendu, chaque passage lit son propre
bloc). La durée est rangée selon `X-Cache-Status` ; une réponse autre que 200 compte comme un
échec de la sonde, pas comme une durée.

## Ce que fait `run.sh`

1. Construit et démarre l'emplacement avec le calque, attend que le dernier patch soit promu
   (`/api/meta`), puis laisse à l'API le temps d'épingler ses catalogues.
2. Pour chaque scénario : une chauffe de même charge (non mesurée), puis le passage mesuré,
   pendant lequel `docker stats` relève web-ssr, api, postgres et nginx toutes les 5 s ; leurs
   compteurs cgroup (`cpu.stat`) sont lus avant et après par un conteneur jetable de l'image
   k6. Puis l'état des conteneurs (`OOMKilled`, redémarrages, processus tués par le noyau ou à
   court de mémoire dans leurs journaux) et ce que le SSR et l'API ont journalisé pendant le
   passage. Un repos sépare deux scénarios.
3. Écrit le résumé et supprime l'emplacement.

Réglages : `LODB_BENCH_PROJECT` (`lodb-dev-e1`), les ports `LODB_*_PORT` de l'emplacement,
`LODB_BENCH_SCENARIOS`, `LODB_BENCH_DURATION` (`3m`), `LODB_BENCH_WARMUP` (`30s`),
`LODB_BENCH_COOLDOWN` (30 s), `LODB_BENCH_READY_WAIT` (1 800 s), `LODB_BENCH_SETTLE` (60 s),
`LODB_BENCH_OUT`, `LODB_BENCH_KEEP_STACK=1` (garde l'emplacement), `LODB_BENCH_WRAP` (préfixe
de la construction et de chaque scénario, par exemple `nice -n 10`).

## Lire le résumé

`tools/load/.runs/<heure UTC>/summary.md` (et `summary.json`) : un tableau de synthèse, puis
chaque scénario en détail : statuts de la charge et de la sonde, rendus admis et refusés par le
SSR (couloir et raison, qui disent quelle borne a délesté ; le SSR ne journalise que les minutes
où il a refusé), avertissements de l'API par événement, cœurs et mémoire de chaque conteneur
(centiles de `docker stats`, moyenne exacte et part des périodes bridées par le quota, lues
dans les compteurs cgroup), critères un par un. Code de sortie : 0 si tous les critères
tiennent, 1 si l'un manque, 2 si le banc n'a pas tourné jusqu'au bout. Les fichiers bruts
restent à côté : `<S>.k6.json` (résumé k6), `<S>.stats.ndjson`, `<S>.cpu.txt`,
`<S>.health.ndjson`, `<S>.admission.ndjson`, `<S>.api-events.txt`.

| Critère | Borne |
|---|---|
| CPU du SSR (web-ssr), 95ᵉ centile des relevés | ≤ 0,6 cœur |
| CPU de l'API, 95ᵉ centile des relevés | ≤ 0,6 cœur |
| Aucun conteneur tué pour sa mémoire ni redémarré | — |
| Sonde : p95 d'un HIT | < 100 ms |
| Sonde : p95 d'un MISS du dernier patch | < 1,5 s |
| Sonde : réponses autres que 200 | < 1 % |

Un critère sans mesure (aucun HIT, aucun MISS) est manqué : le passage ne prouve rien. Un 503
ou un 429 de la charge n'est pas un échec : c'est le délestage attendu, qu'on lit dans les
statuts.

## Limites de la mesure

- Un poste Windows (Docker Desktop, WSL 2) n'a ni les cœurs ni la contention d'un VPS à vCPU
  partagés : les quotas bornent chaque conteneur, pas la vitesse d'un cœur. Un écart se juge
  d'abord à la répartition des statuts et à la sonde, puis aux cœurs.
- `docker stats` mesure chaque relevé sur environ une seconde : le 95ᵉ centile retient les
  pointes de quelques secondes (compilation à chaud, ramasse-miettes) qu'une moyenne lisse.
- L'ingestion à froid télécharge vraiment Data Dragon : S1, S3 et S5 dépendent du réseau.
- Les caches (pages de nginx, catalogues de l'API, versions ingérées) durent d'un scénario à
  l'autre : chaque scénario parcourt ses propres URL, et la sonde ses propres fiches.
- L'API de l'emplacement tourne en `Development` (documents OpenAPI en plus) : sans effet sur
  les chemins mesurés.
