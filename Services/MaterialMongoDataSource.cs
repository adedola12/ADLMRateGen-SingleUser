using ADLMRateGen.ViewModel.Model;
using MongoDB.Driver;

namespace ADLMRateGen.Services
{
    public class MaterialMongoDataSource: IMaterialDataSource
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
		public IEnumerable<MaterialModel> LoadMaterials()
		{
			var client = new MongoClient(_connectionString);
			var database = client.GetDatabase(_databaseName);
			var collection = database.GetCollection<MaterialModel>(_collectionName);
			
			return collection.Find(FilterDefinition<MaterialModel>.Empty).ToList();

		}
		public void SaveMaterials(IEnumerable<MaterialModel> materials)
		{
			var client = new MongoClient(_connectionString);
			var database = client.GetDatabase(_databaseName);
			var collection = database.GetCollection<MaterialModel>(_collectionName);

			collection.DeleteMany(Builders<MaterialModel>.Filter.Empty);
			collection.InsertMany(materials);
		}
	}
}
