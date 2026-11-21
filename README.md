# QCM App

Application de quiz multiplateforme réalisée avec .NET MAUI et .NET 10. Elle propose des questionnaires, conserve l’historique et les statistiques localement, et peut générer des questions avec l’API Gemini. Sans clé ou connexion disponible, l’application utilise sa banque de questions locale.

## Fonctionnalités

- Quiz par catégorie et niveau de difficulté
- Historique des parties et statistiques
- Gestion de questions pour les enseignants
- Génération de questions avec Gemini et repli sur la banque locale
- Stockage local avec SQLite

## Prérequis

- .NET 10 SDK
- Visual Studio 2026 avec la charge de travail **Développement .NET MAUI** pour la plateforme visée

## Lancement sous Windows

1. Clonez le dépôt et ouvrez `Qcm app.slnx` dans Visual Studio.
2. Sélectionnez la cible **Windows Machine** et la configuration **Debug**.
3. Lancez l’application. Les questions locales fonctionnent sans configuration supplémentaire.

Pour activer la génération Gemini en développement, copiez `.env.example` vers `.env` à la racine du dépôt et renseignez `GEMINI_API_KEY`. Consultez [Qcm app/GEMINI_SETUP.md](Qcm%20app/GEMINI_SETUP.md) pour les détails. Ne commitez jamais `.env` ni une clé réelle. Une clé intégrée à une application cliente distribuée ne peut pas être protégée ; utilisez un backend pour la production.

## Compilation

Depuis un terminal avec le SDK .NET 10 installé :

```powershell
dotnet restore "Qcm app.slnx"
dotnet build "Qcm app.slnx" -f net10.0-windows10.0.19041.0
```

Les autres cibles MAUI nécessitent leurs outils et workloads spécifiques à la plateforme.

## Licence

Consultez [LICENSE.txt](LICENSE.txt).
