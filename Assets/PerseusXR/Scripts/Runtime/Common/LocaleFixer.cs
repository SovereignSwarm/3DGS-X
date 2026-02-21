using System.Globalization;
using System.Threading;
using UnityEngine;

namespace PerseusXR.Common
{
    public class LocaleFixer : MonoBehaviour
    {
        // Run before any Awake() to ensure locale is set before ThreadPool threads spawn
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void FixCultureEarly()
        {
            CultureInfo culture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
        }

        void Awake()
        {
            // Redundant safety: ensure locale is set after scene load too
            FixCultureEarly();
            Debug.Log($"[Locale] Fixed culture: {CultureInfo.CurrentCulture.Name}");
        }
    }
}