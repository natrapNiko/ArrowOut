using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArrowOut.Data.Common;
using ArrowOut.Game;
using ArrowOut.Services.Models;

namespace ArrowOut.Services.Levels;

// JSON format for exporting and importing levels.
public sealed class LevelFile
{
    public const string FormatName = "arrowout-level";
    public const int CurrentVersion = 1;
    public const long MaxFileBytes = 64 * 1024;

    public string Format { get; set; } = FormatName;

    public int Version { get; set; } = CurrentVersion;

    [Range(DataConstants.Level.NumberMin, DataConstants.Level.NumberMax)]
    public int Number { get; set; }

    [Required]
    [StringLength(DataConstants.Level.NameMaxLength, MinimumLength = DataConstants.Level.NameMinLength)]
    [RegularExpression(DataConstants.Level.NamePattern)]
    public string Name { get; set; } = string.Empty;

    [Range(DataConstants.Level.SizeMin, DataConstants.Level.SizeMax)]
    public int Width { get; set; }

    [Range(DataConstants.Level.SizeMin, DataConstants.Level.SizeMax)]
    public int Height { get; set; }

    [Range(DataConstants.Level.LivesMin, DataConstants.Level.LivesMax)]
    public int MaxLives { get; set; } = 3;

    [EnumDataType(typeof(Difficulty))]
    public Difficulty Difficulty { get; set; }

    [Required]
    public List<ArrowInputModel> Arrows { get; set; } = [];

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        MaxDepth = 8,
        Converters = { new JsonStringEnumConverter() },
    };
}
