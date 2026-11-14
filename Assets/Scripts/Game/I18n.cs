// AI-assisted code (generated with Claude Code) — see README.
using System.Collections.Generic;
using LunarOutpost.Sim;
using UnityEngine;

namespace LunarOutpost.Game
{
    /// <summary>All on-screen text comes from Resources/i18n/{lang}.json.</summary>
    public static class I18n
    {
        static Dictionary<string, object> _table;
        public static string Lang { get; private set; }

        public static void Load(string lang)
        {
            var asset = Resources.Load<TextAsset>($"i18n/{lang}");
            if (asset == null) throw new MissingReferenceException($"i18n/{lang}.json is missing");
            _table = (Dictionary<string, object>)MiniJson.Parse(asset.text);
            Lang = lang;
        }

        public static void Toggle() => Load(Lang == "en" ? "ja" : "en");

        public static string T(string key, params object[] args)
        {
            if (!_table.TryGetValue(key, out var v)) throw new KeyNotFoundException($"i18n/{Lang}.json has no key '{key}'");
            return args.Length == 0 ? (string)v : string.Format((string)v, args);
        }
    }
}
