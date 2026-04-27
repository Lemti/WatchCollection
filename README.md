# WatchCollection

Application de gestion de collection horlogère développée en Avalonia UI / .NET 10
dans le cadre du cours .NET III à HELB-Ilya Prigogine (Bachelier en Informatique de Gestion,
finalité Mobile Development).

## Auteurs

- **Harith Lemti** — Conception, développement, intégration MongoDB, logique d'import CSV
- **Younes Loukili** — Conception, développement, fonctionnalités UI

## Encadrement

Frédéric Van Goethem — Enseignant titulaire du cours .NET III, HELB-Ilya Prigogine.

## Description

WatchCollection est une application de bureau permettant à un utilisateur de
gérer une collection personnelle de montres. Elle propose les fonctionnalités
attendues d'une application CRUD multi-utilisateurs : authentification,
gestion d'une base privée par utilisateur, ajout, modification, suppression
et consultation, ainsi que l'import et l'export de la collection au format CSV.

L'application se distingue par une logique d'import CSV robuste, conçue
autour des bonnes pratiques de l'ingénierie de données (clé naturelle composite,
sémantique PATCH, idempotence, fusion identitaire).

## Architecture

### Patron MVVM

L'application suit strictement le patron Model-View-ViewModel grâce à la
bibliothèque `CommunityToolkit.Mvvm` :

- `Models/` — Entités du domaine (User, Watch).
- `Views/` — Interfaces utilisateur en AXAML.
- `ViewModels/` — Logique de présentation et liaison avec les vues.
- `Services/` — Accès aux données (MongoDB) et import/export (CSV, JSON).
- `Helpers/` — Utilitaires transverses (scanner code-barre, etc.).

### Persistance

La persistance est assurée par MongoDB, hébergée sur un serveur académique
mis à disposition par l'enseignement. Deux collections sont utilisées :

- `Users` — Comptes utilisateurs et rôles.
- `Watches` — Montres rattachées à un utilisateur via un champ `OwnerId`.

Deux index optimisent les opérations fréquentes :

- `ux_users_email` — Index unique sur l'adresse e-mail (unicité des comptes).
- `ix_watches_owner` — Index sur `OwnerId` (filtrage rapide par utilisateur).

## Pile technologique

- **.NET 10** — Framework cible (`net10.0`).
- **Avalonia 11** — Framework UI multiplateforme.
- **MongoDB.Driver 3.6** — Client officiel MongoDB pour .NET.
- **CommunityToolkit.Mvvm** — Génération de propriétés observables et de commandes
  via attributs source generators.
- **LiveChartsCore.SkiaSharpView.Avalonia** — Rendu de graphiques pour la vue d'analyse.

## Installation et configuration

### Prérequis

- SDK .NET 10 ou supérieur.
- Accès réseau au serveur MongoDB du cours.
- (Optionnel) JetBrains Rider ou Visual Studio 2022 pour le développement.

### Compilation et exécution

```bash
git clone https://github.com/Lemti/WatchCollection.git
cd WatchCollection
dotnet restore
dotnet build
dotnet run
```

### Configuration MongoDB

Les paramètres de connexion sont déclarés dans `Services/MongoDBService.cs`.
La chaîne de connexion par défaut pointe vers le serveur académique fourni.

## Fonctionnalités

### Authentification et gestion des comptes

- Inscription avec validation des champs (nom, prénom, e-mail, mot de passe).
- Connexion par e-mail et mot de passe.
- Hashage des mots de passe via PBKDF2 (RFC 2898), 10 000 itérations,
  recommandé par l'OWASP. Plus robuste qu'un SHA-256 simple ou qu'un bcrypt
  pour les volumes attendus.
- Le premier utilisateur inscrit obtient automatiquement le rôle
  d'administrateur, conformément au cahier des charges.
- Mode hors-ligne fonctionnel : si le serveur MongoDB est indisponible,
  l'application reste utilisable en mémoire avec un message clair.

### Collection de montres

- Ajout, consultation, modification et suppression des montres.
- Champs gérés : code-barre, marque, modèle, référence, mouvement,
  diamètre, matériau, prix, année, stock.
- Validation systématique avant enregistrement (intervalles de diamètre,
  d'année, de prix, de stock).
- Tri serveur par marque puis modèle.
- Vue d'analyse avec graphiques (répartition par marque, par mouvement,
  valorisation totale).

### Import et export CSV

L'import et l'export CSV sont les fonctionnalités les plus travaillées
de l'application. Elles répondent aux exigences du cahier des charges
tout en intégrant des concepts d'ingénierie de données.

#### Algorithme d'import

Pour chaque ligne du fichier CSV, l'application applique la séquence suivante :

