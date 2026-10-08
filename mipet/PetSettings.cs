using System;
using System.IO;
using System.Text.Json;

namespace mipet
{
    public class PetSettings
    {
        public double Scale { get; set; } = 1.0;
        public double FlingPower { get; set; } = 0.5;
        public double MaxAngle { get; set; } = 45;
        public bool ReverseFling { get; set; } = true;

        // 사용자가 고른 이미지 경로 (null이면 기본 이미지)
        public string? SittingBodyPath { get; set; }
        public string? HeadOpenPath { get; set; }
        public string? HeadClosedPath { get; set; }
        public string? GrabbedImagePath { get; set; }
        public string? FallingImagePath { get; set; }
        public string? LandedImagePath { get; set; }

        public const double Damping = 3.0;
        public const double SpringStrength = 60;
        public const double IdleWiggle = 1.5;

        private static string FilePath
        {
            get
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "mipet");
                return Path.Combine(folder, "settings.json");
            }
        }

        public static PetSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    var loaded = JsonSerializer.Deserialize<PetSettings>(json);
                    if (loaded != null) return loaded;
                }
            }
            catch { }
            return new PetSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch { }
        }
    }
}