# Palworld Server Editor

Editor **portable per Windows** di `PalWorldSettings.ini`: un unico `.exe` da ~120 KB, niente installazione, niente dipendenze.

## Download e avvio

1. Scarica `PalworldServerEditor.exe` dalla pagina **Releases** (oppure dagli *artifact* dell'ultima build nella scheda **Actions**).
2. Mettilo dove vuoi, ad esempio sul Desktop accanto al tuo `PalWorldSettings.ini`.
3. Doppio clic. Il file si apre da solo se si trova nella stessa cartella dell'exe, sul Desktop, oppure se è l'ultimo che hai aperto.
   Altrimenti usa **Apri…** o trascina il file nella finestra (o sull'icona dell'exe).

Requisiti: Windows 10 o 11 (usa .NET Framework 4.8, già incluso in Windows).
L'exe non è firmato, quindi SmartScreen può mostrare un avviso: *Ulteriori informazioni → Esegui comunque*.

## Funzioni

- Impostazioni divise in sezioni (Server, Mondo, Raccolta e oggetti, Combattimento, Sopravvivenza, Basi, Giocatori e gilde, Avanzate) con nomi e descrizioni in italiano; il nome originale della chiave è sempre visibile.
- Interruttori Sì/No, menu a scelta, campi numerici (accettano sia `,` che `.`), password nascoste con casella **Mostra**, piattaforme crossplay con caselle di spunta.
- Ricerca istantanea (anche senza accenti: `velocita` trova *Velocità*).
- Le righe modificate sono evidenziate, la barra laterale mostra quante modifiche ci sono in ogni sezione, ogni riga ha il pulsante per annullare la modifica e il link per tornare al valore predefinito di Palworld.
- La sezione **Avanzate** contiene *tutte* le altre chiavi del file: anche i parametri che arriveranno con le prossime versioni di Palworld restano modificabili.
- Salvataggio sicuro:
  - cambiano solo i valori modificati; il resto del file (ordine, commenti, formattazione, codifica, a capo) resta identico;
  - prima di scrivere viene creato un backup nella cartella `PalWorldSettings - backup` accanto al file (restano gli ultimi 15);
  - se nel frattempo il file è stato cambiato da un altro programma, l'editor avvisa e applica le tue modifiche sopra la versione attuale.
- Avviso se il file è ancora il modello di esempio (`DefaultPalWorldSettings.ini`).
- Scorciatoie: **Ctrl+S** salva, **Ctrl+O** apri, **Ctrl+F** cerca, **F5** ricarica.

## Dove va il file

Il server dedicato legge solo:

```
PalServer\Pal\Saved\Config\WindowsServer\PalWorldSettings.ini
```

Se lavori su una copia (ad esempio sul Desktop), dopo aver salvato copiala lì oppure incollane il contenuto nel pannello del tuo hosting. Conviene modificare il file a server spento e riavviarlo dopo il salvataggio.

## Privacy e sicurezza

Il tuo `PalWorldSettings.ini` contiene `AdminPassword` e `ServerPassword`: non caricarlo nel repository (il `.gitignore` lo esclude).
L'editor lavora solo in locale e non si collega a Internet; ricorda soltanto il percorso dell'ultimo file aperto, in `%APPDATA%\PalworldServerEditor`.

## Compilare

Serve .NET SDK 8 o superiore (funziona anche da Linux/macOS):

```
dotnet build src/PalworldServerEditor -c Release
```

L'exe si trova in `src/PalworldServerEditor/bin/Release/net48/`.
GitHub Actions compila l'exe a ogni push; un tag `v*` (ad esempio `v1.0.0`) pubblica una Release con l'exe allegato.
