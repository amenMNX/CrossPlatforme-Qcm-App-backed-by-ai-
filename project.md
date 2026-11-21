# Documentation du projet — QCM App

## 1. Présentation

**QCM App** est une application de quiz multiplateforme développée en C# avec .NET MAUI et .NET 10. Elle permet de lancer des questionnaires à choix multiples à partir d’une banque locale, de consulter les résultats et l’historique, et de créer des questions manuellement ou avec l’aide de Google Gemini.

L’application est conçue pour rester utilisable hors ligne : les questions incluses dans le projet sont disponibles sans compte ni accès réseau. La génération par IA est optionnelle et peut retomber sur la banque locale lorsqu’elle ne peut pas fournir de questions.

## 2. Fonctionnalités

### Parcours de quiz

- Sélection d’une catégorie, d’un niveau (`Easy`, `Medium`, `Hard`) ou de toutes les catégories.
- Choix entre les questions locales et la génération avec Gemini.
- Présentation des questions une par une, avec quatre choix de réponse.
- Retour immédiat indiquant si la réponse est correcte et, en cas d’erreur, la bonne réponse.
- Mélange des questions et des choix de réponse pour les quiz locaux.
- Enregistrement du score, de la date, de la durée, de la catégorie, du niveau et du mode de génération.

Un quiz local demande jusqu’à dix questions et un quiz IA jusqu’à quatre. Le nombre réel dépend des questions disponibles et des filtres sélectionnés.

### Historique et statistiques

Les sessions enregistrées sont consultables dans l’historique. La page de statistiques calcule le nombre de parties, le meilleur pourcentage de réussite et la moyenne des scores à partir des sessions locales.

### Espace enseignant

L’espace enseignant permet d’ajouter une question avec quatre réponses, une réponse correcte, une catégorie et un niveau. Il permet aussi de demander à Gemini des questions sur un sujet, de les réviser puis de sauvegarder individuellement celles qui sont acceptées.

La connexion enseignant actuelle est une barrière de démonstration intégrée au client, pas un mécanisme d’authentification sécurisé. Elle ne doit pas servir à protéger des données ou fonctions sensibles en production.

## 3. Technologies et plateformes

- **Langage et SDK :** C#, .NET 10.
- **Interface :** .NET MAUI, XAML et architecture MVVM.
- **Navigation :** .NET MAUI Shell.
- **État et commandes MVVM :** CommunityToolkit.Mvvm.
- **Persistance locale :** SQLite avec `sqlite-net-pcl` et `SQLitePCLRaw.bundle_green`.
- **Appels HTTP :** `HttpClient` fourni par l’injection de dépendances.
- **Sérialisation :** `System.Text.Json`.

Le projet cible Android, iOS, Mac Catalyst et Windows. Les plateformes iOS et Mac Catalyst nécessitent les outils de développement Apple correspondants ; Android nécessite le workload et le SDK Android.

## 4. Architecture

Le projet suit une séparation MVVM :

- **Views** — pages XAML et code-behind pour l’interface.
- **ViewModels** — état des écrans et commandes utilisateur, avec `ObservableObject` et les attributs du Community Toolkit.
- **Models** — objets métier du quiz, comme `Question`, `Difficulty` et `QuizSettings`.
- **Services** — chargement des questions, génération IA et abstraction de fournisseur de questions.
- **Data** — objets persistés et accès à SQLite.

`MauiProgram.cs` configure les services et ViewModels dans le conteneur d’injection de dépendances. `AppShell.xaml` et `AppShell.xaml.cs` déclarent la navigation vers les pages de catégories, quiz, résultats, historique, statistiques et enseignant.

### Parcours principal des données

1. `CategoriesViewModel` charge les catégories disponibles et transmet la catégorie, le niveau et le choix IA à la page du quiz.
2. `QuizViewModel` sélectionne `QuestionService` ou `AiQuestionService` via l’interface `IQuestionProvider`.
3. Le service fournit des objets `Question` ; le ViewModel gère la progression, les réponses et le score.
4. À la fin, une session est enregistrée par `QuizDatabase` et l’utilisateur est dirigé vers la page de résultat.
5. Les pages d’historique et de statistiques relisent les sessions conservées localement.

## 5. Questions et stockage local

La banque initiale se trouve dans `Qcm app/Resources/Raw/questions.json` et est embarquée comme ressource MAUI. `QuestionService` la charge, récupère aussi les questions locales enregistrées, filtre par catégorie et difficulté, puis sélectionne et mélange les questions.

