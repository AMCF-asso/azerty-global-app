// Liste hardcodée de jeux et frameworks gaming pour la couche compatibilité.
//
// Sources : rapports Gemini et Perplexity du 2026-04-26 (recherches Internet
// approfondies sur les anti-cheats kernel-level avec bans documentés pour
// outils de remapping clavier — AHK, reWASD, PowerToys Keyboard Manager).
//
// Critère d'inclusion anti-cheat : driver Windows ring 0 qui scanne la mémoire
// des processes et peut détecter les hooks ou inputs synthétiques. Les anti-cheats
// user-mode permissifs (VAC standard sans extension) sont écartés.
//
// Cette liste vit dans le code source — sa mise à jour nécessite une nouvelle
// release. À l'horizon v1.0+, envisager de la charger depuis un fichier embarqué
// ou téléchargé périodiquement (cf. plan v0.9.7 § Évolutions futures).
//
// Ajouts 1.3.0 (2026-10-23) : fiches Steam « Uses Kernel Level Anti-Cheat » (déclaration de
// l'éditeur), pages éditeurs datées, PCGamingWiki, AreWeAntiCheatYet ; noms d'exe relevés
// dans la liste des exécutables détectables de Discord, la configuration de lancement Steam
// et des journaux de plantage. Tableau jeu par jeu, avec sources et exclusions motivées :
// operations/2026-10-23-anti-triche-131/tableau.md du dossier projet (hors dépôt).

namespace TypingEngine.Windows;

public static class GameRegistry
{
    /// <summary>
    /// Clients de connexion à distance pour lesquels le remapping local doit être suspendu.
    /// Le poste distant applique son propre remapping : laisser les deux instances actives
    /// transformerait deux fois les frappes qui ne passent pas nativement.
    /// </summary>
    public static readonly string[] RemoteAccessProcesses =
    {
        "parsecd.exe",
        "mstsc.exe",
        "msrdc.exe",
        "msrdcw.exe",
        "AnyDesk.exe",
        "TeamViewer.exe",
        "rustdesk.exe",

        // Consoles de machines virtuelles et clients de bureau virtuel (ajout du 2026-10-12,
        // report de l'audit du 2026-09-24, docs/audit-2026-09-24-v1.3.0-bugs/rapport.md:34).
        // Elles transmettent des scan codes à l'invité : un caractère injecté en Unicode y
        // devient la touche du scan code de même valeur (« . » U+002E reçu comme C, SC 0x2E,
        // mesuré dans Windows Sandbox). L'invité garde sa propre disposition.
        "vmconnect.exe",           // Hyper-V, connexion de base et session améliorée
        "WindowsSandbox.exe",      // Windows Sandbox, lanceur
        "WindowsSandboxClient.exe", // Windows Sandbox, fenêtre de la session
        "WindowsSandboxRemoteSession.exe", // Windows Sandbox livrée par le Store (Windows 11 24H2+)
        "vmware.exe",              // VMware Workstation
        "vmplayer.exe",            // VMware Workstation Player
        "VirtualBoxVM.exe",        // fenêtre d'une VM VirtualBox (6.0 et suivantes)
        "wfica32.exe",             // Citrix Workspace, session ICA
        "CDViewer.exe",            // Citrix Desktop Viewer
        "vmware-remotemks.exe",    // VMware Horizon, affichage de la session
    };

    /// <summary>
    /// Un processus qui signale, côté HÔTE, une session de prise en main à distance ouverte :
    /// il injecte les frappes reçues du poste client. Distinct de
    /// <see cref="RemoteAccessProcesses"/>, qui sert côté CLIENT à suspendre l'application
    /// quand le logiciel est au premier plan.
    /// </summary>
    public readonly record struct RemoteSessionMarker(string Process, string Tool);

    /// <summary>
    /// Instantané d'un processus : nom (avec ou sans « .exe ») et session Windows.
    /// </summary>
    public readonly record struct ProcessEntry(string? Name, int SessionId);

