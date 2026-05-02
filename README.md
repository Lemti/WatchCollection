# WatchCollection

> Projet académique de seconde session — HELB Ilya Prigogine, Bachelor en informatique de gestion, .NET III (2025-2026)

Application multiplateforme de gestion d'une collection privée de montres, développée en C# / .NET 10 avec Avalonia UI 11 et MongoDB. Le logiciel permet l'acquisition de montres via scanner USB, la persistance distribuée (MongoDB privé + serveur JSON académique), l'import et l'export CSV, ainsi qu'une visualisation analytique de la collection.

## Auteurs

- **Harith Lemti** — auteur principal
- **Younes Loukili** — co-auteur

## Description fonctionnelle

L'application répond à un cahier des charges (v4.0) de gestion d'objets collectionnables, avec les contraintes suivantes :

- Acquisition d'identifiants via scanner USB (port série), avec saisie manuelle en alternative.
- Persistance privée par utilisateur dans une base MongoDB (collection `Users` + `Watches` liées par `OwnerId`).
- Échange via un serveur JSON académique commun pour la résilience hors-ligne.
- Import et export CSV avec choix des colonnes à exporter.
- Authentification avec hashage PBKDF2 (recommandation OWASP).
- Visualisations analytiques (camemberts, histogrammes) via LiveChartsCore.

## Architecture

Le projet suit une architecture **MVVM** stricte avec séparation des responsabilités :

```
WatchCollection/
├── Models/             # Entités du domaine (Watch, User, ExportColumn)
├── ViewModels/         # Logique de présentation et de navigation
├── Views/              # Interfaces utilisateur Avalonia (.axaml)
├── Services/           # Accès aux données et matériel
│   ├── MongoDBService    # Persistance MongoDB
│   ├── JSONServices      # Communication serveur JSON académique
│   ├── CSVServices       # Import/export CSV
│   └── ScannerManager    # Scanner USB cross-platform
├── Helpers/            # ImageHelper, conversions
├── Styles/             # Palette + styles centralisés (luxe horloger)
└── Assets/             # Images, icônes, polices embarquées
    └── Fonts/          # Inter + Playfair Display (rendu identique partout)
```

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

### Prérequis

- .NET 10 SDK installé ([dotnet.microsoft.com](https://dotnet.microsoft.com/))
- Accès réseau au serveur MongoDB académique (port 443 sortant)

### Cloner et lancer

```bash
git clone https://github.com/Lemti/WatchCollection.git
cd WatchCollection
dotnet restore
dotnet build
dotnet run
```

### Build de production (Linux x64)

```bash
dotnet publish -c Release -r linux-x64 --self-contained true
```

## Identifiants de test

Au premier lancement, créer un compte via la page d'inscription. Le **premier utilisateur enregistré devient automatiquement Admin** ; les suivants sont `User` standard.

## Fonctionnalités principales

### Authentification et collection privée

Chaque utilisateur dispose d'une collection privée. Les administrateurs voient toutes les montres de la base, les utilisateurs standards uniquement les leurs. Les mots de passe sont hashés via **PBKDF2** (10 000 itérations, sel statique applicatif), conformément à la recommandation OWASP pour le stockage de credentials.

### Acquisition par scanner USB

L'application détecte automatiquement les ports série disponibles (cross-platform via `System.IO.Ports`). Le scanner émet le code-barre lu, qui est répercuté dans le champ correspondant du formulaire d'ajout. Le code-barre joue le rôle d'identifiant unique de la montre dans la collection (clé d'unicité primaire).

### Mode hors-ligne (résilience réseau)

L'application gère explicitement la perte de connectivité au serveur MongoDB :

1. Au démarrage, un ping de 10 secondes vérifie la disponibilité du serveur.
2. En cas d'échec, l'authentification standard est désactivée et un message explicite s'affiche.
3. Un lien permanent **« Tester en mode hors-ligne »** sur la page de connexion permet l'accès à l'application avec un utilisateur placeholder (`Role = User`).
4. Les données sont chargées en cascade : MongoDB → serveur JSON académique → données d'exemple en mémoire.
5. Les opérations CRUD restent fonctionnelles en mémoire pendant la session hors-ligne.

### Import CSV avec déduplication intelligente

L'algorithme d'import CSV implémente plusieurs concepts issus de la modélisation de données :

#### 1. Match double-clé

Pour chaque ligne du fichier CSV, la déduplication tente d'abord une correspondance par **`Barcode`** (clé d'unicité primaire). En cas d'absence ou de non-correspondance, l'algorithme tombe sur une **clé naturelle composite** `Brand + Model + Reference`, classique en data warehousing.

