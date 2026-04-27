using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WatchCollection.Models;

public class User
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.Empty;

    [BsonElement("DisplayName")]
    public string DisplayName { get; set; } = string.Empty;

    [BsonElement("FirstName")]
    public string FirstName { get; set; } = string.Empty;

    [BsonElement("LastName")]
    public string LastName { get; set; } = string.Empty;

    [BsonElement("Email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("HashedPassword")]
    public string HashedPassword { get; set; } = string.Empty;

    [BsonElement("Role")]
    public string Role { get; set; } = "User";

    [BsonIgnore]
    public bool IsAdminBadge => Role == "Admin";
}