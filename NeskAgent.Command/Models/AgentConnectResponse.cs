namespace NeskAgent.Command.Models
{
    public class AgentConnectResponse
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Ip { get; set; }
        public string? Status { get; set; }
        public string? Token { get; set; }
        public string? RefreshToken { get; set; }
        public string? ExpiresAt { get; set; }
    }
}