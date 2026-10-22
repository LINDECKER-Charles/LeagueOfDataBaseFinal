---
date: 2026-10-16
type: fix
scope: back
title: Les anciennes adresses d'un patch mènent à la fiche du dernier patch
summary: Un ancien lien qui nommait un patch ouvre directement la fiche ou la liste du patch en cours, en une seule redirection.
tags: [seo]
---

> **Remplacé le 2026-10-22** par [`une-page-par-etat-dans-google`](2026-10-22-une-page-par-etat-dans-google.md) :
> un ancien lien qui nomme un patch ne mène plus à la fiche du patch en cours dès qu'elle
> existe : il mène à la page indexée de ce que ce patch montrait, celle du patch qui a
> ouvert cet état (avec le nom de l'époque), ou à la fiche du patch en cours si cet état
> dure encore. Un champion, un objet, une rune ou un sort retiré du jeu mène à la page du
> patch qui a ouvert son dernier état, même sans patch nommé. La liste du patch en cours, la
> 302 vers la liste d'un patch non stocké et la réponse d'attente tant que le catalogue du
> dernier patch n'est pas lu valent toujours.

## Ce qui change

Les liens de l'ancien site qui nommaient un patch (`/16.4.1/champion/Poppy`,
`/11.7.1/object/3020`, `/16.4.1/champions`) menaient à la page de ce patch, que le site
désigne pourtant aux moteurs de recherche comme une copie de la fiche du patch en cours. Ils
ouvrent désormais directement cette fiche, ou la liste du patch en cours, en une seule
redirection, même quand l'objet a changé de nom entre-temps. Un champion, un objet, une rune
ou un sort retiré du jeu garde sa page du patch nommé, comme avant.

## Pourquoi

Google suit encore des milliers de ces anciens liens. Les pages d'anciens patchs sont rendues
avec parcimonie : Google se voyait souvent répondre une erreur temporaire au lieu d'une page,
et un objet renommé lui coûtait deux redirections au lieu d'une.

## Technique

- ADR 0005, amendement du 2026-10-16 ; table des 301 héritées (`plan-migration.md`).
- `LegacyResolver` : une entité trouvée au catalogue du dernier patch mène à l'URL courte
  (`MovedToLatest`), une liste à la liste nue ; seule une entité absente du dernier patch garde
  la version nommée et le slug de son catalogue (`MovedToPinned`), ou la 302 vers la liste de
  sa version. Un catalogue du dernier patch pas encore lu répond `Pending` au lieu d'une fiche
  épinglée.
- 225 des 256 réponses 5xx servies à Googlebot depuis le 2026-10-10 (Search Console) étaient des
  pages épinglées atteintes ainsi, au-delà de la borne des rendus épinglés du SSR
  (`read-render-limits.ts`).
- Tests : `LegacyCatalogRedirectTests`, `LegacyStoredOnlyTests`, E2E `legacy-redirects.spec.ts`.
