# Brouillons du changelog joueurs de la bascule

Entrées de [`docs/changelog/`](../../changelog/README.md) pour la bascule vers la nouvelle
stack, rédigées à l'avance (L8.3) : aucune n'entre dans `docs/changelog/` avant le jour de la
bascule, puisqu'une entrée décrit ce qui arrive **en prod**. Format et règles de rédaction :
[`TEMPLATE.md`](../../changelog/TEMPLATE.md) et [`README.md`](../../changelog/README.md) du
changelog.

| Brouillon | Type | Sujet |
|---|---|---|
| `AAAA-MM-JJ-adresses-par-langue.md` | `feat` | la langue dans l'adresse, anciens liens redirigés |
| `AAAA-MM-JJ-pages-plus-rapides.md` | `perf` | pages rendues par le serveur et mises en cache |
| `AAAA-MM-JJ-nouveau-patch-sans-attente.md` | `perf` | les derniers patchs prêts dès leur sortie |
| `AAAA-MM-JJ-reconnexion-unique.md` | `devops` | une reconnexion demandée une fois, comptes et mots de passe inchangés |
| `AAAA-MM-JJ-applications.md` | `feat` | applications Windows, macOS et Android |

## Publication (runbook, § 4)

1. Remplacer `AAAA-MM-JJ` par la date de la bascule, dans le nom de chaque fichier et dans
   son champ `date`, puis déplacer les fichiers dans `docs/changelog/<année>/`.
2. Relire chaque entrée contre ce qui est réellement en prod ce jour-là :
   - `applications` : ne publier que les plateformes réellement disponibles au public
     (release desktop signée, app sur une piste Play ouverte) ; sinon retirer les lignes
     concernées, ou garder l'entrée pour le jour de leur publication ;
   - `adresses-par-langue` : le nombre de langues est celui de la release (21 à la
     rédaction) ;
   - rien ne s'affirme qui n'a pas été vérifié pendant la fenêtre.
3. Synthétiser dans la release publique de la bascule
   (`src/LoDb.Web/src/app/features/editorial/changelog/published/`), puis
   archiver les entrées dans `docs/changelog/archived/<année>/`.

En cas de retour arrière avant la publication, les brouillons restent ici. Après la
publication, une entrée `fix` ou `devops` datée du jour du retour arrière l'explique.
