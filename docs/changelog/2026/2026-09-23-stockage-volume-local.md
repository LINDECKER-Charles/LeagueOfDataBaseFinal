---
date: 2026-09-23
type: devops
scope: full-stack
title: Les images du site sont servies directement depuis le disque
summary: Le serveur de stockage qui redémarrait en boucle est supprimé ; les images et données viennent désormais d'un simple volume, servi sans intermédiaire.
tags: [stockage, docker, nginx, supervision, performance]
---

## Ce qui change

Les images (champions, objets, runes, sorts) sont maintenant servies **directement depuis
le disque du serveur**, sans passer par un service de stockage intermédiaire. Pour le
joueur, rien ne change à l'écran, mais il y a un maillon de moins entre la page et ses
images.

## Pourquoi

Le service de stockage (MinIO) dépassait régulièrement sa limite mémoire et redémarrait,
en déclenchant des alertes en boucle. Il ne servait pourtant qu'à ranger des fichiers sur
le même disque que le reste du site, sans réplication ni sauvegarde : un simple volume
fait le même travail, sans ce coût.

## Détails

- Les images sont servies par nginx directement depuis le volume, avec le même cache d'un an.
- Seul le dossier des images est exposé sur le web : les données internes (jeux de données,
  manifestes, statistiques, journal d'audit) ne sont plus accessibles par une URL publique.
- Au premier déploiement, le volume part vide et le préchargement habituel le remplit à
  nouveau depuis Data Dragon.

## Technique

- Services `minio` et `minio-init` supprimés, ainsi que `docker/minio/init.sh`, le volume
  `minio_data` et toutes les variables `MINIO_*`. Nouveau volume `storage` monté en
  `/srv/storage` : lecture-écriture pour php, lecture seule pour nginx et go-api
  (`STORAGE_DIR`).
- PHP : Flysystem local à la place d'async-aws (dépendance `league/flysystem-async-aws-s3`
  retirée). `App\Service\Storage\AtomicWriteAdapter` écrit chaque fichier dans `.staging/`
  puis le `rename()` en place : c'est ce que garantissait un PUT S3. Sans lui, un lecteur
  concurrent (nginx, go-api, read-merge-write du manifeste) pouvait lire un fichier à moitié
  écrit.
- go-api : lecture via `os.DirFS` (`minio-go` retiré) ; les chemins invalides (`..`)
  donnent `ErrNotFound`. La clé de dépendance du `/healthz` passe de `minio` à `storage`.
- nginx : `location /cdn/blobs/` en `alias` sur le volume ; tout autre `/cdn/*` renvoie 404
  (liste d'autorisation). Cela remplace l'ancien bucket en lecture publique + liste de refus,
  qui exposait `/cdn/data/` et `/cdn/manifest/`.
- Sonde admin « MinIO » renommée « Stockage » (clé `storage`).
- `compose.deploy.yaml` : limite `minio` 1g supprimée. nginx garde 256m : le cache disque
  des images qu'il sert est désormais imputé à son cgroup (le working set lu par
  `ConteneurProcheDeSaLimiteMemoire` en compte la part active), mais il reste petit
  (~17 Mio de blobs par version).
- Migration : aucune copie. Le warmup du déploiement (`app:ddragon:warmup --limit=3` +
  `app:ddragon:webp`) repeuple le volume. Les agrégats analytics et les archives d'audit
  qui étaient dans MinIO sont abandonnés. L'ancien volume `<projet>_minio_data` reste sur
  l'hôte jusqu'à suppression manuelle.
