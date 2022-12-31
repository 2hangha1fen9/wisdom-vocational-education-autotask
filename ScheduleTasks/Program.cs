using Hangfire;
using ScheduleTasks.Domain;
using ScheduleTasks.Utils;
using StackExchange.Redis;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//邮件配置注册
builder.Services.AddSingleton<MailConfig>(builder.Configuration.GetSection("MailConfig").Get<MailConfig>());
builder.Services.AddScoped<MailHelper>();
//Redis客户端注册
builder.Services.AddSingleton<IConnectionMultiplexer>(cm =>
{
    return ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("redis"));
});
//注册http客户端用于发起请求
builder.Services.AddHttpClient("executer", conf =>
{
    conf.BaseAddress = new Uri(builder.Configuration.GetConnectionString("api"));
    conf.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");
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
app.UseHangfireDashboard();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.UseCors("default"); //使用默认跨域配置

app.Run();
