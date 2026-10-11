# Politique de sécurité

LeagueOfDataBase est un projet maintenu par une seule personne. Les signalements de
vulnérabilité sont les bienvenus et traités en priorité sur le reste du développement.

*English summary at the end of this file.*

## Versions prises en charge

| Composant | Pris en charge |
|---|---|
| Site de production `https://league-of-data-base.com` (branche `main`) | ✅ |
| API publique `/v1` | ✅ |
| Application desktop et Android : dernière version publiée | ✅ |
| Application desktop et Android : versions antérieures | ❌ (l'API impose la mise à jour) |
| Staging `https://test.league-of-data-base.com` | ✅ (préproduction, mêmes correctifs en avance) |
| Ancienne stack archivée sous [`legacy/`](legacy/README.md) (ni construite ni déployée) | ❌ |

## Signaler une vulnérabilité

**N'ouvrez pas d'issue, de pull request ni de discussion publique.**

Utilisez le signalement privé de GitHub : onglet **Security** du dépôt, puis
**Report a vulnerability**
([lien direct](https://github.com/LINDECKER-Charles/LeagueOfDataBaseFinal/security/advisories/new)).

Indiquez si possible :

- le composant touché (URL, route d'API, application, fichier et commit) ;
- l'impact et le scénario d'attaque ;
- les étapes de reproduction ou une preuve de concept minimale ;
- si des données d'autres personnes ont été consultées, et lesquelles ;
- comment vous recontacter et si vous souhaitez être crédité.

## Délais

Délais visés, en jours ouvrés, sans engagement contractuel :

| Étape | Délai |
|---|---|
| Accusé de réception | 3 jours |
| Évaluation et sévérité (CVSS) | 7 jours |
| Correctif déployé, sévérité critique | 7 jours après confirmation |
| Correctif déployé, sévérité élevée | 30 jours après confirmation |
| Correctif déployé, autres sévérités | prochaine livraison |

Une fois le correctif en production, un avis de sécurité GitHub est publié, avec votre
crédit si vous le souhaitez. Merci de ne rien divulguer avant cette publication ou avant
90 jours sans réponse de notre part. Le projet n'offre pas de prime (*bug bounty*).

## Périmètre

**Dans le périmètre** : le code de ce dépôt hors `legacy/`, le site de production et son
API, l'API publique `/v1`, les applications desktop et Android, la chaîne de livraison
(workflows GitHub Actions, images publiées).

**Hors périmètre** :

- le déni de service volumétrique, les tests de charge et le crawling massif (notamment
  des anciennes versions du jeu) ;
- l'ingénierie sociale, le hameçonnage et les attaques physiques ;
- les services tiers (Riot Games Data Dragon, CommunityDragon, Stripe, Google,
  l'hébergeur) : signalez-les à leur éditeur ;
- les rapports de scanners automatiques sans démonstration d'un impact exploitable ;
- l'absence d'un en-tête ou d'une bonne pratique sans scénario d'attaque concret
  (clickjacking d'une page sans action sensible, SPF/DMARC, bannières de version…) ;
- `legacy/`, archivé.

## Règles de test

Pour rester dans un cadre de bonne foi :

- testez avec vos propres comptes ; n'accédez pas aux données d'autrui, ne les modifiez ni
  ne les supprimez, et arrêtez-vous dès que la preuve est faite ;
- respectez les limites de débit : pas de déni de service, pas d'envoi massif par le
  formulaire de contact ou les courriels de compte ;
- n'utilisez aucun moyen de paiement réel ou appartenant à un tiers ;
- signalez rapidement ce que vous trouvez et ne le divulguez pas avant le correctif.

Un chercheur qui respecte ces règles de bonne foi ne fera l'objet d'aucune poursuite de
notre part.

## Pratiques du projet

Dépendances suivies par Dependabot (alertes et mises à jour hebdomadaires), analyse
statique CodeQL, détection et blocage des secrets à la publication, actions GitHub
épinglées par SHA de commit, images de base épinglées par digest et analysées avant le
déploiement, audit npm des dépendances de production en CI.

---

## English summary

Please report vulnerabilities **privately** through GitHub's private vulnerability
reporting (**Security** tab → **Report a vulnerability**). Do not open public issues. We aim
to acknowledge within 3 business days and to ship a fix within 7 days for critical issues.
Supported: the production site and API, the public `/v1` API and the latest desktop and
Android releases; `legacy/` is archived and unsupported. Out of scope: volumetric DoS and
mass crawling, social engineering, third-party services, scanner output without a
demonstrated impact. Test only with your own accounts and respect the rate limits; good-faith
research following these rules will not be pursued. There is no bug bounty.
