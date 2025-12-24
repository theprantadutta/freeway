namespace Freeway.Application.DTOs;

public class ProjectDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ApiKeyPrefix { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsActive { get; set; }
    public int RateLimitPerMinute { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public class ProjectWithKeyDto : ProjectDto
{
    public string ApiKey { get; set; } = string.Empty;
}

public class CreateProjectRequest
{
    public string Name { get; set; } = string.Empty;
    public int RateLimitPerMinute { get; set; } = 60;
    public Dictionary<string, object>? Metadata { get; set; }
}

public class UpdateProjectRequest
{
    public string? Name { get; set; }
    public bool? IsActive { get; set; }
    public int? RateLimitPerMinute { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public class ProjectsListDto
{
    public List<ProjectDto> Projects { get; set; } = new();
    public int TotalCount { get; set; }
}

public class RotateKeyResultDto
{
    public Guid Id { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string ApiKeyPrefix { get; set; } = string.Empty;
}
