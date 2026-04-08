using CS2AdminTool.Models;

namespace CS2AdminTool.Services;

public class MapLibraryService
{
    public MapProfile Duplicate(MapProfile source)
    {
        return new MapProfile
        {
            Id = Guid.NewGuid(),
            DisplayName = $"{source.DisplayName} Copy",
            Category = source.Category,
            IsWorkshopMap = source.IsWorkshopMap,
            WorkshopMapId = source.WorkshopMapId,
            StandardMapName = source.StandardMapName,
            Notes = source.Notes,
            Tags = source.Tags.ToList()
        };
    }
}
