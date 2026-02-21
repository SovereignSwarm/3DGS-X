using System.Globalization;
using UnityEngine;

namespace PerseusXR.IO
{
    public class LocaleFixer : MonoBehaviour
    {
        void Awake()
        {
            CultureInfo culture = new CultureInfo("en-US");
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            Debug.Log($"[Locale] Fixed culture: {CultureInfo.CurrentCulture.Name}");
        }
    }
}