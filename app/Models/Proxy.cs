using System;
using Newtonsoft.Json;

namespace NeskAgent.Models
{
    public class Proxy
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("domain")]
        public string Domain { get; set; }

        [JsonProperty("target_host")]
        public string TargetHost { get; set; }

        [JsonProperty("target_port")]
        public int? TargetPort { get; set; }

        [JsonProperty("enabled")]
        public bool Enabled { get; set; }

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
