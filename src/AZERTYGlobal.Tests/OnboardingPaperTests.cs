using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Lot accueil papier de l'audit 1.3.0 : A7 (étapes 2 à 4 dans l'identité papier : polices, cases
/// dessinées, cadres, pastilles), P4 (titres alignés sur celui de l'étape 1), A8 (ordre de
/// tabulation, focus à l'arrivée sur la dernière étape, descriptions pour le lecteur d'écran) et
/// A14 (contraste élevé : couleurs du système).
///
/// Chaque test ouvre la vraie fenêtre sur un fil STA, comme l'application, sous
/// <see cref="BancCapture.Isolation"/> (configuration jetable, fenêtre invisible). Le rendu à
/// l'écran, le Narrateur et un vrai contraste élevé ne se testent pas ici : voir le rapport du lot.
/// </summary>
public class OnboardingPaperTests
{
    private const int IDC_CHK_DONT_SHOW = 2001;
    private const int IDC_BTN_NEXT = 2002;
    private const int IDC_CHK_AUTOSTART = 2006;
    private const int IDC_CHK_TRAINING = 2013;

    private const int Changes = 0, Languages = 1, HowTo = 2, Resources = 3;

    [DllImport("user32.dll")] private static extern IntPtr GetNextDlgTabItem(IntPtr dialog, IntPtr control, bool previous);
    [DllImport("user32.dll")] private static extern int GetDlgCtrlID(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr GetWindowLongPtrW(IntPtr hwnd, int index);
    [DllImport("gdi32.dll")] private static extern uint GetPixel(IntPtr hdc, int x, int y);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] private static extern int GetTextFaceW(IntPtr hdc, int count, StringBuilder face);

    // ── Fenêtre sur un fil STA ────────────────────────────────────────

    private static void Avec(Action<OnboardingWindow, IntPtr> corps)
    {
        Exception? échec = null;
        var fil = new Thread(() =>
        {
            try
            {
                using var isolation = new BancCapture.Isolation();
                var fenêtre = new OnboardingWindow();
                try
                {
                    fenêtre.Show();
                    corps(fenêtre, BancCapture.Handle(fenêtre));
                }
                finally { BancCapture.Teardown(fenêtre); }
            }
            catch (Exception ex) { échec = ex; }
        });
        fil.SetApartmentState(ApartmentState.STA);
        fil.Start();
        fil.Join();
        if (échec != null) ExceptionDispatchInfo.Capture(échec).Throw();
    }

    private static void AllerÀ(OnboardingWindow fenêtre, int étape)
    {
        BancCapture.SetField(fenêtre, "_currentStep", étape);
        BancCapture.Call(fenêtre, "UpdateStepVisibility");
    }

    private static void Repeindre(IntPtr hwnd) =>
        BancCapture.Native.RedrawWindow(hwnd, IntPtr.Zero, IntPtr.Zero,
            BancCapture.Native.RDW_INVALIDATE | BancCapture.Native.RDW_ERASE | BancCapture.Native.RDW_ALLCHILDREN | BancCapture.Native.RDW_UPDATENOW);

    private static IntPtr Contrôle(OnboardingWindow fenêtre, string champ) => BancCapture.Field<IntPtr>(fenêtre, champ);

    private static bool Cochée(OnboardingWindow fenêtre, string champ) => BancCapture.Field<bool>(fenêtre, champ);

    // ── A14 : la palette ──────────────────────────────────────────────

    [Fact]
    public void Palette_sans_contraste_eleve_est_la_palette_papier()
    {
        var palette = OnboardingWindow.PaletteFor(highContrast: false, _ => throw new InvalidOperationException("couleur système lue sans contraste élevé"));

        Assert.False(palette.HighContrast);
        Assert.Equal(PaperTheme.Paper, palette.Paper);
        Assert.Equal(PaperTheme.Surface, palette.Surface);
        Assert.Equal(PaperTheme.Ink, palette.Ink);
        Assert.Equal(PaperTheme.TextSecondary, palette.Muted);
        Assert.Equal(PaperTheme.Border, palette.Border);
        // Un seul bleu : l'action des Leçons sert de lien, de pastille et de case cochée.
        Assert.Equal(PaperTheme.Action, palette.Action);
        Assert.Equal(PaperTheme.Action, palette.Fill);
        Assert.Equal(PaperTheme.OnAction, palette.OnFill);
    }

