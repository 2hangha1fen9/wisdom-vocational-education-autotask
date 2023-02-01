using Hangfire;
using Hangfire.Dashboard.BasicAuthorization;
using Microsoft.Extensions.DependencyInjection;
using ScheduleTasks.Domain;
using ScheduleTasks.Utils;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
}


var port = builder.Configuration.GetValue<int>("port");
//配置端口
builder.WebHost.UseUrls($"http://*:{port}");

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//邮件配置注册
builder.Services.AddSingleton(builder.Configuration.GetSection("MailConfig").Get<MailConfig>());
builder.Services.AddScoped<MailHelper>();
//Redis客户端注册
builder.Services.AddSingleton<IConnectionMultiplexer>(cm =>
{
    return ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("redis"));
});
//HttpClient
builder.Services.AddSingleton(new DynamicProxyHttpClientFactory
{
    BaseAddress = new Uri(builder.Configuration.GetConnectionString("api")),
    DefaultContentType = "application/x-www-form-urlencoded",
    Timeout = TimeSpan.FromSeconds(300),
    ProxyAPI = builder.Configuration.GetConnectionString("proxyApi")
});

//配置计划任务
builder.Services.AddHangfire(configuration =>
{
    //使用redis存储
    configuration.UseRedisStorage(builder.Configuration.GetConnectionString("redis"));
});
//计划任务
builder.Services.AddHangfireServer();

//配置网关跨域
builder.Services.AddCors(option =>
{
    //添加默认配置
    option.AddPolicy("default", policy =>
    {
        //前端地址
        policy.SetIsOriginAllowed(s => true) //任意源地址
        .AllowAnyHeader() //允许任意头部信息
        .AllowAnyMethod();//允许任意Http动词
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//启用计划任务控制面板
app.UseHangfireDashboard("/hangfire",new DashboardOptions
{
    Authorization = new[]
    {
        new BasicAuthAuthorizationFilter(new BasicAuthAuthorizationFilterOptions
        {
            RequireSsl = false,
            SslRedirect = false,
            LoginCaseSensitive = true,
            Users = new []
            {
                new BasicAuthAuthorizationUser
                {
                    Login = "hangfire",
                    PasswordClear =  "000000"
                }
            }
        })
    },
    DashboardTitle = "慧职教+任务调度中心",
});

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.UseCors("default"); //使用默认跨域配置

app.Run();
