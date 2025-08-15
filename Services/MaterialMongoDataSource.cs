using System.Collections.Generic;
using ADLMRateGen.ViewModel.Model;
using MongoDB.Driver;

namespace ADLMRateGen.Services
{
    public class MaterialMongoDataSource : IMaterialDataSource
    {
        private readonly string _connectionString;
        private readonly string _databaseName;
        private readonly string _collectionName;

        public MaterialMongoDataSource(string connectionString, string databaseName, string collectionName)
        {
            _connectionString = connectionString;
            _databaseName = databaseName;
            _collectionName = collectionName;
        }

        private IMongoCollection<MaterialModel> Collection
        {
            get
            {
                var client = new MongoClient(_connectionString);
                var db = client.GetDatabase(_databaseName);
                return db.GetCollection<MaterialModel>(_collectionName);
            }
        }

        public IEnumerable<MaterialModel> LoadMaterials() =>
            Collection.Find(Builders<MaterialModel>.Filter.Empty).ToList();

        public void SaveMaterials(IEnumerable<MaterialModel> materials)
        {
            var col = Collection;

            // Upsert per material (by MaterialName). Consider a unique index on MaterialName.
            var opts = new ReplaceOptions { IsUpsert = true };

            foreach (var m in materials)
            {
                var filter = Builders<MaterialModel>.Filter.Eq(x => x.MaterialName, m.MaterialName);
                col.ReplaceOne(filter, m, opts);
            }
            // NOTE: We do NOT delete missing docs; we only add/update.
        }
    }
}
