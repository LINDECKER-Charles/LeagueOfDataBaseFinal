# Informations légales — document de travail

Tout ce qui doit être **complété ou tranché** avant la mise en production des pages légales
(`/legal/notice`, `/legal/privacy`, `/legal/terms`, `/legal/cookies`). Les pages sont rendues
en FR (locale `fr*`) et EN (toutes les autres locales) ; le corps du texte est maintenu dans
`app/templates/legal/{fr,en}/`.

## 1. Paramètres (`app/config/packages/legal_info.yaml`) — état au 2026-07-17

Tous les paramètres `legal.*` sont injectés dans les pages via le DTO `App\Dto\LegalInfo`.

| Paramètre | Valeur posée | Statut |
|---|---|---|
| `legal.site_name` | `League Of Data Base` | ✅ |
| `legal.site_url` | `https://www.league-of-data-base.com` | ✅ (domaine en `.com` → locale par défaut des nouveaux visiteurs = EN, seed par TLD) |
| `legal.publisher_name` | `Charles LINDECKER` | ✅ |
| `legal.publisher_status` | `Particulier (private individual)` | ✅ |
| `legal.publisher_address` | *(vide, volontaire)* | ✅ régime **éditeur non professionnel** (LCEN art. 6, III-2) : les templates notice FR/EN affichent « adresse non publiée ; identité communiquée à l'hébergeur » — l'adresse personnelle n'apparaît nulle part |
| `legal.publisher_email` | `charles.lindecker@outlook.fr` | ✅ (voir décision « email dédié » §2) |
| `legal.publication_director` | `Charles LINDECKER` | ✅ |
| `legal.host_name` | `Hostinger — HOSTINGER operations, UAB` | ✅ opérateur technique (source : hostinger.com/legal/registrar-information) |
| `legal.host_address` | `Švitrigailos str. 34, 03230 Vilnius, Lituanie` | ✅ |
| `legal.host_phone` | `+370 645 03378` | ✅ (seul numéro publié par Hostinger) |
| `legal.siret` | `N/A` | ✅ (particulier) |
| `legal.dpo_email` | `charles.lindecker@outlook.fr` | ✅ pas de DPO formel (non obligatoire pour un particulier) |
| `legal.jurisdiction_country` | `France` | ✅ |
| `legal.effective_date` | `2026-07-17` | ✅ à rafraîchir à la mise en ligne réelle |

