using System;
using System.Collections.Generic;

class Program {
    static void Main() {
        string path = "https://api.openrouteservice.org/geocode/search";
        string baseUrl = "https://api.openrouteservice.org";
        
        string fullUrl;
        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
            path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            fullUrl = path;
        }
        else
        {
            var bUrl = baseUrl.TrimEnd('/');
            var relativePath = path.StartsWith('/') ? path : "/" + path;
            fullUrl = bUrl + relativePath;
        }
        
        Console.WriteLine("Result: " + fullUrl);
    }
}
