using System.Diagnostics;
using ADLMRateGen.ViewModel.Model;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ADLMRateGen.Services
{
    public class MongoDbService
    {
		private readonly IMongoCollection<UserModel> _userCollection;

		public MongoDbService(string connectionString, string databaseName, string collectionName)
		{
			var client = new MongoClient(connectionString);
			var database = client.GetDatabase(databaseName);
			_userCollection = database.GetCollection<UserModel>(collectionName);
		}

		public async Task<UserModel> GetUserAsync(string username, string password)
		{
			var filter = Builders<UserModel>.Filter.Eq(u => u.Username, username) &
						 Builders<UserModel>.Filter.Eq(u => u.Password, password);
			return await _userCollection.Find(filter).FirstOrDefaultAsync();
		}

		public async Task CreateUserAsync(UserModel user)
		{
			await _userCollection.InsertOneAsync(user);
		}

		public async Task UpdateUserAsync(UserModel user)
		{
			var filter = Builders<UserModel>.Filter.Eq(u => u.Id, user.Id);
			await _userCollection.ReplaceOneAsync(filter, user);
		}

		public async Task<string?> GetUserIpAddressByUserIdAsync(string userId)
		{
			if (string.IsNullOrEmpty(userId))
			{
				Debug.WriteLine("User ID is null or empty. Returning null.");
				return null;
			}

			var filter = Builders<UserModel>.Filter.Eq(user => user.Id, userId);

			// Log the filter (very important!)
			Debug.WriteLine($"MongoDB Filter: {filter.ToJson()}");

			var user = await _userCollection.Find(filter).FirstOrDefaultAsync();

			if (user == null)
			{
				Debug.WriteLine($"No user found with ID: {userId}");
				return null;
			}

			return user.IpAddress;
		}
	}
}
