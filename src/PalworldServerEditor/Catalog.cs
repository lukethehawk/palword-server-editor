using System;
using System.Collections.Generic;
using System.Linq;

namespace PalworldServerEditor
{
    enum Kind { Float, Int, Bool, Choice, Text, Secret, Platforms, Raw }

    sealed class ChoiceItem
    {
        public ChoiceItem(string value, string label) { Value = value; Label = label; }
        public string Value { get; }
        public string Label { get; }
        public override string ToString() => Label;
    }

    sealed class SectionDef
    {
        public SectionDef(string id, string title, string subtitle) { Id = id; Title = title; Subtitle = subtitle; }
        public string Id { get; }
        public string Title { get; }
        public string Subtitle { get; }
    }

    sealed class SettingDef
    {
        public string Section, Key, Label, Help;
        public Kind Kind;
        public decimal Min, Max = 1000000m, Step = 1m;
        public int Decimals;
        public string Default;          // value as written in the ini; null = unknown
        public ChoiceItem[] Choices;
    }

    /// <summary>The settings shown with a friendly name. Every other key of the file ends up in "Avanzate".</summary>
    static class Catalog
    {
        public const string Advanced = "advanced";

        public static readonly SectionDef[] Sections =
        {
            new SectionDef("server", "Server", "Nome, password, giocatori e rete"),
            new SectionDef("world", "Mondo e progressione", "Esperienza, catture, ciclo giorno/notte ed eventi"),
            new SectionDef("items", "Raccolta e oggetti", "Risorse, drop, peso e usura"),
            new SectionDef("combat", "Combattimento e morte", "Danni, PvP e penalità"),
            new SectionDef("survival", "Sopravvivenza", "Fame, stamina e rigenerazione"),
            new SectionDef("base", "Basi e costruzioni", "Basi, Pal al lavoro e strutture"),
            new SectionDef("players", "Giocatori e gilde", "Gilde, viaggio rapido, Global Palbox e statistiche"),
            new SectionDef(Advanced, "Avanzate", "Parametri tecnici e tutte le altre chiavi del file"),
        };

