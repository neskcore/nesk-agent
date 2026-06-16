using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NeskAgent.Command.Interfaces;
using NeskAgent.Command.Models;

namespace NeskAgent.Command
{
    public class CommandRouter
    {
        private readonly Dictionary<string, IAgentPlugin> _plugins = new();

        public void RegisterPlugin(IAgentPlugin plugin)
        {
            foreach (var action in plugin.SupportedActions)
            {
                _plugins[action] = plugin;
            }
        }

        public Task<CommandResult> RouteAsync(JsonDocument command, CancellationToken ct)
        {
            if (!command.RootElement.TryGetProperty("action", out var actionElement))
            {
                return Task.FromResult(CommandResult.Error("Missing 'action' field in command."));
            }

            var action = actionElement.GetString();
            if (string.IsNullOrWhiteSpace(action))
            {
                return Task.FromResult(CommandResult.Error("'action' field is empty."));
            }

            if (!_plugins.TryGetValue(action, out var plugin))
            {
                return Task.FromResult(CommandResult.Error($"No plugin registered for action: {action}"));
            }

            return plugin.ExecuteAsync(command, ct);
        }
    }
}
