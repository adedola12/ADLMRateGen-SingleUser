using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ADLMRateGen.ViewModel.Model;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ADLMRateGen.Services
{
    public class MongoDbService : IDisposable
    {
        private readonly IMongoCollection<UserModel> _userCollection;
        private readonly IMongoCollection<MaterialModel> _materialCollection;
        private readonly IMongoCollection<LabourModel> _labourCollection;
        private readonly CancellationTokenSource _cts = new();

        public event Action? PricesChanged;
        public event Action? MaterialPricesChanged;
        public event Action? LabourPricesChanged;

        public MongoDbService(string connectionString, string databaseName, string userCol, string materialCol, string labourCol)
        {
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);

            _userCollection     = database.GetCollection<UserModel>(userCol);
            _materialCollection = database.GetCollection<MaterialModel>(materialCol);
            _labourCollection   = database.GetCollection<LabourModel>(labourCol);

            _ = Task.Run(() => WatchChangesAsync(_materialCollection, _cts.Token));
            _ = Task.Run(() => WatchChangesAsync(_labourCollection, _cts.Token));
        }

        private async Task WatchChangesAsync<T>(IMongoCollection<T> col, CancellationToken token)
        {
            using var cursor = await col.WatchAsync(cancellationToken: token);
            while (!token.IsCancellationRequested && await cursor.MoveNextAsync(token))
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

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        // Materials / Labours
        public Task<List<MaterialModel>> GetLatestMaterialsAsync() =>
            _materialCollection.Find(FilterDefinition<MaterialModel>.Empty).ToListAsync();

        public Task<List<LabourModel>> GetLatestLaboursAsync() =>
            _labourCollection.Find(FilterDefinition<LabourModel>.Empty).ToListAsync();

        // -------- USERS --------

        /// <summary>
        /// Fetch by Mongo _id (24-hex) OR by Username if not a valid ObjectId.
        /// </summary>
        public UserModel? GetUserById(string idOrUsername)
        {
            if (ObjectId.TryParse(idOrUsername, out _))
            {
                // Database stores _id as ObjectId. In your UserModel, make sure:
                //   [BsonId][BsonRepresentation(BsonType.ObjectId)] public string Id {get;set;}
                return _userCollection.Find(u => u.Id == idOrUsername).FirstOrDefault();
            }
            // Fallback: treat input as username
            return _userCollection.Find(u => u.Username == idOrUsername).FirstOrDefault();
        }

        public async Task<UserModel?> GetUserAsync(string username, string password)
        {
            var filter = Builders<UserModel>.Filter.Eq(u => u.Username, username) &
                         Builders<UserModel>.Filter.Eq(u => u.Password, password);
            return await _userCollection.Find(filter).FirstOrDefaultAsync();
        }

        public Task CreateUserAsync(UserModel user) =>
            _userCollection.InsertOneAsync(user);

        public Task UpdateUserAsync(UserModel user)
        {
            var filter = Builders<UserModel>.Filter.Eq(u => u.Id, user.Id);
            return _userCollection.ReplaceOneAsync(filter, user);
        }

        /// <summary>
        /// Persist the device fingerprint. Accepts _id or username.
        /// </summary>
        public async Task<bool> SetHardwareFingerprintAsync(string idOrUsername, string encryptedFingerprint)
        {
            FilterDefinition<UserModel> filter;
            if (ObjectId.TryParse(idOrUsername, out _))
                filter = Builders<UserModel>.Filter.Eq(u => u.Id, idOrUsername);
            else
                filter = Builders<UserModel>.Filter.Eq(u => u.Username, idOrUsername);

            var update = Builders<UserModel>.Update
                .Set(u => u.HardwareFingerprint, encryptedFingerprint)
                .Set(u => u.UpdatedAt, DateTime.UtcNow);

            var result = await _userCollection.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }
    }
}
