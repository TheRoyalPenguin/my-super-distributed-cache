namespace ClusterManager.DTO;

public class NodeStatusDto
{
    public string NodeId { get; set; }
    public string Url { get; set; }
    public bool IsOnline { get; set; }
    public int StatusCode { get; set; }
}
