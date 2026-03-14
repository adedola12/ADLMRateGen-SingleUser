using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ADLMRateGen.ViewModel.Model;
using MongoDB.Bson;
using MongoDB.Driver;

// MongoDB driver internally may throw DnsClient.DnsResponseException for SRV/TXT problems.
// This reference is usually already present via MongoDB.Driver.
using DnsClient;

namespace ADLMRateGen.Services
{
    public class MongoDbService : IDisposable
    {
        private IMongoCollection<UserModel>? _userCollection;
        private IMongoCollection<MaterialModel>? _materialCollection;
        private IMongoCollection<LabourModel>? _labourCollection;

        private readonly CancellationTokenSource _cts = new();

        private readonly string _srvConnectionString;
        private readonly string? _standardConnectionString;
        private readonly string _databaseName;
        private readonly string _userCol;
        private readonly string _materialCol;
        private readonly string _labourCol;
        private readonly object _initializeGate = new();

        private Task? _initializeTask;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_srvConnectionString) || !string.IsNullOrWhiteSpace(_standardConnectionString);
        public bool IsAvailable { get; private set; }
        public string? LastError { get; private set; }

        public event Action? PricesChanged;
        public event Action? MaterialPricesChanged;
        public event Action? LabourPricesChanged;

        /// <summary>
        /// Provide SRV connection string + OPTIONAL standard mongodb:// fallback.
        /// If SRV DNS is blocked on a user network, standard connection string fixes it.
        /// </summary>
        public MongoDbService(
            string srvConnectionString,
            string databaseName,
            string userCol,
            string materialCol,
            string labourCol,
            string? standardConnectionString = null)
        {
            _srvConnectionString = srvConnectionString ?? "";
            _standardConnectionString = string.IsNullOrWhiteSpace(standardConnectionString) ? null : standardConnectionString;
            _databaseName = databaseName ?? "";
            _userCol = userCol ?? "";
            _materialCol = materialCol ?? "";
            _labourCol = labourCol ?? "";
        }

        public Task InitializeAsync()
        {
            lock (_initializeGate)
            {
                _initializeTask ??= Task.Run(() => TryConnect());
                return _initializeTask;
            }
        }

        private void TryConnect()
        {
            // Try SRV first
            if (TryConnectInternal(_srvConnectionString, out var err))
            {
                IsAvailable = true;
                LastError = null;
                StartWatchers();
                return;
            }

            // If SRV failed and we have a standard fallback, try it
            if (!string.IsNullOrWhiteSpace(_standardConnectionString))
            {
                if (TryConnectInternal(_standardConnectionString!, out var err2))
                {
                    IsAvailable = true;
                    LastError = null;
                    StartWatchers();
                    return;
                }

                IsAvailable = false;
                LastError = err2;
                return;
            }

            // No fallback supplied
            IsAvailable = false;
            LastError = err;
        }

        private bool TryConnectInternal(string connectionString, out string error)
        {
            error = "";

            try
            {
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    error = "Mongo connection string is empty.";
                    return false;
                }

                // ✅ add timeouts so it fails fast on restricted networks
                var settings = MongoClientSettings.FromConnectionString(connectionString);
                settings.ServerSelectionTimeout = TimeSpan.FromSeconds(8);
                settings.ConnectTimeout = TimeSpan.FromSeconds(8);
                settings.SocketTimeout = TimeSpan.FromSeconds(8);

                var client = new MongoClient(settings);
                var database = client.GetDatabase(_databaseName);

                // collections
                _userCollection = database.GetCollection<UserModel>(_userCol);
                _materialCollection = database.GetCollection<MaterialModel>(_materialCol);
                _labourCollection = database.GetCollection<LabourModel>(_labourCol);

                // light ping to force a real connect attempt (and catch DNS/network issues here)
                database.RunCommand<BsonDocument>(new BsonDocument("ping", 1));

                return true;
            }
            catch (DnsResponseException ex)
            {
                error =
                    "MongoDB Atlas SRV/TXT DNS lookup failed on this network.\n" +
                    "This is usually caused by restricted DNS (common on office Wi-Fi).\n\n" +
                    "Fix: use a STANDARD (non-SRV) mongodb:// connection string OR change DNS (1.1.1.1 / 8.8.8.8).\n\n" +
                    "Details: " + ex.Message;
                return false;
            }
            catch (MongoConfigurationException ex) when ((ex.Message ?? "").IndexOf("Resolve", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                error =
                    "MongoDB Atlas DNS resolution failed (SRV/TXT blocked).\n" +
                    "Fix: use a STANDARD (non-SRV) mongodb:// connection string.\n\n" +
                    "Details: " + ex.Message;
                return false;
            }
            catch (TimeoutException ex)
            {
                error = "MongoDB connection timed out.\nDetails: " + ex.Message;
                return false;
            }
            catch (Exception ex)
            {
                error = "MongoDB connection failed.\nDetails: " + ex.Message;
                return false;
            }
        }

