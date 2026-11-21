# Configuration de Gemini pour le lancement Windows depuis Visual Studio

`GeminiApiKeyProvider` lit d’abord `GEMINI_API_KEY` dans l’environnement du processus. En configuration **Windows/DEBUG** seulement, il cherche ensuite un fichier `.env` à la racine du dépôt (ou du projet). Le profil `Windows Machine` ne définit pas la variable par défaut ; sans l’une de ces configurations, le quiz utilise la banque locale.

Pour le développement Windows :

1. Copiez `.env.example` vers `.env` à la racine du dépôt et remplacez la valeur vide par la nouvelle clé. `.env` est ignoré par Git. Ne placez jamais la valeur dans le code, `launchSettings.json`, les journaux ou les captures d’écran.
2. Relancez le profil **Windows Machine** en configuration DEBUG. Vous pouvez aussi définir `GEMINI_API_KEY` dans l’environnement Windows ; cette valeur est prioritaire sur `.env`.
3. Si la clé est absente ou si Gemini refuse l’appel, le quiz utilise la banque locale. Le fichier `.env` n’est pas chargé en Release ni sur Android/iOS.

Une variable d’environnement du processus de développement n’est pas un mécanisme de distribution de secrets. Une application cliente distribuée (notamment Android/iOS) ne peut pas protéger une clé API qui lui est fournie. Pour une mise en production, faites appeler Gemini par un service backend contrôlé au lieu d’intégrer la clé dans l’application.