1. **Recherche par code-barre** (clé d'unicité primaire). Si une montre
   du même utilisateur possède le code-barre du fichier, elle est mise à jour.
2. **Recherche par triplet `Brand + Model + Reference`** (clé naturelle
   composite). Utilisée comme fallback lorsque le code-barre est absent.
3. **Insertion** comme nouvelle montre dans tous les autres cas.

Cette double clé permet à l'utilisateur d'importer un CSV partiel
(par exemple sans code-barre) sans introduire de doublons.

#### Sémantique PATCH (fusion enrichissante)

Lorsqu'une montre existante est trouvée, seuls les champs présents et
non vides du CSV sont mis à jour. Les champs absents ou vides du CSV
sont conservés tels quels en base.

Cette logique correspond à la sémantique HTTP PATCH (mise à jour
partielle) plutôt qu'à PUT (remplacement complet). Elle permet par
exemple à l'utilisateur d'importer un CSV ne contenant que des prix
mis à jour, sans perdre les autres informations.

#### Idempotence et choix REPLACE plutôt qu'ADD

Le champ `Stock` suit la même logique de remplacement que les autres
champs : si le CSV indique `Stock = 5`, la valeur en base devient `5`,
et non `stockExistant + 5`.

Ce choix garantit l'**idempotence** de l'import : appliquer le même
CSV deux fois produit le même résultat qu'une seule application. Cette
propriété protège l'utilisateur contre les réimports accidentels.

Une logique d'incrémentation aurait été pertinente dans un contexte
de gestion de stock magasin (livraisons cumulées), mais elle n'a pas
de sens pour une collection horlogère personnelle, qui décrit un état
plutôt qu'un journal d'opérations. Cette interprétation est cohérente
avec les pratiques observées dans les outils ETL standards
(Excel, Notion, Airtable).

#### Fusion identitaire

Si une modification (par import ou édition manuelle) rend une montre
identique à une autre montre du même utilisateur (même triplet
`Brand + Model + Reference`), une fusion est proposée. Dans ce cas
spécifique, et uniquement dans celui-ci, les stocks sont **additionnés**
plutôt que remplacés, afin de ne pas perdre l'inventaire de la fiche
qui sera supprimée.

À l'import, la fusion est automatique. À la modification manuelle,
elle requiert une confirmation explicite de l'utilisateur via un
dialogue dédié.

#### Gestion des fichiers corrompus

L'import CSV traite chaque ligne dans un bloc `try/catch` indépendant.
Une ligne malformée (champ numérique invalide, colonnes manquantes)
est ignorée et comptabilisée dans un rapport, sans interrompre
l'import des lignes valides.

À la fin de l'import, l'utilisateur reçoit un message détaillé indiquant :

- le nombre de montres ajoutées,
- le nombre de montres mises à jour,
- le nombre de montres fusionnées (cas de fusion identitaire),
- le nombre de lignes ignorées pour cause de format invalide,
- la liste des montres incomplètes nécessitant une saisie manuelle.

### Validation et retours utilisateur

Toutes les opérations affichent un retour utilisateur via la propriété
`StatusMessage` du ViewModel concerné. Les erreurs sont caractérisées
(non disponibilité du serveur, validation, conflits) et présentées
de façon non-bloquante.

### Scanner code-barre (multiplateforme)

Le module `ScannerManager` détecte les périphériques USB de type scanner
sur Windows via WMI (`System.Management`). Sur les autres systèmes
d'exploitation, le module se neutralise proprement grâce à de la
compilation conditionnelle, garantissant la portabilité du code.

L'événement `BarcodeScanned` est bindé sur la propriété `Barcode` du
formulaire d'ajout, et non sur l'identifiant interne (`ObjectId`).

## Sécurité

- Mots de passe non stockés en clair (PBKDF2 avec sel et 10 000 itérations).
- Index unique sur l'e-mail empêchant les comptes en double.
- Filtrage des accès aux données par `OwnerId` : un utilisateur ne peut
  pas accéder aux montres d'un autre.
- Aucun appel `Console.WriteLine` ni log non contrôlé en production.

## Choix techniques notables

### Champ de propriété `OwnerId` côté `Watch` plutôt que `WatchIds[]` côté `User`

Deux modélisations sont possibles pour la relation utilisateur-montre :

- Tableau d'identifiants `WatchIds[]` côté `User`.
- Champ `OwnerId` côté `Watch`.

Le second pattern a été retenu car :

- Il évite la duplication d'information (un seul endroit où la relation
  est stockée).
- Les opérations CRUD sur les montres restent atomiques (une seule
  écriture, contre deux dans le pattern tableau).
- Il est plus performant pour la requête la plus fréquente (lister les
  montres d'un utilisateur), grâce à l'index `ix_watches_owner`.
- C'est le pattern standard en modélisation relationnelle (clé étrangère).

### Champ `Role` typé `string` plutôt que `bool` ou `int`

Le champ `Role` accepte les valeurs `"Admin"` ou `"User"`. Une chaîne
de caractères est plus extensible qu'un booléen `IsAdmin` ou qu'un
entier `Role: 1|2`, et plus lisible directement dans la base de données.

## Tests fonctionnels effectués

L'algorithme d'import CSV a été validé par huit cas de test distincts :

| # | Scénario | Résultat attendu |
|---|---|---|
| 1 | Import frais de 15 montres dans une base vide | 15 lignes ajoutées |
| 2 | Réimport du même fichier | 15 lignes mises à jour, aucun doublon |
| 3 | Import sans code-barre, avec triplet identifiant | Mise à jour par triplet, code-barre conservé |
| 4 | Import partiel d'une montre incomplète | Insertion avec avertissement listant les champs manquants |
| 5 | Import partiel mettant à jour le stock | Stock remplacé par la valeur du CSV (REPLACE) |
| 6 | Modification manuelle créant un doublon de triplet | Dialogue de confirmation puis fusion (stocks additionnés) |
| 6 bis | Annulation de la fusion depuis le dialogue | Aucune modification persistée, formulaire conservé |
| Bonus | Import d'un fichier comportant des lignes corrompues | Lignes valides importées, lignes invalides comptées et ignorées |

## Limitations connues

- L'application ne gère pas encore le téléversement d'images de montres
  (le champ `Picture` est prévu en modèle mais non interfacé).
- L'import JSON utilise un serveur partagé fourni par l'enseignement
  et n'est pas branché à la logique de propriété par utilisateur.
- Aucun test unitaire automatisé n'est inclus dans cette livraison.

## Licence

Projet académique, distribué sans licence formelle.
Toute réutilisation doit être préalablement convenue avec les auteurs.
