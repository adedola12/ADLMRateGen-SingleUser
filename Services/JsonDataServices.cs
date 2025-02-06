using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace ADLMRateGen.Services
{
    public class JsonDataServices
    {
        private readonly string _filePath;
        private readonly string _defaultFilePath;

        public JsonDataServices(string filePath, string defaultFilePath)
        {
            _filePath = filePath;
            _defaultFilePath = defaultFilePath;
        }

        public T? LoadData<T>()
        {
            // If the working file doesn't exist, copy the default file.
            if (!File.Exists(_filePath))
            {
                if (File.Exists(_defaultFilePath))
                {
                    File.Copy(_defaultFilePath, _filePath);
                }
            }

            string json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                if (File.Exists(_defaultFilePath))
                {
                    json = File.ReadAllText(_defaultFilePath);
                    File.WriteAllText(_filePath, json);
                }
            }
            return JsonSerializer.Deserialize<T>(json);
        }

        public void SaveData<T>(T data)
        {
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
    }
}
