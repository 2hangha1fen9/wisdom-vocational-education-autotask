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

//�ʼ�����ע��
builder.Services.AddSingleton<MailConfig>(builder.Configuration.GetSection("MailConfig").Get<MailConfig>());
builder.Services.AddScoped<MailHelper>();
//Redis�ͻ���ע��
builder.Services.AddSingleton<IConnectionMultiplexer>(cm =>
{
    return ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("redis"));
});
//ע��http�ͻ������ڷ�������
builder.Services.AddHttpClient("executer", conf =>
{
    conf.BaseAddress = new Uri(builder.Configuration.GetConnectionString("api"));
    conf.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");
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
app.UseHangfireDashboard();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.UseCors("default"); //ʹ��Ĭ�Ͽ�������

app.Run();
