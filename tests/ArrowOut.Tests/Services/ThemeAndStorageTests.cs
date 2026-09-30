using System.Text;
using ArrowOut.Services.Exceptions;
using ArrowOut.Services.Storage;
using ArrowOut.Services.Themes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArrowOut.Tests.Services;

public sealed class ThemeAndStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "arrowout-tests-" + Guid.NewGuid().ToString("N"));
    private readonly LocalFileStorage _storage;
    private readonly ThemeService _themes;

    public ThemeAndStorageTests()
    {
        _storage = new LocalFileStorage(_root);
        _themes = new ThemeService(_storage, new MemoryCache(new MemoryCacheOptions()), NullLogger<ThemeService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static MemoryStream Json(string json) => new(Encoding.UTF8.GetBytes(json));

    private const string OceanTheme = """
        {"key":"ocean","name":"Ocean","colors":{"background":"#eef5fb","surface":"#ffffff","grid":"#bcd3e6",
         "text":"#12324a","muted":"#5b7a91","arrow":"#12324a","accent":"#1f7ae0","danger":"#d64545","success":"#2e9d5b"}}
        """;

    [Fact]
    public async Task EnsureDefaults_WritesBuiltInThemesToStorage()
    {
        await _themes.EnsureDefaultThemesAsync();

        var files = await _storage.ListAsync(ThemeService.Container);
        Assert.Contains("classic.json", files);
        Assert.Contains("midnight.json", files);
        Assert.Equal(files.Count, (await _themes.GetAllAsync()).Count);
    }

    [Fact]
    public async Task EnsureDefaults_RefreshesStaleBuiltInFiles_ButKeepsCustomThemes()
    {
        // An older install still has the old colours saved on disk.
        await _storage.WriteTextAsync(ThemeService.Container, "classic.json",
            """{"key":"classic","name":"Classic Paper","colors":{"background":"#f7f5f0","surface":"#ffffff","grid":"#d9d4c7","text":"#1f1f1f","muted":"#6b6b6b","arrow":"#1f1f1f","accent":"#2f6fed","danger":"#d64545","success":"#2e9d5b"}}""");
        await _themes.SaveAsync(Json(OceanTheme));

        await _themes.EnsureDefaultThemesAsync();

        var classic = await _themes.GetOrDefaultAsync("classic");
        Assert.Equal("Peach Morning", classic.Name);
        Assert.Equal(new ThemeColors().Background, classic.Colors.Background);
        Assert.Equal("#eef5fb", (await _themes.GetOrDefaultAsync("ocean")).Colors.Background); // untouched
    }

    [Fact]
    public async Task Save_ValidTheme_IsStoredUnderItsKey()
    {
        var theme = await _themes.SaveAsync(Json(OceanTheme));

        Assert.Equal("ocean", theme.Key);
        Assert.True(await _storage.ExistsAsync(ThemeService.Container, "ocean.json"));
        Assert.True(await _themes.ExistsAsync("ocean"));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"key":"x","name":"Bad key","colors":{}}""")]
    [InlineData("""{"key":"evil","name":"Evil","colors":{"background":"red;} body{display:none","surface":"#ffffff","grid":"#000000","text":"#000000","muted":"#000000","arrow":"#000000","accent":"#000000","danger":"#000000","success":"#000000"}}""")]
    [InlineData("""{"key":"../../etc","name":"Traversal","colors":{}}""")]
    public async Task Save_InvalidTheme_IsRejected(string json)
    {
        await Assert.ThrowsAsync<ThemeValidationException>(() => _themes.SaveAsync(Json(json)));
    }

    [Fact]
    public async Task Save_OversizedFile_IsRejected()
    {
        var huge = new string(' ', (int)ThemeService.MaxFileBytes + 1);

        await Assert.ThrowsAsync<ThemeValidationException>(() => _themes.SaveAsync(Json(huge)));
    }

    [Fact]
    public async Task Save_CannotOverwriteBuiltIn()
    {
        var json = OceanTheme.Replace("\"ocean\"", "\"classic\"", StringComparison.Ordinal);

        await Assert.ThrowsAsync<ThemeValidationException>(() => _themes.SaveAsync(Json(json)));
    }

    [Fact]
    public async Task Delete_BuiltIn_IsNotAllowed()
    {
        await _themes.EnsureDefaultThemesAsync();

        await Assert.ThrowsAsync<OperationNotAllowedException>(() => _themes.DeleteAsync("classic"));
    }

    [Fact]
    public async Task Delete_CustomTheme_RemovesFile()
    {
        await _themes.SaveAsync(Json(OceanTheme));

        await _themes.DeleteAsync("ocean");

        Assert.False(await _themes.ExistsAsync("ocean"));
    }

    [Fact]
    public async Task GetOrDefault_UnknownKey_FallsBackToClassic()
    {
        var theme = await _themes.GetOrDefaultAsync("does-not-exist");

        Assert.Equal(ThemeService.DefaultKey, theme.Key);
    }

    [Fact]
    public async Task CorruptThemeFile_IsSkipped()
    {
        await _themes.EnsureDefaultThemesAsync();
        await _storage.WriteTextAsync(ThemeService.Container, "broken.json", "{ nope");

        var themes = await new ThemeService(_storage, new MemoryCache(new MemoryCacheOptions()), NullLogger<ThemeService>.Instance).GetAllAsync();

        Assert.DoesNotContain(themes, t => t.Key == "broken");
    }

    [Theory]
    [InlineData("../secrets.json")]
    [InlineData("..")]
    [InlineData("sub/file.json")]
    [InlineData("C:\\windows\\win.ini")]
    [InlineData("UPPER.json")]
    [InlineData("")]
    public async Task Storage_RejectsUnsafeNames(string fileName)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _storage.WriteTextAsync("themes", fileName, "x"));
    }

    [Fact]
    public async Task Storage_RoundTripsText()
    {
        await _storage.WriteTextAsync("exports", "a.json", "{}");

        Assert.Equal("{}", await _storage.ReadTextAsync("exports", "a.json"));
        Assert.True(await _storage.DeleteAsync("exports", "a.json"));
        Assert.Null(await _storage.ReadTextAsync("exports", "a.json"));
    }
}