        public static readonly List<SettingDef> Settings = new List<SettingDef>
        {
            // Server
            Text("server", "ServerName", "Nome del server", "Il nome mostrato nella lista dei server."),
            Text("server", "ServerDescription", "Descrizione", "Testo mostrato sotto il nome del server."),
            Secret("server", "ServerPassword", "Password del server", "Chiesta ai giocatori per entrare. Vuota = server aperto."),
            Secret("server", "AdminPassword", "Password admin", "Serve per i comandi admin in gioco, RCON e REST API."),
            Int("server", "ServerPlayerMaxNum", "Giocatori massimi", "Massimo supportato ufficialmente: 32.", 1, 128, "32"),
            Platforms("server", "CrossplayPlatforms", "Piattaforme ammesse", "Da quali piattaforme ci si può collegare (crossplay)."),
            Int("server", "PublicPort", "Porta pubblica", "Porta UDP del server.", 1, 65535, "8211"),
            Text("server", "PublicIP", "IP pubblico", "Lascia vuoto per il rilevamento automatico."),
            Bool("server", "bShowPlayerList", "Lista giocatori nel menu", "Mostra chi è online nel menu ESC.", "False"),
            Bool("server", "bIsShowJoinLeftMessage", "Messaggi di entrata/uscita", "Avvisa in chat quando un giocatore entra o esce.", "True"),
            Int("server", "ChatPostLimitPerMinute", "Limite messaggi in chat", "Messaggi al minuto per giocatore.", 0, 1000, null),
            Num("server", "AutoSaveSpan", "Salvataggio automatico (secondi)", "Ogni quanti secondi il mondo viene salvato.", 1, 3600, 5, 0, "30"),
            Bool("server", "bIsUseBackupSaveData", "Backup automatici del mondo", "Il server conserva copie di backup del salvataggio.", "True"),
            Bool("server", "bAllowClientMod", "Consenti mod lato client", null, null),
            Bool("server", "RCONEnabled", "RCON attivo", "Amministrazione remota via RCON.", "False"),
            Int("server", "RCONPort", "Porta RCON", null, 1, 65535, "25575"),
            Bool("server", "RESTAPIEnabled", "REST API attiva", "Da esporre solo su reti fidate.", "False"),
            Int("server", "RESTAPIPort", "Porta REST API", null, 1, 65535, "8212"),

            // World
            Rate("world", "ExpRate", "Esperienza", "Moltiplicatore dei punti esperienza.", 0.1m, 20m),
            Rate("world", "PalCaptureRate", "Probabilità di cattura", "Più alto = catture più facili.", 0.1m, 10m),
            Rate("world", "PalSpawnNumRate", "Quantità di Pal selvatici", "Valori alti pesano sulle prestazioni del server.", 0.1m, 5m),
            Rate("world", "DayTimeSpeedRate", "Velocità del giorno", "Più alto = il giorno passa più in fretta.", 0.1m, 10m),
            Rate("world", "NightTimeSpeedRate", "Velocità della notte", "Più alto = la notte passa più in fretta.", 0.1m, 10m),
            Num("world", "PalEggDefaultHatchingTime", "Tempo di schiusa uova (ore)", "Riferito alle uova enormi; 0 = schiusa immediata.", 0, 240, 1, 1, "72"),
            Bool("world", "bEnableInvaderEnemy", "Raid alle basi", "Gruppi di nemici attaccano le basi.", "True"),
            Bool("world", "EnablePredatorBossPal", "Pal predatori", "Abilita i Pal predatori (boss vaganti).", "True"),
            Int("world", "SupplyDropSpan", "Intervallo rifornimenti (minuti)", "Ogni quanto cadono i rifornimenti dal cielo.", 1, 10000, "180"),
            Rate("world", "FishingDifficultyRate", "Difficoltà della pesca", null, 0.1m, 10m),
            Choice("world", "RandomizerType", "Randomizer dei Pal", "Mescola le specie dei Pal selvatici.", "None",
                new ChoiceItem("None", "Disattivato"),
                new ChoiceItem("Region", "Per regione (Region)"),
                new ChoiceItem("All", "Ovunque (All)")),
            Text("world", "RandomizerSeed", "Seed del randomizer", "Vuoto = casuale."),
            Bool("world", "bIsRandomizerPalLevelRandom", "Livelli casuali (randomizer)", "Anche i livelli dei Pal diventano casuali.", "False"),

            // Items
            Rate("items", "CollectionDropRate", "Risorse raccolte", "Quantità ottenuta da alberi, rocce e minerali.", 0.1m, 20m),
            Rate("items", "CollectionObjectHpRate", "Resistenza di alberi e rocce", "Più alto = servono più colpi.", 0.1m, 20m),
            Rate("items", "CollectionObjectRespawnSpeedRate", "Tempo di ricrescita risorse", "Più basso = alberi e rocce ricompaiono prima.", 0.1m, 20m),
            Rate("items", "EnemyDropItemRate", "Oggetti lasciati dai nemici", null, 0.1m, 20m),
            Rate("items", "ItemWeightRate", "Peso degli oggetti", "Più basso = puoi trasportare di più.", 0m, 10m, 0.05m),
            Rate("items", "EquipmentDurabilityDamageRate", "Usura dell'equipaggiamento", "Più basso = armi e armature durano di più.", 0m, 10m),
            Rate("items", "ItemCorruptionMultiplier", "Deperimento del cibo", "Più basso = il cibo marcisce più lentamente.", 0m, 10m),
            Int("items", "DropItemMaxNum", "Oggetti a terra (massimo)", "Oggetti lasciati a terra contemporaneamente nel mondo.", 0, 100000, "3000"),
            Num("items", "DropItemAliveMaxHours", "Durata oggetti a terra (ore)", "Dopo quante ore spariscono.", 0, 1000, 0.5m, 1, "1"),

            // Combat
            Rate("combat", "PalDamageRateAttack", "Danno inflitto dai Pal", null, 0.1m, 10m),
            Rate("combat", "PalDamageRateDefense", "Danno subito dai Pal", null, 0.1m, 10m),
            Rate("combat", "PlayerDamageRateAttack", "Danno inflitto dai giocatori", null, 0.1m, 10m),
            Rate("combat", "PlayerDamageRateDefense", "Danno subito dai giocatori", null, 0.1m, 10m),
            Choice("combat", "DeathPenalty", "Penalità alla morte", "Cosa si lascia a terra quando si muore.", "All",
                new ChoiceItem("None", "Nessuna"),
                new ChoiceItem("Item", "Solo oggetti"),
                new ChoiceItem("ItemAndEquipment", "Oggetti ed equipaggiamento"),
                new ChoiceItem("All", "Tutto, anche i Pal in squadra")),
            Bool("combat", "bIsPvP", "Modalità PvP", null, "False"),
            Bool("combat", "bEnablePlayerToPlayerDamage", "Danni tra giocatori", null, "False"),
            Bool("combat", "bEnableFriendlyFire", "Fuoco amico", null, "False"),
            Bool("combat", "bCanPickupOtherGuildDeathPenaltyDrop", "Raccogli i drop di altre gilde", "Prendere gli oggetti persi alla morte da giocatori di altre gilde.", "False"),
            Bool("combat", "bHardcore", "Modalità hardcore", "Alla morte il personaggio non può rinascere.", "False"),
            Bool("combat", "bCharacterRecreateInHardcore", "Hardcore: ricrea personaggio", "Dopo la morte in hardcore si può creare un nuovo personaggio.", "False"),
            Bool("combat", "bPalLost", "Perdita permanente dei Pal", "I Pal che muoiono vengono persi per sempre.", "False"),
            Num("combat", "BlockRespawnTime", "Attesa per il respawn (secondi)", null, 0, 3600, 1, 1, null),
            Bool("combat", "bEnableAimAssistPad", "Mira assistita (controller)", null, "True"),
            Bool("combat", "bEnableAimAssistKeyboard", "Mira assistita (mouse e tastiera)", null, "False"),

            // Survival
            Rate("survival", "PlayerStomachDecreaceRate", "Fame dei giocatori", "Velocità con cui cala la sazietà.", 0.1m, 10m),
            Rate("survival", "PlayerStaminaDecreaceRate", "Consumo di stamina dei giocatori", null, 0.1m, 10m),
            Rate("survival", "PlayerAutoHPRegeneRate", "Rigenerazione HP dei giocatori", null, 0.1m, 10m),
            Rate("survival", "PlayerAutoHpRegeneRateInSleep", "Rigenerazione HP dei giocatori nel sonno", null, 0.1m, 10m),
            Rate("survival", "PalStomachDecreaceRate", "Fame dei Pal", null, 0.1m, 10m),
            Rate("survival", "PalStaminaDecreaceRate", "Consumo di stamina dei Pal", null, 0.1m, 10m),
            Rate("survival", "PalAutoHPRegeneRate", "Rigenerazione HP dei Pal", null, 0.1m, 10m),
            Rate("survival", "PalAutoHpRegeneRateInSleep", "Rigenerazione HP dei Pal nel Palbox", null, 0.1m, 10m),

            // Base
            Rate("base", "WorkSpeedRate", "Velocità di lavoro dei Pal", null, 0.1m, 20m),
            Int("base", "BaseCampWorkerMaxNum", "Pal al lavoro per base", "Massimo 50.", 1, 50, "15"),
            Int("base", "BaseCampMaxNumInGuild", "Basi per gilda", "Massimo 10.", 1, 10, "4"),
            Int("base", "BaseCampMaxNum", "Basi totali sul server", null, 1, 10000, "128"),
            Rate("base", "MonsterFarmActionSpeedRate", "Produzione del ranch", "Velocità con cui i Pal del ranch producono oggetti.", 0.1m, 20m),
            Rate("base", "BuildObjectHpRate", "Resistenza delle strutture", null, 0.1m, 20m),
            Rate("base", "BuildObjectDamageRate", "Danno subito dalle strutture", null, 0m, 10m),
            Rate("base", "BuildObjectDeteriorationDamageRate", "Deterioramento delle strutture", "0 = le strutture non si rovinano col tempo.", 0m, 10m),
            Int("base", "MaxBuildingLimitNum", "Limite di strutture", "0 = nessun limite.", 0, 100000, "0"),
            Int("base", "MaxBuildingLimitNumPerPlayer", "Limite di strutture per giocatore", "0 = nessun limite.", 0, 100000, "0"),
            Bool("base", "bBuildAreaLimit", "Vieta di costruire vicino ai punti chiave", "Ad esempio vicino ai punti di viaggio rapido.", "False"),
            Bool("base", "bEnableDefenseOtherGuildPlayer", "Difese contro altre gilde", "Le difese della base attaccano i giocatori di altre gilde.", "False"),
            Bool("base", "bInvisibleOtherGuildBaseCampAreaFX", "Nascondi l'area delle basi altrui", null, "False"),
            Bool("base", "bAllowEnemyCampSpawnNearBaseCamp", "Accampamenti nemici vicino alle basi", null, "False"),

            // Players & guilds
            Int("players", "GuildPlayerMaxNum", "Giocatori per gilda", null, 1, 100, "20"),
            Bool("players", "bAutoResetGuildNoOnlinePlayers", "Reset delle gilde inattive", "Elimina le gilde senza giocatori online per molto tempo.", "False"),
            Num("players", "AutoResetGuildTimeNoOnlinePlayers", "Ore prima del reset gilde", null, 1, 100000, 1, 0, "72"),
            Int("players", "GuildRejoinCooldownMinutes", "Attesa per rientrare in una gilda (minuti)", null, 0, 100000, "0"),
            Int("players", "AutoTransferMasterThresholdDays", "Cambio capo gilda inattivo (giorni)", "Dopo questi giorni di assenza il ruolo passa a un altro membro.", 1, 10000, null),
            Bool("players", "bEnableFastTravel", "Viaggio rapido", null, "True"),
            Bool("players", "bEnableFastTravelOnlyBaseCamp", "Viaggio rapido solo verso le basi", null, "False"),
            Bool("players", "bIsStartLocationSelectByMap", "Scegli il punto di partenza sulla mappa", null, null),
            Bool("players", "bExistPlayerAfterLogout", "Il personaggio resta nel mondo dopo il logout", null, "False"),
            Bool("players", "bEnableNonLoginPenalty", "Penalità per lunga assenza", "Penalità per chi non si collega da molto tempo.", "True"),
            Bool("players", "bAllowGlobalPalboxExport", "Esporta nel Global Palbox", null, "True"),
            Bool("players", "bAllowGlobalPalboxImport", "Importa dal Global Palbox", null, "False"),
            Bool("players", "bEnableVoiceChat", "Chat vocale", null, "False"),
            Bool("players", "bAllowEnhanceStat_Health", "Potenziamento: Salute", "Permette di aumentare questa statistica.", "True"),
            Bool("players", "bAllowEnhanceStat_Attack", "Potenziamento: Attacco", "Permette di aumentare questa statistica.", "True"),
            Bool("players", "bAllowEnhanceStat_Stamina", "Potenziamento: Stamina", "Permette di aumentare questa statistica.", "True"),
            Bool("players", "bAllowEnhanceStat_Weight", "Potenziamento: Peso trasportabile", "Permette di aumentare questa statistica.", "True"),
            Bool("players", "bAllowEnhanceStat_WorkSpeed", "Potenziamento: Velocità di lavoro", "Permette di aumentare questa statistica.", "True"),

            // Advanced (named); the remaining keys are added automatically
            Int(Advanced, "CoopPlayerMaxNum", "Giocatori in co-op", "Usato solo nelle partite co-op, non dal server dedicato.", 1, 32, "4"),
            Text(Advanced, "Region", "Regione", null),
            Bool(Advanced, "bUseAuth", "Autenticazione dei giocatori", null, "True"),
            Text(Advanced, "BanListURL", "URL della ban list", null),
            Choice(Advanced, "LogFormatType", "Formato dei log", null, "Text",
                new ChoiceItem("Text", "Testo (Text)"),
                new ChoiceItem("Json", "JSON (Json)")),
            Int(Advanced, "ServerReplicatePawnCullDistance", "Distanza di sincronizzazione dei Pal (cm)", "Tra 5000 e 15000; più basso alleggerisce il server.", 5000, 15000, "15000"),
        };

