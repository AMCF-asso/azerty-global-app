// Notation intégrée au Microsoft Store (v1.2.0).
//
// Décision du 2026-08-16 : jusqu'ici la sollicitation d'avis ouvrait le volet d'avis
// de l'application Store par `ms-windows-store://review/`. Deux bascules d'application
// séparaient l'utilisateur du formulaire, et l'application Store met plusieurs secondes
// à s'ouvrir : sur 671 acquisitions en trois mois, trois avis seulement ont été déposés.
// `RequestRateAndReviewAppAsync` affiche la même boîte à l'intérieur d'AZERTY Global,
// sans quitter l'application.
//
// L'API exige une identité de package (MSIX) et Windows 10 1809 — soit exactement le
// `MinVersion` déclaré dans AppxManifest.xml. Hors package, ou en cas d'échec, l'appelant
// retombe sur le lien profond, qui reste le comportement de la v1.1.
using Windows.Services.Store;

namespace AZERTYGlobal;

/// <summary>
/// Boîte de notation affichée dans l'application, via l'API Store de Windows.
/// Même mécanique WinRT que <see cref="AutoStart"/> (StartupTask), qui tourne déjà
/// en AOT sur les installations Store depuis la v1.0.
/// </summary>
static class StoreReview
{
    /// <summary>
    /// Affiche la boîte de notation intégrée. Retourne false si elle ne peut pas être
    /// affichée — l'appelant doit alors ouvrir <c>StoreReviewUrl</c> lui-même.
    ///
    /// <paramref name="owner"/> : fenêtre propriétaire. L'API est une API UWP ; sur une
    /// application de bureau elle exige un HWND fourni via <c>IInitializeWithWindow</c>,
    /// sans quoi elle lève « A method was called at an unexpected time ».
    ///
    /// L'opération n'est jamais attendue de façon bloquante : la boîte a besoin de la
    /// boucle de messages du thread appelant pour s'afficher, et un
    /// <c>GetAwaiter().GetResult()</c> — le motif employé par <see cref="AutoStart"/>,
    /// qui lui n'affiche aucune interface — la bloquerait définitivement.
    /// </summary>
    public static bool TryShow(IntPtr owner, string fallbackUrl)
    {
        if (!ConfigManager.IsPackaged) return false;
        if (owner == IntPtr.Zero) return false;

        try
        {
            var context = StoreContext.GetDefault();
            if (context == null) return false;

            WinRT.Interop.InitializeWithWindow.Initialize(context, owner);

            var operation = context.RequestRateAndReviewAppAsync();
            operation.Completed = (asyncOperation, status) =>
                OnCompleted(asyncOperation, status, fallbackUrl);
            return true;
        }
        catch (Exception ex)
        {
            // Identité de package absente, Store indisponible, HWND refusé : l'appelant
            // reprend la main sur le lien profond.
            ConfigManager.Log("StoreReview.TryShow", ex);
            return false;
        }
    }

    /// <summary>
    /// Fin de la boîte de notation. Exécuté sur un thread de pool, jamais sur le thread
    /// d'interface : rien n'y touche à l'état de l'application ni à ses fenêtres, seul le
    /// journal est écrit — <see cref="ConfigManager.Log"/> sérialise ses écritures.
    ///
    /// Un échec réseau ou interne se rattrape sur le lien profond : la boîte s'est fermée
    /// sans rien recueillir, et l'essai a déjà été consommé côté configuration.
    /// Une annulation par l'utilisateur, elle, est une réponse — on n'insiste pas.
    /// </summary>
    private static void OnCompleted(
        Windows.Foundation.IAsyncOperation<StoreRateAndReviewResult> operation,
        Windows.Foundation.AsyncStatus status,
        string fallbackUrl)
    {
        try
        {
            if (status != Windows.Foundation.AsyncStatus.Completed)
            {
                Win32.ShellExecuteW(IntPtr.Zero, "open", fallbackUrl, null, null, 1);
                return;
            }

            var result = operation.GetResults();
            if (result.Status is StoreRateAndReviewStatus.NetworkError or StoreRateAndReviewStatus.Error)
            {
                ConfigManager.Log("StoreReview.OnCompleted",
                    new InvalidOperationException($"status={result.Status}"));
                Win32.ShellExecuteW(IntPtr.Zero, "open", fallbackUrl, null, null, 1);
            }
        }
        catch (Exception ex)
        {
            ConfigManager.Log("StoreReview.OnCompleted", ex);
        }
    }
}
