# Palworld Server Editor

Editor grafico **portable per Windows**, scritto in PowerShell 5.1 + WinForms. Nessuna installazione o dipendenza esterna.

## Avvio
1. Scarica **Start Editor.cmd** e **PalworldServerEditor.ps1** nella stessa cartella, anche sul Desktop.
2. Fai doppio clic su **Start Editor.cmd**.
3. Seleziona il file reale del server: \`PalServer\Pal\Saved\Config\WindowsServer\PalWorldSettings.ini\`.
4. Arresta il server, modifica i parametri e premi **Salva modifiche e backup**.
5. Riavvia il server per applicare le modifiche.

Puoi anche mettere il tuo INI nella cartella dell'editor per aprirlo automaticamente (sconsigliato se la cartella e sincronizzata o pubblica).

## Funzioni
- Schede per mondo, basi/risorse, combattimento e server.
- Controlli numerici, menu a scelta e interruttori True/False.
- Carica i valori reali, non sovrascrive i parametri sconosciuti.
- Cambia solo i valori editati, con controllo dei conflitti se il file e cambiato da altri programmi.
- Crea un backup \`.bak\` con data e ora prima di scrivere.
- I parametri fuori dall'intervallo della UI sono disabilitati, senza essere corretti automaticamente.

## Sicurezza
**Non aggiungere mai il tuo PalWorldSettings.ini al repository:** puo contenere AdminPassword e ServerPassword. L'editor non modifica ne mostra le password. Le opzioni REST API devono essere esposte solo su rete fidata. Lo script e leggibile e puo essere controllato prima dell'esecuzione.

## Note
L'app e uno script con launcher, **non e un .exe**. Non richiede Python, Node.js o mod del gioco. Le impostazioni supportate dipendono dalla versione di Palworld. Se l'INI riporta la frase "Changes to this file will NOT be reflected on the server", potrebbe essere un esempio anziche il file attivo.
