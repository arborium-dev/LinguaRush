using UnityEngine;

public static class SelectionState
{
    private const string SelectedCountryKey = "SelectedCountry";
    private static string _cachedCountry;
    private static bool _loaded;

    public static void SetSelectedCountry(string country)
    {
        if (string.IsNullOrWhiteSpace(country))
        {
            return;
        }

        _cachedCountry = country;
        _loaded = true;

        PlayerPrefs.SetString(SelectedCountryKey, country);
        PlayerPrefs.Save();
    }

    public static bool TryGetSelectedCountry(out string country)
    {
        EnsureLoaded();
        country = _cachedCountry;
        return !string.IsNullOrWhiteSpace(country);
    }

    public static void ClearSelectedCountry()
    {
        _cachedCountry = string.Empty;
        _loaded = true;
        PlayerPrefs.DeleteKey(SelectedCountryKey);
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _cachedCountry = PlayerPrefs.GetString(SelectedCountryKey, string.Empty);
        _loaded = true;
    }
}

