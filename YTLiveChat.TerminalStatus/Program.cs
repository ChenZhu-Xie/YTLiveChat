using YTLiveChat.TerminalStatus.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<StatusStore>();

var app = builder.Build();

// 关键：必须在 UseStaticFiles 之前调用，才能让 / 访问到 index.html
app.UseDefaultFiles(); 
app.UseStaticFiles();

app.MapHub<StatusHub>("/statusHub");

app.Run();
