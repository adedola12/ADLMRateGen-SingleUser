using System.Collections.Generic;
using ADLMRateGen.ViewModel.Model;
using MongoDB.Driver;

namespace ADLMRateGen.Services
{
    public class LabourMongoDataSource : ILabourDataSource
    {
        private readonly string _connectionString;
        private readonly string _databaseName;
        private readonly string _collectionName;

        public LabourMongoDataSource(string connectionString, string databaseName, string collectionName)
        {
            _connectionString = connectionString;
            _databaseName = databaseName;
            _collectionName = collectionName;
        }

        private IMongoCollection<LabourModel> Collection
        {
            get
            {
                var client = new MongoClient(_connectionString);
                var db = client.GetDatabase(_databaseName);
                return db.GetCollection<LabourModel>(_collectionName);
            }
        }

        public IEnumerable<LabourModel> LoadLabours() =>
            Collection.Find(Builders<LabourModel>.Filter.Empty).ToList();

        public void SaveLabours(IEnumerable<LabourModel> labours)
        {
            var col = Collection;
            var opts = new ReplaceOptions { IsUpsert = true };

            foreach (var l in labours)
            {
                var filter = Builders<LabourModel>.Filter.Eq(x => x.LabourName, l.LabourName);
                col.ReplaceOne(filter, l, opts);
            }
            // No deletes – preserves existing user/customized docs.
        }
    }
}
