# League Of Data Base — README (provisoire) ✨📘🛠️

Un mini-projet **Symfony** pour explorer les données **Data Dragon (LoL)** : sélection de **version/langue**, cache local des **Summoner Spells**, affichage stylé (Twig/Tailwind-like), et petite CI GitHub Actions. ⚙️🧭✨

---

## Aperçu 🔍📚✨

* **Setup** des préférences (version/langue) : `/setup`
* **Liste des Summoner Spells** (avec images locales si dispo) : `/summoners`
* **Sandbox** : `/test`
* **Cache disque** des JSON + images pour éviter de retaper l’API
* **DTO `ClientData`** injecté dans les vues : versions, langues, labels, locale courante, session

---

## Prérequis 🧰🔧📦

* PHP 8.3+
* Composer
* Node.js + Yarn/NPM (si tu utilises Webpack Encore)
* Extensions Symfony standard (HTTP Client, Cache, etc.)

---

## Installation rapide 🚀🧩✨

```bash
# Dépendances PHP
composer install

# (optionnel) Front si tu utilises Encore
yarn install && yarn dev   # ou: npm install && npm run dev
```

Fichiers env (minimaux) : 🗂️🔐✨

```bash
cp .env .env.local
# Dans .env.local, fixe au moins:
# APP_ENV=dev
# APP_SECRET=une_valeur_random
```

---

## Lancer en local 🖥️🏁⚡

```bash
symfony server:start -d   # ou: php -S 127.0.0.1:8000 -t public
# puis http://127.0.0.1:8000/setup
```

---

## Architecture (services & DTO) 🧱📦🔗

* `App@Service\ClientManager`

  * Détecte la **langue navigateur**, lit/écrit **session**, **cookies signés** (remember), hydratation depuis le cookie.
  * Méthode utilitaire proposée : `resolveLocaleAndVersion(...)`.

* `App@Service\VersionManager`

  * Récupère **versions** et **langues** depuis DDragon (mise en cache), expose `validateSelection()`.

* `App@Service\SummonerManager`

  * `getSummoners(version, lang)` : récup JSON (cache disque → sinon fetch DDragon)
  * `parseSummoners(json)` : parse + tri alpha
  * `getSummonersParsed(version, lang)` : tout-en-un
  * `fetchSummonerImages(version, lang, force=false)` : télécharge toutes les images localement

* `App@Service\UploadManager`

  * `saveJson($dir, $filename, $content)`
  * `saveImage($dir, $filename, UploadedFile $file)`

* `App\Service\APICaller`

  * `call($url)` : GET ultra-simple (retourne le corps ou jette une exception)

* `App\Service\Utils`

  * `fileIsExisting($absPath): ?string`
  * `decodeJson($json)` / `encodeJson($data)`

* `App\Dto\ClientData`

  * `versions: string[]`
  * `languages: string[]`
  * `languageLabels: array<string,string>`
  * `currentLocale: string`
  * `session: array{locale:?string,version:?string}`
  * `fromServices(VersionManager, ClientManager): self`

---

## Vues & routes 🧭🧩🗺️

* `GET /setup` — page de sélection version/langue

  * **Variables Twig** via `client: ClientData`
  * Form POST → `POST /setup-submit` (CSRF, validation, cookie “remember” optionnel)

* `GET /summoners` — liste des sorts d’invocateur

  * Utilise `SummonerManager::getSummonersParsed()`
  * Images locales si téléchargées via `fetchSummonerImages()`

* `GET /test` — sandbox

Template d’en-tête : **header avec sélecteurs** version/langue (+ bouton Valider) utilisable sur n’importe quelle page. 🧱🧷✨

---

## Stockage local (cache) 💾📂🗃️

* JSON : `public/upload/{version}/{lang}/summoner/summoners.json`
* Images : `public/upload/{version}/{lang}/summoner/img/{image.full}`

> Les URL DDragon typiques : 🔗🧭📎
>
> * JSON Summoners : `https://ddragon.leagueoflegends.com/cdn/{version}/data/{lang}/summoner.json`
> * Image d’un spell : `https://ddragon.leagueoflegends.com/cdn/{version}/img/spell/{image.full}`

---

## Tests ✅🧪🧰

Installer PHPUnit : 📦⚙️✨

```bash
composer require --dev symfony/phpunit-bridge
./bin/phpunit
```

---

## CI (GitHub Actions) 🔄🧰🚦

Workflow (branche `dev`) : 🧵🔧✅

* Installe PHP 8.3, dépendances Composer
* Lint YAML/Twig/Container
* Lance les tests
* **(Option)** merge/push vers `main` si tests OK (selon ta règle/PR)

> Si `main` est protégée, pense à autoriser le token à bypass les protections ou à créer une **PR auto**. 🛡️⚠️🤖

---

## Conseils d’usage 💡🧭🪄

* Toujours passer par `ClientData` dans les vues needing versions/langues.
* Pour l’affichage des tooltips : `|raw` (les placeholders restent, mais c’est OK pour un rendu informatif).
* Évite l’over-engineering sur les chemins : on **écrit là où on dit** (UploadManager minimal).

---

## Roadmap (TODO) 📝🧱🚀

* [ ] Pages Champions / Objets / Runes
* [ ] Pré-chargement des images au build
* [ ] Filtrage par mode (ARAM/CLASSIC/URF…) côté UI
* [ ] Tests d’intégration SummonerManager (mock HTTP + FS)


---

## Licence 📄⚖️🧷

À définir (MIT ?). ✍️🧩🤝