#### 2. Fusion enrichissante (sémantique PATCH)

Lorsqu'une montre déjà présente est retrouvée, les champs non vides du CSV viennent **enrichir** la montre existante sans écraser les valeurs déjà saisies (sémantique d'un `HTTP PATCH`, par opposition à `PUT` qui remplacerait l'enregistrement). Cette approche permet d'utiliser un CSV partiel pour mettre à jour ponctuellement certains champs.

#### 3. Idempotence du stock (REPLACE et non ADD)

Le champ `Stock` est **remplacé** et non additionné. Justification :

- L'idempotence : importer N fois le même CSV doit produire le même résultat.
- La sémantique : la collection privée n'est pas un stock magasin où l'on incrémente.
- Cohérence avec les ETL standards (Excel, Notion, Airtable).

#### 4. Fusion identitaire (twin merge)

Si après mise à jour, la montre devient strictement identique (au sens de la clé naturelle) à une autre montre déjà présente dans la collection, une **boîte de dialogue** propose à l'utilisateur de fusionner les deux entités, en additionnant leurs stocks et en supprimant le doublon. L'utilisateur peut annuler la fusion.

### Export CSV avec sélection de colonnes

L'utilisateur peut choisir, via une liste de cases à cocher, les colonnes à inclure dans l'export. Le CSV produit est encodé en UTF-8 avec BOM, séparateur `;`, compatible Excel et Notion.

### Visualisations

La page Graphiques affiche, via LiveChartsCore :

- Camembert du stock par marque
- Camembert des types de mouvements
- Histogramme comparatif des prix

## Choix techniques notables

### Modélisation MongoDB : `OwnerId` côté Watch

Plutôt que de stocker `WatchIds[]` côté `User`, chaque montre porte un champ `OwnerId` indexé. Avantages :

- Opérations atomiques (ajout/suppression d'une montre = un seul document).
- Filtrage performant (index dédié `ix_watches_owner`).
- Scalabilité (pas de tableau qui grossit indéfiniment dans le document utilisateur).
- Pattern relationnel standard appliqué à MongoDB.

### Indexation

Deux index sont créés au démarrage :

- `ux_users_email` (unique) : empêche les doublons d'inscription.
- `ix_watches_owner` : accélère le filtrage de la collection privée.

### Rôle utilisateur stocké comme `string`

Le champ `Role` est un `string` (`"Admin"` ou `"User"`) plutôt qu'un booléen `IsAdmin` ou un enum sérialisé en entier. Cela permet une évolution future (ajout de rôles intermédiaires) sans migration de données.

### Polices embarquées

Inter et Playfair Display sont embarqués comme `AvaloniaResource` plutôt que chargées depuis le système. Cela garantit un rendu identique sur tous les postes (Windows, Linux, macOS), indépendamment des polices installées localement.

### Direction esthétique

L'interface adopte une direction **« Maison Horlogère »** : palette noir profond + or champagne + crème, typographie serif (Playfair Display) pour les titres et sans-serif (Inter) pour le corps. Cohérent avec le domaine fonctionnel (collection de montres de luxe).

## Tests fonctionnels validés

| # | Scénario | Résultat |
|---|---|---|
| 1 | Login normal MongoDB | OK |
| 2 | Login en mode hors-ligne | OK |
| 3 | Ajout manuel d'une montre | OK |
| 4 | Ajout via scanner USB | OK |
| 5 | Modification d'une montre | OK |
| 6 | Suppression d'une montre | OK |
| 7 | Import CSV avec déduplication par Barcode | OK |
| 8 | Import CSV avec déduplication par clé naturelle | OK |
| 9 | Import CSV avec fusion identitaire (popup) | OK |
| 10 | Import CSV en mode hors-ligne | OK |
| 11 | Export CSV avec sélection de colonnes | OK |
| 12 | Visualisations (camemberts, histogramme) | OK |
| 13 | Gestion utilisateurs (admin uniquement) | OK |