        static readonly HashSet<string> DefinedKeys =
            new HashSet<string>(Settings.Select(s => s.Key), StringComparer.OrdinalIgnoreCase);

        public static bool IsDefined(string key) => DefinedKeys.Contains(key);

        /// <summary>Editor for a key this tool has no description for, guessed from its current value.</summary>
        public static SettingDef Infer(string key, string raw)
        {
            var def = new SettingDef { Section = Advanced, Key = key, Label = key };
            string t = raw.Trim();
            if (IniValue.IsBool(t))
                def.Kind = Kind.Bool;
            else if (IniValue.IsQuoted(t))
                def.Kind = Kind.Text;
            else if (IniValue.IsInt(t) && IniValue.TryParseNumber(t, out decimal i) && Math.Abs(i) < 1000000000m)
            {
                def.Kind = Kind.Int;
                def.Min = -1000000000m;
                def.Max = 1000000000m;
            }
            else if (IniValue.IsFloat(t) && IniValue.TryParseNumber(t, out decimal f) && Math.Abs(f) < 1000000000m)
            {
                def.Kind = Kind.Float;
                def.Min = -1000000000m;
                def.Max = 1000000000m;
                def.Decimals = Math.Max(2, Math.Min(6, IniValue.SignificantDecimals(f)));
                def.Step = Math.Abs(f) >= 100 ? 1m : 0.1m;
            }
            else
                def.Kind = Kind.Raw;
            return def;
        }

