using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ADLMRateGen.ViewModel.Model;
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

        // Users
        public UserModel? GetUserById(string id) =>
            _userCollection.Find(u => u.Id == id).FirstOrDefault();

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

        // Persist hardware fingerprint (first successful login)
        public async Task<bool> SetHardwareFingerprintAsync(string userId, string encryptedFingerprint)
        {
            var filter = Builders<UserModel>.Filter.Eq(u => u.Id, userId);
            var update = Builders<UserModel>.Update
                .Set(u => u.HardwareFingerprint, encryptedFingerprint)
                .Set(u => u.UpdatedAt, DateTime.UtcNow);

            var result = await _userCollection.UpdateOneAsync(filter, update); // <- fixed here
            return result.ModifiedCount > 0;
        }
    }
}
