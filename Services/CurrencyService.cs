using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace ADLMRateGen.Services
{
	/// <summary>
	/// Global, observable store for the currently‑selected currency and the
	/// NGN → Currency exchange rate.  
	/// All pricing code should multiply NGN base prices by <see cref="Rate"/>.
	/// </summary>
	public sealed class CurrencyService : INotifyPropertyChanged
	{
		/* ────────── Singleton ────────── */
		private static readonly Lazy<CurrencyService> _lazy =
			new(() => new CurrencyService());

		public static CurrencyService Instance => _lazy.Value;

		private CurrencyService()
		{
			_ = RefreshRateAsync();    // prime with NGN → NGN   (=1)
		}

		/* ────────── Public API ────────── */

		/// <summary>ISO‑4217 code currently in use (NGN / USD / EUR …).</summary>
		public string Code
		{
			get => _code;
			set
			{
				if (string.IsNullOrWhiteSpace(value) || value.Equals(_code, StringComparison.OrdinalIgnoreCase))
					return;

				_code = value.ToUpperInvariant();
				OnPropertyChanged();
				_ = RefreshRateAsync();          // fire‑and‑forget download
			}
		}

		/// <summary>
		///   How many <see cref="Code"/> you get for 1 NGN  
		///   (e.g. NGN→USD ≈ 0.00083).  
		///   Bind UI elements to this; it notifies on change.
		/// </summary>
		public double Rate
		{
			get => _rate;
			private set
			{
				if (Math.Abs(value - _rate) < 1e-12) return;
				_rate = value;
				OnPropertyChanged();
			}
		}

		/// <summary>❗ TEMP shim so older code/XAML that still binds to
		///  <c>CurrencyService.Selected</c> keeps compiling.  Prefer <see cref="Code"/>.</summary>
		public string Selected
		{
			get => Code;
			set => Code = value;        // just forward to the new property
		}


		/// <summary>Currencies you expose in the header picker.</summary>
		public IReadOnlyList<string> Supported { get; } =
			new[] { "NGN", "USD", "EUR", "QAR", "GHS", "ZAR" };

		/// <summary>Utility – convert a NGN amount to the selected currency.</summary>
		public double FromNgn(double amountNgn) => amountNgn * Rate;

		/* ────────── INotifyPropertyChanged ────────── */

		public event PropertyChangedEventHandler? PropertyChanged;
		private void OnPropertyChanged([CallerMemberName] string? name = null) =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

		/* ────────── Fields ────────── */

		private string _code = "NGN";
		private double _rate = 1.0;

		/* ────────── Internals ────────── */

		private async Task RefreshRateAsync()
		{
			if (Code == "NGN") { Rate = 1.0; return; }

			try
			{
				using var http = new HttpClient();
				var uri = $"https://api.exchangerate.host/latest?base=NGN&symbols={Code}";
				var json = await http.GetStringAsync(uri);

				using var doc = JsonDocument.Parse(json);
				var fetched = doc.RootElement
								 .GetProperty("rates")
								 .GetProperty(Code)
								 .GetDouble();

				Rate = fetched == 0 ? FallbackRate(Code) : fetched;
			}
			catch   // offline or API down → fall back to hard‑coded guess
			{
				Rate = FallbackRate(Code);
			}
		}

		// 2️⃣  only used when the live call fails (e.g. offline)
		private static double FallbackRate(string code) => code switch
		{
			// old ones
			"USD" => 1.0 / 1600.0,
			"EUR" => 1.0 / 1750.0,

			// new currencies – *very* rough guesses, tweak if you like
			"QAR" => 1.0 / 6000.0,
			"GHS" => 1.0 / 130.0,
			"ZAR" => 1.0 / 90.0,

			_ => 1.0          // NGN or unknown
		};
	}
}
