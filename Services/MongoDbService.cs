using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver;
using WatchCollection.Models;

namespace WatchCollection.Services;

public class MongoDbService
{
    private readonly IMongoCollection<User> _usersCollection;

    public MongoDbService()
    {
        var settings = MongoClientSettings.FromConnectionString(
            "mongodb://student:IAmTh3B3st@localhost:5003/?connectTimeoutMS=3000&serverSelectionTimeoutMS=3000");
        
        var client = new MongoClient(settings);
        var database = client.GetDatabase("WatchCollectionDb");
        _usersCollection = database.GetCollection<User>("users");
    }

    public static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }

    public async Task<User?> LoginAsync(string email, string password)
    {
        var hashedPassword = HashPassword(password);
        var filter = Builders<User>.Filter.Eq(u => u.Email, email) &
                     Builders<User>.Filter.Eq(u => u.Password, hashedPassword);
        return await _usersCollection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<bool> RegisterAsync(User user)
    {
        var existingUser = await _usersCollection.Find(
            Builders<User>.Filter.Eq(u => u.Email, user.Email)).FirstOrDefaultAsync();

        if (existingUser != null) return false;

        // Premier utilisateur = admin
        var count = await _usersCollection.CountDocumentsAsync(_ => true);
        if (count == 0)
            user.Role = "admin";

        user.Password = HashPassword(user.Password);
        await _usersCollection.InsertOneAsync(user);
        return true;
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        return await _usersCollection.Find(_ => true).ToListAsync();
    }

    public async Task<bool> DeleteUserAsync(string email)
    {
        var result = await _usersCollection.DeleteOneAsync(
            Builders<User>.Filter.Eq(u => u.Email, email));
        return result.DeletedCount > 0;
    }
}
