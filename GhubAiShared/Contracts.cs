namespace GhubAiShared
{
    public record NodeRegistration(string MachineName, IEnumerable<string> AvailableModels);

    public record InferenceRequest(
        string RequestId,
        string Model,
        string EndpointUri,
        string RequestBody);

    public record InferenceChunk(string RequestId, string TokenFragment, bool IsFinal = false);

    public class NodeMetadata
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string MachineName { get; set; } = string.Empty;
        public HashSet<string> HostedModels { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int CurrentLoad; // made a field to allow Interlocked operations
        public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
    }
}
