# 🐳 Commandes Docker utiles

Référence des commandes Docker / Docker Compose pour la stack **LODB**.

> Toutes les commandes `docker compose` sont à lancer depuis la **racine du repo**
> (où se trouve `compose.yaml`). Le fichier `compose.override.yaml` est **auto-mergé**
> en local : pas besoin de le préciser. Le nom de projet est `lodb`.

## 🧱 Services de la stack

| Service      | Rôle                          | Port local (dev)                 |
|--------------|-------------------------------|----------------------------------|
| `nginx`      | Reverse proxy / front HTTP    | `8080` (`HTTP_PORT`) → app       |
| `php`        | Application Symfony (FPM)     | interne                          |
| `go-fetcher` | Worker Go (fetch DDragon)     | `8085`                           |
| `minio`      | Stockage objet (S3)           | `9000` API · `9001` console      |
| `minio-init` | Init bucket (one-shot)        | —                                |
| `mailer`     | Mailpit (SMTP + UI de test)   | `8025` UI · `1025` SMTP          |

Prérequis : copier `.env.example` → `.env` (voir `docs/configuration.md`).

---

## 🚀 Cycle de vie de la stack

```bash
# Démarrer toute la stack en arrière-plan (dev, override auto-mergé)
docker compose up -d

# Démarrer en (re)buildant les images locales
docker compose up -d --build

# Démarrer et suivre les logs (foreground)
docker compose up

# Arrêter les conteneurs (garde volumes + réseau)
docker compose stop

# Arrêter et supprimer conteneurs + réseau (garde les volumes)
docker compose down

# Down + suppression des volumes (⚠️ efface les données MinIO)
docker compose down -v

# Down + suppression des images buildées localement
docker compose down --rmi local

# Redémarrer un service
docker compose restart php

# État des services + healthchecks
docker compose ps
docker compose ps --format "table {{.Name}}\t{{.State}}\t{{.Status}}"
```

### Cibler un ou plusieurs services

```bash
# Ne (re)démarrer qu'un service et ses dépendances
docker compose up -d php

# Démarrer un service sans ses dépendances
docker compose up -d --no-deps go-fetcher

# Recréer un conteneur à neuf
docker compose up -d --force-recreate nginx
```

---

## 🏗️ Build & images

```bash
# Builder toutes les images du compose
docker compose build

# Builder sans cache (force from scratch)
docker compose build --no-cache

# Builder un service précis
docker compose build php

# Builder une cible multi-stage précise (Dockerfile PHP : php_base | app)
docker build -f docker/php/Dockerfile --target php_base -t lodb/app:dev .

# Lister les images
docker images
docker images "lodb/*"

# Supprimer une image
docker rmi lodb/app:latest
```

> En **dev**, le service `php` tourne sur la source live (`./app` bind-mount) via
> l'override, cible `php_base` : pas besoin de rebuild pour voir tes modifs PHP.
> En **prod** (`compose.yaml` seul), la source est copiée dans l'image (cible `app`).

---

## 📜 Logs

```bash
# Logs de tous les services (suivi live)
docker compose logs -f

# Logs d'un service
docker compose logs -f php

# N dernières lignes
docker compose logs --tail=100 nginx

# Logs depuis un instant
docker compose logs --since=10m go-fetcher

# Horodatage
docker compose logs -f --timestamps php
```

---

## 🖥️ Exécuter des commandes dans les conteneurs

```bash
# Shell interactif dans le conteneur PHP
docker compose exec php sh          # (bash si dispo)

# One-shot sans TTY (utile en CI / script)
docker compose exec -T php php -v

# Lancer une commande sans démarrer le service persistant
docker compose run --rm php sh
```

### Commandes Symfony / Composer (service `php`)

```bash
# Console Symfony
docker compose exec php php bin/console cache:clear
docker compose exec php php bin/console debug:router
docker compose exec php php bin/console debug:container

# Composer
docker compose exec php composer install
docker compose exec php composer dump-autoload --optimize

# Tests
docker compose exec php php bin/phpunit
```

### MinIO (client `mc`)

```bash
# Ouvrir un mc jetable connecté au MinIO de la stack
docker compose run --rm --entrypoint sh minio-init

# À l'intérieur : configurer l'alias puis lister/gérer les buckets
mc alias set local http://minio:9000 "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD"
mc ls local
mc ls local/ddragon
mc cp fichier.png local/ddragon/
```

---

## 🔎 Inspection & debug

```bash
# Détails complets d'un conteneur (config, mounts, env, réseau)
docker inspect $(docker compose ps -q php)

# État de santé d'un service
docker inspect --format='{{.State.Health.Status}}' $(docker compose ps -q go-fetcher)

# Processus tournant dans un service
docker compose top php

# Stats live (CPU / RAM / I/O)
docker stats

# Config finale mergée (compose.yaml + override + .env résolu)
docker compose config

# Vérifier la résolution des variables d'env
docker compose config | grep -A5 environment
```

---

## 💾 Volumes & données

```bash
# Lister les volumes
docker volume ls
docker volume ls --filter name=lodb

# Inspecter le volume MinIO
docker volume inspect lodb_minio_data

# ⚠️ Supprimer le volume MinIO (reset complet du stockage objet)
docker compose down
docker volume rm lodb_minio_data
```

---

## 🌐 Réseau

```bash
# Lister les réseaux
docker network ls

# Inspecter le réseau du projet (services + IP internes)
docker network inspect lodb_default

# Tester la résolution DNS inter-services depuis php
docker compose exec php sh -c "getent hosts go-fetcher minio nginx"
```

> Les services se joignent par **nom** sur le réseau interne :
> `http://go-fetcher:8085`, `http://minio:9000`, etc. (voir env du service `php`).

---

## 🧹 Nettoyage

```bash
# Supprimer conteneurs arrêtés, réseaux inutilisés, cache de build, images dangling
docker system prune

# Idem + images non utilisées et volumes anonymes (⚠️ agressif)
docker system prune -a --volumes

# Nettoyer uniquement le cache de build
docker builder prune

# Espace disque occupé par Docker
docker system df
```

---

## 🩺 Dépannage rapide

```bash
# Un service reste unhealthy → voir la cause
docker compose ps
docker compose logs --tail=50 <service>

# "APP_SECRET is required" / "MINIO_ROOT_PASSWORD is required"
#   → variable manquante dans .env (voir docs/configuration.md)
docker compose config   # montre quelles substitutions échouent

# Repartir de zéro (⚠️ efface les données)
docker compose down -v --remove-orphans
docker compose up -d --build

# Conteneur qui redémarre en boucle → inspecter le dernier exit code
docker inspect --format='{{.State.ExitCode}} {{.State.Error}}' $(docker compose ps -q <service>)

# Forcer la suppression d'orphelins (services retirés du compose)
docker compose up -d --remove-orphans
```

---

## 🔗 URLs utiles (dev)

| Service        | URL                     |
|----------------|-------------------------|
| Application    | http://localhost:8080   |
| MinIO Console  | http://localhost:9001   |
| Mailpit UI     | http://localhost:8025   |
| Go fetcher     | http://localhost:8085/healthz |
