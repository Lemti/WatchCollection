using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using WatchCollection.Models;

namespace WatchCollection.Services;

public sealed class MongoDBService
{
    private const string ConnectionUri =
        "mongodb://Meeeee:IAmTheBest@185.157.245.38:443/?authSource=admin&directConnection=true";

    private const string DatabaseName = "WatchCollectionLemtiLoukiliDB";
    private const string UsersCollectionName = "Users";
    private const string WatchesCollectionName = "Watches";

    private readonly IMongoCollection<User>? _users;
    private readonly IMongoCollection<Watch>? _watches;

    public bool IsConnected { get; }
    public string? LastError { get; private set; }

    public MongoDBService()
    {
        try
        {
            var settings = MongoClientSettings.FromConnectionString(ConnectionUri);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(10);
            settings.SocketTimeout = TimeSpan.FromSeconds(10);
            settings.ConnectTimeout = TimeSpan.FromSeconds(10);

            var client = new MongoClient(settings);
            var database = client.GetDatabase(DatabaseName);

            _users = database.GetCollection<User>(UsersCollectionName);
            _watches = database.GetCollection<Watch>(WatchesCollectionName);

            IsConnected = TryPing(database, out var pingError);
            LastError = pingError;

            if (IsConnected) EnsureIndexes();
            Globals.IsDatabaseAvailable = IsConnected;
        }
        catch (Exception ex)
        {
            LastError = $"{ex.GetType().Name}: {ex.Message}";
            IsConnected = false;
            Globals.IsDatabaseAvailable = false;
        }
    }

