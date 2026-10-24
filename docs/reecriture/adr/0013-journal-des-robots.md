# ADR 0013 — Journal des robots

- **Statut** : acceptée — 2026-10-24
- **Révise** : [ADR 0009](0009-identite-authentification-sessions.md) (une troisième exception à
  « jamais `AllowAnonymous` »), [ADR 0004](0004-stockage-etat-et-scaling.md) (tables
  `robot_agent`, `robot_hit`, `robot_daily`)
- **Détail** : [`analytics.md`](../../architecture/analytics.md) § « Journal des robots »

## Contexte

La mesure maison ne compte que les pages de la dernière version : le miroir de nginx ignore
toute version épinglée (un point dans le chemin), `robots.txt`, les sitemaps et les fichiers,
et le statut qu'elle garde est celui que le catalogue prédit, pas celui que nginx a rendu
(403, 429, 503 du délestage). Depuis l'arrêt du stockage des user-agents, un robot n'y est plus
qu'un compteur (`botViews`). Or l'essentiel du crawl porte sur les anciens patchs : 97 % des
requêtes du crawler du 2026-10-10, qui passait sous un user-agent de Chrome. L'admin ne pouvait
dire ni qui crawle, ni quoi, ni comment il est servi, ni si un « Googlebot » en est un.

## Décision

- **nginx rapporte, l'API classe.** Le journal d'accès du site formate une ligne vide dont la
  variable `js_set` (`docker/nginx/njs/robot-journal.js`, phase de log : statut et durée
  connus) range la requête dans une zone partagée (`lodb_robot_journal`, 8 Mo, 10 min,
  `evict`). Toutes les 2 s, `js_periodic` poste la zone en NDJSON à
  `POST /internal/robots/journal` ; une réponse autre que 202 garde le lot pour l'intervalle
  suivant. Sont rapportées : chaque requête d'un client dont l'user-agent est celui d'un robot
  (`$lodb_robot_agent`, `nginx.conf`), quel que soit le chemin ; chaque requête de page des
  autres. Jamais la sonde `/healthz`, ni une requête refusée par la garde de l'edge.
- **Identification par le catalogue** (`LoDb.Infrastructure/Robots/Identification`) : une
  table par famille (moteurs, IA, à la demande, aperçus, SEO, surveillance, archives, outils),
  puis un robot inconnu nommé d'après son propre jeton, puis la bibliothèque. Le robot n'est
  jamais stocké : les rapports le relisent du texte de l'user-agent, si bien qu'un ajout au
  catalogue renomme aussi le passé. `RobotAgentMapTests` tient la map de nginx alignée sur lui.
- **Robots non déclarés, par la cadence** : une page lue par un lecteur est rendue par
  l'application après la première ; au-delà de `UndeclaredThreshold` (40) pages demandées à
  nginx en `UndeclaredWindow` (10 min) par un même client (empreinte HMAC de l'adresse et de
  l'user-agent), il est tenu pour un robot pendant `UndeclaredMemory` (24 h), nommé d'après son
  réseau.
- **Vérification par DNS inverse confirmé**, la méthode que publient Google, Bing, Apple,
  Yandex, Baidu, Amazon, Seznam et Petal : l'adresse se renverse sous un domaine documenté et
  ce nom se résout vers elle. Verdicts `verified`, `mismatch` (usurpation probable),
  `unresolved`, `unverifiable` (opérateur sans domaine). Budget par lot
  (`DnsLookupsPerBatch`), cache borné, questions en vol plafonnées : un résolveur muet ne prend
  jamais plus de `DnsParallelism` threads. Le réseau affiché est le domaine enregistrable d'un
  nom confirmé, jamais celui qu'un détenteur d'adresse se donne.
- **Stockage** : `robot_agent` (user-agents distincts), `robot_hit` (une requête, gardée
  `HitRetention` = 30 j par la tâche `robots.retention`), `robot_daily` (comptes par jour et
  dimensions, sans limite), écrits en une transaction par lot.
- **Lecture** : `/api/admin/analytics/robots`, `/robots/{key}`, `/robot-hits`, admin seul,
  `no-store`. Pages `/admin/robots`, `/admin/robots/{key}`, `/admin/robots/journal`.
- **Troisième exception `AllowAnonymous`**, sur le modèle du miroir : nginx n'a pas de
  session, la route est hors de `/api`, la garde réseau (`NetworkGate`) répond 404 à tout appel
  hors du réseau de la pile, `AnonymousEndpointsTests` ferme la liste.

## Données personnelles

L'adresse d'un client ne sert qu'à la recherche DNS et à l'empreinte de cadence, en mémoire :
elle n'est jamais stockée, son nom d'hôte non plus. Une requête de lecteur sous le seuil n'est
pas gardée du tout. Sont gardés l'user-agent d'un robot (`robot_agent`) et le domaine
enregistrable de son réseau.

## Alternatives écartées

| Alternative | Pourquoi non |
|---|---|
| Identifier le robot dans `analytics_event` | Aveugle aux anciens patchs, à `robots.txt`, aux sitemaps et aux statuts rendus ; bâti sur une mesure appelée à partir (ADR 0012) |
| Lire le journal de l'edge dans VictoriaLogs | Couple l'admin à `infra-vps` (réseau `observability`, accès à VL), rien en local ni en E2E |
| `access_log syslog:` vers l'API | nginx résout l'adresse au démarrage : un conteneur d'API recréé ne reçoit plus rien jusqu'au rechargement de nginx |
| L'API tire le journal de nginx | Un port de nginx joignable du réseau `edge`, partagé avec d'autres projets, avec des adresses de lecteurs dedans |
| Vérifier par les plages d'adresses publiées (OpenAI, DuckDuckGo…) | Autant de nouveaux hôtes sortants (ADR 0002) à tenir ; ces robots restent `unverifiable` |

## Conséquences

- **+** Les robots de toutes les pages, leurs statuts réels (le délestage vu par Googlebot), la
  latence, les robots qui se cachent, les faux Googlebot.
- **+** Le catalogue évolue sans migration et réécrit l'historique.
- **−** Une requête de lecteur coûte une entrée de zone partagée et une ligne d'un POST groupé.
- **−** Les requêtes d'un robot non déclaré avant le seuil ne sont pas gardées : ses comptes
  partent de la requête qui l'a trahi.
- **−** Des questions DNS sortent de l'API vers le résolveur de Docker (pas un hôte HTTP : hors
  de la table de l'ADR 0002).
