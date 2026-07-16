# 📋 Prompt de correction — à copier-coller

Prompt destiné à une session Claude Code ouverte à la racine du repo.
Il s'appuie sur [`docs/PERFORMANCE-AUDIT.md`](./PERFORMANCE-AUDIT.md).

> 💡 **Conseil** : ne lance pas les 3 lots d'un coup. Fais le **Lot 1**, vérifie, commit, puis enchaîne.
> Le Lot 1 seul devrait déjà régler les deux symptômes visibles.

---

## 🔥 Lot 1 — Les 4 correctifs qui règlent les symptômes

```
Lis d'abord docs/PERFORMANCE-AUDIT.md : c'est un audit de ce repo, les constats
sont référencés fichier:ligne. Applique les correctifs A2, B1, A3 et B2.
Ne fais QUE ces quatre-là, ne refactore rien d'autre au passage.

────────────────────────────────────────────────────────────
1. A2 — Le manifeste perd des entrées sous concurrence (CRITIQUE)

Dans app/src/Service/API/AbstractManager.php, ingestMissing() (L.179-202) lit le
manifeste, le modifie en mémoire, puis réécrit le fichier ENTIER via saveManifest().
Deux process concurrents (le loader SSE et le flush de kernel.terminate, notamment)
s'écrasent mutuellement : last-write-wins. Le cache ne converge donc jamais et les
images sont re-téléchargées indéfiniment.

Corrige en relisant le manifeste depuis MinIO JUSTE AVANT l'écriture et en
fusionnant, plutôt qu'en écrasant :
  - dans saveManifest(), relis l'état frais du storage (en contournant le memo
    $manifestCache ET le pool ddragon.cache — les deux serviraient une copie périmée),
    puis écris $frais + $ajouts.
  - attention : loadManifest() memoïse dans $this->manifestCache (L.308) — ne
    réutilise pas ce chemin pour la relecture.

Cela réduit la fenêtre sans la fermer complètement (read-modify-write reste non
atomique sur S3). Si tu juges que ça ne suffit pas, propose-moi l'alternative
"un fichier par entrée" (manifest/{version}/{type}/{name}.json), qui supprime le
problème par construction — mais NE la mets pas en œuvre sans me demander.

Ajoute un test qui échoue avec le code actuel et passe après : deux ingestions
séquentielles partant du même état initial de manifeste, où la seconde ne doit
pas faire disparaître les entrées de la première.

────────────────────────────────────────────────────────────
2. B1 — Le loader ne montre rien pendant tout le téléchargement (CRITIQUE)

Dans AbstractManager::ingestMissing(), $this->goFetcher->fetchMany() (L.184) est
bloquant et récupère TOUT le lot avant que la boucle L.185-195 n'appelle $onStored.
Résultat : zéro événement SSE pendant toute la phase réseau, la barre du loader
reste figée à 0 %, et aucun nom de ressource ne s'affiche.

Corrige en traitant par petits lots : découpe $missing en chunks de 12, et pour
chaque chunk fais fetchMany(chunk) puis store + $onStored. Les événements partent
ainsi tout au long du warm.

Garde le comportement identique pour l'appelant : même valeur de retour, même
signature publique. Les appels sans $onStored (rendu de page, warmup CLI) ne
doivent pas changer de sémantique.

────────────────────────────────────────────────────────────
3. A3 — Un HeadObject inutile par image

app/src/Service/Storage/BlobStore.php:36 fait fileExists($key) avant write().
La clé EST le SHA-256 du contenu, donc le PUT est idempotent : ce Head ne sert à
rien. Supprime-le et écris directement. Garde le fileExists() de ensureWebp()
(L.53) : lui évite un transcodage GD coûteux, il est justifié.

────────────────────────────────────────────────────────────
4. B2 — Le watchdog abandonne avant la fin

assets/vue/components/ResourceLoader.vue:58 → WATCHDOG = 15000, timeout absolu.
Une page froide dépasse largement 15 s, donc le loader abandonne et navigue vers
une page pas encore chaude.

Remplace le timeout absolu par un watchdog d'INACTIVITÉ : réarme-le à chaque
événement `start` / `phase` / `item` reçu. Un warm long mais qui progresse ne doit
plus être interrompu ; un stream réellement mort doit toujours l'être.
Mets à jour assets/vue/components/ResourceLoader.spec.ts en conséquence.

────────────────────────────────────────────────────────────
Contraintes :
  - Respecte le style du repo : PHP strict_types, final, docblocks explicatifs
    (le "pourquoi", pas le "quoi"), commentaires en anglais côté code.
  - Fais tourner les tests : cd app && vendor/bin/phpunit && npm test
  - Explique-moi chaque diff avant de commit.
```

