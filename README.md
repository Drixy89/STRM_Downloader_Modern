# STRM Downloader

Application Windows .NET 8 WinForms pour scanner des fichiers `.strm` et télécharger
les médias référencés par des URL HTTP/HTTPS directement accessibles.

## Fonctionnalités

- Scan récursif des fichiers `.strm`
- Recherche de films et séries
- Détection simple des séries/saisons
- Téléchargement en `.mkv`
- Progression, vitesse et ETA
- Annulation du téléchargement
- Les fichiers `.strm` d'origine ne sont pas modifiés

## Développement

Projet .NET 8 WinForms destiné à être compilé en configuration Release.

## Signature

La signature du binaire n'est pas incluse dans le dépôt. Une chaîne de build de
signature externe peut être utilisée, par exemple SignPath pour un projet
open source éligible.

## Utilisation responsable

L'application télécharge uniquement des ressources HTTP/HTTPS auxquelles
l'utilisateur a légalement accès. Elle ne contourne pas les DRM, authentifications
ou protections d'accès.
