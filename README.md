# WatchCollection

> Projet académique — HELB Ilya Prigogine, Bachelor en informatique de gestion, .NET III (2025-2026)

Application multiplateforme de gestion d'une collection privée de montres, développée en C# / .NET 10 avec Avalonia UI 11 et MongoDB. Le logiciel permet l'acquisition de montres via scanner USB, la persistance distribuée (MongoDB privé + serveur JSON académique), l'import et l'export CSV, ainsi qu'une visualisation analytique de la collection.

## Auteurs

- **Harith Lemti** — auteur principal
- **Younes Loukili** — co-auteur

## Description fonctionnelle

L'application répond à un cahier des charges (v4.0) de gestion d'objets collectionnables :

- Acquisition d'identifiants via scanner USB (port série), avec saisie manuelle en alternative.
- Persistance privée par utilisateur dans une base MongoDB (collection `Users` + `Watches` liées par `OwnerId`).
- Échange via un serveur JSON académique commun pour la résilience hors-ligne.
- Import et export CSV avec choix des colonnes à exporter.
- Authentification avec hashage PBKDF2 (recommandation OWASP).
- Visualisations analytiques (camemberts, histogrammes) via LiveChartsCore.

## Architecture

Le projet suit une architecture **MVVM** stricte avec séparation des responsabilités.

## Pile technologique

| Couche | Technologie |
|---|---|
| Framework | .NET 10 |
| UI | Avalonia 11.3.11 + FluentTheme |
| MVVM | CommunityToolkit.Mvvm 8.2 |
| Base de données | MongoDB 2.28 |
| Visualisations | LiveChartsCore.SkiaSharpView 2.0 |
| Polices | Inter + Playfair Display (embarquées) |
| Scanner USB | System.IO.Ports + System.Management (Windows) |

## Installation et lancement

```bash
git clone https://github.com/Lemti/WatchCollection.git
cd WatchCollection
dotnet restore
dotnet build
dotnet run
```

## Fonctionnalités principales

### Mode hors-ligne (résilience réseau)

L'application gère explicitement la perte de connectivité au serveur MongoDB. Un lien permanent **« Tester en mode hors-ligne »** sur la page de connexion permet l'accès à l'application avec un utilisateur placeholder. Les données sont chargées en cascade : MongoDB → serveur JSON académique → données d'exemple.

### Import CSV avec déduplication intelligente

L'algorithme implémente plusieurs concepts de modélisation de données : match double-clé (Barcode → triplet Brand+Model+Reference), fusion enrichissante (sémantique PATCH HTTP), idempotence du stock (REPLACE et non ADD), et fusion identitaire (twin merge avec popup de confirmation).

### Direction esthétique

L'interface adopte une direction **« Maison Horlogère »** : palette noir profond + or champagne + crème, typographie serif (Playfair Display) pour les titres et sans-serif (Inter) pour le corps.

