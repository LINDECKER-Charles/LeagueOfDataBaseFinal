# Archive docs/changelog/

Entrées techniques **déjà synthétisées dans une release publiée**.

## Pourquoi

docs/changelog/YYYY/ ne doit contenir que les entrées **non encore publiées** — le backlog
de la prochaine release. Quand une release est synthétisée, toutes les entrées qui l'ont
alimentée sont déplacées ici. Cela évite de re-publier deux fois la même implémentation.

## Organisation

    docs/changelog/archived/
      YYYY/
        YYYY-MM-DD-slug.md     ← entrée déplacée telle quelle

L'année correspond toujours à la date d'origine de l'entrée (champ date du frontmatter),
**pas** à l'année d'archivage.

## Pendant la synthèse pré-release

1. Lister docs/changelog/YYYY/*.md (jamais archived/).
2. Trier : ce qui est visible joueur → entre dans la release publiée.
3. Quand toutes les entrées sont traitées, déplacer la totalité du contenu vers archived/YYYY/.
4. docs/changelog/YYYY/ redevient le slot vide pour la prochaine release.
