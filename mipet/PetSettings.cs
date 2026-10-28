using System;
using System.IO;
using System.Text.Json;

namespace mipet
{
    public class PetSettings
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "캐릭터 1";
        public bool IsVisible { get; set; } = true;
        public bool HeadOnTop { get; set; } = true;
        public double Scale { get; set; } = 1.0;
        public double FlingPower { get; set; } = 0.5;
        public double MaxAngle { get; set; } = 45;
        public bool ReverseFling { get; set; } = true;
        public string? SittingBodyPath { get; set; }
        public string? HeadOpenPath { get; set; }
        public string? HeadClosedPath { get; set; }
        public string? GrabbedImagePath { get; set; }
        public string? FallingImagePath { get; set; }
        public string? LandedImagePath { get; set; }
        public const double Damping = 3.0;
        public const double SpringStrength = 60;
        public const double IdleWiggle = 1.5;
        private static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "mipet", "settings.json");

        public static List<PetSettings> LoadAll()
        {
            try
            {
                if (!File.Exists(FilePath)) return new();
                string json = File.ReadAllText(FilePath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    return JsonSerializer.Deserialize<List<PetSettings>>(json) ?? new();
                var old = JsonSerializer.Deserialize<PetSettings>(json);
                if (old != null) { old.Id = string.IsNullOrWhiteSpace(old.Id) ? Guid.NewGuid().ToString("N") : old.Id; old.Name = "캐릭터 1"; return new() { old }; }
            }
            catch { }
            return new();
        }
        public static void SaveAll(IEnumerable<PetSettings> pets)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(pets, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }
    }
}
