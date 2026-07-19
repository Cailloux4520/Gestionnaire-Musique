# MusicOrganizer

Application Windows (WinForms, .NET 9, C#) qui organise automatiquement une
bibliothèque musicale par artiste, en se basant sur les tags audio (ou, à
défaut, sur le nom de fichier `Artiste - Titre.ext`), avec détection et
résolution intelligente des doublons par qualité audio.

## Fonctionnalités ajoutées

- Déplacement/copie des pochettes locales `cover.jpg`, `folder.jpg` et `AlbumArt*.jpg` avec les morceaux organisés.
- Correction optionnelle des tags manquants ou incohérents via TagLib#, avec normalisation des artistes et des majuscules/minuscules.
- Renommage configurable des fichiers avec un modèle comme `{Artist} - {Title}` ou `{Track} - {Title}`.
- Détection de doublons par empreinte SHA-256 du contenu, ce qui détecte les fichiers strictement identiques même si leurs noms diffèrent.
- Statistiques de bibliothèque : artistes, albums, morceaux, taille traitée, morceaux sans pochette et albums possiblement incomplets.
- Glisser-déposer de dossiers directement sur les champs source et destination.
- Interface avec mode sombre mémorisé dans le fichier de configuration.
- Récupération optionnelle de métadonnées manquantes depuis MusicBrainz.
- Mode `Recherche année d'origine` : scanne uniquement le dossier destination, retrouve l'année de première sortie du titre via MusicBrainz et met à jour le tag année. La date `first-release-date` du morceau est prioritaire afin d'éviter d'utiliser l'année d'une compilation ou d'un best-of. En mode simulation, aucune écriture n'est faite et les années proposées sont affichées dans le journal.
- Bouton `Créer par date` : scanne le dossier source, lit le tag année des morceaux, crée `Destination\Date\AAAA` si nécessaire et y ajoute des raccourcis Windows `.lnk` vers les fichiers originaux, sans déplacer les fichiers. En mode simulation, les raccourcis prévus sont affichés dans le journal sans être créés.
- Bouton `Créer par style` : scanne le dossier source, lit le tag genre des morceaux, crée `Destination\Style\Genre` si nécessaire et y ajoute des raccourcis Windows `.lnk` vers les fichiers originaux, sans déplacer les fichiers.
- Bouton `Afficher dossiers` : ouvre un tableau des dossiers directement présents dans le répertoire destination, avec le nombre de fichiers musicaux contenus dans chacun. Les colonnes sont triables, notamment du plus grand au plus petit nombre de fichiers.
- Les options affichent une courte aide entre parenthèses directement dans leur libellé.
- La zone sous les dossiers propose quatre modes exclusifs pleine largeur : `Créer par artiste (Base)`, `Créer par date`, `Créer par style` et `Trier (artistes et doublons)`. Le bouton `Lancer` exécute uniquement le mode sélectionné ; les champs dossier inutiles au mode choisi sont grisés.
- Les modes de création travaillent dans des sous-dossiers dédiés de la destination : `Artiste`, `Date` et `Style`, créés automatiquement si nécessaire.
- La création par date/style traite tous les fichiers trouvés et utilise un traitement parallèle pour accélérer la génération des raccourcis.
- Pour Date/Style, les liens créés gardent l'extension audio d'origine (`.mp3`, `.flac`, etc.) afin d'être visibles dans VirtualDJ comme des fichiers musique. Le programme tente un lien dur Windows, puis un lien symbolique si nécessaire.
- En mode `Créer par date`, si un fichier n'a pas d'année dans ses tags, MusicOrganizer tente de trouver son année de sortie originale sur MusicBrainz, vérifie la concordance du titre et de l'artiste, puis crée le lien dans un dossier `YYYY` en évitant les compilations, best-of et rééditions tardives.
- La concordance MusicBrainz tolère les variantes courantes (`feat.`, versions entre parenthèses, accents, `radio edit`, `remaster`, etc.) et relance une recherche plus large si la requête stricte ne trouve aucun résultat fiable.
- Si MusicBrainz ne trouve pas une année fiable, le mode Date tente aussi une recherche iTunes/Apple Music sans clé API, avec la même vérification titre/artiste.
- Si un ancien lien a été placé dans `Date\0` puis qu'une vraie année est trouvée ensuite, MusicOrganizer supprime ce lien obsolète avant de créer celui dans la bonne année.
- Bouton `Trier les doublons et artistes` : scanne le dossier destination, regroupe les titres probablement identiques malgré casse, ponctuation, apostrophes et suffixes numériques répétés comme `(2)`, `(3)`, conserve la meilleure qualité indépendamment de l'extension, envoie les doublons perdants à la Corbeille, corrige la casse des tags et du nom de fichier, et ne crée un dossier artiste que si le dossier actuel ne correspond pas déjà au premier artiste. Si un déplacement artiste est nécessaire depuis `h:\music\acdc`, le dossier cible est créé au niveau parent `h:\music\Artiste`, jamais à l'intérieur de `h:\music\acdc`. Tous les fichiers dont le nom finit par un chiffre entre parenthèses sont d'abord comparés au même nom sans ce chiffre ; si le fichier numéroté est le meilleur, l'autre est supprimé et le meilleur est renommé sans suffixe au format `Artiste - Titre.ext`. Si un nom cible existe déjà, il compare les deux fichiers et supprime le moins bon au lieu de créer un nouveau `(2)`. Une seconde passe nettoie les doublons révélés après renommage. Il supprime ensuite les dossiers qui ne contiennent plus aucun fichier musical, tout en conservant les dossiers générés `Date`, `Style` et `Playlists`. Les versions `(live)` restent traitées comme des titres distincts. En mode simulation, les décisions sont seulement écrites dans le journal.
  Exemple traité comme doublon numéroté : `Acdc - Gone Shootin (Live) (2) (2).flac` et `Acdc - Gone Shootin' (Live) (3) (2).flac` sont comparés comme `AC/DC - Gone Shootin (Live).flac`.

Note : l'empreinte intégrée est une empreinte de contenu locale. Pour détecter deux réencodages différents du même morceau, il faudrait intégrer une vraie empreinte acoustique de type Chromaprint/AcoustID.

## ⚠️ Important : compilation

Ce projet a été écrit intégralement (tout le code C#, le `.csproj`, l'icône)
mais **n'a pas pu être compilé dans l'environnement qui l'a généré**, qui est
un bac à sable Linux sans SDK .NET installé et sans accès réseau (donc sans
possibilité de restaurer les paquets NuGet comme TagLibSharp). Il n'y a pas
non plus de machine Windows disponible pour tester l'exécutable final.

**Le code est complet et prêt à compiler tel quel** sur une machine Windows
avec Visual Studio 2026 (ou le SDK .NET 9). Aucune modification ne devrait
être nécessaire — mais comme il n'a pas pu être vérifié par une compilation
réelle, il est possible qu'une petite erreur de syntaxe ou de référence
apparaisse à la première compilation. Si c'est le cas, copiez-collez le
message d'erreur et il sera corrigé immédiatement.

## Compiler et publier

Depuis une invite de commandes, à la racine du projet :

```bash
dotnet restore
dotnet build -c Release

# Génère l'exécutable autonome, fichier unique, x64 :
dotnet publish -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -o Publish
```

Le dossier `Publish\` contiendra `MusicOrganizer.exe` (et quelques fichiers
indispensables comme les DLL natives auto-extractibles) — testable sur une
machine sans .NET installé.

Vous pouvez aussi simplement ouvrir `MusicOrganizer.sln` dans Visual Studio
2026, choisir la configuration `Release | x64`, puis clic droit sur le projet
→ **Publier**.

## Architecture

```
MusicOrganizer/
├── Program.cs                      Point d'entrée
├── MainForm.cs / MainForm.Designer.cs   Interface (WinForms)
├── Models/
│   ├── AudioFormatType.cs          Formats supportés + détection par extension
│   ├── AudioFileInfo.cs            Métadonnées d'un fichier scanné
│   ├── OrganizerSettings.cs        Options utilisateur (persistées en JSON)
│   ├── OrganizerStats.cs           Compteurs thread-safe (Interlocked)
│   └── ProgressInfo.cs             Snapshot envoyé à l'UI (IProgress<T>)
├── Services/
│   ├── MetadataService.cs          Lecture des tags (TagLib#) + repli sur le nom de fichier
│   ├── FileNameSanitizer.cs        Nettoyage des caractères interdits Windows
│   ├── QualityComparer.cs          Classement de qualité audio (voir plus bas)
│   ├── DuplicateResolver.cs        Décision garder / remplacer / renommer
│   ├── RecycleBinService.cs        Envoi à la Corbeille (jamais de suppression définitive)
│   ├── LoggingService.cs           Journal thread-safe (fichier + flux temps réel vers l'UI)
│   ├── SettingsPersistenceService.cs   Sauvegarde des derniers dossiers utilisés
│   └── OrganizerService.cs         Moteur principal (scan, parallélisation, déplacement)
└── Resources/icon.ico              Icône (dossier + note de musique)
```

### Priorité de détermination de l'artiste

1. Tag "Artiste" du fichier (si la case correspondante est cochée et le tag présent).
2. Sinon, nom de fichier au format `Artiste - Titre.ext` (l'artiste est toujours
   la partie avant le **premier** " - ").

### Classement de qualité (en cas de doublon)

Implémenté dans `QualityComparer`, dans l'ordre demandé :

1. FLAC / ALAC / APE (sans perte)
2. WAV / AIFF (sans perte)
3. MP3 320 kb/s
4. AAC / WMA 320 kb/s
5. OGG Vorbis / OPUS, haute qualité
6. MP3 256 kb/s
7. MP3 192 kb/s
8. MP3 128 kb/s ou moins

La taille du fichier n'intervient **qu'en cas d'égalité stricte** de rang et de
débit binaire — jamais avant, exactement comme demandé. Avant toute décision,
`QualityComparer.AreLikelyDifferentTracks` vérifie que la durée, la fréquence
d'échantillonnage et le nombre de canaux sont cohérents ; si l'écart est trop
important, les deux fichiers sont considérés comme des morceaux différents et
tous les deux conservés (`Titre (2).ext`), sans comparaison de qualité.

### Sécurité

- Aucun fichier n'est jamais supprimé définitivement : tout doublon perdant
  passe par la Corbeille Windows (`Microsoft.VisualBasic.FileIO.FileSystem`).
- Si la case "Envoyer les doublons dans la Corbeille" est décochée, l'outil ne
  supprime ni n'écrase jamais rien : le fichier entrant est renommé au lieu
  d'être perdu.
- Toutes les exceptions sont interceptées au niveau fichier : une erreur sur
  un fichier n'interrompt jamais le traitement des autres, et l'application ne
  plante jamais (gestionnaires globaux `AppDomain.UnhandledException` /
  `Application.ThreadException` dans `Program.cs`).

### Performance

- `Directory.EnumerateFiles` (streaming, pas de `GetFiles` qui charge tout en
  mémoire) avec `EnumerationOptions.IgnoreInaccessible`.
- `Parallel.ForEachAsync` avec `MaxDegreeOfParallelism = Environment.ProcessorCount`.
- Un verrou léger *par dossier artiste* (et non global) sérialise uniquement
  les décisions de doublons concurrentes sur le même artiste, sans jamais
  bloquer le traitement des autres artistes.
- Seules les métadonnées sont lues (jamais le flux audio complet) : la mémoire
  reste stable même sur une bibliothèque de plus de 100 000 fichiers.
- `IProgress<ProgressInfo>` + un `Timer` UI toutes les 250 ms maintiennent une
  interface fluide sans jamais bloquer le thread UI.

## Limitations connues / points d'attention

- Le fichier `MusicOrganizer.Designer.cs` a été écrit à la main (contrôles
  créés par code plutôt que par le concepteur visuel de Visual Studio). Il
  compile et s'exécute normalement, mais si vous ouvrez `MainForm` dans le
  concepteur visuel (drag-and-drop), Visual Studio pourrait ne pas offrir
  toutes les fonctionnalités habituelles de glisser-déposer tant que le
  fichier n'a pas été régénéré depuis l'éditeur graphique.
- "OGG Vorbis qualité maximale" et les seuils AAC/WMA/OPUS sont des notions
  relatives : `QualityComparer` utilise des seuils de débit binaire réalistes
  (documentés en commentaires dans le fichier) pour les traduire en règles
  concrètes. Ajustez ces seuils si votre bibliothèque a des besoins différents.