    /// <summary>
    /// Processus qui ne tournent que pendant une session distante, dans la session Windows
    /// pilotée. Leur présence dans la session de l'application fait traiter une injection
    /// étrangère comme physique (KeyboardHook.ShouldTreatAsPhysical).
    ///
    /// Historique : décision C5 d'Antoine du 2026-09-24, fondée sur une mesure faite sur un
    /// hôte Parsec (149 événements, tous LLKHF_INJECTED, dwExtraInfo à 0, scan codes
    /// physiques réels). AG130-07 écartait toute frappe distante : plus aucun remappage, et
    /// les exercices ne recevaient rien. C5 remappait dès qu'un hôte TOURNAIT ; or les
    /// services TeamViewer, AnyDesk, Parsec et Chrome Remote Desktop tournent en permanence
    /// dès l'installation, et l'auto-saisie de KeePass ou Bitwarden arrivait remappée, donc
    /// fausse (audit du 30/10, M1). Décision d'Antoine du 2026-10-30 : remapper seulement
    /// pendant une session active. Un service seul (TeamViewer_Service, pservice, AnyDesk ou
    /// RustDesk en service, remoting_host) ne compte plus.
    ///
    /// Marqueurs, sources consultées le 2026-10-30 :
    /// <list type="bullet">
    /// <item>remoting_desktop.exe — code Chromium : lancé dans la session attachée à chaque
    /// connexion (remoting/host/desktop_session_win.cc), « recreated per connection »
    /// (daemon_process.cc).</item>
    /// <item>TeamViewer_Desktop.exe — lancé par TeamViewer_Service.exe au début d'une session
    /// entrante (règle SigmaHQ proc_creation_win_remote_access_tools_teamviewer_incoming_connection).
    /// Source tierce unique : sa fin avec la session n'est pas prouvée.</item>
    /// <item>parsecd.exe — ⚠️ pas un signal de session : aucun n'est connu pour Parsec. Gardé
    /// pour le cas mesuré du 24/09 (frappe via Parsec remappée, exercices servis), mais
    /// seulement dans la session de l'utilisateur ; pservice.exe seul ne compte plus.</item>
    /// </list>
    ///
    /// Limites connues :
    /// <list type="bullet">
    /// <item>tant que parsecd.exe tourne dans la session, l'auto-saisie, AutoHotkey et le
    /// clavier visuel sont remappés eux aussi — aucun drapeau Win32 ne les distingue
    /// (extra 0 dans les deux cas) ;</item>
    /// <item>AnyDesk et RustDesk n'ont aucun processus propre à la session (RustDesk lance
    /// « rustdesk.exe --cm », même nom que son service) : leur frappe distante n'est plus
    /// remappée ;</item>
    /// <item>Chrome Remote Desktop peut se replier sur « remoting_host.exe --type=desktop »
    /// quand remoting_desktop.exe manque : session alors non vue ;</item>
    /// <item>la sonde passe toutes les 5 s : les premières frappes d'une session peuvent
    /// arriver avant qu'elle soit vue.</item>
    /// </list>
    ///
    /// ⛔ Pas de mstsc / msrdc : ce sont des clients RDP, et l'entrée RDP côté hôte n'arrive
    /// pas injectée.
    /// </summary>
    public static readonly RemoteSessionMarker[] RemoteSessionMarkers =
    {
        new("remoting_desktop.exe", "Chrome Remote Desktop"),
        new("TeamViewer_Desktop.exe", "TeamViewer"),
        new("parsecd.exe", "Parsec"),
    };

