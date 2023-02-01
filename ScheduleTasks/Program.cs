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
//���ö˿�
builder.WebHost.UseUrls($"http://*:{port}");

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//�ʼ�����ע��
builder.Services.AddSingleton(builder.Configuration.GetSection("MailConfig").Get<MailConfig>());
builder.Services.AddScoped<MailHelper>();
//Redis�ͻ���ע��
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

//���üƻ�����
builder.Services.AddHangfire(configuration =>
{
    //ʹ��redis�洢
    configuration.UseRedisStorage(builder.Configuration.GetConnectionString("redis"));
});
//�ƻ�����
builder.Services.AddHangfireServer();

//�������ؿ���
builder.Services.AddCors(option =>
{
    //���Ĭ������
    option.AddPolicy("default", policy =>
    {
        //ǰ�˵�ַ
        policy.SetIsOriginAllowed(s => true) //����Դ��ַ
        .AllowAnyHeader() //��������ͷ����Ϣ
        .AllowAnyMethod();//��������Http����
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//���üƻ�����������
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
    DashboardTitle = "��ְ��+�����������",
});

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.UseCors("default"); //ʹ��Ĭ�Ͽ�������

app.Run();
