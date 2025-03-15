using System.IO;
using Newtonsoft.Json;

namespace ADLMRateGen.Helpers
{
	public static class ConfigManager
	{
		private static readonly string ConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.config.json");

		public static AppConfig LoadConfig()
		{
			if (File.Exists(ConfigFilePath))
			{
				try
				{
					string json = File.ReadAllText(ConfigFilePath);
					return JsonConvert.DeserializeObject<AppConfig>(json);
				}
				catch (JsonException ex)
				{
					Console.WriteLine($"Error loading config: {ex.Message}");
					return new AppConfig();
				}
			}

			return new AppConfig();
		}

		public static void SaveConfig(AppConfig config)
		{
			try
			{
				string json = JsonConvert.SerializeObject(config, Formatting.Indented);
				File.WriteAllText(ConfigFilePath, json);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error saving config: {ex.Message}");
			}
		}
	}
}
