using ADLMRateGen.ViewModel;
using ADLMRateGen.ViewModel.CustomRate;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ADLMRateGen.Services
{
    public sealed class UserRatesCloudSync
    {
        private static readonly JsonSerializerOptions ReadJsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly JsonSerializerOptions WriteJsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static UserRatesCloudSync Instance { get; } = new UserRatesCloudSync();

        private readonly SemaphoreSlim _syncLock = new SemaphoreSlim(1, 1);

        public DateTime? LastSyncUtc { get; private set; }
        public string LastStatus { get; private set; } = "User rates cloud sync not started.";

        private UserRatesCloudSync() { }

        public async Task<bool> PushSnapshotAsync(MainViewModel vm, CancellationToken ct = default)
        {
            if (vm == null)
            {
                LastStatus = "User rates sync skipped: main view model is unavailable.";
                return false;
            }

            var auth = AuthProvider.Instance.Client;
            if (!auth.HasSession || string.IsNullOrWhiteSpace(auth.AccessToken))
            {
                LastStatus = "User rates sync skipped: not signed in.";
                return false;
            }

            await _syncLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var snapshot = BuildSnapshot(vm);
                var serverState = await GetServerStateAsync(auth, ct).ConfigureAwait(false);

                using var http = CreateHttpClient();

                var overrideSync = await SyncRateOverridesAsync(
                    auth,
                    http,
                    snapshot.RateOverrides,
                    serverState.RateOverrides,
                    ct).ConfigureAwait(false);

                var customSync = await SyncCustomRatesAsync(
                    auth,
                    http,
                    snapshot.CustomRates,
                    serverState.CustomRates,
                    ct).ConfigureAwait(false);

                LastSyncUtc = DateTime.Now;
                LastStatus =
                    $"User rates synced: {overrideSync.Upserted} overrides updated, {overrideSync.Deleted} removed, " +
                    $"{customSync.Upserted} custom rates updated, {customSync.Deleted} removed.";

                return true;
            }
            catch (Exception ex)
            {
                LastStatus = $"User rates sync failed: {ex.Message}";
                return false;
            }
            finally
            {
                _syncLock.Release();
            }
        }

        private static async Task<ServerLibraryState> GetServerStateAsync(
            ADLMRateGen.ADLM.Auth.AuthClient auth,
            CancellationToken ct)
        {
            using var doc = await auth.GetJsonAsync("/rategen/library", ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<ServerLibraryState>(
                       doc.RootElement.GetRawText(),
                       ReadJsonOptions)
                   ?? new ServerLibraryState();
        }

        private static async Task<SyncCounters> SyncRateOverridesAsync(
            ADLMRateGen.ADLM.Auth.AuthClient auth,
            HttpClient http,
            IReadOnlyList<RateOverridePayload> localOverrides,
            IReadOnlyList<RateOverridePayload> serverOverrides,
            CancellationToken ct)
        {
            var masterRateIds = BuildMasterRateLookup();

            var loadedSections = new HashSet<string>(
                localOverrides
                    .Select(item => NormalizeText(item.SectionKey).ToLowerInvariant())
                    .Where(section => !string.IsNullOrWhiteSpace(section)),
                StringComparer.OrdinalIgnoreCase);

            var localKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var upserted = 0;
            var deleted = 0;

            foreach (var local in localOverrides)
            {
                var key = BuildRateIdentityKey(local);
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                localKeys.Add(key);
                var server = FindServerOverride(serverOverrides, key);

                local.RateId = ResolveRateId(local, server, masterRateIds);
                local.ClientUpdatedAt = DateTime.Now;

                if (server != null && AreEquivalent(local, server))
                {
                    continue;
                }

                await SendJsonAsync(
                    auth,
                    http,
                    HttpMethod.Put,
                    $"/rategen-v2/library/user-rates/override/{Uri.EscapeDataString(local.RateId)}",
                    local,
                    ct).ConfigureAwait(false);

                upserted += 1;
            }

            foreach (var server in serverOverrides ?? Array.Empty<RateOverridePayload>())
            {
                var key = BuildRateIdentityKey(server);
                if (string.IsNullOrWhiteSpace(key) || localKeys.Contains(key))
                {
                    continue;
                }

                var sectionKey = NormalizeText(server.SectionKey).ToLowerInvariant();
                if (!loadedSections.Contains(sectionKey))
                {
                    continue;
                }

                var rateId = !string.IsNullOrWhiteSpace(server.RateId)
                    ? server.RateId
                    : BuildFallbackRateId(key);

                await SendJsonAsync(
                    auth,
                    http,
                    HttpMethod.Delete,
                    $"/rategen-v2/library/user-rates/override/{Uri.EscapeDataString(rateId)}",
                    null,
                    ct).ConfigureAwait(false);

                deleted += 1;
            }

            return new SyncCounters(upserted, deleted);
        }

        private static async Task<SyncCounters> SyncCustomRatesAsync(
            ADLMRateGen.ADLM.Auth.AuthClient auth,
            HttpClient http,
            IReadOnlyList<CustomRatePayload> localCustomRates,
            IReadOnlyList<CustomRatePayload> serverCustomRates,
            CancellationToken ct)
        {
            var serverById = (serverCustomRates ?? Array.Empty<CustomRatePayload>())
                .Where(rate => !string.IsNullOrWhiteSpace(rate.CustomRateId))
                .GroupBy(rate => rate.CustomRateId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var localIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var upserted = 0;
            var deleted = 0;

            foreach (var local in localCustomRates)
            {
                if (string.IsNullOrWhiteSpace(local.CustomRateId))
                {
                    continue;
                }

                localIds.Add(local.CustomRateId);
                serverById.TryGetValue(local.CustomRateId, out var server);

                if (server != null && AreEquivalent(local, server))
                {
                    continue;
                }

                if (local.CreatedAt == default)
                {
                    local.CreatedAt = DateTime.Now;
                }

                local.UpdatedAt = DateTime.Now;

                await SendJsonAsync(
                    auth,
                    http,
                    HttpMethod.Put,
                    $"/rategen-v2/library/custom-rates/{Uri.EscapeDataString(local.CustomRateId)}",
                    local,
                    ct).ConfigureAwait(false);

                upserted += 1;
            }

            foreach (var server in serverCustomRates ?? Array.Empty<CustomRatePayload>())
            {
                if (string.IsNullOrWhiteSpace(server.CustomRateId) || localIds.Contains(server.CustomRateId))
                {
                    continue;
                }

                await SendJsonAsync(
                    auth,
                    http,
                    HttpMethod.Delete,
                    $"/rategen-v2/library/custom-rates/{Uri.EscapeDataString(server.CustomRateId)}",
                    null,
                    ct).ConfigureAwait(false);

                deleted += 1;
            }

            return new SyncCounters(upserted, deleted);
        }

        private static async Task SendJsonAsync(
            ADLMRateGen.ADLM.Auth.AuthClient auth,
            HttpClient http,
            HttpMethod method,
            string path,
            object? body,
            CancellationToken ct)
        {
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                using var request = new HttpRequestMessage(method, CombineUrl(auth.BaseUrl, path));
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

                if (body != null)
                {
                    var json = JsonSerializer.Serialize(body, WriteJsonOptions);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }

                using var response = await http.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        ct)
                    .ConfigureAwait(false);

                var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }

                if ((response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden) &&
                    attempt == 1)
                {
                    using var refreshDoc = await auth.GetJsonAsync("/rategen-v2/library/meta", ct).ConfigureAwait(false);
                    continue;
                }

                throw CreateHttpException(response.StatusCode, text);
            }

            throw new TimeoutException("User rate sync request timed out.");
        }

        private static HttpClient CreateHttpClient()
        {
            var http = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30),
                DefaultRequestVersion = HttpVersion.Version11,
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
            };

            http.DefaultRequestHeaders.Accept.Clear();
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            return http;
        }

        private static Exception CreateHttpException(HttpStatusCode statusCode, string text)
        {
            var message = $"HTTP {(int)statusCode}";
            if (!string.IsNullOrWhiteSpace(text))
            {
                message += $": {Trim(text, 280)}";
            }

            return statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                ? new UnauthorizedAccessException(message)
                : new InvalidOperationException(message);
        }

        private static string CombineUrl(string baseUrl, string path)
        {
            var cleanBase = (baseUrl ?? string.Empty).TrimEnd('/');
            var cleanPath = (path ?? string.Empty).TrimStart('/');
            return $"{cleanBase}/{cleanPath}";
        }

        private static RateOverridePayload? FindServerOverride(
            IReadOnlyList<RateOverridePayload> serverOverrides,
            string key)
        {
            return (serverOverrides ?? Array.Empty<RateOverridePayload>())
                .FirstOrDefault(item => string.Equals(BuildRateIdentityKey(item), key, StringComparison.OrdinalIgnoreCase));
        }

        private static UserRateSnapshot BuildSnapshot(MainViewModel vm)
        {
            var overrides = new List<RateOverridePayload>();

            foreach (var section in EnumerateSections(vm))
            {
                overrides.AddRange(BuildSectionOverrides(section.key, section.label, section.viewModel));
            }

            var customRates = CustomRateServices.LoadCustomRates()
                .Select(ToCustomRatePayload)
                .ToList();

            return new UserRateSnapshot(overrides, customRates);
        }

        private static IEnumerable<(string key, string label, object viewModel)> EnumerateSections(MainViewModel vm)
        {
            yield return (SectionKeys.Ground, "Groundwork", vm.GroundWorkViewModel);
            yield return (SectionKeys.Concrete, "Concrete Works", vm.ConcreteViewModel);
            yield return (SectionKeys.Blockwork, "Blockwork", vm.BlockworkViewModel);
            yield return (SectionKeys.Finishes, "Finishes", vm.FinishesViewModel);
            yield return (SectionKeys.Roofing, "Roofing", vm.RoofWorkViewModel);
            yield return (SectionKeys.DoorsWindows, "Windows & Doors", vm.WindowAndDoorViewModel);
            yield return (SectionKeys.Paint, "Painting", vm.PaintWorkViewModel);
            yield return (SectionKeys.Steelwork, "Steelwork", vm.SteelWorkViewModel);
            yield return (SectionKeys.CarbonOthers, "Carbon and Others", vm.CarbonOthersViewModel);
        }

        private static IEnumerable<RateOverridePayload> BuildSectionOverrides(string sectionKey, string sectionLabel, object viewModel)
        {
            var rows = FindRowsEnumerable(viewModel);
            if (rows == null)
            {
                yield break;
            }

            var overheadPercent = GetDecimalProp(viewModel, "OverheadPercent");
            var profitPercent = GetDecimalProp(viewModel, "ProfitPercent");

            foreach (var row in rows)
            {
                if (row == null)
                {
                    continue;
                }

                var description = GetStringProp(row, "Description", "Name", "Title");
                if (string.IsNullOrWhiteSpace(description))
                {
                    continue;
                }

                yield return new RateOverridePayload
                {
                    SectionKey = sectionKey,
                    SectionLabel = sectionLabel,
                    ItemNo = GetIntProp(row, "ItemNo", "SNo"),
                    Description = description.Trim(),
                    Unit = GetStringProp(row, "Unit") ?? string.Empty,
                    NetCost = GetDecimalProp(row, "NetCost"),
                    OverheadPercent = overheadPercent,
                    ProfitPercent = profitPercent,
                    OverheadValue = GetDecimalProp(row, "OverheadValue"),
                    ProfitValue = GetDecimalProp(row, "ProfitValue"),
                    TotalCost = GetDecimalProp(row, "TotalCost", "TotalPrice", "Total"),
                    Breakdown = BuildBreakdownPayload(row).ToList()
                };
            }
        }

        private static IEnumerable<BreakdownPayload> BuildBreakdownPayload(object row)
        {
            var breakdown = FindBreakdownEnumerable(row);
            if (breakdown == null)
            {
                yield break;
            }

            foreach (var line in breakdown)
            {
                if (line == null)
                {
                    continue;
                }

                var componentName = GetStringProp(line, "ComponentName", "Description", "Name", "RefName");
                var quantity = GetDecimalProp(line, "Quantity", "Qty");
                var unitPrice = GetDecimalProp(line, "UnitPrice", "Rate", "Price");
                var lineTotal = GetDecimalProp(line, "LineTotal", "TotalPrice", "Total");

                if (string.IsNullOrWhiteSpace(componentName) && quantity <= 0 && unitPrice <= 0 && lineTotal <= 0)
                {
                    continue;
                }

                yield return new BreakdownPayload
                {
                    ComponentName = componentName ?? string.Empty,
                    Quantity = quantity,
                    Unit = GetStringProp(line, "Unit") ?? string.Empty,
                    UnitPrice = unitPrice,
                    LineTotal = lineTotal,
                    RefKind = GetStringProp(line, "RefKind") ?? string.Empty,
                    RefSn = GetIntProp(line, "RefSn", "Sn"),
                    RefName = GetStringProp(line, "RefName") ?? componentName ?? string.Empty
                };
            }
        }

        private static CustomRatePayload ToCustomRatePayload(CustomRate rate)
        {
            var materials = (rate.MaterialItems ?? new List<RateEntryItem>())
                .Select(item => new CustomRateLinePayload
                {
                    RateType = "material",
                    Description = item.Description ?? string.Empty,
                    Quantity = item.Quantity,
                    Unit = item.Unit ?? string.Empty,
                    UnitPrice = item.UnitPrice,
                    TotalCost = item.TotalCost
                })
                .ToList();

            var labour = (rate.LabourItems ?? new List<RateEntryItem>())
                .Select(item => new CustomRateLinePayload
                {
                    RateType = "labour",
                    Description = item.Description ?? string.Empty,
                    Quantity = item.Quantity,
                    Unit = item.Unit ?? string.Empty,
                    UnitPrice = item.UnitPrice,
                    TotalCost = item.TotalCost
                })
                .ToList();

            return new CustomRatePayload
            {
                CustomRateId = rate.Id.ToString(),
                Title = rate.Title ?? string.Empty,
                Description = rate.Description ?? string.Empty,
                Unit = InferUnit(rate),
                Materials = materials,
                Labour = labour,
                Breakdown = BuildCustomBreakdown(materials, labour),
                NetCost = rate.OverallTotal,
                OverheadPercent = rate.OverheadPercent,
                ProfitPercent = rate.ProfitPercent,
                TotalCost = rate.GrandTotal,
                CreatedAt = rate.CreatedDate
            };
        }

        private static List<BreakdownPayload> BuildCustomBreakdown(
            IEnumerable<CustomRateLinePayload> materials,
            IEnumerable<CustomRateLinePayload> labour)
        {
            return (materials ?? Enumerable.Empty<CustomRateLinePayload>())
                .Concat(labour ?? Enumerable.Empty<CustomRateLinePayload>())
                .Select(line => new BreakdownPayload
                {
                    ComponentName = line.Description ?? string.Empty,
                    Quantity = line.Quantity,
                    Unit = line.Unit ?? string.Empty,
                    UnitPrice = line.UnitPrice,
                    LineTotal = line.TotalCost,
                    RefKind = line.RateType ?? string.Empty,
                    RefSn = line.RefSn,
                    RefName = !string.IsNullOrWhiteSpace(line.RefName) ? line.RefName : line.Description ?? string.Empty
                })
                .ToList();
        }

        private static string InferUnit(CustomRate rate)
        {
            var units = (rate.MaterialItems ?? new List<RateEntryItem>())
                .Concat(rate.LabourItems ?? new List<RateEntryItem>())
                .Select(item => item.Unit)
                .Where(unit => !string.IsNullOrWhiteSpace(unit))
                .Select(unit => unit!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return units.Count == 1 ? units[0] : string.Empty;
        }

        private static Dictionary<string, string> BuildMasterRateLookup()
        {
            return (RateLibraryStore.Items ?? Array.Empty<RateDefinition>())
                .Where(rate => !string.IsNullOrWhiteSpace(rate.Id))
                .GroupBy(rate => BuildRateIdentityKey(rate.SectionKey, rate.ItemNo, rate.Description, rate.Unit), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Id ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        }

        private static string ResolveRateId(
            RateOverridePayload local,
            RateOverridePayload? server,
            IReadOnlyDictionary<string, string> masterRateIds)
        {
            if (!string.IsNullOrWhiteSpace(server?.RateId))
            {
                return server.RateId;
            }

            var key = BuildRateIdentityKey(local);
            if (masterRateIds.TryGetValue(key, out var masterId) && !string.IsNullOrWhiteSpace(masterId))
            {
                return masterId;
            }

            return BuildFallbackRateId(key);
        }

        private static string BuildFallbackRateId(string key)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
            var hex = Convert.ToHexString(bytes).ToLowerInvariant();
            return hex.Substring(0, 24);
        }

        private static bool AreEquivalent(RateOverridePayload local, RateOverridePayload server)
        {
            var localSignature = JsonSerializer.Serialize(new
            {
                key = BuildRateIdentityKey(local),
                sectionLabel = NormalizeText(local.SectionLabel),
                netCost = Round(local.NetCost),
                overheadPercent = Round(local.OverheadPercent),
                profitPercent = Round(local.ProfitPercent),
                overheadValue = Round(local.OverheadValue),
                profitValue = Round(local.ProfitValue),
                totalCost = Round(local.TotalCost),
                breakdown = NormalizeBreakdown(local.Breakdown)
            });

            var serverSignature = JsonSerializer.Serialize(new
            {
                key = BuildRateIdentityKey(server),
                sectionLabel = NormalizeText(server.SectionLabel),
                netCost = Round(server.NetCost),
                overheadPercent = Round(server.OverheadPercent),
                profitPercent = Round(server.ProfitPercent),
                overheadValue = Round(server.OverheadValue),
                profitValue = Round(server.ProfitValue),
                totalCost = Round(server.TotalCost),
                breakdown = NormalizeBreakdown(server.Breakdown)
            });

            return string.Equals(localSignature, serverSignature, StringComparison.Ordinal);
        }

        private static bool AreEquivalent(CustomRatePayload local, CustomRatePayload server)
        {
            var localSignature = JsonSerializer.Serialize(new
            {
                id = NormalizeText(local.CustomRateId),
                sectionKey = NormalizeText(local.SectionKey).ToLowerInvariant(),
                sectionLabel = NormalizeText(local.SectionLabel),
                title = NormalizeText(local.Title),
                description = NormalizeText(local.Description),
                unit = NormalizeText(local.Unit),
                netCost = Round(local.NetCost),
                overheadPercent = Round(local.OverheadPercent),
                profitPercent = Round(local.ProfitPercent),
                totalCost = Round(local.TotalCost),
                materials = NormalizeCustomLines(local.Materials),
                labour = NormalizeCustomLines(local.Labour),
                breakdown = NormalizeBreakdown(local.Breakdown)
            });

            var serverSignature = JsonSerializer.Serialize(new
            {
                id = NormalizeText(server.CustomRateId),
                sectionKey = NormalizeText(server.SectionKey).ToLowerInvariant(),
                sectionLabel = NormalizeText(server.SectionLabel),
                title = NormalizeText(server.Title),
                description = NormalizeText(server.Description),
                unit = NormalizeText(server.Unit),
                netCost = Round(server.NetCost),
                overheadPercent = Round(server.OverheadPercent),
                profitPercent = Round(server.ProfitPercent),
                totalCost = Round(server.TotalCost),
                materials = NormalizeCustomLines(server.Materials),
                labour = NormalizeCustomLines(server.Labour),
                breakdown = NormalizeBreakdown(server.Breakdown)
            });

            return string.Equals(localSignature, serverSignature, StringComparison.Ordinal);
        }

        private static IEnumerable<object> NormalizeBreakdown(IEnumerable<BreakdownPayload>? lines)
        {
            return (lines ?? Enumerable.Empty<BreakdownPayload>())
                .Select(line => new
                {
                    componentName = NormalizeText(line.ComponentName),
                    quantity = Round(line.Quantity),
                    unit = NormalizeText(line.Unit),
                    unitPrice = Round(line.UnitPrice),
                    lineTotal = Round(line.LineTotal),
                    refKind = NormalizeText(line.RefKind),
                    refSn = line.RefSn,
                    refName = NormalizeText(line.RefName)
                })
                .ToList();
        }

        private static IEnumerable<object> NormalizeCustomLines(IEnumerable<CustomRateLinePayload>? lines)
        {
            return (lines ?? Enumerable.Empty<CustomRateLinePayload>())
                .Select(line => new
                {
                    rateType = NormalizeText(line.RateType).ToLowerInvariant(),
                    description = NormalizeText(line.Description),
                    quantity = Round(line.Quantity),
                    unit = NormalizeText(line.Unit),
                    unitPrice = Round(line.UnitPrice),
                    totalCost = Round(line.TotalCost),
                    category = NormalizeText(line.Category),
                    refSn = line.RefSn,
                    refName = NormalizeText(line.RefName)
                })
                .ToList();
        }

        private static decimal Round(decimal value) =>
            Math.Round(value, 4, MidpointRounding.AwayFromZero);

        private static string BuildRateIdentityKey(RateOverridePayload rate) =>
            BuildRateIdentityKey(rate.SectionKey, rate.ItemNo, rate.Description, rate.Unit);

        private static string BuildRateIdentityKey(string? sectionKey, int? itemNo, string? description, string? unit)
        {
            return string.Join("|", new[]
            {
                NormalizeText(sectionKey).ToLowerInvariant(),
                itemNo?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                NormalizeText(description).ToLowerInvariant(),
                NormalizeText(unit).ToLowerInvariant()
            });
        }

        private static string NormalizeText(string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace("\r", " ").Replace("\n", " ");

        private static string Trim(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength) + "...";
        }

        private static IEnumerable? FindRowsEnumerable(object vm)
        {
            if (vm == null)
            {
                return null;
            }

            foreach (var property in vm.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.PropertyType == typeof(string))
                {
                    continue;
                }

                if (!typeof(IEnumerable).IsAssignableFrom(property.PropertyType))
                {
                    continue;
                }

                var value = property.GetValue(vm) as IEnumerable;
                if (value == null)
                {
                    continue;
                }

                object? first = null;
                foreach (var item in value)
                {
                    if (item != null)
                    {
                        first = item;
                        break;
                    }
                }

                if (first == null)
                {
                    continue;
                }

                var itemType = first.GetType();
                var hasItemNo = itemType.GetProperty("ItemNo") != null || itemType.GetProperty("SNo") != null;
                var hasDescription =
                    itemType.GetProperty("Description") != null ||
                    itemType.GetProperty("Name") != null ||
                    itemType.GetProperty("Title") != null;
                var hasTotal =
                    itemType.GetProperty("TotalCost") != null ||
                    itemType.GetProperty("TotalPrice") != null ||
                    itemType.GetProperty("Total") != null;

                if (hasItemNo && hasDescription && hasTotal)
                {
                    return value;
                }
            }

            return null;
        }

        private static IEnumerable? FindBreakdownEnumerable(object row)
        {
            foreach (var property in row.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.PropertyType == typeof(string))
                {
                    continue;
                }

                if (!typeof(IEnumerable).IsAssignableFrom(property.PropertyType))
                {
                    continue;
                }

                if (!property.Name.Contains("Breakdown", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return property.GetValue(row) as IEnumerable;
            }

            return null;
        }

        private static string? GetStringProp(object source, params string[] names)
        {
            foreach (var name in names)
            {
                var property = source.GetType().GetProperty(name);
                if (property == null)
                {
                    continue;
                }

                var value = property.GetValue(source);
                if (value != null)
                {
                    return value.ToString();
                }
            }

            return null;
        }

        private static decimal GetDecimalProp(object source, params string[] names)
        {
            foreach (var name in names)
            {
                var property = source.GetType().GetProperty(name);
                if (property == null)
                {
                    continue;
                }

                var value = property.GetValue(source);
                if (value == null)
                {
                    continue;
                }

                try
                {
                    return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                }
                catch
                {
                    // ignore and keep checking
                }
            }

            return 0m;
        }

        private static int? GetIntProp(object source, params string[] names)
        {
            foreach (var name in names)
            {
                var property = source.GetType().GetProperty(name);
                if (property == null)
                {
                    continue;
                }

                var value = property.GetValue(source);
                if (value == null)
                {
                    continue;
                }

                try
                {
                    return Convert.ToInt32(value, CultureInfo.InvariantCulture);
                }
                catch
                {
                    // ignore and keep checking
                }
            }

            return null;
        }

        private sealed class UserRateSnapshot
        {
            public UserRateSnapshot(List<RateOverridePayload> rateOverrides, List<CustomRatePayload> customRates)
            {
                RateOverrides = rateOverrides;
                CustomRates = customRates;
            }

            public List<RateOverridePayload> RateOverrides { get; }
            public List<CustomRatePayload> CustomRates { get; }
        }

        private sealed class ServerLibraryState
        {
            public List<RateOverridePayload> RateOverrides { get; set; } = new List<RateOverridePayload>();
            public List<CustomRatePayload> CustomRates { get; set; } = new List<CustomRatePayload>();
        }

        private sealed class RateOverridePayload
        {
            public string RateId { get; set; } = string.Empty;
            public string SectionKey { get; set; } = string.Empty;
            public string SectionLabel { get; set; } = string.Empty;
            public int? ItemNo { get; set; }
            public string Description { get; set; } = string.Empty;
            public string Unit { get; set; } = string.Empty;
            public decimal NetCost { get; set; }
            public decimal OverheadPercent { get; set; }
            public decimal ProfitPercent { get; set; }
            public decimal OverheadValue { get; set; }
            public decimal ProfitValue { get; set; }
            public decimal TotalCost { get; set; }
            public List<BreakdownPayload> Breakdown { get; set; } = new List<BreakdownPayload>();
            public DateTime? ClientUpdatedAt { get; set; }
            public DateTime? SourceUpdatedAt { get; set; }
        }

        private sealed class CustomRatePayload
        {
            public string CustomRateId { get; set; } = string.Empty;
            public string SectionKey { get; set; } = string.Empty;
            public string SectionLabel { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Unit { get; set; } = string.Empty;
            public List<CustomRateLinePayload> Materials { get; set; } = new List<CustomRateLinePayload>();
            public List<CustomRateLinePayload> Labour { get; set; } = new List<CustomRateLinePayload>();
            public List<BreakdownPayload> Breakdown { get; set; } = new List<BreakdownPayload>();
            public decimal NetCost { get; set; }
            public decimal OverheadPercent { get; set; }
            public decimal ProfitPercent { get; set; }
            public decimal TotalCost { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? UpdatedAt { get; set; }
        }

        private sealed class CustomRateLinePayload
        {
            public string RateType { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public decimal Quantity { get; set; }
            public string Unit { get; set; } = string.Empty;
            public decimal UnitPrice { get; set; }
            public decimal TotalCost { get; set; }
            public string Category { get; set; } = string.Empty;
            public int? RefSn { get; set; }
            public string RefName { get; set; } = string.Empty;
        }

        private sealed class BreakdownPayload
        {
            public string ComponentName { get; set; } = string.Empty;
            public decimal Quantity { get; set; }
            public string Unit { get; set; } = string.Empty;
            public decimal UnitPrice { get; set; }
            public decimal LineTotal { get; set; }
            public string RefKind { get; set; } = string.Empty;
            public int? RefSn { get; set; }
            public string RefName { get; set; } = string.Empty;
        }

        private readonly struct SyncCounters
        {
            public SyncCounters(int upserted, int deleted)
            {
                Upserted = upserted;
                Deleted = deleted;
            }

            public int Upserted { get; }
            public int Deleted { get; }
        }
    }
}
