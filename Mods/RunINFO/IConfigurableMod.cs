using System;
using System.Collections.Generic;

namespace MiniMotorwaysModLoader
{
    /// <summary>
    /// interface for mods that want to expose configurable settings through ModManager
    /// </summary>
    public interface IConfigurableMod
    {
        /// <summary>
        /// get all config entries for this mod
        /// </summary>
        List<ModConfigEntry> GetConfigEntries();
        
        /// <summary>
        /// called when a config value changes
        /// </summary>
        void OnConfigChanged(string key, object value);
        
        /// <summary>
        /// Load saved config values from storage
        /// </summary>
        void LoadConfig(Dictionary<string, string> savedValues);
    }
    
    /// <summary>
    /// represents a single configurable setting
    /// </summary>
    public class ModConfigEntry
    {
        /// <summary>unique key for this setting (e.g., "show_efficiency")</summary>
        public string Key { get; set; }
        
        /// <summary>display name shown in UI (e.g., "Show Efficiency")</summary>
        public string DisplayName { get; set; }
        
        /// <summary>description/tooltip for the setting</summary>
        public string Description { get; set; }
        
        /// <summary>type of config control</summary>
        public ConfigType Type { get; set; }
        
        /// <summary>default value</summary>
        public object DefaultValue { get; set; }
        
        /// <summary>current value</summary>
        public object CurrentValue { get; set; }
        
        /// <summary>minimum value (for Int/Float types)</summary>
        public object MinValue { get; set; }
        
        /// <summary>maximum value (for Int/Float types)</summary>
        public object MaxValue { get; set; }
        
        /// <summary>options for Enum/Dropdown type</summary>
        public string[] EnumOptions { get; set; }
        
        /// <summary>
        /// create a boolean toggle config
        /// </summary>
        public static ModConfigEntry CreateBool(string key, string displayName, bool defaultValue, string description = "")
        {
            return new ModConfigEntry
            {
                Key = key,
                DisplayName = displayName,
                Description = description,
                Type = ConfigType.Bool,
                DefaultValue = defaultValue,
                CurrentValue = defaultValue
            };
        }
        
        /// <summary>
        /// create an integer config with optional range
        /// </summary>
        public static ModConfigEntry CreateInt(string key, string displayName, int defaultValue, int min = int.MinValue, int max = int.MaxValue, string description = "")
        {
            return new ModConfigEntry
            {
                Key = key,
                DisplayName = displayName,
                Description = description,
                Type = ConfigType.Int,
                DefaultValue = defaultValue,
                CurrentValue = defaultValue,
                MinValue = min,
                MaxValue = max
            };
        }
        
        /// <summary>
        /// create a float config with optional range
        /// </summary>
        public static ModConfigEntry CreateFloat(string key, string displayName, float defaultValue, float min = float.MinValue, float max = float.MaxValue, string description = "")
        {
            return new ModConfigEntry
            {
                Key = key,
                DisplayName = displayName,
                Description = description,
                Type = ConfigType.Float,
                DefaultValue = defaultValue,
                CurrentValue = defaultValue,
                MinValue = min,
                MaxValue = max
            };
        }
        
        /// <summary>
        /// create a text input config
        /// </summary>
        public static ModConfigEntry CreateString(string key, string displayName, string defaultValue, string description = "")
        {
            return new ModConfigEntry
            {
                Key = key,
                DisplayName = displayName,
                Description = description,
                Type = ConfigType.String,
                DefaultValue = defaultValue,
                CurrentValue = defaultValue
            };
        }
        
        /// <summary>
        /// create a dropdown/enum config
        /// </summary>
        public static ModConfigEntry CreateEnum(string key, string displayName, string[] options, int defaultIndex = 0, string description = "")
        {
            return new ModConfigEntry
            {
                Key = key,
                DisplayName = displayName,
                Description = description,
                Type = ConfigType.Enum,
                DefaultValue = defaultIndex,
                CurrentValue = defaultIndex,
                EnumOptions = options
            };
        }
        
        /// <summary>
        /// get current value as bool
        /// </summary>
        public bool GetBool() => CurrentValue is bool b ? b : bool.TryParse(CurrentValue?.ToString(), out var result) && result;
        
        /// <summary>
        /// get current value as int
        /// </summary>
        public int GetInt() => CurrentValue is int i ? i : int.TryParse(CurrentValue?.ToString(), out var result) ? result : 0;
        
        /// <summary>
        /// get current value as float
        /// </summary>
        public float GetFloat() => CurrentValue is float f ? f : float.TryParse(CurrentValue?.ToString(), out var result) ? result : 0f;
        
        /// <summary>
        /// get current value as string
        /// </summary>
        public string GetString() => CurrentValue?.ToString() ?? "";
        
        /// <summary>
        /// serialize value to string for storage
        /// </summary>
        public string Serialize()
        {
            return CurrentValue?.ToString() ?? DefaultValue?.ToString() ?? "";
        }
        
        /// <summary>
        /// deserialize value from string storage
        /// </summary>
        public void Deserialize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                CurrentValue = DefaultValue;
                return;
            }
            
            switch (Type)
            {
                case ConfigType.Bool:
                    CurrentValue = bool.TryParse(value, out var boolVal) ? boolVal : DefaultValue;
                    break;
                case ConfigType.Int:
                    CurrentValue = int.TryParse(value, out var intVal) ? intVal : DefaultValue;
                    break;
                case ConfigType.Float:
                    CurrentValue = float.TryParse(value, out var floatVal) ? floatVal : DefaultValue;
                    break;
                case ConfigType.String:
                    CurrentValue = value;
                    break;
                case ConfigType.Enum:
                    CurrentValue = int.TryParse(value, out var enumVal) ? enumVal : DefaultValue;
                    break;
            }
        }
    }
    
    /// <summary>
    /// types of config controls
    /// </summary>
    public enum ConfigType
    {
        /// <summary>toggle on/off</summary>
        Bool,
        /// <summary>integer number input</summary>
        Int,
        /// <summary>decimal number input</summary>
        Float,
        /// <summary>text input</summary>
        String,
        /// <summary>dropdown selection</summary>
        Enum
    }
}
