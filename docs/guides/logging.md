# 🪵 Journalisation applicative — quoi écrire, et sous quelle forme

Ce guide fixe **ce qu'on écrit** dans les logs applicatifs. Il complète
[`observabilite.md`](observabilite.md), qui décrit le transport : côté code on ne
choisit ni le transport ni la rétention, seulement le contenu.

---

## Les trois contraintes de la chaîne

1. **Vector ne lit que `stdout` / `stderr`.** Un log écrit dans un fichier n'est jamais
   collecté.
2. **Vector ne parse aucun JSON applicatif.** Il devine `level` par regex sur le texte
   brut.
3. **`stack`, `service`, `container`, `stream` sont les champs de flux**, l'axe
   d'indexation. Pour un log applicatif, Vector ne produit rien d'autre : `channel`,
   `level_name` et tout le contexte n'existent que comme texte dans `_msg`.

Conséquence directe : **la clé d'événement est le seul point d'agrégation fiable.**

---

## La clé d'événement

Un message de log est une **clé pointée `domaine.sujet.résultat`**, jamais une phrase.

```
catalog.page.unavailable
catalog.fetch_batch.unresolved
ingest.deferred_task.failed
mail.send.failed
stripe.webhook.signature_invalid
```

Dans VictoriaLogs la clé est une sous-chaîne exacte de `_msg` : elle se compte et se
groupe.

```logsql
_time:24h stack:lodb-prod service:php "catalog.page.unavailable" | count()
```

Une phrase (« Erreur lors de la récupération des versions Riot ») n'est ni stable ni
groupable, et elle change au premier reformulage. Les trois segments se lisent de
gauche à droite comme un filtre de plus en plus fin : `"catalog."` donne tout le
domaine, `"catalog.fetch_batch."` tout le sujet.

**Deux exceptions assumées.** Le miroir du journal d'audit émet `audit.<AuditAction>`
(ex. `audit.user.login_failed`) : `AuditAction` est un contrat on-disk fermé, on ne le
reformule pas pour faire joli dans les logs. Et les clés déjà en place sur le chemin
argent sont préfixées `stripe.` (`stripe.api.*`, `stripe.checkout.*`) alors que leur
canal s'appelle `billing` : on **ne les renomme pas** — le préfixe désigne le
prestataire, ce qui reste juste, et un renommage casserait les recherches existantes.
Toute nouvelle clé de ce domaine suit `stripe.`.

---

## Le contexte

Tout ce qui varie va dans le **contexte PSR-3**, jamais dans le message.

```php
// NON — la clé n'existe plus, chaque ligne est unique
$this->logger->warning("Version {$version} indisponible");

// OUI
$this->logger->warning('catalog.page.unavailable', [
    'version' => $version,
    'lang' => $lang,
    'exception' => $e,
]);
```

Pas d'interpolation `{placeholder}` non plus. monolog-bundle active
`PsrLogMessageProcessor` **par défaut** sur tout handler non imbriqué : un `{version}`
laissé dans un message y serait réécrit *en silence*, et la clé d'agrégation
disparaîtrait sans que personne le voie. Les quatre handlers de production déclarent
donc `process_psr_3_messages: false` — la violation devient un littéral visible. Si un
handler venait à être ajouté sans cette clé, l'interdiction ci-dessus redeviendrait la
seule protection.

Le modèle déjà en place dans le dépôt est `App\Service\PublicApi\ApiEntitlementApplier` :
huit appels, tous en clé pointée, à contexte scalaire ou vide, avec un docblock qui
porte la règle PII (« Logs carry internal ids only — never customer identity »).

### L'exception passe sous la clé `exception`, en objet

`['exception' => $e]` — l'objet `Throwable`, pas `$e->getMessage()`. Ce que
`JsonFormatter` en écrit :

```json
"exception": {
  "class": "RuntimeException",
  "message": "go-fetcher: upstream status 503 for …",
  "code": 0,
  "file": "/var/www/html/src/Service/Tools/GoFetcherClient.php:200",
  "previous": { "class": "…", "message": "…", "code": 0, "file": "…" }
}
```

- `trace` est **retiré** (`includeStacktraces` est faux sur le service
  `monolog.formatter.json`). On ne l'active pas — voir la troncature ci-dessous.
- La chaîne `previous` est suivie récursivement.
- `getMessage()` seul perdrait la classe, le `fichier:ligne` et la cause, c'est-à-dire
  tout ce qui permet de dédupliquer deux occurrences.

### Le plafond FPM : 8192 octets par ligne

L'image php-fpm de base impose `log_limit = 8192` — le lot 1a le relève à 65536, mais
la contrainte de conception reste. `Monolog\LogRecord::toArray()` sérialise dans cet
ordre :

```
message, context, level, level_name, channel, datetime, extra
```

Le contexte est **en deuxième position**. Une ligne trop longue ne perd donc pas son
contexte : elle perd `level`, `level_name`, `channel` et `datetime`, et le JSON reste
ouvert. **Un contexte volumineux détruit la ligne entière, pas seulement sa fin.**

D'où :