    [Fact]
    public void Palette_en_contraste_eleve_prend_les_couleurs_du_systeme()
    {
        // Chaque indice GetSysColor rend une couleur propre : on lit lequel est pris pour quel rôle.
        var palette = OnboardingWindow.PaletteFor(highContrast: true, index => 0x00100000u + (uint)index);

        const int WINDOW = 5, WINDOWTEXT = 8, HIGHLIGHT = 13, HIGHLIGHTTEXT = 14, GRAYTEXT = 17, HOTLIGHT = 26;
        Assert.True(palette.HighContrast);
        Assert.Equal(0x00100000u + WINDOW, palette.Paper);
        Assert.Equal(0x00100000u + WINDOW, palette.Surface);
        Assert.Equal(0x00100000u + WINDOWTEXT, palette.Ink);
        Assert.Equal(0x00100000u + WINDOWTEXT, palette.Muted); // pas de gris : le texte secondaire reste lisible
        Assert.Equal(0x00100000u + WINDOWTEXT, palette.Border);
        Assert.Equal(0x00100000u + GRAYTEXT, palette.Faint);
        Assert.Equal(0x00100000u + HOTLIGHT, palette.Action);
        Assert.Equal(0x00100000u + HIGHLIGHT, palette.Fill);
        Assert.Equal(0x00100000u + HIGHLIGHTTEXT, palette.OnFill);
    }

    // ── P4 : les titres des étapes 2 à 4 au même endroit que celui de l'étape 1 ──

    [Fact]
    public void Titres_des_etapes_2_a_4_ont_le_cadre_du_titre_de_l_etape_1()
    {
        Avec((fenêtre, hwnd) =>
        {
            Win32.GetClientRect(hwnd, out var client);
            float échelle = BancCapture.Field<float>(fenêtre, "_dpiScale");
            int y = 64;
            var titre = (Win32.RECT)BancCapture.Call(fenêtre, "StepTitleRect", client.right, y)!;

            // Le cadre de l'étape 1 : bord de 28 px, 30 px de haut (maquette 720 px × échelle).
            Assert.Equal((int)Math.Round(28 * échelle), titre.left);
            Assert.Equal(y, titre.top);
            Assert.Equal(y + (int)Math.Round(30 * échelle), titre.bottom);
            Assert.Equal(client.right - titre.left, titre.right);

            // L'étape « Vos langues » le reprend, son sous-titre dessous, comme à l'étape 1.
            IntPtr hdc = Win32.GetDC(hwnd);
            try
            {
                object langues = BancCapture.Call(fenêtre, "GetLanguagesLayout", hdc, client.right, y)!;
                var titreLangues = (Win32.RECT)langues.GetType().GetField("Title")!.GetValue(langues)!;
                var sousTitre = (Win32.RECT)langues.GetType().GetField("Subtitle")!.GetValue(langues)!;
                Assert.Equal(titre, titreLangues);
                Assert.Equal(titre.left, sousTitre.left);
                Assert.Equal(titre.bottom + (int)Math.Round(4 * échelle), sousTitre.top);
            }
            finally { Win32.ReleaseDC(hwnd, hdc); }

            // L'étape Ressources : son panneau commence sous le titre, pas dessus.
            var args = new object?[18];
            args[0] = y;
            args[1] = client.right;
            BancCapture.Call(fenêtre, "GetStep3Layout", args);
            Assert.True(((Win32.RECT)args[2]!).top >= titre.bottom);
        });
    }

