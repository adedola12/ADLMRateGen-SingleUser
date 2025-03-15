using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace ADLMRateGen.ViewModel.Model
{
    public class UserModel
    {
		[BsonId]
		[BsonRepresentation(BsonType.ObjectId)]
		public string Id { get; set; }

		[BsonElement("Username")]
		public string Username { get; set; }

		[BsonElement("Email")]
		public string Email { get; set; }

		[BsonElement("Password")]
		public string Password { get; set; }

		[BsonElement("CreationAt")]
		[BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
		public DateTime? CreationAt { get; set; }

		[BsonElement("SubscriptionDuration")]
		public int SubscriptionDuration { get; set; } // in days

		[BsonElement("ExpirationDate")]
		[BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
		public DateTime? ExpirationDate { get; set; }

		[BsonElement("UpdatedAt")]
		public DateTime? UpdatedAt { get; set; }

		[BsonElement("IpAddress")]
		public string IpAddress { get; set; }

		[BsonIgnore]
		public string SubscriptionStatus
		{
			get
			{
				if (ExpirationDate == null)
					return "No Expiration Date";
				var daysLeft = (ExpirationDate.Value - DateTime.UtcNow).Days;
				return daysLeft < 0 ? "Expired" : $"{daysLeft} days left";
			}
		}
	}
}
