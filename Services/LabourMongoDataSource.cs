using ADLMRateGen.ViewModel.Model;
using MongoDB.Driver;

namespace ADLMRateGen.Services
{
    public class LabourMongoDataSource: ILabourDataSource
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

		public IEnumerable<LabourModel> LoadLabours()
		{
			var client = new MongoClient(_connectionString);
			var database = client.GetDatabase(_databaseName);
			var collection = database.GetCollection<LabourModel>(_collectionName);
			return collection.Find(FilterDefinition<LabourModel>.Empty).ToList();
		}

		public void SaveLabours(IEnumerable<LabourModel> labours)
		{
			var client = new MongoClient(_connectionString);
			var database = client.GetDatabase(_databaseName);
			var collection = database.GetCollection<LabourModel>(_collectionName);
			// Remove all previous entries and re-insert the new collection.
			collection.DeleteMany(Builders<LabourModel>.Filter.Empty);
			collection.InsertMany(labours);
		}
	}
}