    /// <summary>
    /// Termes-clés (sous-chaîne, case-insensitive) qui identifient un process protégé
    /// par un anti-cheat kernel-level avec risque de ban pour injection de frappes.
    /// AZERTY Global se désactive complètement quand ces processes sont au foreground.
    /// </summary>
    public static readonly string[] AntiCheatTerms =
    {
        "valorant",         // Riot Vanguard (kernel ring 0, boot-start)
        "league of legends",// Riot Vanguard (depuis mai 2024)
        "fortnite",         // Easy Anti-Cheat (kernel)
        "r5apex",           // Apex Legends — EAC
        "blackops",         // Call of Duty: Black Ops 6 — RICOCHET (kernel)
        "modernwarfare",    // CoD: Modern Warfare — RICOCHET
        "tslgame",          // PUBG — BattlEye (kernel)
        "rainbowsix",       // R6 Siege — BattlEye
        "tarkov",           // Escape from Tarkov — BattlEye
        "genshin",          // Genshin Impact — mhyprot2 (kernel)
        "starrail",         // Honkai: Star Rail — mhyprot3
        "roblox",           // Roblox — Hyperion / Byfron (anti-tamper)
        "faceit",           // FACEIT AC (kernel) — bloque PowerToys Keyboard Manager
        "bf2042",           // Battlefield 2042 — EAC
        "deltaforce",       // Delta Force — Tencent ACE (kernel)
        "marvel-win64",     // Marvel Rivals — NetEase AC
        "helldivers2",      // Helldivers 2 — nProtect GameGuard
        "cs2",              // Counter-Strike 2 — VAC + VACnet (Valve laisse passer la plupart des
                            // outils de remapping, mais inclus par précaution : jeu compétitif e-sport)

        // Ajouts 1.3.0 (2026-10-23) — anti-triche noyau déclaré par l'éditeur
        "gta5",             // GTA V Legacy et Enhanced (GTA5.exe, GTA5_Enhanced.exe) — BattlEye
                            // depuis le 17/09/2024. Limite connue : FiveM Enhanced lance le même
                            // GTA5_Enhanced.exe, sans BattlEye, et l'app s'y suspend aussi.
        "pioneergame",      // ARC Raiders — Denuvo Anti-Cheat depuis le patch 1.29.0 (19/05/2026)
        "dayz",             // DayZ — BattlEye
        "arma3",            // Arma 3 — BattlEye
        "armareforger",     // Arma Reforger — BattlEye
        "dunesandbox",      // Dune: Awakening — BattlEye
        "nightreign",       // Elden Ring Nightreign — EAC
        "brawlhalla",       // Brawlhalla — EAC
        "lostark",          // Lost Ark — EAC
        "newworld",         // New World: Aeternum — EAC
        "maplestory",       // MapleStory — Nexon Game Security
        "bpsr",             // Blue Protocol: Star Resonance (BPSR_STEAM.exe) — ACE
        "blue protocol star resonance",
        "zenlesszonezero",  // Zenless Zone Zero — HoYoKProtect
        "x6game",           // Infinity Nikki (X6Game-Win64-Shipping.exe) — ACE
    };

