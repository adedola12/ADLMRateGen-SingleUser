using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace ADLMRateGen.Services
{
	/// <summary>
	/// Tiny helper that serialises any POCO collection to JSON.
	/// </summary>
	/// <typeparam name="T">Model type stored inside the file</typeparam>
	public sealed class JsonDataServices<T> where T : class
	{
		private readonly string _file;          // absolute path – avoids surprises

		public JsonDataServices(string file, string defaultFile)
		{
			_file = Path.GetFullPath(file);

			// first‑run bootstrap – copy the bundled defaults if the working file is missing
			if (!File.Exists(_file) && File.Exists(defaultFile))
				File.Copy(defaultFile, _file);
		}

		public ObservableCollection<T> LoadData()
		{
			if (!File.Exists(_file))
				return new ObservableCollection<T>();

			var json = File.ReadAllText(_file);
			return JsonSerializer.Deserialize<ObservableCollection<T>>(json)!
				   ?? new ObservableCollection<T>();
		}

		public void SaveData(IEnumerable<T> list)
		{
			var json = JsonSerializer.Serialize(
				list,
				new JsonSerializerOptions { WriteIndented = true });

			File.WriteAllText(_file, json);
		}
	}
}
