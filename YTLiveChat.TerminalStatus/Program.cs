using System.Text;
using YTLiveChat.TerminalStatus.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<StatusStore>();

var app = builder.Build();

// 1. 设置控制台初始状态
Console.OutputEncoding = Encoding.UTF8;
Console.Title = "KanBan 看板";

// 2. 打印状态
Console.WriteLine();
Console.WriteLine(" ----------------------------------------");
Console.WriteLine($" URL: \x1b[36mhttp://localhost:5150/admin.html\x1b[0m");
Console.WriteLine(" ----------------------------------------");
Console.WriteLine();

// 关键：必须在 UseStaticFiles 之前调用，才能让 / 访问到 index.html
app.UseDefaultFiles(); 
app.UseStaticFiles();

app.MapHub<StatusHub>("/statusHub");

app.Run();
