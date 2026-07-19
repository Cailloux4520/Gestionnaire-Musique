# Gestionnaire Musique

Application Windows WinForms en C#/.NET 9 pour organiser, corriger et nettoyer une bibliotheque musicale locale. Elle lit les tags audio avec TagLib#, utilise le nom de fichier `Artiste - Titre.ext` comme repli, compare la qualite des fichiers pour les doublons et peut interroger MusicBrainz/iTunes pour certaines corrections.

Le programme est publie sous le nom **Gestionnaire Musique**. L'executable autonome genere se trouve dans `Publish\Gestionnaire Musique.exe`.

## Modes de traitement

Les boutons du haut ne lancent pas directement une action. Ils selectionnent seulement le traitement a effectuer. Le traitement reel demarre uniquement avec le bouton **Lancer**.

### Creer par artiste (Base)

- Utilise le dossier source et le dossier destination.
- Cree ou utilise `Destination\Artiste`.
- Deplace les fichiers dans des dossiers par artiste.
- Peut copier les pochettes associees.
- Peut corriger les tags manquants artiste/titre.
- Peut detecter les doublons par empreinte locale.
- Peut normaliser artiste/titre et garder seulement l'artiste principal.
- Peut analyser la bibliotheque finale : artistes, albums, morceaux, taille, pochettes manquantes et albums possiblement incomplets.

### Verifier Date Exacte (Lien)

- Utilise le dossier source et le dossier destination.
- Recherche l'annee originale du morceau via MusicBrainz, avec repli iTunes/Apple Music.
- Verifie la concordance titre/artiste pour eviter les compilations, best-of et reeditions tardives.
- Cree un lien audio dans le dossier destination selon l'annee trouvee.
- Ne modifie pas le tag date du fichier original.

### Verifier Date Exacte (Fichier)

- Meme logique que **Verifier Date Exacte (Lien)**.
- Le bouton est place sous le bouton Date Lien dans l'interface.
- Produit un lien fichier compatible avec les usages type VirtualDJ.
- Ne deplace pas les fichiers source.

### Creer par style

- Utilise uniquement le dossier source ; le dossier destination est grise.
- Lit les fichiers un par un.
- Cherche le style principal via MusicBrainz, puis iTunes si necessaire.
- Ne garde qu'un seul style principal.
- Modifie uniquement le tag **Genre/Style** si le style trouve est different.
- Ne touche pas a la date, au titre, a l'artiste ou au nom de fichier.
- Ne cree pas de dossiers et ne deplace aucun fichier.

### Trier et Corriger (Artistes et doublons)

- Utilise uniquement le dossier source ; le dossier destination est grise.
- Detecte les doublons probables par artiste/titre normalises.
- Detecte les doublons numerotes comme `Titre (1).mp3`, `Titre (2).mp3`.
- Peut detecter les doublons strictement identiques par empreinte de contenu.
- Garde le meilleur fichier selon la qualite audio, puis le bitrate, puis la taille.
- Envoie les doublons perdants a la Corbeille Windows.
- Ignore les versions differentes, par exemple live/studio ou durees trop eloignees.
- Peut corriger uniquement les tags artiste/titre selon le morceau reel.
- Ne deplace pas les fichiers par artiste.
- Ne renomme pas les fichiers pour les ranger dans un dossier artiste.
- Ne modifie pas la date ni le style.

## Options

L'interface affiche les options en colonnes alignees sous les modes : Artiste, Date, Style et Trier. Certaines options peuvent etre dans une autre colonne mais rester actives si le mode selectionne les utilise reellement.

- **Inclure les sous-dossiers** : parcourt aussi les dossiers enfants.
- **Lire les tags avant le nom du fichier** : priorise les metadonnees audio avant le nom de fichier.
- **Copier les pochettes associees** : copie les images de pochette avec les morceaux deplaces.
- **Corriger les tags artiste/titre** : complete ou corrige artiste et titre sans toucher a la date ou au style.
- **Detecter les doublons par empreinte** : compare le contenu du fichier pour trouver des doublons identiques.
- **Completer via MusicBrainz** : interroge MusicBrainz pour les metadonnees utiles au mode selectionne.
- **Recherche exacte de la date de sortie** : cherche l'annee originale fiable du morceau.
- **Normaliser artiste/titre** : corrige la casse et les espaces.
- **Garder seulement l'artiste principal** : retire les artistes invites du nom artiste utilise par le traitement.
- **Analyser la bibliotheque finale** : calcule les statistiques de la bibliotheque apres traitement.
- **Chercher le style sur internet** : cherche le genre principal via MusicBrainz puis iTunes.

Les options non utilisees par le mode selectionne sont grisees.

## Securite

- Les doublons sont envoyes a la Corbeille Windows, pas supprimes definitivement.
- Les traitements capturent les erreurs fichier par fichier pour continuer sur le reste de la bibliotheque.
- Les modes Style et Trier/Corriger travaillent sur le dossier source et ne deplacent pas les fichiers.
- Les parametres sont enregistres dans `%AppData%\Gestionnaire Musique\settings.json`.
- Les anciens parametres `%AppData%\MusicOrganizer\settings.json` sont repris automatiquement au premier lancement si necessaire.

## Qualite audio et doublons

Le classement de qualite est defini dans `Services\QualityComparer.cs` :

1. FLAC / ALAC / APE
2. WAV / AIFF
3. MP3 320 kb/s
4. AAC / WMA 320 kb/s
5. OGG Vorbis / OPUS haute qualite
6. MP3 256 kb/s
7. MP3 192 kb/s
8. MP3 128 kb/s ou moins

La taille du fichier ne sert qu'en dernier critere quand la qualite et le bitrate sont equivalents.

## Compiler

Prerequis : Windows avec le SDK .NET 9 ou Visual Studio compatible WinForms/.NET 9.

Depuis la racine du projet :

```bash
dotnet restore
dotnet build MusicOrganizer.sln
```

## Publier l'executable

Commande utilisee pour generer l'executable autonome Windows x64 :

```bash
dotnet publish MusicOrganizer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=false -o Publish
```

Sortie attendue :

```text
Publish\Gestionnaire Musique.exe
```

## Structure du projet

```text
Gestionnaire Musique/
├── Program.cs
├── MainForm.cs
├── MainForm.Designer.cs
├── MusicOrganizer.csproj
├── MusicOrganizer.sln
├── Models/
│   ├── AudioFileInfo.cs
│   ├── AudioFormatType.cs
│   ├── OrganizerSettings.cs
│   ├── OrganizerStats.cs
│   └── ProgressInfo.cs
├── Services/
│   ├── OrganizerService.cs
│   ├── DuplicateArtistSorterService.cs
│   ├── StyleTagCorrectionService.cs
│   ├── MusicBrainzService.cs
│   ├── MetadataService.cs
│   ├── QualityComparer.cs
│   ├── RecycleBinService.cs
│   └── SettingsPersistenceService.cs
└── Resources/
    └── icon.ico
```

## Notes

- L'empreinte de doublon integree est une empreinte de contenu local. Elle detecte les fichiers identiques, pas deux reencodages differents du meme morceau.
- MusicBrainz limite le rythme des requetes ; les traitements qui interrogent internet peuvent donc etre volontairement plus lents.
- Le nom interne du projet et du fichier `.sln` reste `MusicOrganizer`, mais le produit publie et les titres affiches sont **Gestionnaire Musique**.