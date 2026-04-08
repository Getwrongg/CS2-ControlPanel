using System.IO;
using CS2AdminTool.Models;

namespace CS2AdminTool.Services;

public class ConfigLibraryService
{
    private readonly JsonStorageService _storageService;

    public ConfigLibraryService(JsonStorageService storageService)
    {
        _storageService = storageService;
    }

    public string DataRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CS2AdminTool", "Data");
    private string CategoriesFile => Path.Combine(DataRoot, "categories.json");
    private string MapsFile => Path.Combine(DataRoot, "maps.json");
    private string ServerConfigsFile => Path.Combine(DataRoot, "serverConfigs.json");

    public async Task<AppDataStore> LoadAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);

        var categories = await _storageService.LoadAsync<List<ConfigCategory>>(CategoriesFile, cancellationToken) ?? new List<ConfigCategory>();
        var maps = await _storageService.LoadAsync<List<MapProfile>>(MapsFile, cancellationToken) ?? new List<MapProfile>();
        var configs = await _storageService.LoadAsync<List<ServerConfigProfile>>(ServerConfigsFile, cancellationToken) ?? new List<ServerConfigProfile>();

        EnsureCategories(categories);

        return new AppDataStore
        {
            Categories = categories.OrderBy(c => c.Name).ToList(),
            Maps = maps.OrderBy(m => m.DisplayName).ToList(),
            ServerConfigs = configs.OrderBy(c => c.Name).ToList(),
            RunnerOptions = new RunnerOptions()
        };
    }

    public async Task SaveAsync(AppDataStore dataStore, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(DataRoot);
        await _storageService.SaveAsync(CategoriesFile, dataStore.Categories.OrderBy(c => c.Name).ToList(), cancellationToken);
        await _storageService.SaveAsync(MapsFile, dataStore.Maps.OrderBy(m => m.DisplayName).ToList(), cancellationToken);
        await _storageService.SaveAsync(ServerConfigsFile, dataStore.ServerConfigs.OrderBy(c => c.Name).ToList(), cancellationToken);
    }

    public async Task ExportAllAsync(string filePath, AppDataStore dataStore, CancellationToken cancellationToken = default)
    {
        await _storageService.SaveAsync(filePath, dataStore, cancellationToken);
    }

    public async Task<AppDataStore?> ImportAllAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return await _storageService.LoadAsync<AppDataStore>(filePath, cancellationToken);
    }

    public async Task ExportConfigsAsync(string filePath, IEnumerable<ServerConfigProfile> configs, CancellationToken cancellationToken = default)
    {
        await _storageService.SaveAsync(filePath, configs.ToList(), cancellationToken);
    }

    public async Task<List<ServerConfigProfile>> ImportConfigsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return await _storageService.LoadAsync<List<ServerConfigProfile>>(filePath, cancellationToken) ?? new List<ServerConfigProfile>();
    }


    public async Task ExportConfigAsync(string filePath, ServerConfigProfile config, CancellationToken cancellationToken = default)
    {
        await _storageService.SaveAsync(filePath, config, cancellationToken);
    }

    public async Task<ServerConfigProfile?> ImportConfigAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return await _storageService.LoadAsync<ServerConfigProfile>(filePath, cancellationToken);
    }
    public async Task ExportMapsAsync(string filePath, IEnumerable<MapProfile> maps, CancellationToken cancellationToken = default)
    {
        await _storageService.SaveAsync(filePath, maps.ToList(), cancellationToken);
    }

    public async Task<List<MapProfile>> ImportMapsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return await _storageService.LoadAsync<List<MapProfile>>(filePath, cancellationToken) ?? new List<MapProfile>();
    }

    private async Task EnsureSeededAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(DataRoot);

        var appBase = AppContext.BaseDirectory;
        var seedRoot = Path.Combine(appBase, "Data", "Seeds");

        await _storageService.CopyIfMissingAsync(Path.Combine(seedRoot, "categories.json"), CategoriesFile, cancellationToken);
        await _storageService.CopyIfMissingAsync(Path.Combine(seedRoot, "maps.json"), MapsFile, cancellationToken);
        await _storageService.CopyIfMissingAsync(Path.Combine(seedRoot, "serverConfigs.json"), ServerConfigsFile, cancellationToken);
    }

    private static void EnsureCategories(List<ConfigCategory> categories)
    {
        var defaults = new[] { "Practice", "Competitive", "Deathmatch", "Surf", "Bhop", "Deathrun", "Fun", "Custom" };
        foreach (var name in defaults)
        {
            if (!categories.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                categories.Add(new ConfigCategory { Name = name, Description = $"{name} setups" });
            }
        }
    }
}
