namespace ClusterManager.DTO;

public class NodeConfigurationDTO
{
    public string Image { get; set; }
    public string ContainerName { get; set; }
    public Dictionary<string, string> EnvironmentVariables { get; set; }
    public Dictionary<string, string> PortBindings { get; set; }
}
