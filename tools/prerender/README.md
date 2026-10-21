# Stockage des pages pré-rendues (Scaleway)

Le catalogue pré-rendu ([ADR 0011](../../docs/reecriture/adr/0011-catalogue-pre-rendu-et-cdn.md))
range chaque page d'ancien patch dans un bucket Scaleway Object Storage, un par environnement :
`lodb-pages-staging` dans le projet `lodb-pages-staging`, `lodb-pages-prod` dans le projet
`lodb-pages-prod`, région `fr-par`. L'API y écrit avec la clé d'une application IAM
d'écriture ; nginx y lit sans clé, depuis l'adresse du VPS seule.

[`scaleway-bucket.sh`](scaleway-bucket.sh) met un bucket dans cet état, ou l'y remet, et le
dit. Rejoué, il ne touche qu'à ce qui s'en écarte. L'AWS CLI tourne dans son image officielle,
épinglée : Docker suffit, bash 3.2 (macOS) aussi. Les clés passent par l'environnement,
jamais en argument, et ne sont jamais affichées.

| Commande | Où | Avec quelle clé | Ce qu'elle fait |
|---|---|---|---|
| `configurer` (par défaut) | le poste de l'opérateur | sa clé temporaire | crée le bucket s'il manque ; refuse un bucket versionné ; pose le cycle de vie ; pose la politique, **en dernier**, la relit, puis vérifie que l'opérateur garde l'accès |
| `verifier` | le VPS | celle de l'application d'écriture | écrit une sonde comme la passe écrira une page, vérifie que la clé ne liste pas le bucket, puis relit la sonde sans clé, en IPv4, comme nginx |
| `supprimer-politique` | le poste de l'opérateur | sa clé temporaire | retire la politique : la sortie d'une politique fautive (§ 6) |

Il ne crée **pas** les applications IAM ni leurs clés : l'API S3 ne le permet pas. Elles se
font dans la console (§ 1).

## 1. Dans la console Scaleway, une fois

Les projets `lodb-pages-prod` et `lodb-pages-staging` existent déjà. Un projet par
environnement : les droits IAM de Scaleway portent sur un projet entier, jamais sur un seul
bucket. Une clé du staging ne peut donc jamais écrire en prod, et aucune de ces clés n'atteint
le bucket des sauvegardes (`sauvegardes-vps`, projet par défaut).

1. **Propriétaire.** *Organisation → Utilisateurs* : votre compte est le propriétaire de
   l'organisation. C'est le chemin de retour si une politique écarte un accès (§ 6). Notez
   votre **identifiant d'utilisateur** (*IAM → Utilisateurs → vous*), un UUID.
2. **Projets.** Dans chacun des deux projets, *Paramètres* : notez son **identifiant**.
3. **Alerte de facturation**, si le compte en propose : 5 € par mois sur l'organisation.
4. **Buckets**, s'ils ne sont pas déjà créés (sinon le script les crée) : *Object Storage →
   Créer un bucket*, en choisissant d'abord le projet, `lodb-pages-staging` dans le projet
   du même nom puis `lodb-pages-prod`, région **Paris (fr-par)**, visibilité **privée**,
   versionnage **désactivé**, verrouillage d'objet **désactivé**. Des tirets seulement : un
   point casse le certificat `*.s3.fr-par.scw.cloud`. Un nom est unique chez Scaleway, et
   supprimé dans la console il reste réservé 24 heures.
5. **Applications d'écriture**, `lodb-pages-writer-staging` puis `lodb-pages-writer-prod` :
   - *IAM → Applications → Créer*, puis une politique d'une seule règle : le projet de
     l'environnement **seul**, l'ensemble de permissions **`ObjectStorageObjectsWrite`**
     seul. L'application n'a ni lecture, ni liste, ni suppression : elle écrit des pages,
     et le VPS les relit sans clé ;
   - une clé API de l'application, projet préféré celui de l'environnement ;
   - notez l'**identifiant de l'application** ; rangez la clé d'accès et la clé secrète dans
     le gestionnaire de mots de passe. Elles n'entrent dans `ENV_STAGING` / `ENV_PROD`
     qu'au § 3.
