---
date: 2026-10-14
type: fix
scope: infra
title: Correctifs de sécurité de .NET appliqués au serveur
summary: Le serveur passe sur la dernière version corrective de .NET, qui ferme plusieurs failles publiées par Microsoft.
tags: [securite]
---

## Ce qui change

Rien de visible : le serveur du site tourne sur la version de .NET publiée le 13 octobre, qui
corrige plusieurs failles de sécurité d'ASP.NET Core (contournements de protections, déni de
service, élévation de privilèges).

## Technique

- Images de base `mcr.microsoft.com/dotnet/sdk:10.0` et `aspnet:10.0-noble-chiseled-extra`
  épinglées sur leurs digests 10.0.13 (`docker/api/Dockerfile`) : le scan Trivy du build
  bloquait sur CVE-2026-70188, CVE-2026-88092 et cinq autres, toutes corrigées en 10.0.13.
- Les paquets NuGet .NET étaient déjà passés en 10.0.13, `System.Security.Cryptography.Pkcs`
  épinglé (`Directory.Packages.props`).
