using System;

namespace NeskAgent.Models
{
    public class Proxy
    {
        public string Id { get; set; }
        public string Domain { get; set; }
        public string TargetHost { get; set; }
        public int TargetPort { get; set; }
        public bool Enabled { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
