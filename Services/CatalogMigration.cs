using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ADLMRateGen.Services
{
	/// <summary>
	/// Brings an existing install's working price file up to the catalog shipped
	/// with the current build, without discarding prices the user set themselves.
	///
	/// Why this exists: <see cref="JsonDataServices{T}"/> only seeds the working
	/// file when it is missing, so every machine that had ever launched RateGen
	/// kept its original prices forever. A rate engine whose rates never move is
	/// the one thing the product cannot afford, but overwriting the file wholesale
	/// would throw away the user's own edits — which are usually the most valuable
	/// numbers in it.
	///
	/// The merge is three-way, against the defaults that were last applied:
	///
	///     working == baseline   -> the user never touched it, take the new default
	///     working != baseline   -> the user priced it, keep their value
	///     new item              -> add it
	///
	/// After a successful merge the applied defaults become the new baseline, so
	/// the next catalog release merges just as cleanly.
	/// </summary>
	public static class CatalogMigration
	{
		/// <summary>
		/// Bump this whenever Data\default*.json ships new prices. The stamp written
		/// beside the working file is compared against it on every launch.
		/// </summary>
		public const string CatalogVersion = "2026.08";

		public sealed class Result
		{
			public bool Ran { get; init; }
			public int Refreshed { get; init; }
			public int Preserved { get; init; }
			public int Added { get; init; }
			public string Error { get; init; }
		}

		private static readonly JsonSerializerOptions ReadOpts =
			new() { PropertyNameCaseInsensitive = true, AllowTrailingCommas = true };

		/// <summary>
		/// Merge <paramref name="defaultFile"/> into <paramref name="workingFile"/>.
		/// Safe to call on every launch: it is a no-op once the stamp matches.
		/// </summary>
		/// <param name="keyOf">
		/// Stable identity for an item. The catalog deliberately carries the same name
		/// in more than one category (19 names, 23 extra rows: "Transportation" under
		/// both Crushed Rock and Bituminous, the same plywood sizes under White, Brown
		/// and Veneer, and so on), so this must combine name, unit and category. Rows
		/// that still collide are matched in the order they appear.
		/// </param>
		/// <param name="priceOf">Reads the price off an item.</param>
		/// <param name="setPrice">Writes a price onto an item.</param>
		/// <param name="seedBaselineFile">
		/// Read-only v1 snapshot bundled with the app, used as the baseline the first
		/// time an install migrates. Everything the migration writes goes beside the
		/// working file, never into the install directory, which is typically
		/// read-only under Program Files.
		/// </param>
		public static Result Run<T>(
			string workingFile,
			string defaultFile,
			string seedBaselineFile,
			Func<T, string> keyOf,
			Func<T, decimal> priceOf,
			Action<T, decimal> setPrice) where T : class
		{
			try
			{
				workingFile = Path.GetFullPath(workingFile);
				var stampFile = workingFile + ".catalogversion";
				var baselineFile = workingFile + ".baseline";

				// Nothing to do if this install is already on the shipped catalog.
				if (File.Exists(stampFile) &&
					string.Equals(File.ReadAllText(stampFile).Trim(), CatalogVersion,
								  StringComparison.OrdinalIgnoreCase))
					return new Result { Ran = false };

				// No working file yet: JsonDataServices will seed it from defaults.
				// Just lay down the baseline and stamp so the next release merges.
				if (!File.Exists(workingFile))
				{
					StampOnly(defaultFile, baselineFile, stampFile);
					return new Result { Ran = false };
				}

				if (!File.Exists(defaultFile))
					return new Result { Ran = false, Error = "shipped catalog not found" };

				var working = Read<T>(workingFile);
				var shipped = Read<T>(defaultFile);
				if (working == null || shipped == null || shipped.Count == 0)
					return new Result { Ran = false, Error = "catalog could not be parsed" };

				// The baseline is the catalog this install was last seeded/merged from.
				// On the very first migration it is the v1 snapshot bundled with the app.
				var baselineSource = File.Exists(baselineFile) ? baselineFile
								   : File.Exists(seedBaselineFile) ? seedBaselineFile
								   : null;
				var baseline = baselineSource != null ? Read<T>(baselineSource) : null;

				var baseByKey = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
				if (baseline != null)
					foreach (var (k, b) in Ordinal(baseline, keyOf))
						baseByKey[k] = priceOf(b);

				var workByKey = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
				foreach (var (k, w) in Ordinal(working, keyOf))
					workByKey[k] = w;

				int refreshed = 0, preserved = 0, added = 0;
				var merged = new List<T>(working);

				foreach (var (key, s) in Ordinal(shipped, keyOf))
				{

					if (!workByKey.TryGetValue(key, out var mine))
					{
						merged.Add(s);          // new item in this release
						added++;
						continue;
					}

					// Without a baseline we cannot tell an edit from an untouched
					// default, so the conservative choice is to keep the user's value.
					if (!baseByKey.TryGetValue(key, out var was)) { preserved++; continue; }

					if (priceOf(mine) == was)
					{
						setPrice(mine, priceOf(s));
						refreshed++;
					}
					else
					{
						preserved++;           // user priced this one, leave it alone
					}
				}

				WriteAtomic(workingFile, merged);
				File.Copy(defaultFile, baselineFile, overwrite: true);
				File.WriteAllText(stampFile, CatalogVersion);

				return new Result
				{
					Ran = true,
					Refreshed = refreshed,
					Preserved = preserved,
					Added = added
				};
			}
			catch (Exception ex)
			{
				// A failed migration must never stop the app from opening — the user
				// keeps their existing prices and we try again next launch.
				return new Result { Ran = false, Error = ex.Message };
			}
		}

		private static void StampOnly(string defaultFile, string baselineFile, string stampFile)
		{
			if (File.Exists(defaultFile))
				File.Copy(defaultFile, baselineFile, overwrite: true);
			File.WriteAllText(stampFile, CatalogVersion);
		}

		/// <summary>
		/// Pairs each item with its key, suffixed by how many times that key has already
		/// been seen. Keeps genuinely duplicated rows distinct and matched in order,
		/// instead of collapsing them onto one entry that silently never updates.
		/// </summary>
		private static IEnumerable<(string Key, T Item)> Ordinal<T>(
			IEnumerable<T> items, Func<T, string> keyOf)
		{
			var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			foreach (var item in items)
			{
				var baseKey = keyOf(item);
				if (string.IsNullOrWhiteSpace(baseKey)) continue;
				seen.TryGetValue(baseKey, out var n);
				seen[baseKey] = n + 1;
				yield return ($"{baseKey}#{n}", item);
			}
		}

		private static List<T> Read<T>(string path)
		{
			var json = File.ReadAllText(path);
			return JsonSerializer.Deserialize<List<T>>(json, ReadOpts);
		}

		private static void WriteAtomic<T>(string path, List<T> items)
		{
			var json = JsonSerializer.Serialize(items,
				new JsonSerializerOptions { WriteIndented = true });

			var tmp = path + ".tmp";
			File.WriteAllText(tmp, json);

			// Keep the pre-merge file recoverable rather than replacing in place.
			var backup = path + ".bak";
			if (File.Exists(backup)) File.Delete(backup);
			File.Replace(tmp, path, backup);
		}
	}
}
