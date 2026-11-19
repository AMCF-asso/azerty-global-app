namespace AZERTYGlobal;

/// <summary>Explication d'un caractère déplacé ou nouveau du parcours : pourquoi il est là,
/// et comment retenir sa place. <see cref="Remember"/> est null quand la phrase du geste dit déjà
/// tout (QCM d'Antoine du 07/11, « Garder l'analogie seule ») : l'encadré n'a alors pas de ligne
/// « Pour retenir ».</summary>
internal readonly record struct CharacterExplanation(string Why, string? Remember)
{
    public bool HasRemember => !string.IsNullOrWhiteSpace(Remember);
}

internal static partial class L
{
    // ── Explications « Pourquoi ici » du bilan du parcours (remontée 13b, 07/11) ──
    public static string Explanation_RememberLabel => T("Pour retenir : ", "To remember: ");
    /// <summary>Onglet des deux espaces insécables, invisibles à l'écran.</summary>
    public static string Explanation_NbspTab => T("Espaces insécables", "Non-breaking spaces");
    /// <summary>Nom lu d'un onglet du bilan, pour le lecteur d'écran.</summary>
    public static string Explanation_TabName(string characters) => T($"Explication de {characters}", $"Explanation for {characters}");

    /// <summary>
    /// Explication du caractère <paramref name="c"/>, ou null s'il n'en a pas. Les textes viennent
    /// de operations/2026-10-14-sortie-v2-130/explications-caracteres-parcours.md, validé par
    /// Antoine (revue markano, QCM du 07/11 « Tu rédiges, puis je valide »). Ne pas retoucher à
    /// la main : corriger le brouillon, le faire valider, puis régénérer ce bloc
    /// (operations/2026-10-14-sortie-v2-130/outils/gen_explications.py).
    /// </summary>
    public static CharacterExplanation? TutorialExplanation(char c) => c switch
    {
        // <généré par gen_explications.py>
        'É' => new(T("Verr. Maj. n’affecte plus que les lettres, donc é è ç à donnent É È Ç À pour les mots en capitales, alors que sur l’AZERTY classique Verr. Maj. puis é donne le chiffre 2.",
            "Caps Lock now affects letters only, so é è ç à give É È Ç À for all-caps words, whereas on classic AZERTY Caps Lock then é gives the digit 2."),
            T("Une lettre accentuée passe en majuscule avec Verr. Maj. comme toutes les autres lettres.",
              "An accented letter goes uppercase with Caps Lock like any other letter.")),
        'È' => new(T("Verr. Maj. n’affecte plus que les lettres, donc é è ç à donnent É È Ç À pour les mots en capitales, alors que sur l’AZERTY classique Verr. Maj. puis é donne le chiffre 2.",
            "Caps Lock now affects letters only, so é è ç à give É È Ç À for all-caps words, whereas on classic AZERTY Caps Lock then é gives the digit 2."),
            T("Une lettre accentuée passe en majuscule avec Verr. Maj. comme toutes les autres lettres.",
              "An accented letter goes uppercase with Caps Lock like any other letter.")),
        'À' => new(T("Verr. Maj. n’affecte plus que les lettres, donc é è ç à donnent É È Ç À pour les mots en capitales, alors que sur l’AZERTY classique Verr. Maj. puis é donne le chiffre 2.",
            "Caps Lock now affects letters only, so é è ç à give É È Ç À for all-caps words, whereas on classic AZERTY Caps Lock then é gives the digit 2."),
            T("Une lettre accentuée passe en majuscule avec Verr. Maj. comme toutes les autres lettres.",
              "An accented letter goes uppercase with Caps Lock like any other letter.")),
        'Ç' => new(T("Verr. Maj. n’affecte plus que les lettres, donc é è ç à donnent É È Ç À pour les mots en capitales, alors que sur l’AZERTY classique Verr. Maj. puis é donne le chiffre 2.",
            "Caps Lock now affects letters only, so é è ç à give É È Ç À for all-caps words, whereas on classic AZERTY Caps Lock then é gives the digit 2."),
            T("Une lettre accentuée passe en majuscule avec Verr. Maj. comme toutes les autres lettres.",
              "An accented letter goes uppercase with Caps Lock like any other letter.")),
        '@' => new(T("L’arobase des adresses e-mail passe en accès direct sur l’ancienne touche ², comme sur l’AZERTY de macOS.",
            "The @ of email addresses moves to direct access on the former ² key, as on the macOS AZERTY."),
            T("La touche en haut à gauche, comme sur Mac.",
              "The top-left key, as on a Mac.")),
        '«' => new(T("Les guillemets français sont des doubles chevrons, donc ils se placent à côté de la touche < >, sur W et X, à la même place que sous Linux.",
            "French quotation marks are double angle brackets, so they sit next to the < > key, on W and X, matching their place on Linux."),
            T("Des doubles chevrons, à côté de la touche < >.",
              "Double angle brackets, next to the < > key.")),
        '»' => new(T("Les guillemets français sont des doubles chevrons, donc ils se placent à côté de la touche < >, sur W et X, à la même place que sous Linux.",
            "French quotation marks are double angle brackets, so they sit next to the < > key, on W and X, matching their place on Linux."),
            T("Des doubles chevrons, à côté de la touche < >.",
              "Double angle brackets, next to the < > key.")),
        '’' => new(T("L’apostrophe typographique est la forme soignée de l’apostrophe droite, donc elle partage sa touche.",
            "The typographic apostrophe is the polished form of the straight one, so it shares its key."),
            T($"La même touche que l’apostrophe droite, avec {AltGrKey}.",
              $"The same key as the straight apostrophe, with {AltGrKey}.")),
        'œ' => new(T("La ligature française œ (cœur, œuvre) commence par o.",
            "The French ligature œ (cœur, œuvre) starts with an o."),
            T("Un o et un e liés.",
              "An o and an e joined.")),
        '—' => new(T("Les tirets vont sur T, l’initiale de «\u00A0tiret\u00A0»\u202F; le cadratin, plus long, s’obtient avec Maj.",
            "The dashes sit on T, the initial of “tiret” (French for dash); the longer em dash takes Shift."),
            null),
        '\u202F' => new(T("En typographie française stricte l’espace fine insécable (avant\u202F;\u202F!\u202F?) est la plus fréquente, donc elle a le geste le plus simple\u202F; l’espace insécable normale (dans «\u00A0», avant les deux-points), moins fréquente, demande Maj en plus.",
            "In strict French typography the narrow non-breaking space (before ; ! ?) is the most frequent, so it gets the simplest gesture; the regular non-breaking space (inside « », before colons) is less frequent and needs Shift as well."),
            T($"Sur la barre d’espace, avec {AltGrKey} et {AltGrKey} + Maj.",
              $"On the space bar, with {AltGrKey} and {AltGrKey} + Shift.")),
        '\u00A0' => new(T("En typographie française stricte l’espace fine insécable (avant\u202F;\u202F!\u202F?) est la plus fréquente, donc elle a le geste le plus simple\u202F; l’espace insécable normale (dans «\u00A0», avant les deux-points), moins fréquente, demande Maj en plus.",
            "In strict French typography the narrow non-breaking space (before ; ! ?) is the most frequent, so it gets the simplest gesture; the regular non-breaking space (inside « », before colons) is less frequent and needs Shift as well."),
            T($"Sur la barre d’espace, avec {AltGrKey} et {AltGrKey} + Maj.",
              $"On the space bar, with {AltGrKey} and {AltGrKey} + Shift.")),
        '.' => new(T("Le point est plus de dix fois plus fréquent que le point-virgule, donc les deux échangent leur niveau sur la même touche (l’AZERTY français et l’AZERTY belge sont les seuls au monde où le point demande Maj).",
            "The period is more than ten times more frequent than the semicolon, so they swap levels on the same key (French and Belgian AZERTY are the only keyboards in the world where the period needs Shift)."),
            T("Le point en accès direct, le point-virgule avec Maj.",
              "The period on its own, the semicolon with Shift.")),
        ';' => new(T("Le point est plus de dix fois plus fréquent que le point-virgule, donc les deux échangent leur niveau sur la même touche (l’AZERTY français et l’AZERTY belge sont les seuls au monde où le point demande Maj).",
            "The period is more than ten times more frequent than the semicolon, so they swap levels on the same key (French and Belgian AZERTY are the only keyboards in the world where the period needs Shift)."),
            T("Le point en accès direct, le point-virgule avec Maj.",
              "The period on its own, the semicolon with Shift.")),
        '{' => new(T("Les symboles du code se tapent sans quitter la rangée de repos, en paires côte à côte (l’ouvrant à gauche, le fermant à droite)\u202F; la barre oblique inversée et la barre verticale, voisines dans le terminal (chemins, pipes), sont voisines au milieu de la rangée.",
            "Code symbols are typed without leaving the home row, in side-by-side pairs (opening on the left, closing on the right); the backslash and the vertical bar, neighbors in the terminal (paths, pipes), are neighbors in the middle of the row."),
            T("Sous le majeur gauche, avec } juste à droite.",
              "Under the left middle finger, with } just to its right.")),
        '}' => new(T("Les symboles du code se tapent sans quitter la rangée de repos, en paires côte à côte (l’ouvrant à gauche, le fermant à droite)\u202F; la barre oblique inversée et la barre verticale, voisines dans le terminal (chemins, pipes), sont voisines au milieu de la rangée.",
            "Code symbols are typed without leaving the home row, in side-by-side pairs (opening on the left, closing on the right); the backslash and the vertical bar, neighbors in the terminal (paths, pipes), are neighbors in the middle of the row."),
            T("Sous l’index gauche, juste à droite de {.",
              "Under the left index finger, just right of {.")),
        '[' => new(T("Les symboles du code se tapent sans quitter la rangée de repos, en paires côte à côte (l’ouvrant à gauche, le fermant à droite)\u202F; la barre oblique inversée et la barre verticale, voisines dans le terminal (chemins, pipes), sont voisines au milieu de la rangée.",
            "Code symbols are typed without leaving the home row, in side-by-side pairs (opening on the left, closing on the right); the backslash and the vertical bar, neighbors in the terminal (paths, pipes), are neighbors in the middle of the row."),
            T("Sous l’index droit, avec ] juste à droite.",
              "Under the right index finger, with ] just to its right.")),
        ']' => new(T("Les symboles du code se tapent sans quitter la rangée de repos, en paires côte à côte (l’ouvrant à gauche, le fermant à droite)\u202F; la barre oblique inversée et la barre verticale, voisines dans le terminal (chemins, pipes), sont voisines au milieu de la rangée.",
            "Code symbols are typed without leaving the home row, in side-by-side pairs (opening on the left, closing on the right); the backslash and the vertical bar, neighbors in the terminal (paths, pipes), are neighbors in the middle of the row."),
            T("Sous le majeur droit, juste à droite de [.",
              "Under the right middle finger, just right of [.")),
        '\\' => new(T("Les symboles du code se tapent sans quitter la rangée de repos, en paires côte à côte (l’ouvrant à gauche, le fermant à droite)\u202F; la barre oblique inversée et la barre verticale, voisines dans le terminal (chemins, pipes), sont voisines au milieu de la rangée.",
            "Code symbols are typed without leaving the home row, in side-by-side pairs (opening on the left, closing on the right); the backslash and the vertical bar, neighbors in the terminal (paths, pipes), are neighbors in the middle of the row."),
            T("À droite de l’index gauche, voisine de | comme dans le terminal.",
              "Right of the left index finger, next to | as in the terminal.")),
        '|' => new(T("Les symboles du code se tapent sans quitter la rangée de repos, en paires côte à côte (l’ouvrant à gauche, le fermant à droite)\u202F; la barre oblique inversée et la barre verticale, voisines dans le terminal (chemins, pipes), sont voisines au milieu de la rangée.",
            "Code symbols are typed without leaving the home row, in side-by-side pairs (opening on the left, closing on the right); the backslash and the vertical bar, neighbors in the terminal (paths, pipes), are neighbors in the middle of the row."),
            T("À gauche de l’index droit, voisine de \\.",
              "Left of the right index finger, next to \\.")),
        'í' => new(T($"La touche à droite du M porte maintenant les accents des autres langues\u202F; l’aigu y est en accès direct parce qu’il est le plus fréquent dans les langues européennes, et le grave le rejoint avec Maj\u202F; le ù lui-même est passé en {AltGrKey} + U.",
            $"The key right of M now carries the accents of other languages; the acute is direct there because it is the most frequent in European languages, and the grave joins it with Shift; ù itself moved to {AltGrKey} + U."),
            T($"C’est la touche des accents\u00A0: l’aigu seul, le grave avec Maj, le tilde avec {AltGrKey}.",
              $"It is the accent key: acute alone, grave with Shift, tilde with {AltGrKey}.")),
        'ò' => new(T($"La touche à droite du M porte maintenant les accents des autres langues\u202F; l’aigu y est en accès direct parce qu’il est le plus fréquent dans les langues européennes, et le grave le rejoint avec Maj\u202F; le ù lui-même est passé en {AltGrKey} + U.",
            $"The key right of M now carries the accents of other languages; the acute is direct there because it is the most frequent in European languages, and the grave joins it with Shift; ù itself moved to {AltGrKey} + U."),
            T($"C’est la touche des accents\u00A0: l’aigu seul, le grave avec Maj, le tilde avec {AltGrKey}.",
              $"It is the accent key: acute alone, grave with Shift, tilde with {AltGrKey}.")),
        'ã' => new(T("C’est un raccourci du portugais, qui évite de passer par la touche morte tilde.",
            "It is a Portuguese shortcut that avoids going through the tilde dead key."),
            T("P comme Portugal.",
              "P for Portugal.")),
        'ɛ' => new(T("Les lettres latines spéciales demandent ainsi 2 appuis au lieu de 3, sur une place libre\u202F; chacune va sur sa lettre de base (ɛ est un e ouvert, ɔ un o ouvert, très courants dans les langues d’Afrique\u202F; ŋ est un n), et ɲ va sur J, voisine de N.",
            "Special Latin letters need 2 key presses instead of 3, in a free position; each sits on its base letter (ɛ is an open e, ɔ an open o, both very common in African languages; ŋ is an n), and ɲ goes on J, next to N."),
            T("La touche - est celle du 6, et le schwa ə, emblème de ces lettres, est un 6 en miroir. Puis, selon la lettre\u00A0: ɛ est un e ouvert\u202F; ɔ, un o ouvert\u202F; ŋ, un n à queue\u202F; ɲ va à côté du N.",
              "The - key is the 6 key, and the schwa ə, which stands for these letters, is a mirrored 6. Then, per letter: ɛ is an open e; ɔ, an open o; ŋ, a tailed n; ɲ sits next to N.")),
        'ɔ' => new(T("Les lettres latines spéciales demandent ainsi 2 appuis au lieu de 3, sur une place libre\u202F; chacune va sur sa lettre de base (ɛ est un e ouvert, ɔ un o ouvert, très courants dans les langues d’Afrique\u202F; ŋ est un n), et ɲ va sur J, voisine de N.",
            "Special Latin letters need 2 key presses instead of 3, in a free position; each sits on its base letter (ɛ is an open e, ɔ an open o, both very common in African languages; ŋ is an n), and ɲ goes on J, next to N."),
            T("La touche - est celle du 6, et le schwa ə, emblème de ces lettres, est un 6 en miroir. Puis, selon la lettre\u00A0: ɛ est un e ouvert\u202F; ɔ, un o ouvert\u202F; ŋ, un n à queue\u202F; ɲ va à côté du N.",
              "The - key is the 6 key, and the schwa ə, which stands for these letters, is a mirrored 6. Then, per letter: ɛ is an open e; ɔ, an open o; ŋ, a tailed n; ɲ sits next to N.")),
        'ŋ' => new(T("Les lettres latines spéciales demandent ainsi 2 appuis au lieu de 3, sur une place libre\u202F; chacune va sur sa lettre de base (ɛ est un e ouvert, ɔ un o ouvert, très courants dans les langues d’Afrique\u202F; ŋ est un n), et ɲ va sur J, voisine de N.",
            "Special Latin letters need 2 key presses instead of 3, in a free position; each sits on its base letter (ɛ is an open e, ɔ an open o, both very common in African languages; ŋ is an n), and ɲ goes on J, next to N."),
            T("La touche - est celle du 6, et le schwa ə, emblème de ces lettres, est un 6 en miroir. Puis, selon la lettre\u00A0: ɛ est un e ouvert\u202F; ɔ, un o ouvert\u202F; ŋ, un n à queue\u202F; ɲ va à côté du N.",
              "The - key is the 6 key, and the schwa ə, which stands for these letters, is a mirrored 6. Then, per letter: ɛ is an open e; ɔ, an open o; ŋ, a tailed n; ɲ sits next to N.")),
        'ɲ' => new(T("Les lettres latines spéciales demandent ainsi 2 appuis au lieu de 3, sur une place libre\u202F; chacune va sur sa lettre de base (ɛ est un e ouvert, ɔ un o ouvert, très courants dans les langues d’Afrique\u202F; ŋ est un n), et ɲ va sur J, voisine de N.",
            "Special Latin letters need 2 key presses instead of 3, in a free position; each sits on its base letter (ɛ is an open e, ɔ an open o, both very common in African languages; ŋ is an n), and ɲ goes on J, next to N."),
            T("La touche - est celle du 6, et le schwa ə, emblème de ces lettres, est un 6 en miroir. Puis, selon la lettre\u00A0: ɛ est un e ouvert\u202F; ɔ, un o ouvert\u202F; ŋ, un n à queue\u202F; ɲ va à côté du N.",
              "The - key is the 6 key, and the schwa ə, which stands for these letters, is a mirrored 6. Then, per letter: ɛ is an open e; ɔ, an open o; ŋ, a tailed n; ɲ sits next to N.")),
        'ɓ' => new(T("Ɓ est fréquent dans les langues d’Afrique, donc 2 appuis au lieu de 3, sur la touche morte la plus directe.",
            "Ɓ is frequent in African languages, so 2 key presses instead of 3, on the most direct dead key."),
            T($"Un b avec un crochet. {AltGrKey} + la touche du 1 marche aussi\u00A0: le 1 a le même petit crochet en haut.",
              $"A b with a hook. {AltGrKey} + the 1 key works too: the 1 has the same small hook on top.")),
        'ɗ' => new(T($"Le crochet sert à beaucoup de langues d’Afrique et a gagné un accès direct en {AltGrKey}, sur une place libre\u202F; ɗ est la lettre D avec son crochet.",
            $"The hook is used by many African languages, so it now has direct {AltGrKey} access, in a free position; ɗ is the letter D with its hook."),
            T("La touche & est celle du 1, et le 1 a un petit crochet en haut, comme ɗ.",
              "The & key is the 1 key, and the 1 has a small hook on top, like ɗ.")),
        // </généré>
        _ => null,
    };
}
