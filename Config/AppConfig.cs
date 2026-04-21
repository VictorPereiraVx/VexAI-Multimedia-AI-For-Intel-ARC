using Newtonsoft.Json;

namespace VexAI.Config
{
    public class AppConfig
    {
        public string SdNextUrl { get; set; } = "http://127.0.0.1:7860";
        public string SdNextBatPath { get; set; } = "";
        public string RvcFolderPath { get; set; } = "";
        public string DeepLiveFastPath { get; set; } = "";
        public string DeepLiveEnhancedPath { get; set; } = "";
        public string WatermarkImagePath { get; set; } = "";
        public string OutputFolder { get; set; } = "";
        public bool AutoStartSdNext { get; set; } = true;

        private static readonly string ConfigPath = Path.Combine(AppContext.BaseDirectory, "config.json");

        public static AppConfig Load()
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
            }
            return new AppConfig();
        }

        public void Save()
        {
            File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        public bool IsConfigured()
        {
            return !string.IsNullOrWhiteSpace(RvcFolderPath)
                && !string.IsNullOrWhiteSpace(DeepLiveFastPath)
                && !string.IsNullOrWhiteSpace(OutputFolder);
        }
    }
}