    /// <summary>
    /// Les bornes de l'encre foncée dans la bande du titre, lues sur le rendu de la fenêtre :
    /// (bord gauche, haut, bas), ou null si rien n'est lisible (fenêtre hors écran : le DC rend noir).
    /// </summary>
    private static (int Left, int Top, int Bottom)? EncreDuTitre(OnboardingWindow fenêtre, IntPtr hwnd)
    {
        Repeindre(hwnd);
        Win32.GetClientRect(hwnd, out var client);
        float échelle = BancCapture.Field<float>(fenêtre, "_dpiScale");
        int haut = BancCapture.Field<int>(fenêtre, "_contentY");
        int hauteur = (int)Math.Round(30 * échelle);

        IntPtr fenêtreDc = Win32.GetDC(hwnd);
        IntPtr mémoire = Win32.CreateCompatibleDC(fenêtreDc);
        IntPtr image = Win32.CreateCompatibleBitmap(fenêtreDc, client.right, hauteur);
        IntPtr ancienne = Win32.SelectObject(mémoire, image);
        try
        {
            Win32.BitBlt(mémoire, 0, 0, client.right, hauteur, fenêtreDc, 0, haut, Win32.SRCCOPY);
            int gauche = int.MaxValue, premier = int.MaxValue, dernier = -1;
            bool clair = false;
            for (int y = 0; y < hauteur; y++)
            {
                for (int x = 0; x < client.right; x++)
                {
                    uint c = GetPixel(mémoire, x, y);
                    int r = (int)(c & 0xFF), g = (int)((c >> 8) & 0xFF), b = (int)((c >> 16) & 0xFF);
                    if (r + g + b > 600) clair = true;
                    if (r + g + b >= 300) continue; // l'encre est #1B1813
                    gauche = Math.Min(gauche, x);
                    premier = Math.Min(premier, y);
                    dernier = Math.Max(dernier, y);
                }
            }
            return clair && dernier >= 0 ? (gauche, premier, dernier) : null;
        }
        finally
        {
            Win32.SelectObject(mémoire, ancienne);
            Win32.DeleteObject(image);
            Win32.DeleteDC(mémoire);
            Win32.ReleaseDC(hwnd, fenêtreDc);
        }
    }

    [Fact]
    public void Titres_peints_des_quatre_etapes_partent_du_meme_bord_et_de_la_meme_ligne()
    {
        Avec((fenêtre, hwnd) =>
        {
            float échelle = BancCapture.Field<float>(fenêtre, "_dpiScale");
            var encres = new List<(int Left, int Top, int Bottom)>();
            foreach (int étape in new[] { Changes, Languages, HowTo, Resources })
            {
                AllerÀ(fenêtre, étape);
                var encre = EncreDuTitre(fenêtre, hwnd);
                if (encre == null) return; // fenêtre non lisible à l'écran (session sans bureau) : géométrie testée ci-dessus
                encres.Add(encre.Value);
            }

            int bord = (int)Math.Round(28 * échelle);
            foreach (var encre in encres)
                Assert.InRange(encre.Left, bord - 1, bord + 6); // le chasse de la première lettre varie
            // « Comment utiliser… » et « Ressources… » n'ont ni jambage ni accent : même ligne, au pixel près.
            Assert.InRange(Math.Abs(encres[2].Top - encres[3].Top), 0, 1);
            Assert.InRange(Math.Abs(encres[2].Bottom - encres[3].Bottom), 0, 1);
        });
    }

    // ── A7 : polices et cadres ─────────────────────────────────────────

    private static string PoliceDe(IntPtr police)
    {
        IntPtr écran = Win32.GetDC(IntPtr.Zero);
        IntPtr dc = Win32.CreateCompatibleDC(écran);
        Win32.ReleaseDC(IntPtr.Zero, écran);
        IntPtr ancienne = Win32.SelectObject(dc, police);
        try
        {
            var nom = new StringBuilder(64);
            GetTextFaceW(dc, nom.Capacity, nom);
            return nom.ToString();
        }
        finally
        {
            Win32.SelectObject(dc, ancienne);
            Win32.DeleteDC(dc);
        }
    }

    [Fact]
    public void Boutons_et_liens_sont_en_AG_Texte_et_seul_l_essai_garde_Segoe()
    {
        Avec((fenêtre, _) =>
        {
            Assert.Equal(AppFonts.Text, PoliceDe(BancCapture.Field<IntPtr>(fenêtre, "_hFontButton")));
            Assert.Equal(AppFonts.Text, PoliceDe(BancCapture.Field<IntPtr>(fenêtre, "_hFontLinkStrong")));
            // Un EDIT n'a pas le secours par caractère de PaperText, et AG Texte n'a pas ɓ ɗ ɛ.
            Assert.Equal(TypeRamp.Family, PoliceDe(BancCapture.Field<IntPtr>(fenêtre, "_hFontTrial")));
        });
    }

    [Fact]
    public void Les_trois_cases_sont_des_boutons_dessines_et_non_des_cases_natives()
    {
        Avec((fenêtre, _) =>
        {
            foreach (string champ in new[] { "_hWndChkAutoStart", "_hWndChkDontShow", "_hWndChkTraining" })
            {
                uint style = (uint)GetWindowLongPtrW(Contrôle(fenêtre, champ), -16).ToInt64();
                Assert.Equal(Win32.BS_OWNERDRAW, style & 0xF);
                Assert.NotEqual(0u, style & Win32.WS_TABSTOP);
            }
        });
    }

