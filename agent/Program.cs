using GameControl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
var builder=Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options=>options.ServiceName="Playtimely Agent");
builder.Services.AddHostedService<AgentWorker>();
builder.Services.AddHostedService<AutomaticUpdate>();
await builder.Build().RunAsync();
