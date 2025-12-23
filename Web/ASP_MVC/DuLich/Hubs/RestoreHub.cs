using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace DuLich.Hubs
{
    // By using an interface, we can leverage the IHubContext<RestoreHub, IRestoreClient>
    // to have strongly-typed client method calls, which is less error-prone than using strings.
    public interface IRestoreClient
    {
        Task ReceiveRestoreProgress(string status, string message, int percent);
    }

    public class RestoreHub : Hub<IRestoreClient>
    {
        // This method can be called by a client if we need two-way communication,
        // but for now, we only need server-to-client communication.
        // The server will send messages to clients via the IHubContext.
        public override async Task OnConnectedAsync()
        {
            // You could add users to groups here if you wanted to send messages
            // to specific users, for example, based on their username.
            // For now, we broadcast to all admins.
            // await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
            await base.OnConnectedAsync();
        }
    }
}