    // ── A7 : bascule des cases ─────────────────────────────────────────

    private const uint WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202;
    private const int VK_SPACE = 0x20;

    [Fact]
    public void Clic_Espace_Entree_et_BM_CLICK_basculent_une_case_dessinee()
    {
        Avec((fenêtre, hwnd) =>
        {
            AllerÀ(fenêtre, Resources);
            IntPtr case1 = Contrôle(fenêtre, "_hWndChkDontShow");
            bool départ = Cochée(fenêtre, "_dontShowChecked");

            Win32.SendMessageW(case1, Win32.BM_CLICK, IntPtr.Zero, IntPtr.Zero);
            Assert.Equal(!départ, Cochée(fenêtre, "_dontShowChecked"));

            // Entrée arrive en IDOK (IsDialogMessageW) et presse la case qui a le focus.
            Win32.SetFocus(case1);
            Assert.Equal(case1, Win32.GetFocus());
            Win32.SendMessageW(hwnd, Win32.WM_COMMAND, (IntPtr)DialogNavigation.IDOK, IntPtr.Zero);
            Assert.Equal(départ, Cochée(fenêtre, "_dontShowChecked"));

            // Espace : touche enfoncée puis relâchée sur la case.
            Win32.SendMessageW(case1, WM_KEYDOWN, (IntPtr)VK_SPACE, IntPtr.Zero);
            Win32.SendMessageW(case1, WM_KEYUP, (IntPtr)VK_SPACE, IntPtr.Zero);
            Assert.Equal(!départ, Cochée(fenêtre, "_dontShowChecked"));

            // Clic de souris dans la case.
            IntPtr milieu = (IntPtr)((5) | (5 << 16));
            Win32.SendMessageW(case1, WM_LBUTTONDOWN, (IntPtr)1, milieu);
            Win32.SendMessageW(case1, WM_LBUTTONUP, IntPtr.Zero, milieu);
            Assert.Equal(départ, Cochée(fenêtre, "_dontShowChecked"));
        });
    }

    [Fact]
    public void Entree_sur_le_bouton_final_ne_touche_a_aucune_case()
    {
        Avec((fenêtre, hwnd) =>
        {
            AllerÀ(fenêtre, Resources);
            bool démarrage = Cochée(fenêtre, "_autoStartChecked");
            bool ne_plus_afficher = Cochée(fenêtre, "_dontShowChecked");
            IntPtr suivant = Contrôle(fenêtre, "_hWndBtnNext");
            Win32.SetFocus(suivant);
            Assert.Equal(suivant, Win32.GetFocus());

            // IDOK presse le bouton qui a le focus, et lui seul : la case de démarrage n'est pas basculée.
            // (Sans accord d'activation, « Activer » demande l'accord et ne ferme pas.)
            BancCapture.SetField(fenêtre, "_activationAccepted", false);
            Win32.SendMessageW(hwnd, Win32.WM_COMMAND, (IntPtr)DialogNavigation.IDOK, IntPtr.Zero);
            Assert.Equal(démarrage, Cochée(fenêtre, "_autoStartChecked"));
            Assert.Equal(ne_plus_afficher, Cochée(fenêtre, "_dontShowChecked"));
        });
    }

    [Fact]
    public void Une_case_n_est_modifiee_par_l_utilisateur_que_si_elle_differe_de_son_affichage()
    {
        Avec((fenêtre, _) =>
        {
            AllerÀ(fenêtre, Resources);
            IntPtr démarrage = Contrôle(fenêtre, "_hWndChkAutoStart");
            bool affichée = BancCapture.Field<bool>(fenêtre, "_autoStartShownChecked");
            Assert.Equal(affichée, Cochée(fenêtre, "_autoStartChecked"));

            // Telle qu'affichée : croix, Échap ou Alt+F4 n'appliquent rien (B2, OnboardingCloseDecisionTests).
            Assert.False(OnboardingWindow.ShouldApplyPreferenceOnClose(false, true, true, Cochée(fenêtre, "_autoStartChecked"), affichée));

            Win32.SendMessageW(démarrage, Win32.BM_CLICK, IntPtr.Zero, IntPtr.Zero);
            Assert.True(OnboardingWindow.ShouldApplyPreferenceOnClose(false, true, true, Cochée(fenêtre, "_autoStartChecked"), affichée));

            // Cochée puis décochée : redevenue telle qu'affichée, donc plus un choix.
            Win32.SendMessageW(démarrage, Win32.BM_CLICK, IntPtr.Zero, IntPtr.Zero);
            Assert.False(OnboardingWindow.ShouldApplyPreferenceOnClose(false, true, true, Cochée(fenêtre, "_autoStartChecked"), affichée));
        });
    }

