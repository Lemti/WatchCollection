# WatchCollection

Application de gestion et de présentation d'une collection de montres, développée en C# avec Avalonia UI et le pattern MVVM.

## Auteurs
- Harith Lemti
- Younes Loukili

## Stack technique
| Composant | Technologie |
|-----------|-------------|
| Framework | .NET 9.0 / C# |
| UI | Avalonia 11.3.11 (XAML) |
| Pattern | MVVM (CommunityToolkit.Mvvm) |
| Base de données | MongoDB 4.4 (Docker) |
| Graphiques | LiveChartsCore |
| SerialPort | System.IO.Ports |
| JSON distant | HttpClient / JSONManagerAPI |
| CSV | Import/Export avec System.IO |

## Fonctionnalités
- Collection de montres avec data binding (ObservableCollection)
- Ajout de montres via formulaire et scanner USB (SerialPort M900D)
- Import/Export CSV avec sélection de colonnes par l'utilisateur
- Chargement et sauvegarde JSON sur serveur distant
- Comptes utilisateurs stockés en MongoDB (mot de passe crypté SHA256)
- Page administration (gestion des utilisateurs, accès restreint au rôle admin)
- Graphiques : stock par marque, répartition par mouvement, comparaison des prix
- Mode bypass si MongoDB est indisponible
- Modification et suppression de montres depuis la page détails

## Prérequis
- .NET SDK 9.0 : https://dotnet.microsoft.com/download/dotnet/9.0
- JetBrains Rider ou Visual Studio
- Docker (pour MongoDB)

## Installation de MongoDB

Créer un fichier `compose.yaml` :

```yaml
services:
  mongo:
    image: mongo:4.4
    container_name: mongoLab
    ports:
      - "5003:27017"
    volumes:
      - MongoLAB:/data/db
    environment:
      - MONGO_INITDB_ROOT_USERNAME=student
      - MONGO_INITDB_ROOT_PASSWORD=IAmTh3B3st

volumes:
  MongoLAB:
```

Lancer MongoDB :

```bash
docker compose up -d
```

## Lancer le projet
1. Ouvrir `WatchCollection.sln` dans JetBrains Rider
2. Attendre la restauration des packages NuGet
3. Build (`Ctrl+Shift+B`)
4. Run (`F5`)

## Paramètres MongoDB
- Host : localhost
- Port : 5003
- Username : student
- Password : IAmTh3B3st
- Database : WatchCollectionDb

## Structure du projet
```
WatchCollection/
├── Models/          # Watch.cs, User.cs
├── ViewModels/      # MVVM ViewModels
├── Views/           # Interfaces XAML
├── Services/        # MongoDB, JSON, CSV, Scanner
├── Helpers/         # ImageHelper
├── Assets/          # Images des montres
└── Globals.cs       # Données globales
```
