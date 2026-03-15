// <author>QROST</author>

using System;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;

namespace MeshyRhino.Services
{
    public class MeshySettings
    {
        [JsonProperty("api_key")]
        public string ApiKey { get; set; } = string.Empty;

        [JsonProperty("default_topology")]
        public string DefaultTopology { get; set; } = "triangle";

        [JsonProperty("default_polycount")]
        public int DefaultPolycount { get; set; } = 30000;

        [JsonProperty("default_ai_model")]
        public string DefaultAiModel { get; set; } = "latest";

        [JsonProperty("default_model_type")]
        public string DefaultModelType { get; set; } = "standard";

        [JsonProperty("default_symmetry_mode")]
        public string DefaultSymmetryMode { get; set; } = "auto";

        [JsonProperty("enable_pbr")]
        public bool EnablePbr { get; set; } = false;

        [JsonProperty("default_format")]
        public string DefaultFormat { get; set; } = "glb";

        [JsonProperty("default_placement")]
        public string DefaultPlacement { get; set; } = "mesh";

        [JsonProperty("poll_interval_ms")]
        public int PollIntervalMs { get; set; } = 3000;

        [JsonProperty("api_retry_count")]
        public int ApiRetryCount { get; set; } = 2;
    }

    public static class MeshySettingsService
    {
        private static readonly string _settingsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MeshyRhino");

        private static readonly string _settingsFile = Path.Combine(_settingsFolder, "settings.json");

        private static readonly object _lock = new object();
        private static MeshySettings _cached;

        public static MeshySettings Load()
        {
            lock (_lock)
            {
                if (_cached != null)
                    return _cached;

                if (!File.Exists(_settingsFile))
                {
                    _cached = new MeshySettings();
                    return _cached;
                }

                try
                {
                    string json = File.ReadAllText(_settingsFile);
                    _cached = JsonConvert.DeserializeObject<MeshySettings>(json) ?? new MeshySettings();
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning(
                        $"[Meshy Rhino] Failed to load settings from {_settingsFile}: {ex.Message}. Using defaults.");
                    _cached = new MeshySettings();
                }

                return _cached;
            }
        }

        public static void Save(MeshySettings settings)
        {
            lock (_lock)
            {
                _cached = settings;

                if (!Directory.Exists(_settingsFolder))
                    Directory.CreateDirectory(_settingsFolder);

                string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(_settingsFile, json);
            }
        }

        public static bool HasApiKey()
        {
            var settings = Load();
            return !string.IsNullOrWhiteSpace(settings.ApiKey)
                && settings.ApiKey.StartsWith("msy_");
        }

        public static void ClearCache()
        {
            lock (_lock)
            {
                _cached = null;
            }
        }
    }
}