`QuizDatabase` initialise les tables SQLite suivantes :

- **`GameSession`** — résultats des parties, durée et indication du mode IA.
- **`TeacherQuestion`** — questions créées ou acceptées dans l’espace enseignant.
- **`GeneratedQuestion`** — questions générées par IA et enregistrées pour réutilisation.

La base s’appelle `quiz_history.db3`. En développement Windows/Debug, le code la place près du projet lorsqu’il détecte le fichier projet ; dans les autres cas, elle est placée dans le répertoire de données de l’application.

## 6. Génération Gemini et configuration

`AiQuestionService` envoie une demande de génération à Gemini et convertit la réponse structurée en questions. Les quiz IA utilisent un lot limité ; les questions reçues dans le parcours de quiz sont conservées dans SQLite. Dans l’espace enseignant, les questions générées sont proposées en aperçu pour validation avant leur sauvegarde.

La clé est lue d’abord depuis la variable d’environnement `GEMINI_API_KEY`. En configuration Windows/Debug uniquement, `GeminiApiKeyProvider` peut également la lire dans un fichier `.env` à la racine du dépôt ou du projet. Les instructions détaillées figurent dans [`Qcm app/GEMINI_SETUP.md`](Qcm%20app/GEMINI_SETUP.md).

Si la clé est absente ou si la requête IA échoue pendant un quiz, l’application tente d’utiliser les questions locales. La génération destinée à l’espace enseignant signale l’échec et ne sauvegarde pas de question non générée.

> **Sécurité :** une clé utilisée directement par une application cliente peut être extraite du client ou de ses requêtes. Le fichier `.env` sert uniquement au développement local : il ne doit jamais être commité. Pour une application distribuée, faites transiter les appels Gemini par un backend contrôlé et protégez-y la clé. Les questions générées doivent également être vérifiées avant utilisation.

## 7. Démarrage et compilation

### Prérequis

- .NET 10 SDK.
- Visual Studio avec la charge de travail **Développement .NET MAUI** pour la plateforme ciblée.
- Les SDK, workloads et outils natifs requis pour la plateforme choisie.

### Windows

1. Cloner le dépôt et ouvrir `Qcm app.slnx` dans Visual Studio.
2. Choisir la cible **Windows Machine** et la configuration **Debug**.
3. Lancer l’application. La banque locale ne nécessite aucune clé ni connexion Internet.

Pour activer Gemini en développement, copier `.env.example` vers `.env`, y renseigner `GEMINI_API_KEY`, puis redémarrer l’application. Ne remplacez pas `.env.example` par une clé personnelle.

### Compilation en ligne de commande sous Windows

```powershell
dotnet restore "Qcm app.slnx"
dotnet build "Qcm app.slnx" -f net10.0-windows10.0.19041.0
```

Les autres plateformes doivent être compilées sur un environnement disposant de leurs workloads et outils natifs. Le dépôt ne contient pas actuellement de projet de tests automatisés dédié.

## 8. Repères dans le dépôt

```text
Qcm app.slnx
Qcm app/
  Data/                 Entités et accès SQLite
  Models/               Modèles du quiz
  Platforms/            Configuration spécifique aux plateformes
  Resources/            Icônes, styles, polices et banque locale
  Services/              Fournisseurs de questions et intégration Gemini
  ViewModels/            État et commandes des pages
  Views/                 Pages XAML
  GEMINI_SETUP.md        Configuration locale de la clé Gemini
.env.example             Modèle sans secret pour le développement local
README.md                Présentation et démarrage rapide
project.md               Documentation détaillée du projet
```

## 9. Dépendances NuGet principales

Les références directes déclarées dans `Qcm app/Qcm app.csproj` comprennent :

- `CommunityToolkit.Mvvm` — primitives MVVM et génération de commandes/propriétés.
- `Microsoft.Maui.Controls` — contrôles et infrastructure MAUI.
- `Microsoft.Extensions.Logging.Debug` — journalisation de débogage.
- `Microsoft.Extensions.Http` — enregistrement des clients HTTP.
- `sqlite-net-pcl` et `SQLitePCLRaw.bundle_green` — accès SQLite multiplateforme.

Les versions exactes sont définies dans le fichier projet. La restauration s’effectue depuis NuGet.org, configuré dans `NuGet.Config`.
