using System;
using System.Text.Json.Serialization;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MongoDB.Bson;

namespace WatchCollection.Models;

public class Watch
{
    public Watch() { }
    public ObjectId Id { get; set; } = ObjectId.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string Movement { get; set; } = string.Empty;
    public double Diameter { get; set; }
    public string CaseMaterial { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Year { get; set; }
    public int Stock { get; set; }
    [JsonIgnore]
    internal IImage? Picture { get; set; }
}
