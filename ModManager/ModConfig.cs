using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ModManagerMod
{
    /// <summary>
    /// Configuration for ModManager - stores disabled mods and per-mod settings
    /// </summary>
    [Serializable]
    public class ModConfig
    {
        public List<string> disabledMods = new List<string>();
        
        /// <summary>Per-mod settings: modName -> (key -> value)</summary>
        public Dictionary<string, Dictionary<string, string>> modSettings = new Dictionary<string, Dictionary<string, string>>();
        
        private static string ConfigPath => Path.Combine(Application.dataPath, "Mods", "ModManager.json");
        
        /// <summary>
        /// Load configuration from file
        /// </summary>
        public static ModConfig Load()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    Debug.Log("[ModManager] No config file found, creating new one");
                    return new ModConfig();
                }
                
                string json = File.ReadAllText(ConfigPath);
                ModConfig config = new ModConfig();
                
                int disabledStart = json.IndexOf("\"disabledMods\"");
                if (disabledStart >= 0)
                {
                    int arrayStart = json.IndexOf("[", disabledStart);
                    int arrayEnd = json.IndexOf("]", arrayStart);
                    if (arrayStart >= 0 && arrayEnd > arrayStart)
                    {
                        string arrayContent = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
                        string[] entries = arrayContent.Split(',');
                        foreach (var entry in entries)
                        {
                            string trimmed = entry.Trim().Trim('"', ' ', '\n', '\r');
                            if (!string.IsNullOrEmpty(trimmed))
                            {
                                config.disabledMods.Add(trimmed);
                            }
                        }
                    }
                }
                
                int settingsStart = json.IndexOf("\"modSettings\"");
                if (settingsStart >= 0)
                {
                    int outerBrace = json.IndexOf("{", settingsStart);
                    if (outerBrace >= 0)
                    {
                        int braceCount = 1;
                        int pos = outerBrace + 1;
                        while (pos < json.Length && braceCount > 0)
                        {
                            if (json[pos] == '{') braceCount++;
                            else if (json[pos] == '}') braceCount--;
                            pos++;
                        }
                        
                        string settingsJson = json.Substring(outerBrace, pos - outerBrace);
                        config.ParseModSettings(settingsJson);
                    }
                }
                
                Debug.Log($"[ModManager] Loaded config: {config.disabledMods.Count} disabled mods, {config.modSettings.Count} mod configs");
                return config;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModManager] Error loading config: {ex.Message}");
                return new ModConfig();
            }
        }
        
        private void ParseModSettings(string json)
        {
            int pos = 1;
            while (pos < json.Length - 1)
            {
                int nameStart = json.IndexOf('"', pos);
                if (nameStart < 0) break;
                
                int nameEnd = json.IndexOf('"', nameStart + 1);
                if (nameEnd < 0) break;
                
                string modName = json.Substring(nameStart + 1, nameEnd - nameStart - 1);
                
                // look for the mod's settings object
                int objStart = json.IndexOf('{', nameEnd);
                if (objStart < 0) break;
                
                int braceCount = 1;
                int objEnd = objStart + 1;
                while (objEnd < json.Length && braceCount > 0)
                {
                    if (json[objEnd] == '{') braceCount++;
                    else if (json[objEnd] == '}') braceCount--;
                    objEnd++;
                }
                
                string modJson = json.Substring(objStart + 1, objEnd - objStart - 2);
                var settings = new Dictionary<string, string>();
                
                var kvPairs = modJson.Split(',');
                foreach (var pair in kvPairs)
                {
                    var parts = pair.Split(':');
                    if (parts.Length >= 2)
                    {
                        string key = parts[0].Trim().Trim('"', ' ');
                        string value = parts[1].Trim().Trim('"', ' ');
                        if (!string.IsNullOrEmpty(key))
                        {
                            settings[key] = value;
                        }
                    }
                }
                
                if (settings.Count > 0)
                {
                    modSettings[modName] = settings;
                }
                
                pos = objEnd;
            }
        }
        
        /// <summary>
        /// Save configuration to file
        /// </summary>
        public void Save()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("{");
                
                // disabledMods
                sb.AppendLine("  \"disabledMods\": [");
                for (int i = 0; i < disabledMods.Count; i++)
                {
                    sb.Append($"    \"{disabledMods[i]}\"");
                    if (i < disabledMods.Count - 1) sb.Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("  ],");
                
                // modSettings
                sb.AppendLine("  \"modSettings\": {");
                int modIndex = 0;
                foreach (var mod in modSettings)
                {
                    sb.AppendLine($"    \"{mod.Key}\": {{");
                    int settingIndex = 0;
                    foreach (var setting in mod.Value)
                    {
                        sb.Append($"      \"{setting.Key}\": \"{EscapeJson(setting.Value)}\"");
                        if (settingIndex < mod.Value.Count - 1) sb.Append(",");
                        sb.AppendLine();
                        settingIndex++;
                    }
                    sb.Append("    }");
                    if (modIndex < modSettings.Count - 1) sb.Append(",");
                    sb.AppendLine();
                    modIndex++;
                }
                sb.AppendLine("  }");
                sb.AppendLine("}");
                
                // does mods directory exist?
                string modsDir = Path.Combine(Application.dataPath, "Mods");
                if (!Directory.Exists(modsDir))
                {
                    Directory.CreateDirectory(modsDir);
                }
                
                using (var writer = new StreamWriter(ConfigPath, false))
                {
                    writer.Write(sb.ToString());
                }
                Debug.Log($"[ModManager] Config saved: {disabledMods.Count} disabled mods, {modSettings.Count} mod configs");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModManager] Error saving config: {ex.Message}");
            }
        }
        
        private string EscapeJson(string value)
        {
            return value?.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r") ?? "";
        }
        
        /// <summary>
        /// Check if a mod DLL is disabled
        /// </summary>
        public bool IsModDisabled(string dllPath)
        {
            string fileName = Path.GetFileName(dllPath);
            return disabledMods.Contains(fileName);
        }
        
        /// <summary>
        /// Enable or disable a mod
        /// </summary>
        public void SetModEnabled(string dllPath, bool enabled)
        {
            string fileName = Path.GetFileName(dllPath);
            
            if (enabled)
            {
                disabledMods.Remove(fileName);
            }
            else
            {
                if (!disabledMods.Contains(fileName))
                {
                    disabledMods.Add(fileName);
                }
            }
        }
        
        /// <summary>
        /// Get all settings for a mod
        /// </summary>
        public Dictionary<string, string> GetModSettings(string modName)
        {
            if (modSettings.TryGetValue(modName, out var settings))
            {
                return new Dictionary<string, string>(settings);
            }
            return new Dictionary<string, string>();
        }
        
        /// <summary>
        /// Get a specific setting for a mod
        /// </summary>
        public string GetModSetting(string modName, string key, string defaultValue = "")
        {
            if (modSettings.TryGetValue(modName, out var settings))
            {
                if (settings.TryGetValue(key, out var value))
                {
                    return value;
                }
            }
            return defaultValue;
        }
        
        /// <summary>
        /// Set a specific setting for a mod
        /// </summary>
        public void SetModSetting(string modName, string key, string value)
        {
            if (!modSettings.ContainsKey(modName))
            {
                modSettings[modName] = new Dictionary<string, string>();
            }
            modSettings[modName][key] = value;
        }
        
        /// <summary>
        /// Set all settings for a mod at once
        /// </summary>
        public void SetModSettings(string modName, Dictionary<string, string> settings)
        {
            modSettings[modName] = new Dictionary<string, string>(settings);
        }
    }
}
