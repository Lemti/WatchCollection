using System.Text.Json.Serialization;
using Avalonia.Media;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WatchCollection.Models;

public class Watch
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.Empty;

    [BsonElement("OwnerId")]
    public ObjectId OwnerId { get; set; } = ObjectId.Empty;

    [BsonElement("Barcode")]
    public string Barcode { get; set; } = string.Empty;

    [BsonElement("Brand")]
    public string Brand { get; set; } = string.Empty;

    [BsonElement("Model")]
    public string Model { get; set; } = string.Empty;

    [BsonElement("Reference")]
    public string Reference { get; set; } = string.Empty;

    [BsonElement("Movement")]
    public string Movement { get; set; } = string.Empty;

    [BsonElement("Diameter")]
    public double Diameter { get; set; }

    [BsonElement("CaseMaterial")]
    public string CaseMaterial { get; set; } = string.Empty;

    [BsonElement("Price")]
    public decimal Price { get; set; }

    [BsonElement("Year")]
    public int Year { get; set; }

    [BsonElement("Stock")]
    public int Stock { get; set; }

    [BsonIgnore]
    [JsonIgnore]
    public IImage? Picture { get; set; }

    /// <summary>
    /// Indique si la montre a des champs essentiels manquants (utile après import CSV partiel).
    /// Permet d'afficher un badge "À compléter" dans la liste pour aider l'utilisateur
    /// à repérer les fiches incomplètes à enrichir.
    /// </summary>
    [BsonIgnore]
    [JsonIgnore]
    public bool IsIncomplete =>
        string.IsNullOrWhiteSpace(Barcode)
        || string.IsNullOrWhiteSpace(Reference)
        || Diameter <= 0
        || Price <= 0;
}