    /// <summary>
    /// Match exact + chemin contenant un fragment (pour les noms ambigus).
    /// `cod.exe` = Call of Duty officiel ; `ace.exe` est ambigu donc on exige
    /// que le chemin contienne `\Tencent\` (ACE = Tencent Anti-Cheat Expert).
    /// `Discovery-d.exe` / `Discovery-e.exe` = The Finals (Embark Studios) post-patch 8.0.0.
    /// `Discovery.exe` = The Finals avant patch 8.0.0 OU n'importe quel autre exe ; on exige
    /// donc le path contient `\Embark\` ou `\TheFinals\`.
    /// Ajouts 1.3.0 : noms courts (`bf1.exe`, `aces.exe`, `TL.exe`…) en match exact, pour qu'une
    /// sous-chaîne ne prenne pas un exe sans rapport ; `Client-Win64-Shipping.exe`, nom générique
    /// d'Unreal Engine, n'est retenu que dans le dossier de Wuthering Waves.
    /// </summary>
    public static readonly (string Exact, string? PathContains)[] AntiCheatExactWithPath =
    {
        ("cod.exe", null),
        ("ace.exe", @"\Tencent\"),
        ("Discovery-d.exe", null),
        ("Discovery-e.exe", null),
        ("Discovery.exe", @"\Embark\"),

        // Ajouts 1.3.0 (2026-10-23) — anti-triche noyau déclaré par l'éditeur
        ("bf6.exe", null),          // Battlefield 6 — EA Javelin (noyau, Secure Boot exigé)
        ("bfv.exe", null),          // Battlefield V — EA Javelin depuis avril 2024
        ("bf1.exe", null),          // Battlefield 1 — EA Javelin depuis octobre 2024
        ("aces.exe", null),         // War Thunder — BattlEye depuis décembre 2024
        ("BH3.exe", null),          // Honkai Impact 3rd — HoYoKProtect
        ("UAGame.exe", null),       // Arena Breakout: Infinite — ACE
        ("Hemingway-Win64-Shipping.exe", null), // SMITE 2 — EAC
        ("TL.exe", @"\TL\Binaries\Win64\"),     // Throne and Liberty — EAC
        ("Client-Win64-Shipping.exe", @"\Wuthering Waves Game\"), // Wuthering Waves — ACE
    };

    /// <summary>
    /// Termes niveau 2 — désactivation par précaution (anti-cheat kernel mais
    /// politique moins stricte vs remappers, ou ban risqué mais non documenté).
    /// </summary>
    public static readonly string[] PrecautionAntiCheatTerms =
    {
        "destiny2",          // BattlEye
        "rustclient",        // EAC
        "narakabladepoint",  // Tencent ACE
        "fc25",              // EA Sports FC 25 — EA AC
        "fc26",              // EA Sports FC 26 — EA AC
        "haloinfinite",      // EAC
        "fallguys_client",   // EAC

        // Ajouts 1.3.0 (2026-10-23) — anti-triche nommé, niveau noyau non sourcé
        "eldenring",         // Elden Ring — EAC
        "armoredcore6",      // Armored Core VI — EAC
        "blackdesert",       // Black Desert — XIGNCODE3
        "predecessorclient", // Predecessor — EAC
        "paladins",          // Paladins — EAC
        "deadbydaylight",    // Dead by Daylight — EAC
        "huntgame",          // Hunt: Showdown 1896 — EAC + Anybrain (analyse clavier et souris)
        "squadgame",         // Squad — EAC
        "hll-win64-shipping",// Hell Let Loose — EAC
        "hellletloose",      // Hell Let Loose — EAC
        "insurgencyclient",  // Insurgency: Sandstorm — EAC
        "sotgame",           // Sea of Thieves — EAC
        "rocketleague",      // Rocket League — EAC depuis le 28/04/2026
        "gzwclient",         // Gray Zone Warfare — EAC + Anybrain
        "mcc-win64-shipping",// Halo: The Master Chief Collection — EAC
        "thedivision2",      // The Division 2 — EAC
        "forhonor",          // For Honor — EAC
        "mechabreak",        // Mecha BREAK — ACE
        "once_human",        // Once Human — NetEase Anti-Cheat
        "hottagame",         // Tower of Fantasy — KSophon
        "arkascended",       // ARK: Survival Ascended — BattlEye
        "sparkingzero",      // Dragon Ball: Sparking! ZERO — EAC
    };

    /// <summary>
    /// Signatures de DLL chargées qui indiquent un framework filtrant VK_PACKET
    /// et nécessitant une combo native pour les caractères AZ Global hors layout natif.
    /// Tout match (case-insensitive, sur le nom court de la DLL) déclenche le mode NativeCombo.
    /// </summary>
    public static readonly string[] GameFrameworkDlls =
    {
        // GLFW (Minecraft Java natif, beaucoup d'indés OpenGL/Vulkan)
        "glfw3.dll",
        "glfw.dll",
        // LWJGL — Minecraft Java + tous les mods (JEI inclus)
        "lwjgl.dll",
        "lwjgl_glfw.dll",
        "lwjgl_opengl.dll",
        // SDL — gros volume d'indés et certains AAA
        "sdl.dll",
        "sdl2.dll",
        "sdl3.dll",
        // DirectInput legacy
        "dinput.dll",
        "dinput8.dll",
        // Unity Engine (tous les jeux Unity)
        "unityplayer.dll",
        // Allegro
        "allegro-5.2.dll",
    };

    /// <summary>
    /// Indique si le process foreground est protégé par un anti-cheat kernel-level
    /// connu, et que l'app doit être désactivée pour éviter un ban utilisateur.
    /// </summary>
    /// <param name="processName">Nom court (ex: "valorant.exe").</param>
    /// <param name="fullPath">Chemin complet (peut être null si non disponible).</param>
    public static bool IsAntiCheatProcess(string? processName, string? fullPath)
    {
        if (string.IsNullOrEmpty(processName)) return false;

        // Niveau 1 — sous-chaîne case-insensitive
        foreach (var term in AntiCheatTerms)
            if (processName.Contains(term, StringComparison.OrdinalIgnoreCase))
                return true;

        // Niveau 1 — match exact + check chemin (ace.exe / cod.exe)
        foreach (var (exact, pathContains) in AntiCheatExactWithPath)
        {
            if (!string.Equals(processName, exact, StringComparison.OrdinalIgnoreCase))
                continue;
            if (pathContains == null)
                return true;
            if (fullPath != null && fullPath.Contains(pathContains, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Niveau 2 — sous-chaîne case-insensitive (précaution)
        foreach (var term in PrecautionAntiCheatTerms)
            if (processName.Contains(term, StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }

    public static bool IsRemoteAccessProcess(string? processName)
    {
        if (string.IsNullOrEmpty(processName)) return false;
        return RemoteAccessProcesses.Any(candidate =>
            string.Equals(processName, candidate, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Décision pure : une session distante est-elle ouverte dans la session Windows
    /// <paramref name="currentSessionId"/> ? Rend le nom de l'outil (pour le journal), sinon
    /// null. Un marqueur d'une autre session (autre utilisateur, session 0) ne compte pas :
    /// ses injections n'atteignent pas notre bureau. Noms avec ou sans « .exe »
    /// (Process.ProcessName n'en a pas), sans tenir compte de la casse.
    /// </summary>
    public static string? FindActiveRemoteSession(IEnumerable<ProcessEntry>? processes, int currentSessionId)
    {
        if (processes == null) return null;
        foreach (var process in processes)
        {
            string? name = process.Name;
            if (string.IsNullOrEmpty(name) || process.SessionId != currentSessionId) continue;
            string withExe = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name : name + ".exe";
            foreach (var marker in RemoteSessionMarkers)
                if (string.Equals(withExe, marker.Process, StringComparison.OrdinalIgnoreCase))
                    return marker.Tool;
        }
        return null;
    }

    /// <summary>
    /// Énumère les processus de la machine et applique <see cref="FindActiveRemoteSession"/>
    /// avec la session de l'application. Une seule énumération (Process.GetProcesses lit
    /// l'instantané système, nom et session compris, sans ouvrir les processus). À appeler
    /// hors du rappel du hook ; rend null si l'énumération échoue.
    /// </summary>
    public static string? FindActiveRemoteSession()
    {
        int currentSessionId;
        System.Diagnostics.Process[] processes;
        try
        {
            using (var self = System.Diagnostics.Process.GetCurrentProcess())
                currentSessionId = self.SessionId;
            processes = System.Diagnostics.Process.GetProcesses();
        }
        catch { return null; }
        try
        {
            return FindActiveRemoteSession(processes.Select(Entry), currentSessionId);
        }
        finally
        {
            foreach (var p in processes) p.Dispose();
        }

        static ProcessEntry Entry(System.Diagnostics.Process p)
        {
            try { return new ProcessEntry(p.ProcessName, p.SessionId); }
            catch { return new ProcessEntry(null, -1); } // processus terminé entre l'instantané et la lecture
        }
    }

    /// <summary>
    /// Indique si la liste de modules contient au moins une DLL signature gaming
    /// (GLFW, SDL, Unity, etc.). Un match déclenche le mode NativeCombo.
    /// </summary>
    public static bool HasGameFrameworkLoaded(IEnumerable<string>? moduleNames)
    {
        if (moduleNames == null) return false;
        foreach (var name in moduleNames)
        {
            if (string.IsNullOrEmpty(name)) continue;
            foreach (var dll in GameFrameworkDlls)
                if (string.Equals(name, dll, StringComparison.OrdinalIgnoreCase))
                    return true;
        }
        return false;
    }
}