    // ── A8 : ordre de tabulation et focus ──────────────────────────────

    private static List<int> ArrêtsDeTabulation(IntPtr hwnd)
    {
        var ids = new List<int>();
        IntPtr premier = GetNextDlgTabItem(hwnd, IntPtr.Zero, false);
        IntPtr courant = premier;
        do
        {
            ids.Add(GetDlgCtrlID(courant));
            courant = GetNextDlgTabItem(hwnd, courant, false);
        } while (courant != premier && courant != IntPtr.Zero && ids.Count < 40);
        return ids;
    }

    [Fact]
    public void A_la_derniere_etape_les_cases_precedent_le_bouton_final_dans_la_tabulation()
    {
        Avec((fenêtre, hwnd) =>
        {
            AllerÀ(fenêtre, Resources);
            var arrêts = ArrêtsDeTabulation(hwnd);

            Assert.Equal(IDC_BTN_NEXT, arrêts[^1]); // « C'est parti ! » ferme la marche
            Assert.True(arrêts.IndexOf(IDC_CHK_AUTOSTART) >= 0 && arrêts.IndexOf(IDC_CHK_AUTOSTART) < arrêts.IndexOf(IDC_BTN_NEXT));
            Assert.True(arrêts.IndexOf(IDC_CHK_DONT_SHOW) >= 0 && arrêts.IndexOf(IDC_CHK_DONT_SHOW) < arrêts.IndexOf(IDC_BTN_NEXT));
            if (arrêts.Contains(IDC_CHK_TRAINING))
                Assert.True(arrêts.IndexOf(IDC_CHK_TRAINING) < arrêts.IndexOf(IDC_BTN_NEXT));
            Assert.Equal(1, arrêts.Count(id => id == IDC_BTN_NEXT));
        });
    }

    [Fact]
    public void Arrivee_a_la_derniere_etape_le_focus_quitte_Suivant_pour_la_premiere_case()
    {
        Avec((fenêtre, hwnd) =>
        {
            AllerÀ(fenêtre, HowTo);
            IntPtr suivant = Contrôle(fenêtre, "_hWndBtnNext");
            Win32.SetFocus(suivant);
            Assert.Equal(suivant, Win32.GetFocus());

            BancCapture.Call(fenêtre, "GoToNextStep");

            Assert.Equal(Resources, BancCapture.Field<int>(fenêtre, "_currentStep"));
            Assert.Equal(Contrôle(fenêtre, "_hWndChkAutoStart"), Win32.GetFocus());
            // Une seconde Entrée bascule donc la case, et ne valide pas le démarrage automatique.
            Assert.True(BancCapture.Field<bool>(fenêtre, "_step3Reached"));
            bool avant = Cochée(fenêtre, "_autoStartChecked");
            Win32.SendMessageW(hwnd, Win32.WM_COMMAND, (IntPtr)DialogNavigation.IDOK, IntPtr.Zero);
            Assert.Equal(!avant, Cochée(fenêtre, "_autoStartChecked"));
            Assert.True(Win32.IsWindowVisible(hwnd));
        });
    }

    [Fact]
    public void Revenir_en_arriere_depuis_une_case_ne_laisse_pas_le_focus_sur_un_controle_masque()
    {
        Avec((fenêtre, _) =>
        {
            AllerÀ(fenêtre, Resources);
            Win32.SetFocus(Contrôle(fenêtre, "_hWndChkDontShow"));
            Assert.Equal(Contrôle(fenêtre, "_hWndChkDontShow"), Win32.GetFocus());

            BancCapture.Call(fenêtre, "GoToPreviousStep");

            Assert.Equal(Contrôle(fenêtre, "_hWndBtnNext"), Win32.GetFocus());
        });
    }

