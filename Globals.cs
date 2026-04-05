using System.Collections.Generic;
using WatchCollection.Models;

namespace WatchCollection;

public static class MyGlobals
{
    public static List<Watch> MyWatches { get; set; } = [];
    public static User? CurrentUser { get; set; }
    public static bool IsAdmin => CurrentUser?.Role == "admin";
}