using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WatchCollection.Models;

public class User
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.Empty;

    [BsonElement("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [BsonElement("lastName")]
    public string LastName { get; set; } = string.Empty;

    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("password")]
    public string Password { get; set; } = string.Empty;

    [BsonElement("role")]
    public string Role { get; set; } = "user";
}
