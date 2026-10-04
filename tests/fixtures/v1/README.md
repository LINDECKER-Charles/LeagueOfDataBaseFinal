# Références de contrat `/v1`

Comportement observable de go-api, capturé par `tools/v1-capture/` (chantier L6.1).
Les tests de contrat du module `/v1` (L6.3) rejouent ces scénarios sur le même jeu de
données et comparent leurs réponses normalisées à celles-ci. Scénarios, particularités et
écarts volontaires : [`contrat-v1.md`](../../../docs/reecriture/rapports/contrat-v1.md).

## Contenu

| Chemin | Rôle |
|---|---|
| `seed/schema.sql` | instantané du schéma de l'ancienne stack (`pg_dump --schema-only`, base de dev migrée) |
| `seed/dataset.sql` | jeu de données, à charger dans une base vide de ce schéma |
| `seed/keys.json` | clés brutes de test, désignées dans les scénarios par `{{key:<alias>}}` |
| `seed/storage/` | datasets Data Dragon réduits (`data/<version>/en_US/*.json`) |
| `seed/daily.json` | agrégats quotidiens, datés par décalage depuis le jour de la capture (UTC) |
| `scenarios/*.json` | groupes de scénarios, en entrée de la capture |
| `references/*.json` | réponses enregistrées, une par étape, dans l'ordre des scénarios |

## Étapes

Chaque groupe part d'un état neuf : données rechargées, processus de l'API redémarré
(cache de clés, seaux de rate limit et cache des tendances vides), volume de stockage
régénéré. Une étape est soit une requête, soit une action :

- `request` : `method`, `path` et `headers`, envoyés tels quels (valeurs non retouchées) ;
  `repeat: n` la déroule en `id#1` à `id#n` ;
- `sleep` (`ms`) : laisse passer le temps (vidage du métrage en 1 s, TTL du cache de clés
  de 60 s) ;
- `sql` : modifie la base pendant que l'API tourne (révocation, création, recharge) ;
- `stopDatabase` : arrête Postgres ;
- `hideStorage` : fait disparaître le répertoire de stockage sous l'API.

## Forme d'une réponse enregistrée

```json
{ "status": 429,
  "headers": { "content-type": "application/json; charset=utf-8", "x-ratelimit-reset": "<unix-time>" },
  "body": { "error": { "code": "rate_limited", "message": "…" } } }
```

- `headers` ne garde, en minuscules, que `content-type`, `x-content-type-options`, `allow`,
  `location`, les trois `access-control-allow-*` et les trois `x-ratelimit-*`. Un en-tête
  absent est une donnée du contrat (pas de `X-RateLimit-*` sur un 401, par exemple).
- `x-ratelimit-reset` devient `<unix-time>` s'il est un horodatage Unix entier compris
  entre le début de la requête et une minute après sa fin, sinon `<invalid-unix-time>`.
- `body` : corps JSON décodé (l'ordre des clés ne compte pas) ; `text` : autre corps,
  à l'octet près ; ni l'un ni l'autre : corps vide (204, `HEAD`).
- Les dates des corps viennent du jeu de données : elles sont fixes, donc comparées
  telles quelles. `Date` et `Content-Length` ne sont pas enregistrés.