        static SettingDef Rate(string section, string key, string label, string help, decimal min, decimal max, decimal step = 0.1m) =>
            new SettingDef { Section = section, Key = key, Label = label, Help = help, Kind = Kind.Float, Min = min, Max = max, Step = step, Decimals = 2, Default = "1" };

        static SettingDef Num(string section, string key, string label, string help, decimal min, decimal max, decimal step, int decimals, string def) =>
            new SettingDef { Section = section, Key = key, Label = label, Help = help, Kind = Kind.Float, Min = min, Max = max, Step = step, Decimals = decimals, Default = def };

        static SettingDef Int(string section, string key, string label, string help, decimal min, decimal max, string def) =>
            new SettingDef { Section = section, Key = key, Label = label, Help = help, Kind = Kind.Int, Min = min, Max = max, Default = def };

        static SettingDef Bool(string section, string key, string label, string help, string def) =>
            new SettingDef { Section = section, Key = key, Label = label, Help = help, Kind = Kind.Bool, Default = def };

        static SettingDef Choice(string section, string key, string label, string help, string def, params ChoiceItem[] choices) =>
            new SettingDef { Section = section, Key = key, Label = label, Help = help, Kind = Kind.Choice, Default = def, Choices = choices };

        static SettingDef Text(string section, string key, string label, string help) =>
            new SettingDef { Section = section, Key = key, Label = label, Help = help, Kind = Kind.Text };

        static SettingDef Secret(string section, string key, string label, string help) =>
            new SettingDef { Section = section, Key = key, Label = label, Help = help, Kind = Kind.Secret };

        static SettingDef Platforms(string section, string key, string label, string help) =>
            new SettingDef { Section = section, Key = key, Label = label, Help = help, Kind = Kind.Platforms };
    }
}