Note Hostinger : l'entité **contractante** selon les CGV est `Hostinger International Ltd,
61 Lordou Vironos str., 6023 Larnaca, Chypre` (mentionnée en complément dans la section
Hébergement des mentions) ; l'opérateur technique avec téléphone publié est
`HOSTINGER operations, UAB` (Vilnius). Vérifie l'entité qui figure sur **ta facture**
Hostinger — si c'est une autre, inverse les deux dans `legal_info.yaml` (1 ligne).

## 2. Décisions à prendre

| Sujet | Décision attendue | Impact |
|---|---|---|
| ~~Statut éditeur~~ | **Tranché : particulier**, adresse non publiée (LCEN art. 6-III-2) | Fait — SIRET `N/A`, mention explicative rendue par les templates notice. Option restante : anonymat **complet** (retirer aussi le nom) — possible tant que l'hébergeur connaît ton identité ; peu utile ici, le nom figure déjà dans le footer et sur le repo GitHub |
| ~~Hébergeur~~ | **Tranché : Hostinger** (operations UAB + entité contractante Intl Ltd) | Fait — entités UE (Lituanie/Chypre), la section « Transferts hors UE » de la confidentialité reste valable. **Micro-point** : choisis un data-center UE (ex. France) dans le panel Hostinger pour rester aligné |
| ~~Email de contact~~ | **Tranché : email perso** (`charles.lindecker@outlook.fr`) | Fait — posé dans `publisher_email` + `dpo_email` (cohérent avec le footer). Réversible en 2 lignes si un `contact@league-of-data-base.com` est créé un jour |
| **DPO / contact données** | Désigner un DPO ? (non obligatoire pour un particulier / petite structure) | Recommandé : pas de DPO formel, mais un email de contact données dédié (`legal.dpo_email`). Si aucun : pointer `dpo_email` sur `publisher_email` |
| **GeoLite2 (pays)** | Activer la géolocalisation pays (`GEOIP_DB_PATH` / `var/geoip/GeoLite2-Country.mmdb`) ? | La politique de confidentialité présente déjà le pays comme **optionnel et résolu localement** — vraie dans les deux cas. Si activation : respecter la licence MaxMind (attribution) et le process de mise à jour de la base |
| **Rétention analytics — POINT CRITIQUE** | Voir détail ci-dessous | La politique annonce 13 mois (bruts) / 25 mois (agrégats) : il faut rendre ça vrai |
| **Nom de domaine définitif** | Choisir et poser dans `legal.site_url` | Mentions légales + cohérence du cookie/locale par TLD (`.fr` → défaut FR) |

### Rétention analytics — **tranché le 2026-07-17 : option 3 (durées annoncées corrigées)**

État du code (inchangé) : **aucune purge automatique n'existe.**

- Les journaux bruts NDJSON (`var/analytics/events/{date}.ndjson`) contiennent **IP et
  User-Agent en clair** et s'accumulent tant que la consolidation manuelle n'est pas lancée.
- La consolidation vers MinIO (`analytics/daily/{date}.json`, agrégats **sans IP ni UA**)
  est **manuelle** : commande `app:analytics:rollup` (ou déclencheur admin), option
  `--prune` opt-in pour supprimer le NDJSON des journées consolidées. Aucun cron/scheduler.

Décision appliquée : les deux pages privacy (`legal/{fr,en}/privacy.html.twig`, tableau §1
+ §3) annoncent désormais la réalité — bruts « conservés jusqu'à consolidation puis
suppression manuelles, sans durée fixe garantie », agrégats « sans limite de durée ».

⚠️ **Trade-off assumé, à réévaluer avant la prod** : une conservation sans durée fixe de
journaux avec IP/UA colle mal à la doctrine CNIL mesure d'audience (~13 mois) et fragilise
la base « intérêt légitime » en cas de contrôle. Le correctif reste trivial le jour voulu :
planifier `php bin/console app:analytics:rollup --prune` en quotidien (cron/scheduler), puis
ré-annoncer des durées bornées dans les 2 pages privacy.

## 3. Stripe (dons)

- Créer le compte Stripe (profil « dons/soutien », particulier accepté) et activer
  **Stripe Checkout** (redirection — aucune donnée carte ne transite par le site).
- Basculer **mode test → mode live** avant la prod ; poser les clés dans l'environnement :
  `STRIPE_SECRET_KEY`, `STRIPE_WEBHOOK_SECRET` (placeholders déjà prévus dans les `.env`).
- Les **reçus** sont émis par Stripe (activer les « email receipts » dans le dashboard) —
  les CGU et la politique de confidentialité renvoient déjà vers Stripe pour ça.
- Les pages légales référencent `stripe.com/privacy` et la politique cookies Stripe ; rien
  d'autre à faire tant que le flux reste « Checkout hébergé, dons sans contrepartie ».

## 4. Rappel — conditions Riot Games (« Legal Jibber Jabber »)

- Projet **fan, non commercial** : OK tant que le site reste gratuit, sans pub et sans
  vente d'avantages. Les **dons pour couvrir les coûts** d'hébergement sont autorisés ;
  ne jamais vendre de contrepartie liée aux contenus Riot (pas de premium, pas de
  déblocage payant).
- **Disclaimer obligatoire** : affiché dans les mentions légales (§4) en EN verbatim sur
  la page EN, et en FR + EN d'origine en note sur la page FR — fait.
- Les assets restent la propriété de Riot ; le site les sert depuis les services publics
  Data Dragon / CommunityDragon (déjà le cas — pas de redistribution modifiée).

## 5. Checklist avant mise en production

- [x] ~~Compléter `legal_info.yaml`~~ — **100 % rempli le 2026-07-17** (aucun
      `[[À COMPLÉTER]]` restant, `site_url = https://www.league-of-data-base.com`).
- [x] ~~Rétention analytics~~ — tranché : durées annoncées corrigées (voir §2,
      trade-off documenté ; purge automatisable plus tard en 1 cron).
- [ ] Relire les 8 templates (`app/templates/legal/{fr,en}/*.html.twig`) :
      cohérence identité/hébergeur/emails, et rythme FR/EN.
- [x] ~~Clés de traduction `legal.*`~~ — posées en `en` + `fr` (les 19 autres locales
      retombent sur le fallback `en`, convention du repo).
- [x] ~~Email de contact~~ — tranché : perso (`charles.lindecker@outlook.fr`), aligné
      footer + pages légales.
- [x] ~~Lien « Mentions légales » du footer~~ — branché vers `app_legal_notice`
      + colonne Légal complète (privacy/terms/cookies) au footer.
- [x] ~~`legal.effective_date`~~ — `2026-07-17` confirmé (à rafraîchir seulement si la
      mise en ligne publique intervient à une autre date).
- [ ] Vérifier l'entité Hostinger de ta facture (voir note §1) et le data-center choisi (UE).
- [ ] Après lancement des comptes/dons : vérifier que les fonctionnalités décrites
      (suppression de compte sur le profil, visibilités des builds, checkout Stripe)
      correspondent exactement au comportement livré.
