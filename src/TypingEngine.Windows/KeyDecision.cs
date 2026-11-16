namespace TypingEngine.Windows;

/// <summary>
/// Ce que le moteur a décidé pour un appui de touche : absorbé ou laissé passer, et le texte
/// qu'il a réellement envoyé à Windows pendant cet appui (vide s'il n'a rien émis). Un lot SendInput
/// partiel annonce les caractères dont au moins un événement est parti, pas « rien émis ». Les Leçons
/// s'en servent pour attendre ce que le moteur écrit, sans le recalculer (ticket ME-06).
/// </summary>
public readonly record struct KeyDecision(uint ScanCode, bool Handled, string EmittedText);