6. **Clé temporaire de l'opérateur**, au moment de lancer le script : *IAM → Clés API →
   Générer une clé*, pour vous-même, avec une expiration courte. Le projet préféré importe
   peu : le script ajoute `@<PROJECT_ID>` à la clé d'accès (la façon documentée de viser un
   autre projet). Supprimez-la après les passages, comme pour les sauvegardes.
7. **Adresse de sortie du VPS**, sur l'hôte :

   ```bash
   docker run --rm curlimages/curl:8.22.0 -s https://api.ipify.org   # 187.77.173.149
   curl -4 -sI https://s3.fr-par.scw.cloud | head -n 1                # une réponse HTTP
   ```

   Si la première affiche une autre adresse, c'est elle que reçoit `VPS_IPV4`.

Ne collez jamais une clé secrète dans un ticket, un chat ou un commit ; une clé exposée se
révoque et se remplace.

## 2. Lancer le script : le staging, puis la prod

Depuis la racine du dépôt, sur le poste. La clé secrète se saisit sans écho, pour rester hors
de l'historique du shell :

```bash
export AWS_ACCESS_KEY_ID=SCWXXXXXXXXXXXXXXXXX       # clé d'accès temporaire de l'opérateur
read -rs AWS_SECRET_ACCESS_KEY && export AWS_SECRET_ACCESS_KEY

ENVIRONMENT=staging BUCKET=lodb-pages-staging \
  PROJECT_ID=<projet lodb-pages-staging> OPERATOR_USER_ID=<votre utilisateur> \
  WRITER_APPLICATION_ID=<application lodb-pages-writer-staging> VPS_IPV4=187.77.173.149 \
  bash tools/prerender/scaleway-bucket.sh
```