- pas de payload dans le contexte (corps de réponse, dump de dataset, tableau
  d'entrées) — un **compte** et un **identifiant**, jamais les données ;
- pas de `include_stacktraces: true` sur un handler stderr ;
- ce qui est volumineux par nature reste dans le NDJSON sous `var/state/`.

---

## Les niveaux

| Niveau | Quand | Ancrage dans le dépôt |
|---|---|---|
| `critical` | L'argent, la sécurité ou l'intégrité légale est en jeu et rien ne se répare seul. | `STRIPE_WEBHOOK_SECRET` vide en prod : le webhook répond 503 à **tous** les appels, Stripe finit par désactiver l'endpoint, les crédits achetés ne sont jamais appliqués. |
| `error` | Une requête n'a pas pu être servie comme prévu, et la cause est chez nous ou inconnue. | `searchResponse()` qui répond 503 — ce endpoint n'est appelé que par notre propre front, avec une sélection déjà validée. Idem un lot go-fetcher où **aucune** URL n'a été résolue. |
| `warning` | Dégradé, mais l'application s'est comportée correctement : le visiteur garde quelque chose d'utilisable, ou la cause est une panne amont transitoire. C'est le **volume** qui fait signal. | `redirectToHomeWithError()`, `HomeController::preview()` (une section vide sur quatre), `DeferredImageIngestor::flush()` (la prochaine visite réessaie). |
| `info` | Un événement métier **qui change un état** a réussi et doit être retrouvable après coup. | `stripe.api.credits_added`, `stripe.api.plan_activated`, miroir d'audit d'un `AuditOutcome::Success`. Un `Failure` ou un `Denied` sort en `warning`. |
| `debug` | Interdit en prod applicatif. | — |

`alert` et `emergency` ne sont pas utilisés : cinq niveaux suffisent, et une échelle
qu'on n'arbitre pas devient du bruit. `notice` n'est pas employé **par le code
applicatif** non plus, mais il n'est pas libre pour autant : c'est le seuil du handler
always-on, et le mapping `framework.exceptions` du lot 1a y range les 429 des
rate-limiters. Ne pas s'en servir comme d'un « info un peu important ».

---

## ⛔ Interdictions

### 1. Aucune clé de contexte nommée `error`

C'est le piège le plus concret, et il existe **aujourd'hui** dans le dépôt :

```php
// app/src/Controller/Billing/ApiBillingController.php:108
$this->logger->warning('stripe.api.session_failed', ['error' => $e->getMessage()]);

// app/src/Controller/Billing/DonationController.php:85
$this->logger->warning('stripe.checkout.session_failed', ['error' => $e->getMessage()]);

// app/src/Controller/Billing/StripeWebhookController.php:87 — déjà en `error`,
// mais la clé de contexte reste à renommer
$this->logger->error('stripe.webhook.handler_failed', [
    'event' => $event->id, 'type' => $event->type, 'error' => $e->getMessage(),
]);
```

Les deux premières sont émises en **warning**. Le JSON produit contient
`{"context":{"error":"…"}}`, donc le texte brut contient le mot `error`, donc la regex
de Vector les classe en **`level=error`**. Un warning devient une erreur, et le tableau
de bord ment. Même piège avec `critical`, `fatal`, `panic` — comme clé **ou** comme
valeur de chaîne libre.

La clé `exception` est l'exception assumée : elle est indispensable, et on sait qu'elle
tire la ligne vers `level=error`. On l'accepte, **et on ne construit aucune alerte sur
`level`**.

### 2. Aucune donnée personnelle

Pas d'email, pas d'IP, pas de token, pas de mot de passe, pas de texte saisi par un
visiteur. Un log applicatif part dans un index cherchable 90 jours, hors du périmètre
de rétention déclaré. On écrit à la place un **id interne** (`user_id`, `api_key_id`,
`build_id`) qui permet de rejoindre la donnée réelle là où elle est légalement
conservée.

### 3. Aucun message multi-lignes

Vector fabrique un événement par ligne. Un message contenant `\n` devient N événements,
dont N-1 sans clé et sans niveau. C'est aussi pour cela que la stack trace ne va pas
dans le message.

---

## Les canaux

Le canal ne sert pas à chercher (Vector ne le voit pas comme un champ) : il sert à
**router**. En prod, ce qui n'est pas sur un canal métier n'atteint `stderr` qu'à
partir de `notice` ; les canaux métier, eux, sont émis dès `info` par le handler
`business`.

| Canal | Périmètre |
|---|---|
| `audit` | miroir du journal légal (`AuditLogger`), échec d'écriture de `AuditLogStore` |
| `billing` | tout le chemin argent : checkout, webhook, entitlements, dons |
| `catalog` | lecture Data Dragon : pages ressources, recherche, `GoFetcherClient`, `VersionManager` |
| `ingest` | écritures vers MinIO : warm du loader, images différées |
| `mail` | remise sortante |
| `app` (défaut) | le reste — n'apparaît en prod qu'à partir de `notice` |

### Injection

Sur une classe concrète, l'attribut suffit :

```php
#[WithMonologChannel('billing')]
final class StripeWebhookController extends AbstractController
```

> ⚠️ **Un attribut de classe n'est pas hérité.** `AttributeAutoconfigurationPass` lit
> `ReflectionClass::getAttributes()`, qui ne remonte pas la hiérarchie : le poser sur
> une base abstraite n'a **aucun effet** sur ses sous-classes. Pour
> `AbstractResourceController` et consorts, passer par le **nom de l'argument**, alias
> enregistré par `LoggerChannelPass` :
>
> ```php
> public function __construct(
>     private readonly LoggerInterface $catalogLogger,
> ) {}
> ```

---

## Tests

Le CLAUDE.md impose que toute feature s'accompagne de tests. Pour la journalisation, on
teste le **comportement**, jamais le texte exact du message :

- qu'un chemin d'échec émet bien un enregistrement (et à quel niveau) ;
- qu'un log n'altère pas le flux de contrôle — une page en panne redirige toujours,
  une ingestion différée avale toujours son exception ;
- que le miroir d'audit **n'émet jamais** `ip` ni `meta.identifier`.

Ce dernier est le test le plus important du lot : il garde une garantie RGPD qu'une
relecture de code ne rattraperait pas.
