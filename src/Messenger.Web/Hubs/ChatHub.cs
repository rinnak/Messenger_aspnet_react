using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Messenger.Web.Hubs;

[Authorize]
public sealed class ChatHub : Hub
{
    
}