---

## 🐳 Lot 2 — Le dev sous Docker/Windows

```
Lis docs/PERFORMANCE-AUDIT.md section A1. Le bind-mount ./app:/var/www/html de
compose.override.yaml expose 9 049 fichiers vendor + 3 363 fichiers var/ à travers
la frontière Windows→Linux. C'est le suspect n°1 de la lenteur en dev.

AVANT de modifier quoi que ce soit, aide-moi à mesurer pour confirmer :
lance la stack sans l'override (docker compose -f compose.yaml up -d --build,
donc en prod, sans bind-mount) et compare le temps de réponse d'une même page
avec la stack dev. Donne-moi les deux chiffres.

Si l'écart confirme le diagnostic, propose-moi les options avec leurs
compromis explicites (je veux choisir, ne tranche pas seul) :
  a) volumes nommés pour vendor/ et var/ — quel impact sur le workflow
     composer install / cache:clear depuis l'hôte ?
  b) déplacer le repo dans le FS WSL2 — qu'est-ce que ça change à mes outils
     Windows (IDE, git) ?
  c) ddragon.cache en cache.adapter.apcu au lieu de filesystem — quelles
     conséquences si on scale php à plusieurs conteneurs ?

Mets à jour docs/docker.md avec l'option retenue.
```

---

## 🔍 Lot 3 — Vérifications et dette

```
Lis docs/PERFORMANCE-AUDIT.md sections A5, A6, A7, B3, B4. Traite-les dans cet
ordre, en me faisant valider entre chaque :

1. A7 — MESURE D'ABORD, ne code rien. go-workers utilise http.DefaultTransport
   (MaxIdleConnsPerHost=2) avec MaxConcurrency=16. MAIS DefaultTransport a
   ForceAttemptHTTP2=true : si DDragon sert du h2, le problème n'existe pas.
   Vérifie avec GODEBUG=http2debug=1 sur le conteneur go-fetcher et dis-moi ce
   que tu observes. Ne touche au Transport QUE si HTTP/1.1 est confirmé.

2. A6 — Le docblock de app/src/Service/Client/PageContextResolver.php:18 dit
   "safe to HTTP-cache (see the cache layer)". Cette couche n'existe pas, et la
   session (LocaleSubscriber lit la session à kernel.request) forcerait de toute
   façon Cache-Control: private. Corrige le commentaire pour qu'il dise la vérité.
   Dis-moi ensuite ce que coûterait une vraie couche de cache HTTP (porter la
   locale dans l'URL + s-maxage + ETag) — sans l'implémenter.

3. B3 — Durcis le stream SSE côté nginx plutôt que de dépendre du header
   applicatif X-Accel-Buffering. Dans docker/nginx/default.conf, ajoute
   fastcgi_read_timeout et fastcgi_buffering off / gzip off pour la route loader.
   Attention : /api/loader/prepare passe par location / → try_files → /index.php,
   et location ~ ^/index\.php est marqué `internal`. Trouve le montage correct.

4. B4 — PageContextResolver::loaderSteps($path) prétend lire "purely from the
   request query" mais dépend du RequestStack ambiant tout en recevant $path en
   argument. Passe page et perPage en arguments explicites depuis LoaderController.

5. A5 — L'ingestion différée (FlushDeferredImagesListener, kernel.terminate)
   occupe un worker FPM plusieurs secondes après la réponse ; pm.max_children=20.
   symfony/messenger est déjà installé mais inutilisé. Propose-moi un plan de
   migration vers un message asynchrone — plan uniquement, ne code rien.
```
