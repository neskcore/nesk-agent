using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NeskAgent.Command.Models;

namespace NeskAgent.Command.Interfaces
{
    public interface IAgentPlugin
    {
        IReadOnlySet<string> SupportedActions { get; }
        Task<CommandResult> ExecuteAsync(JsonDocument command, CancellationToken ct);
    }
}