    // ── A8 : lecteur d'écran ───────────────────────────────────────────

    [DllImport("oleacc.dll")]
    private static extern int AccessibleObjectFromWindow(IntPtr hwnd, uint idObject, ref Guid riid, out IntPtr ppv);

    [StructLayout(LayoutKind.Sequential)]
    private struct VariantI4 { public ushort Vt; public ushort R1, R2, R3; public IntPtr Valeur; public IntPtr Reste; }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ProprieteTexte(IntPtr self, VariantI4 child, out IntPtr bstr);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ProprieteVariant(IntPtr self, VariantI4 child, out VariantI4 valeur);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int Libérer(IntPtr self);

    /// <summary>Ce que lit le Narrateur par le proxy MSAA du contrôle (get_accName 10, accDescription 12,
    /// accRole 13, accState 14), comme les témoins AccessibleDescriptionTests et AccessibleCheckStateTests.</summary>
    private static (string? Nom, string? Description, int Rôle, int État) Lire(IntPtr hwnd)
    {
        Guid iidIAccessible = new("618736E0-3C3D-11CF-810C-00AA00389B71");
        Assert.Equal(0, AccessibleObjectFromWindow(hwnd, 0xFFFFFFFC, ref iidIAccessible, out IntPtr acc)); // OBJID_CLIENT
        try
        {
            IntPtr Slot(int i) => Marshal.ReadIntPtr(Marshal.ReadIntPtr(acc), i * IntPtr.Size);
            var soi = new VariantI4 { Vt = 3, Valeur = IntPtr.Zero }; // VT_I4, CHILDID_SELF
            string? Texte(int slot)
            {
                if (Marshal.GetDelegateForFunctionPointer<ProprieteTexte>(Slot(slot))(acc, soi, out IntPtr bstr) != 0 || bstr == IntPtr.Zero)
                    return null;
                try { return Marshal.PtrToStringBSTR(bstr); }
                finally { Marshal.FreeBSTR(bstr); }
            }
            int Entier(int slot)
            {
                Marshal.GetDelegateForFunctionPointer<ProprieteVariant>(Slot(slot))(acc, soi, out VariantI4 valeur);
                return (int)valeur.Valeur;
            }
            return (Texte(10), Texte(12), Entier(13), Entier(14));
        }
        finally
        {
            Marshal.GetDelegateForFunctionPointer<Libérer>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(acc), 2 * IntPtr.Size))(acc);
        }
    }

    private const int ROLE_SYSTEM_CHECKBUTTON = 0x2C;
    private const int STATE_SYSTEM_CHECKED = 0x10;

    [Fact]
    public void Une_case_dessinee_se_presente_comme_une_case_a_cocher_avec_son_etat()
    {
        Avec((fenêtre, _) =>
        {
            AllerÀ(fenêtre, Resources);
            IntPtr case1 = Contrôle(fenêtre, "_hWndChkDontShow");

            var avant = Lire(case1);
            Assert.Equal(ROLE_SYSTEM_CHECKBUTTON, avant.Rôle);
            Assert.Equal(L.Onboarding_ChkDontShow, avant.Nom);
            Assert.Equal(0, avant.État & STATE_SYSTEM_CHECKED);

            Win32.SendMessageW(case1, Win32.BM_CLICK, IntPtr.Zero, IntPtr.Zero);
            Assert.Equal(STATE_SYSTEM_CHECKED, Lire(case1).État & STATE_SYSTEM_CHECKED);
        });
    }

    [Theory]
    [InlineData(Changes)]
    [InlineData(Languages)]
    [InlineData(HowTo)]
    [InlineData(Resources)]
    public void Chaque_etape_decrit_son_texte_sur_le_bouton_principal(int étape)
    {
        Avec((fenêtre, _) =>
        {
            AllerÀ(fenêtre, étape);
            string description = Lire(Contrôle(fenêtre, "_hWndBtnNext")).Description ?? "";

            Assert.Equal(OnboardingWindow.StepAccessibleSummary(étape), description);
            Assert.StartsWith(OnboardingStep.Label(étape), description);
        });
    }

    [Fact]
    public void A_l_etat_C_de_l_etape_1_le_resume_est_sur_Suivant_puisque_Essayer_est_masque()
    {
        Avec((fenêtre, _) =>
        {
            BancCapture.SetField(fenêtre, "_learningModuleDone", true);
            AllerÀ(fenêtre, Changes);
            Assert.False(Win32.IsWindowVisible(Contrôle(fenêtre, "_hWndBtnTry")));

            string description = Lire(Contrôle(fenêtre, "_hWndBtnNext")).Description ?? "";
            Assert.Contains(L.Onboarding_Step1Title, description);
            Assert.Contains(L.Onboarding_NoteCoding, description);
            Assert.Contains(L.Onboarding_PrivacyReassurance, description);
        });
    }

    [Fact]
    public void Les_cartes_et_les_titres_de_l_etape_Ressources_sont_dits_par_le_lecteur_d_ecran()
    {
        string cartes = OnboardingWindow.StepAccessibleSummary(HowTo);
        foreach (string morceau in new[] { L.Onboarding_Step2Title, L.Onboarding_Card1Title, L.Onboarding_Card1Desc,
                     L.Onboarding_Card2Title, L.Onboarding_Card3Title, L.Onboarding_Card4Title })
            Assert.Contains(morceau, cartes);
        Assert.Contains($"Ctrl + {L.Settings_ShortcutModifier2} + Q", cartes);

        string ressources = OnboardingWindow.StepAccessibleSummary(Resources);
        Assert.Contains(L.Onboarding_SectionResources, ressources);
        Assert.Contains(L.Settings_SectionPreferences, ressources);

        Avec((fenêtre, _) =>
        {
            AllerÀ(fenêtre, Resources);
            Assert.Equal(L.Onboarding_SectionResources, Lire(Contrôle(fenêtre, "_hWndLinkLessons")).Description);
            Assert.Equal(L.Settings_SectionPreferences, Lire(Contrôle(fenêtre, "_hWndChkAutoStart")).Description);
        });
    }

    [Fact]
    public void Le_resume_d_etape_suit_la_langue_de_l_interface()
    {
        string langue = L.Language;
        try
        {
            L.Language = "fr";
            Assert.StartsWith("Étape 3 sur 4.", OnboardingWindow.StepAccessibleSummary(HowTo));
            L.Language = "en";
            Assert.StartsWith("Step 3 of 4.", OnboardingWindow.StepAccessibleSummary(HowTo));
        }
        finally { L.Language = langue; }
    }

    // ── La mise en page tient au-dessus des boutons, à plusieurs échelles ──

    [Theory]
    [InlineData(96)]
    [InlineData(168)]
    public void Etapes_Langues_et_Ressources_tiennent_au_dessus_des_boutons(int dpi)
    {
        Avec((fenêtre, hwnd) =>
        {
            BancCapture.ApplyDpi(hwnd, dpi);
            Win32.GetClientRect(hwnd, out var client);
            var origine = new Win32.POINT { x = 0, y = 0 };
            Win32.ClientToScreen(hwnd, ref origine);

            foreach (int étape in new[] { Languages, Resources })
            {
                AllerÀ(fenêtre, étape);
                Win32.GetWindowRect(Contrôle(fenêtre, "_hWndBtnNext"), out var suivant);
                int hautDesBoutons = suivant.top - origine.y;
                Repeindre(hwnd);
                int y = BancCapture.Field<int>(fenêtre, "_contentY");

                int bas;
                if (étape == Languages)
                {
                    IntPtr hdc = Win32.GetDC(hwnd);
                    try
                    {
                        object langues = BancCapture.Call(fenêtre, "GetLanguagesLayout", hdc, client.right, y)!;
                        bas = ((Win32.RECT)langues.GetType().GetField("Tip")!.GetValue(langues)!).bottom;
                    }
                    finally { Win32.ReleaseDC(hwnd, hdc); }
                }
                else
                {
                    var args = new object?[18];
                    args[0] = y;
                    args[1] = client.right;
                    BancCapture.Call(fenêtre, "GetStep3Layout", args);
                    bas = ((Win32.RECT)args[3]!).bottom; // le panneau Préférences
                }
                Assert.True(bas < hautDesBoutons, $"étape {étape + 1} à {dpi} dpi : contenu jusqu'à {bas}, boutons dès {hautDesBoutons}, client {client.right}x{client.bottom}, dpi fenêtre {Win32.GetDpiForWindow(hwnd)}, échelle {BancCapture.Field<float>(fenêtre, "_dpiScale")}");
            }
        });
    }
}
