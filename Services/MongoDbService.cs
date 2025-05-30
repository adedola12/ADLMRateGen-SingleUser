using System.Diagnostics;
using System.Windows.Media.Media3D;
using ADLMRateGen.ViewModel.Model;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ADLMRateGen.Services
{
    public class MongoDbService
    {
		private readonly IMongoCollection<UserModel> _userCollection;
		private readonly IMongoCollection<MaterialModel> _materialCollection;
		private readonly IMongoCollection<LabourModel> _labourCollection;
		private readonly CancellationTokenSource _cts = new();

		public event Action PricesChanged;
		public event Action MaterialPricesChanged;
		public event Action LabourPricesChanged;

		public MongoDbService(string connectionString, string databaseName, string userCol, string materialCol, string labourCol)
		{
			var client = new MongoClient(connectionString);
			var database = client.GetDatabase(databaseName);

			_userCollection = database.GetCollection<UserModel>(userCol);
			_materialCollection = database.GetCollection<MaterialModel>(materialCol);
			_labourCollection = database.GetCollection<LabourModel>(labourCol);

			Task.Run(() => WatchChangesAsync(_materialCollection, _cts.Token));
			Task.Run(() => WatchChangesAsync(_labourCollection, _cts.Token));

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
				}
			}
		}

		public void Dispose()
		{
			_cts.Cancel();
			_cts.Dispose();
		}

		/// <summary>Fetch the live copy of *all* materials.</summary>
		public Task<List<MaterialModel>> GetLatestMaterialsAsync()
		   => _materialCollection.Find(FilterDefinition<MaterialModel>.Empty).ToListAsync();

		/// <summary>Fetch the live copy of *all* labours.</summary>
		public Task<List<LabourModel>> GetLatestLaboursAsync()
		   => _labourCollection.Find(FilterDefinition<LabourModel>.Empty).ToListAsync();


		// ADD to your MongoDbService (or whatever class already wraps the user collection)
		// add right next to GetUserByIdAsync
		public UserModel? GetUserById(string id) =>
				_userCollection.Find(u => u.Id == id).FirstOrDefault();


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
		public async Task UpdateUserIpAddressAsync(string userId, string ipAddress)
		{
			var filter = Builders<UserModel>.Filter.Eq(u => u.Id, userId);
			var update = Builders<UserModel>.Update.Set(u => u.IpAddress, ipAddress);

			await _userCollection.UpdateOneAsync(filter, update);
		}

	


	}
}