        private void StartWatchers()
        {
            try
            {
                if (_materialCollection != null)
                    _ = Task.Run(() => WatchChangesAsync(_materialCollection, _cts.Token));

                if (_labourCollection != null)
                    _ = Task.Run(() => WatchChangesAsync(_labourCollection, _cts.Token));
            }
            catch
            {
                // best-effort; watcher failures should never crash
            }
        }

        private async Task WatchChangesAsync<T>(IMongoCollection<T> col, CancellationToken token)
        {
            try
            {
                using var cursor = await col.WatchAsync(cancellationToken: token).ConfigureAwait(false);
                while (!token.IsCancellationRequested && await cursor.MoveNextAsync(token).ConfigureAwait(false))
                {
                    foreach (var _ in cursor.Current)
                    {
                        if (typeof(T) == typeof(MaterialModel))
                            MaterialPricesChanged?.Invoke();
                        else if (typeof(T) == typeof(LabourModel))
                            LabourPricesChanged?.Invoke();
                        else
                            PricesChanged?.Invoke();
                    }
                }
            }
            catch
            {
                // watcher may fail due to network drop; ignore
            }
        }

        public void Dispose()
        {
            try { _cts.Cancel(); } catch { }
            try { _cts.Dispose(); } catch { }
        }

        // Materials / Labours
        public Task<List<MaterialModel>> GetLatestMaterialsAsync()
        {
            if (!IsAvailable || _materialCollection == null)
                return Task.FromResult(new List<MaterialModel>());

            return _materialCollection.Find(FilterDefinition<MaterialModel>.Empty).ToListAsync();
        }

        public Task<List<LabourModel>> GetLatestLaboursAsync()
        {
            if (!IsAvailable || _labourCollection == null)
                return Task.FromResult(new List<LabourModel>());

            return _labourCollection.Find(FilterDefinition<LabourModel>.Empty).ToListAsync();
        }

        // -------- USERS --------

        /// <summary>
        /// Fetch by Mongo _id (24-hex) OR by Username if not a valid ObjectId.
        /// </summary>
        public UserModel? GetUserById(string idOrUsername)
        {
            if (!IsAvailable || _userCollection == null) return null;

            try
            {
                if (ObjectId.TryParse(idOrUsername, out _))
                {
                    return _userCollection.Find(u => u.Id == idOrUsername).FirstOrDefault();
                }

                return _userCollection.Find(u => u.Username == idOrUsername).FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        public async Task<UserModel?> GetUserAsync(string username, string password)
        {
            if (!IsAvailable || _userCollection == null) return null;

            var filter = Builders<UserModel>.Filter.Eq(u => u.Username, username) &
                         Builders<UserModel>.Filter.Eq(u => u.Password, password);

            return await _userCollection.Find(filter).FirstOrDefaultAsync().ConfigureAwait(false);
        }

        public Task CreateUserAsync(UserModel user)
        {
            if (!IsAvailable || _userCollection == null) return Task.CompletedTask;
            return _userCollection.InsertOneAsync(user);
        }

        public Task UpdateUserAsync(UserModel user)
        {
            if (!IsAvailable || _userCollection == null) return Task.CompletedTask;

            var filter = Builders<UserModel>.Filter.Eq(u => u.Id, user.Id);
            return _userCollection.ReplaceOneAsync(filter, user);
        }

        /// <summary>
        /// Persist the device fingerprint. Accepts _id or username.
        /// </summary>
        public async Task<bool> SetHardwareFingerprintAsync(string idOrUsername, string encryptedFingerprint)
        {
            if (!IsAvailable || _userCollection == null) return false;

            FilterDefinition<UserModel> filter;
            if (ObjectId.TryParse(idOrUsername, out _))
                filter = Builders<UserModel>.Filter.Eq(u => u.Id, idOrUsername);
            else
                filter = Builders<UserModel>.Filter.Eq(u => u.Username, idOrUsername);

            var update = Builders<UserModel>.Update
                .Set(u => u.HardwareFingerprint, encryptedFingerprint)
                .Set(u => u.UpdatedAt, DateTime.UtcNow);

            var result = await _userCollection.UpdateOneAsync(filter, update).ConfigureAwait(false);
            return result.ModifiedCount > 0;
        }
    }
}
