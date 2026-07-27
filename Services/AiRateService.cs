using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdlmAi;
using ADLMRateGen.Helpers;
using ADLMRateGen.ViewModel.CustomRate;

namespace ADLMRateGen.Services
{
	/// <summary>
	/// Bridge between the ADLM AI Service and RateGen's CustomRate model.
	/// Given a plain-language work-item description it returns a ready
	/// CustomRate; every line is tagged [AI] unless it was priced from the
	/// RateGen library on the server.
	///
	/// Availability rules:
	///  - Hidden entirely until ADLM_AI_URL is configured.
	///  - Auth uses the signed-in user's cached licence token (which carries
	///    the `ai` add-on entitlement); ADLM_AI_TOKEN overrides for dev/test.
	///  - Never throws for AI availability problems — check
	///    <see cref="AiRateResult.Status"/> and show <see cref="AiRateResult.Message"/>.
	/// </summary>
	public sealed class AiRateService
	{
		private static readonly Lazy<AiRateService> _instance = new(() => new AiRateService());
		public static AiRateService Instance => _instance.Value;

		private readonly AdlmAiClient? _client;

		private AiRateService()
		{
			var url = AppEnvironment.AiServiceUrl;
			if (!string.IsNullOrWhiteSpace(url))
			{
				_client = new AdlmAiClient(new AdlmAiOptions
				{
					BaseUrl = url!,
					TokenProvider = ResolveToken,
					Product = AppEnvironment.ProductKey, // "rategen"
				});
			}
		}

		private static string ResolveToken()
		{
			var overrideTok = AppEnvironment.AiServiceTokenOverride;
			if (!string.IsNullOrWhiteSpace(overrideTok)) return overrideTok!;
			try
			{
				return AuthProvider.Instance.Client.GetCachedLicenseToken() ?? string.Empty;
			}
			catch
			{
				return string.Empty;
			}
		}

		/// <summary>True when ADLM_AI_URL is configured (controls UI visibility).</summary>
		public bool IsConfigured => _client != null;

		public async Task<AiRateResult> BuildRateAsync(
			string description,
			string? unit = null,
			string? zone = null,
			IProgress<string>? progress = null,
			CancellationToken ct = default)
		{
			if (_client == null)
			{
				return AiRateResult.Fail(AiStatus.Unavailable,
					"AI is not configured. Set ADLM_AI_URL (and sign in, or set ADLM_AI_TOKEN).");
			}
			if (string.IsNullOrWhiteSpace(description))
			{
				return AiRateResult.Fail(AiStatus.BadRequest, "Describe the work item first.");
			}

			var res = await _client.RateBuildupAsync(description, zone, unit, progress, ct)
				.ConfigureAwait(false);

			if (!res.IsSuccess || res.Value == null)
			{
				return AiRateResult.Fail(res.Status, res.Message ?? "AI request failed.");
			}

			return new AiRateResult
			{
				Status = res.Status,
				Message = res.Message,
				Disclaimer = res.Disclaimer,
				Confidence = res.Audit?.Confidence,
				Model = res.Audit?.Model,
				Rate = MapToCustomRate(description, res.Value),
			};
		}

		/// <summary>
		/// Maps the AI build-up onto RateGen's CustomRate. Plant lines go into
		/// MaterialItems (RateGen has no plant category), tagged "(plant)".
		/// Per line the assignment order matters: RateType, Description, and
		/// Unit each trigger ResolveUnitPrice() against the local library, so
		/// the server-priced value is assigned to UnitPrice LAST and wins.
		/// </summary>
		private static CustomRate MapToCustomRate(string description, RateBuildup build)
		{
			var rate = new CustomRate
			{
				Title = Truncate(description, 60),
				Description = string.IsNullOrWhiteSpace(build.Notes)
					? description
					: $"{description} — {build.Notes}",
				OverheadPercent = (decimal)build.OverheadPercent,
				ProfitPercent = (decimal)build.ProfitPercent,
			};

			foreach (var c in build.Components ?? Enumerable.Empty<RateComponent>())
			{
				var isLabour = string.Equals(c.Kind, "labour", StringComparison.OrdinalIgnoreCase);
				var tag = c.Source == "library" ? "" : " [AI]";
				var plantTag = string.Equals(c.Kind, "plant", StringComparison.OrdinalIgnoreCase)
					? " (plant)" : "";

				var item = new RateEntryItem
				{
					RateType = isLabour ? RateItemType.Labour : RateItemType.Material,
					Description = $"{c.Name}{plantTag}{tag}",
					Quantity = (decimal)c.Quantity,
					Unit = c.Unit,
				};
				// Last — overrides whatever ResolveUnitPrice found locally.
				item.UnitPrice = (decimal)c.UnitPriceNgn;

				if (isLabour) rate.LabourItems.Add(item);
				else rate.MaterialItems.Add(item);
			}

			return rate;
		}

		private static string Truncate(string s, int max) =>
			s.Length <= max ? s : s.Substring(0, max - 1) + "…";
	}

	/// <summary>Outcome of an AI rate build — status-first, never throws.</summary>
	public sealed class AiRateResult
	{
		public AiStatus Status { get; set; }
		public string? Message { get; set; }
		public string? Disclaimer { get; set; }
		public double? Confidence { get; set; }
		public string? Model { get; set; }
		public CustomRate? Rate { get; set; }
		public bool IsSuccess => Status == AiStatus.Success || Status == AiStatus.CachedFallback;

		public static AiRateResult Fail(AiStatus status, string message) =>
			new() { Status = status, Message = message };
	}
}