`REGION` vaut `fr-par` par défaut. Chaque entrée est contrôlée avant tout appel (format du
nom, UUID, adresse IPv4) ; une erreur s'affiche `Erreur : <entrée> : <ce qu'elle attend>`.
Le nom du bucket finit par celui de l'environnement (`lodb-pages-staging`,
`lodb-pages-prod`) : les règles du staging, posées sur le bucket de la prod, effaceraient
toutes ses pages après 14 jours, l'epoch servie comprise. Pour la même raison, le script
refuse sur le staging un bucket dont le cycle de vie porte une règle `retire-pages-<N>`, la
marque d'un bucket de prod, et s'arrête avant d'y rien écrire.

```text
Bucket lodb-pages-staging (staging, https://s3.fr-par.scw.cloud, projet …)
  existe deja
  versionnage : inactif
  cycle de vie : expire-pages abandon-multipart (remplace : aucune regle)
  politique : operateur, ecrivain, lecture anonyme depuis 187.77.173.149 (remplacee : aucune)
  acces de l'operateur sous la politique : verifie
```

Ce qui est posé :

- **cycle de vie** : sur le staging, `expire-pages` efface toute page 14 jours après son
  écriture (le staging reste presque vide) ; en prod, une règle `retire-pages-<N>` par epoch
  retirée (§ 4), dont `retire-pages-0`, l'epoch des sondes. Partout, `abandon-multipart`
  efface après un jour les envois en plusieurs parties abandonnés. Une règle étrangère est
  remplacée, et le script dit laquelle ;
- **politique** (version `2023-04-17`, autorisations seules) : `s3:*` pour vous sur le
  bucket ; `s3:PutObject` pour l'application d'écriture sous `pages/` et `build/` ;
  `s3:GetObject` pour tous sous `pages/` et `build/`, depuis `VPS_IPV4/32` seulement. Un
  principal qu'une politique ne cite pas perd l'accès au bucket : le script refuse d'en poser
  une sans vous, la pose après tout le reste, la relit, puis vérifie votre accès (en
  réessayant 5 minutes, le temps que les droits IAM s'appliquent). La relecture compare la
  version et les autorisations, pas l'`Id`, les `Sid` ni la graphie du principal anonyme
  (`"*"`, `{"SCW":"*"}`, `{"AWS":"*"}`) ; si elle diffère quand même, le script vérifie
  d'abord votre accès, puis s'arrête en affichant la politique posée et la politique relue.

Un bucket **versionné** (ou à verrouillage d'objet, qui l'impose) est refusé tel quel :
chaque réécriture d'une page y garderait une copie facturée, et le versionnage ne se retire
plus. Créez-en un neuf.

**Vérifier depuis le VPS**, avec la clé de l'application d'écriture, dans le dossier du
déploiement (le dépôt à la révision servie) :

```bash
cd "$STAGING_PATH"
export AWS_ACCESS_KEY_ID=SCWXXXXXXXXXXXXXXXXX       # clé d'accès de lodb-pages-writer-staging
read -rs AWS_SECRET_ACCESS_KEY && export AWS_SECRET_ACCESS_KEY
BUCKET=lodb-pages-staging bash tools/prerender/scaleway-bucket.sh verifier
```

La sonde (`pages/0/_sonde`) est une page compressée en gzip, en classe `ONEZONE_IA`, avec les
métadonnées du contrat (`content-security-policy`, `content-security-policy-report-only`,
`reporting-endpoints`, `link`, `lodb-build`, moins de 2 Ko). Avec la même clé, le script
demande ensuite la liste du bucket et exige un refus : seul l'opérateur (`s3:*`) peut lister,
une clé qui liste n'est pas celle de l'application d'écriture (une relecture de la sonde avec
la clé ne prouverait rien depuis le VPS, dont l'adresse ouvre la lecture à tous). Il relit
enfin la sonde sans clé, en IPv4, depuis un conteneur, le chemin que prendront les requêtes de
nginx, et exige :

- un `200` au `GET` ;
- `Content-Type`, `Content-Encoding: gzip`, `Cache-Control` et chaque `x-amz-meta-*` tels
  qu'écrits ;
- le corps : le gzip écrit, octet pour octet.

Le `HEAD` est vérifié de même ; un refus y est seulement signalé, nginx ne demandant que des
`GET` au stockage. Une fois les contrôles passés, le script affiche les en-têtes reçus au `GET`
et au `HEAD` (hors `Date` et identifiants de requête) : les renvoyer avec la sortie, ils disent
ce que nginx doit masquer (`Vary`, `ETag`, `x-amz-*`, casse des métadonnées). Chaque lecture
abandonne après 5 s sans connexion ou 30 s en tout. La sonde s'efface d'elle-même (règle
`retire-pages-0` ou `expire-pages`).
Le script affiche enfin la commande à lancer **depuis le poste**, qui doit répondre `403` :
hors de l'adresse du VPS, personne ne lit le bucket.

Une clé absente répond `404 NoSuchKey` au `GET` depuis le VPS, pas `403` : la politique laisse
passer l'adresse du VPS, et S3 peut donc dire l'absence (sonde non écrite, mauvaise clé).
nginx retombe sur le SSR dans les deux cas.

Un `403` au `GET` depuis le VPS : l'adresse de sortie n'est pas `VPS_IPV4` (§ 1, point 7),
ou la lecture anonyme conditionnée par l'adresse ne marche pas chez Scaleway (rien ne la
documente). Le repli que prévoit l'ADR 0011 (correction C3) : une lecture publique de
`pages/*` et `build/*`, sans condition. Le contenu est public de toute façon ; le prix en est
une sortie abusive possible (0,01 € par Go) et des URL du bucket indexables. Ce choix se
décide d'abord dans l'ADR, puis se porte dans `lib/configure.sh`.

**La prod**, une fois le staging vérifié : la même commande avec `ENVIRONMENT=prod`,
`BUCKET=lodb-pages-prod`, les identifiants de la prod, et les epochs :

```bash
ENVIRONMENT=prod BUCKET=lodb-pages-prod SERVED_EPOCH=1 WRITE_EPOCH=1 \
  PROJECT_ID=<projet lodb-pages-prod> OPERATOR_USER_ID=<votre utilisateur> \
  WRITER_APPLICATION_ID=<application lodb-pages-writer-prod> VPS_IPV4=187.77.173.149 \
  bash tools/prerender/scaleway-bucket.sh
```

`SERVED_EPOCH` est l'epoch que nginx sert (`LODB_ARCHIVE_SERVE_EPOCH`), `WRITE_EPOCH` celle
que la passe en cours écrit (l'epoch servie s'il n'y en a pas) ; `0` est réservée aux sondes.
Puis `verifier` sur le VPS, avec la clé de `lodb-pages-writer-prod`, dans `$PROD_PATH`.

## 3. Brancher la stack

Dans `ENV_STAGING` puis `ENV_PROD` (modèles `.env.staging.example`, `.env.prod.example`,
détail dans [`configuration.md`](../../docs/guides/configuration.md), § 4.8) :

- `LoDb__Prerender__Bucket` et `LODB_PAGES_STORE_BUCKET` : le nom du bucket ;
- `LoDb__Prerender__AccessKey` et `LoDb__Prerender__SecretKey` 🔒 : la clé de
  l'application d'écriture. Le déploiement les range en fichiers secrets, jamais dans le
  `.env` ni dans l'environnement d'un conteneur ;
- `LODB_PAGES_STORE_ENABLED=1` une fois `verifier` passé sur l'hôte et nginx livré avec la
  lecture du stockage (lot 3).

Supprimez ensuite votre clé temporaire.

## 4. Retirer une epoch

Une epoch se retire quand elle n'est plus ni servie ni écrite : une fois l'epoch `N + 1`
complète et servie (`LODB_ARCHIVE_SERVE_EPOCH=N+1`, déployée), relancer la configuration de
la prod :

```bash
ENVIRONMENT=prod BUCKET=lodb-pages-prod SERVED_EPOCH=2 WRITE_EPOCH=2 RETIRED_EPOCHS=1 \
  … bash tools/prerender/scaleway-bucket.sh
```

- `RETIRED_EPOCHS` : une ou plusieurs epochs, séparées par des espaces ou des virgules. Le
  script refuse d'y mettre l'epoch servie, l'epoch écrite ou une epoch plus récente que les
  deux (jamais écrite : sa règle resterait et effacerait la passe qui l'écrira). Il refuse
  aussi un bucket dont une règle `retire-pages-<N>` viserait l'une d'elles (une epoch qu'on
  ressert après l'avoir retirée, ou une faute de frappe : retirer d'abord la règle dans la
  console, *Cycle de vie*).
- Les règles déjà posées restent : un passage qui oublie une epoch n'interrompt pas son
  retrait.
- **Délai** : Scaleway efface après le minuit UTC suivant, plus jusqu'à 24 heures, à
  500 000 objets par jour au plus. Une epoch complète (environ 4,2 millions d'objets) met
  donc **au moins 9 jours** à disparaître, pour environ 1,25 € par mois tant qu'elle reste.
- **Plus vite**, si l'espace presse : supprimer le préfixe, avec votre clé temporaire
  (`ListObjectsV2` et `DeleteObjects` par lots de 1 000, requêtes gratuites ; environ
  4 200 appels pour une epoch) :

  ```bash
  AWS_ACCESS_KEY_ID="$AWS_ACCESS_KEY_ID@<projet lodb-pages-prod>" docker run --rm \
    -e AWS_ACCESS_KEY_ID -e AWS_SECRET_ACCESS_KEY -e AWS_DEFAULT_REGION=fr-par \
    amazon/aws-cli:2.36.50@sha256:c240dd63cdbab17197486d55460675ef2b94c9cc372dedae34dee53ccd30657c \
    s3 rm --recursive --endpoint-url https://s3.fr-par.scw.cloud \
    s3://lodb-pages-prod/pages/1/
  ```

  La barre oblique finale compte : `pages/1` viserait aussi `pages/10/`.
- **Bundles** : `build/` n'a aucune règle de cycle de vie et la clé d'écriture ne supprime
  rien ; nginx y cherche tout bundle que le SSR n'a plus. Un chunk vulnérable (correctif de
  sécurité du front) ou tout `build/` (compromission du serveur SSR) se retire donc avec votre
  clé temporaire, par la même commande (`s3api delete-object --bucket lodb-pages-prod --key
  build/<chunk>.js`, ou `s3 rm --recursive s3://lodb-pages-prod/build/`), **avant** de purger
  le cache des bundles de nginx, qui le reprendrait sinon au stockage. Procédure complète,
  dans l'ordre : [`configuration.md`](../../docs/guides/configuration.md), « Monter l'epoch
  des anciens patchs ».

## 5. Facturation : l'arrondi porte sur le total du projet

La grille de Scaleway facture « 1 Ko pendant 1 seconde comme 1 Go-heure ». Cet arrondi
s'applique au **total** du projet, **pas par objet** : la prod coûte environ 1,25 € par mois
(155 Go en One Zone, 0,000011 € par Go-heure). Par objet, chaque page compterait pour 1 Go :
4,2 millions d'objets × 0,000011 € ≈ **46 € par heure**, ce que la mesure exclut.

La mesure (20 octobre 2026) : 300 objets de 1 Ko en `ONEZONE_IA`, écrits entre 18 h 33 et
18 h 40 UTC dans `lodb-pages-staging`. Environ 4 h plus tard, la première ligne de
*Facturation → Consommation* du projet des pages est « Standard One Zone / ONEZONE - fr-par » :
3 Go-heures, soit 0,01 € (couverts par l'essai gratuit, −0,01 €). Un arrondi par objet aurait
affiché environ 300 Go-heures par heure. La décision est consignée dans l'ADR 0011 ; la passe
complète de la prod n'attend plus de vérification de facturation.

Pour surveiller la prod, la même ligne reste la référence : elle doit suivre le volume écrit
(0,000011 € par Go-heure), pas le nombre de pages.

## 6. Erreur de politique : reprendre la main

Symptômes : le script s'arrête sur « la politique ecarte l'operateur », ou un passage suivant
reçoit `403` dès `head-bucket` (un `OPERATOR_USER_ID` faux, par exemple). Gérer une politique
ne dépend pas d'elle : le propriétaire de l'organisation peut toujours la retirer.

```bash
BUCKET=lodb-pages-prod PROJECT_ID=<projet lodb-pages-prod> \
  bash tools/prerender/scaleway-bucket.sh supprimer-politique
```

ou, dans la console : *Object Storage → le bucket → Politique de bucket → Supprimer*. Le
bucket suit alors les seuls droits IAM de son projet : l'application d'écriture n'y a que
l'écriture, et nginx ne lit plus rien (il rend les pages par le SSR, comme avant). Corrigez
l'entrée fautive, puis relancez la configuration.

## 7. Le banc

```bash
node --test tools/prerender/test/*.test.mjs
```

Il lance le script contre la vraie AWS CLI de l'image qu'il épingle et un S3 joué par moto
(`motoserver/moto`, épinglé, le même que le stockage simulé de la stack locale), sur un
réseau Docker jetable : création puis rejeu sans écriture, dérive corrigée et dite, retraits
gardés, epoch vivante ou future refusée, bucket d'un autre environnement laissé intact (par
son nom, ou ses retraits sous `ENVIRONMENT=staging`), politique sans l'opérateur jamais
envoyée, bucket versionné refusé, politique d'une autre graphie reconnue, accès de
l'opérateur vérifié avant l'arrêt sur une politique relue autrement, entrées invalides
refusées avant tout appel, sonde écrite puis relue, clé qui liste le bucket refusée,
politique retirée ; à chaque passage, aucune clé sur une ligne de commande ni à l'écran. Le
`docker` que voit le script est [`test/docker-relay.sh`](test/docker-relay.sh). moto ne
vérifie aucune signature (le banc joue le refus de liste de Scaleway) et ne lit pas le format
de politique de Scaleway : ce que la politique fait chez Scaleway, seul `verifier` le prouve.
La CI le lance (job `workflows`) ; sans démon Docker, il est sauté.
