using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ADLMRateGen.ViewModel.Model
{
    public class UserModel
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("Username")]
        public string Username { get; set; } = string.Empty;

        [BsonElement("Email")]
        public string Email { get; set; } = string.Empty;

        [BsonElement("AvatarUrl")]
        public string AvatarUrl { get; set; } = string.Empty;

        [BsonElement("FirstName")]
        public string FirstName { get; set; } = string.Empty;

        [BsonElement("LastName")]
        public string LastName { get; set; } = string.Empty;

        [BsonElement("Zone")]
        public string Zone { get; set; } = string.Empty;

        [BsonElement("Password")]
        public string Password { get; set; } = string.Empty;

        [BsonElement("CreationAt")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? CreationAt { get; set; }

        [BsonElement("SubscriptionDuration")]
        public int SubscriptionDuration { get; set; }

        [BsonElement("ExpirationDate")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? ExpirationDate { get; set; }

        [BsonElement("UpdatedAt")]
        public DateTime? UpdatedAt { get; set; }

        [BsonElement("HardwareFingerprint")]
        public string HardwareFingerprint { get; set; } = string.Empty;

        [BsonIgnore]
        public string DisplayName
        {
            get
            {
                var fullName = $"{FirstName} {LastName}".Trim();
                if (!string.IsNullOrWhiteSpace(fullName))
                    return fullName;

                if (!string.IsNullOrWhiteSpace(Username))
                    return Username;

                if (!string.IsNullOrWhiteSpace(Email))
                    return Email.Split('@')[0];

                return "ADLM User";
            }
        }

        [BsonIgnore]
        public string AvatarSource =>
            string.IsNullOrWhiteSpace(AvatarUrl)
                ? "pack://application:,,,/Resources/user_avatar.jpg"
                : AvatarUrl;

        [BsonIgnore]
        public string Initials
        {
            get
            {
                var parts = DisplayName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    return "A";

                if (parts.Length == 1)
                    return parts[0][0].ToString().ToUpperInvariant();

                return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
            }
        }

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