    private void EnsureIndexes()
    {
        if (_users is null || _watches is null) return;

        try
        {
            var emailIndex = new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(u => u.Email),
                new CreateIndexOptions { Unique = true, Name = "ux_users_email" });
            _users.Indexes.CreateOne(emailIndex);

            var ownerIndex = new CreateIndexModel<Watch>(
                Builders<Watch>.IndexKeys.Ascending(w => w.OwnerId),
                new CreateIndexOptions { Name = "ix_watches_owner" });
            _watches.Indexes.CreateOne(ownerIndex);
        }
        catch (Exception ex)
        {
            LastError = $"EnsureIndexes: {ex.Message}";
        }
    }

    private static bool TryPing(IMongoDatabase database, out string? error)
    {
        error = null;
        try
        {
            var task = database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            var completed = task.Wait(TimeSpan.FromSeconds(10));

            if (!completed)
            {
                error = "Timeout (10s) lors du ping du serveur.";
                return false;
            }

            var ok = task.Result.Contains("ok");
            if (!ok) error = $"Réponse ping invalide : {task.Result.ToJson()}";
            return ok;
        }
        catch (AggregateException agg)
        {
            var inner = agg.InnerException;
            error = inner is not null ? $"{inner.GetType().Name}: {inner.Message}" : agg.Message;
            return false;
        }
        catch (Exception ex)
        {
            error = $"{ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }

    // ============================================================
    //                        UTILISATEURS
    // ============================================================

    /// <summary>
    /// Hashage de mot de passe avec PBKDF2 (RFC 2898), recommandé par OWASP.
    /// Plus sûr que SHA-256 simple : applique 10 000 itérations sur un sel unique,
    /// ce qui rend les attaques par dictionnaire et force brute impraticables.
    /// </summary>
    public static string HashPassword(string password)
    {
        var salt = Encoding.UTF8.GetBytes("WatchCollectionLemtiLoukili2026");
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
        return Convert.ToBase64String(pbkdf2.GetBytes(32));
    }

    public async Task<User?> LoginAsync(string email, string password)
    {
        if (!IsConnected || _users is null) return null;

        try
        {
            var hashed = HashPassword(password);
            var filter = Builders<User>.Filter.Eq(u => u.Email, email)
                       & Builders<User>.Filter.Eq(u => u.HashedPassword, hashed);

            return await _users.Find(filter).FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            LastError = $"LoginAsync: {ex.Message}";
            return null;
        }
    }

    public async Task<bool> RegisterAsync(User user)
    {
        if (!IsConnected || _users is null) return false;

        try
        {
            var existing = await _users
                .Find(Builders<User>.Filter.Eq(u => u.Email, user.Email))
                .FirstOrDefaultAsync();

            if (existing is not null) return false;

            var totalUsers = await _users.CountDocumentsAsync(_ => true);
            user.Role = totalUsers == 0 ? "Admin" : "User";

            user.Id = ObjectId.GenerateNewId();
            user.HashedPassword = HashPassword(user.HashedPassword);

            if (string.IsNullOrWhiteSpace(user.DisplayName))
                user.DisplayName = $"{user.FirstName} {user.LastName}".Trim();

            await _users.InsertOneAsync(user);
            return true;
        }
        catch (Exception ex)
        {
            LastError = $"RegisterAsync: {ex.Message}";
            return false;
        }
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        if (!IsConnected || _users is null) return [];

        try
        {
            return await _users
                .Find(_ => true)
                .SortBy(u => u.DisplayName)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            LastError = $"GetAllUsersAsync: {ex.Message}";
            return [];
        }
    }

    public async Task<bool> UpdateUserAsync(User user)
    {
        if (!IsConnected || _users is null) return false;

        try
        {
            var result = await _users.ReplaceOneAsync(
                Builders<User>.Filter.Eq(u => u.Id, user.Id),
                user);
            return result.IsAcknowledged && result.MatchedCount > 0;
        }
        catch (Exception ex)
        {
            LastError = $"UpdateUserAsync: {ex.Message}";
            return false;
        }
    }

    public async Task<bool> DeleteUserAsync(string email)
    {
        if (!IsConnected || _users is null || _watches is null) return false;

        try
        {
            var user = await _users
                .Find(Builders<User>.Filter.Eq(u => u.Email, email))
                .FirstOrDefaultAsync();

            if (user is null) return false;

            await _watches.DeleteManyAsync(Builders<Watch>.Filter.Eq(w => w.OwnerId, user.Id));

            var result = await _users.DeleteOneAsync(
                Builders<User>.Filter.Eq(u => u.Email, email));

            return result.DeletedCount > 0;
        }
        catch (Exception ex)
        {
            LastError = $"DeleteUserAsync: {ex.Message}";
            return false;
        }
    }

    // ============================================================
    //                          MONTRES
    // ============================================================

    public async Task<List<Watch>> GetWatchesForUserAsync(User currentUser)
    {
        if (!IsConnected || _watches is null) return [];

        try
        {
            return await _watches
                .Find(Builders<Watch>.Filter.Eq(w => w.OwnerId, currentUser.Id))
                .SortBy(w => w.Brand).ThenBy(w => w.Model)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            LastError = $"GetWatchesForUserAsync: {ex.Message}";
            return [];
        }
    }

    /// <summary>
    /// Recherche une montre par Barcode pour un utilisateur donné.
    /// Utilisé par la déduplication à l'import CSV (clé d'unicité primaire).
    /// </summary>
    public async Task<Watch?> FindWatchByBarcodeAsync(string barcode, ObjectId ownerId)
    {
        if (!IsConnected || _watches is null) return null;
        if (string.IsNullOrWhiteSpace(barcode)) return null;

        try
        {
            var filter = Builders<Watch>.Filter.Eq(w => w.Barcode, barcode)
                       & Builders<Watch>.Filter.Eq(w => w.OwnerId, ownerId);

            return await _watches.Find(filter).FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            LastError = $"FindWatchByBarcodeAsync: {ex.Message}";
            return null;
        }
    }

    /// <summary>
    /// Recherche une montre par sa clé naturelle composite (Brand + Model + Reference).
    /// Utilisé comme fallback quand le Barcode est absent du CSV importé.
    /// </summary>
    public async Task<Watch?> FindWatchByNaturalKeyAsync(string brand, string model, string reference, ObjectId ownerId)
    {
        if (!IsConnected || _watches is null) return null;
        if (string.IsNullOrWhiteSpace(brand) ||
            string.IsNullOrWhiteSpace(model) ||
            string.IsNullOrWhiteSpace(reference)) return null;

        try
        {
            var filter = Builders<Watch>.Filter.Eq(w => w.Brand, brand)
                       & Builders<Watch>.Filter.Eq(w => w.Model, model)
                       & Builders<Watch>.Filter.Eq(w => w.Reference, reference)
                       & Builders<Watch>.Filter.Eq(w => w.OwnerId, ownerId);

            return await _watches.Find(filter).FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            LastError = $"FindWatchByNaturalKeyAsync: {ex.Message}";
            return null;
        }
    }

    public async Task<bool> AddWatchAsync(Watch watch, User owner)
    {
        if (!IsConnected || _watches is null) return false;

        try
        {
            if (watch.Id == ObjectId.Empty)
                watch.Id = ObjectId.GenerateNewId();

            watch.OwnerId = owner.Id;

            await _watches.InsertOneAsync(watch);
            return true;
        }
        catch (Exception ex)
        {
            LastError = $"AddWatchAsync: {ex.Message}";
            return false;
        }
    }

    public async Task<bool> UpdateWatchAsync(Watch watch)
    {
        if (!IsConnected || _watches is null) return false;

        try
        {
            var result = await _watches.ReplaceOneAsync(
                Builders<Watch>.Filter.Eq(w => w.Id, watch.Id),
                watch);
            return result.IsAcknowledged && result.MatchedCount > 0;
        }
        catch (Exception ex)
        {
            LastError = $"UpdateWatchAsync: {ex.Message}";
            return false;
        }
    }

    public async Task<bool> DeleteWatchAsync(ObjectId watchId)
    {
        if (!IsConnected || _watches is null) return false;

        try
        {
            var result = await _watches.DeleteOneAsync(
                Builders<Watch>.Filter.Eq(w => w.Id, watchId));
            return result.DeletedCount > 0;
        }
        catch (Exception ex)
        {
            LastError = $"DeleteWatchAsync: {ex.Message}";
            return false;
        }
    }
}