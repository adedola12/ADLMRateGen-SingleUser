using ADLMRateGen.ADLM.Auth;
using ADLMRateGen.Services;
using ADLMRateGen.ViewModel.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ADLMRateGen.Services
{
    /// <summary>
    /// Keeps a cached copy of the user library from the server and pushes local additions/edits.
    /// Works best-effort (no blocking UI): if the user is offline, local changes stay and will sync next time.
    /// </summary>
    public sealed class UserLibrarySync
    {
        private static readonly Lazy<UserLibrarySync> _lazy = new(() => new UserLibrarySync());
        public static UserLibrarySync Instance => _lazy.Value;

        private readonly object _gate = new();
        private int _version = 0;
        private readonly List<RateRow> _myMaterials = new();
        private readonly List<RateRow> _myLabour = new();

        private sealed class RateItemDto
        {
            public int sn { get; set; }
            public string description { get; set; } = "";
            public string unit { get; set; } = "";
            public decimal price { get; set; }
            public string? category { get; set; }   // NEW
        }
        
        private record RateRow(int sn, string description, string unit, decimal price, string? category);

        private sealed class LibraryPayload
        {
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public int? baseVersion { get; set; }   // null on first push to skip conflict check
            public RateItemDto[] materials { get; set; } = Array.Empty<RateItemDto>();
            public RateItemDto[] labour { get; set; } = Array.Empty<RateItemDto>();
        }

        // Optionally reuse these options anywhere you serialize the payload
        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private UserLibrarySync() { }

        public IReadOnlyList<(int sn, string description, string unit, decimal price, string? category)> MyMaterials
        {
            get { lock (_gate) return _myMaterials.Select(r => (r.sn, r.description, r.unit, r.price, r.category)).ToList(); }
        }
        public IReadOnlyList<(int sn, string description, string unit, decimal price, string? category)> MyLabour
        {
            get { lock (_gate) return _myLabour.Select(r => (r.sn, r.description, r.unit, r.price, r.category)).ToList(); }
        }


        //public IReadOnlyList<(int sn, string description, string unit, decimal price)> MyMaterials
        //{
        //    get { lock (_gate) return _myMaterials.Select(r => (r.sn, r.description, r.unit, r.price)).ToList(); }
        //}
        //public IReadOnlyList<(int sn, string description, string unit, decimal price)> MyLabour
        //{
        //    get { lock (_gate) return _myLabour.Select(r => (r.sn, r.description, r.unit, r.price)).ToList(); }
        //}

        /// <summary>Load current user library from server (best-effort).</summary>
        public async Task LoadAsync()
        {
            try
            {
                var auth = AuthProvider.Instance.Client;
                if (!auth.HasSession) return;

                using var doc = await auth.GetJsonAsync("/rategen/library");
                var root = doc.RootElement;

                int ver = root.TryGetProperty("version", out var v) && v.TryGetInt32(out var vv) ? vv : 1;

                var mats = new List<RateRow>();
                if (root.TryGetProperty("materials", out var mArr) && mArr.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var el in mArr.EnumerateArray())
                    {
                        mats.Add(new RateRow(
                            el.GetProperty("sn").GetInt32(),
                            el.GetProperty("description").GetString() ?? "",
                            el.GetProperty("unit").GetString() ?? "",
                            el.GetProperty("price").GetDecimal(),
                            el.TryGetProperty("category", out var c) ? (c.GetString() ?? "") : null
                        ));
                    }
                }

                var labs = new List<RateRow>();
                if (root.TryGetProperty("labour", out var lArr) && lArr.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var el in lArr.EnumerateArray())
                    {
                        labs.Add(new RateRow(
                            el.GetProperty("sn").GetInt32(),
                            el.GetProperty("description").GetString() ?? "",
                            el.GetProperty("unit").GetString() ?? "",
                            el.GetProperty("price").GetDecimal(),
                            el.TryGetProperty("category", out var c) ? (c.GetString() ?? "") : null
                        ));
                    }
                }

                lock (_gate)
                {
                    _version = ver;
                    _myMaterials.Clear(); _myMaterials.AddRange(mats);
                    _myLabour.Clear(); _myLabour.AddRange(labs);
                }
            }
            catch
            {
                // ignore (offline or not signed in). We’ll try again later.
            }
        }

        /// <summary>
        /// Append a new user material. The caller should have assigned a SerialNumber that continues after master list.
        /// We mirror that S/N into the server row’s 'sn'.
        /// </summary>
        public async Task TryAddMaterialAsync(MaterialModel m)
        {
            lock (_gate)
            {
                _myMaterials.RemoveAll(r => r.sn == m.SerialNumber); // de-dupe on S/N
                _myMaterials.Add(new RateRow(m.SerialNumber, m.MaterialName ?? "", m.MaterialUnit ?? "", m.MaterialPrice, string.IsNullOrWhiteSpace(m.MaterialCategory) ? null : m.MaterialCategory));
            }
            await PushAsync();
        }

        public async Task TryAddLabourAsync(LabourModel l)
        {
            lock (_gate)
            {
                _myLabour.RemoveAll(r => r.sn == l.SerialNumber);
                _myLabour.Add(new RateRow(l.SerialNumber, l.LabourName ?? "", l.LabourUnit ?? "", l.LabourPrice, string.IsNullOrWhiteSpace(l.LabourCategory) ? null : l.LabourCategory));
            }
            await PushAsync();
        }

        /// <summary>When user edits price locally, reflect to server if that row is part of user additions.</summary>
        public async Task TryUpdateMaterialAsync(MaterialModel m)
        {
            lock (_gate)
            {
                // If it's already one of my rows, update it; otherwise create an override for this SN.
                var existing = _myMaterials.FirstOrDefault(x => x.sn == m.SerialNumber
                    || string.Equals(x.description, m.MaterialName, StringComparison.OrdinalIgnoreCase));

                var desc = m.MaterialName ?? existing?.description ?? "";
                var unit = m.MaterialUnit ?? existing?.unit ?? "";
                var cat = !string.IsNullOrWhiteSpace(m.MaterialCategory) ? m.MaterialCategory : existing?.category;


                _myMaterials.RemoveAll(x => x.sn == m.SerialNumber);
                _myMaterials.Add(new RateRow(m.SerialNumber, desc, unit, m.MaterialPrice, cat));
            }
            await PushAsync();
        }

        public async Task TryUpdateLabourAsync(LabourModel l)
        {
            lock (_gate)
            {
                var existing = _myLabour.FirstOrDefault(x => x.sn == l.SerialNumber
                    || string.Equals(x.description, l.LabourName, StringComparison.OrdinalIgnoreCase));

                var desc = l.LabourName ?? existing?.description ?? "";
                var unit = l.LabourUnit ?? existing?.unit ?? "";
                var cat = !string.IsNullOrWhiteSpace(l.LabourCategory) ? l.LabourCategory : existing?.category;


                _myLabour.RemoveAll(x => x.sn == l.SerialNumber);
                _myLabour.Add(new RateRow(l.SerialNumber, desc, unit, l.LabourPrice, cat));
            }
            await PushAsync();
        }



        //private async Task PushAsync()
        //{
        //    try
        //    {
        //        var auth = AuthProvider.Instance.Client;
        //        if (!auth.HasSession) return;

        //        int ver;
        //        List<RateRow> mats;
        //        List<RateRow> labs;
        //        lock (_gate)
        //        {
        //            ver = _version;
        //            mats = _myMaterials.OrderBy(r => r.sn).ToList();
        //            labs = _myLabour.OrderBy(r => r.sn).ToList();
        //        }

        //        var payload = new
        //        {
        //            baseVersion = ver,
        //            materials = mats.Select(r => new { sn = r.sn, description = r.description, unit = r.unit, price = r.price }).ToArray(),
        //            labour = labs.Select(r => new { sn = r.sn, description = r.description, unit = r.unit, price = r.price }).ToArray()
        //        };

        //        using var doc = await auth.PutJsonAsync("/rategen/library", payload);
        //        var root = doc.RootElement;
        //        var newVer = root.TryGetProperty("version", out var v) && v.TryGetInt32(out var vv) ? vv : ver;

        //        lock (_gate) _version = newVer;
        //    }
        //    catch
        //    {
        //        // swallow: we’ll retry on next add/edit or next app run.
        //    }
        //}

        private async Task PushAsync()
        {
            try
            {
                var auth = AuthProvider.Instance.Client;
                if (!auth.HasSession) return;

                int ver;
                List<RateRow> mats;
                List<RateRow> labs;

                lock (_gate)
                {
                    ver = _version;
                    mats = _myMaterials.OrderBy(r => r.sn).ToList();
                    labs = _myLabour.OrderBy(r => r.sn).ToList();
                }

                var payload = new LibraryPayload
                {
                    baseVersion = ver > 0 ? ver : (int?)null,   // omit on first push
                    materials = mats.Select(r => new RateItemDto { sn = r.sn, description = r.description, unit = r.unit, price = r.price, category = r.category }).ToArray(),
                    labour    = labs.Select(r => new RateItemDto { sn = r.sn, description = r.description, unit = r.unit, price = r.price, category = r.category }).ToArray()
                };

                using var doc = await auth.PutJsonAsync("/rategen/library", payload);
                var root = doc.RootElement;
                var newVer = root.TryGetProperty("version", out var v) && v.TryGetInt32(out var vv) ? vv : ver;
                lock (_gate) _version = newVer;
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("409"))
            {
                try
                {
                    var auth = AuthProvider.Instance.Client;
                    using var cur = await auth.GetJsonAsync("/rategen/library");
                    var root = cur.RootElement;
                    var latest = root.TryGetProperty("version", out var v) && v.TryGetInt32(out var vv) ? vv : 1;
                    lock (_gate) _version = latest;
                }
                catch { /* ignore */ }

                await PushAsync(); // one retry
            }
            catch
            {
                // best-effort: will retry on next change/app run
            }
        }

        
        public async Task DeleteMaterialAsync(int sn, string? name = null)
        {
            lock (_gate)
            {
                // delete by sn OR description fallback (in case local renumbering happened)
                _myMaterials.RemoveAll(r => r.sn == sn ||
                    (!string.IsNullOrWhiteSpace(name) &&
                     r.description.Equals(name, StringComparison.OrdinalIgnoreCase)));

                CompactSerials(_myMaterials);
            }
            await PushAsync();
        }

        public async Task DeleteLabourAsync(int sn, string? name = null)
        {
            lock (_gate)
            {
                _myLabour.RemoveAll(r => r.sn == sn ||
                    (!string.IsNullOrWhiteSpace(name) &&
                     r.description.Equals(name, StringComparison.OrdinalIgnoreCase)));

                CompactSerials(_myLabour);
            }
            await PushAsync();
        }

        private static void CompactSerials(List<RateRow> rows)
        {
            int next = 1;
            foreach (var r in rows.OrderBy(r => r.sn).ThenBy(r => r.description, StringComparer.OrdinalIgnoreCase).ToList())
                rows[rows.IndexOf(r)] = r with { sn = next++ };
        }


    }
}
