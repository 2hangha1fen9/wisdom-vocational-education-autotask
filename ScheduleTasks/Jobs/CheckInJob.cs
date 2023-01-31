using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ScheduleTasks.Domain;
using ScheduleTasks.Utils;
using System.ComponentModel;
using System.Net;

namespace ScheduleTasks.Jobs
{
    public class CheckInJob
    {
        private readonly IHttpClientFactory clientFactory;
        private readonly MailHelper mailHelper;
        private readonly IConfiguration configuration;
        private readonly ILogger<CheckInJob> logger;

        public CheckInJob(IHttpClientFactory clientFactory, MailHelper mailHelper,IConfiguration configuration, ILogger<CheckInJob> logger) 
        { 
            this.clientFactory = clientFactory;
            this.mailHelper = mailHelper;
            this.configuration = configuration;
            this.logger = logger;
        }

        /// <summary>
        /// 执行签到
        /// </summary>
        /// <returns></returns>
        public async Task CheckIn(string checkInfoStr)
       {
            try
            {
                if (string.IsNullOrWhiteSpace(checkInfoStr))
                {
                    return;
                }
                var checkInfo = JsonConvert.DeserializeObject<CheckInReq>(checkInfoStr);

                //判断是否要进行打卡
                if (checkInfo.StartTime[2] == 0 && DateTime.Now.DayOfWeek == DayOfWeek.Saturday || checkInfo.StartTime[3] == 0 && DateTime.Now.DayOfWeek == DayOfWeek.Sunday)
                {
                    return;
                }
                //参数处理
                if (checkInfo.tFloat.Value)
                {
                    Random r = new Random();
                    Thread.Sleep(r.Next(5 * 60) * 1000);
                }
                if (checkInfo.lFloat.Value)
                {
                    Random r = new Random();
                    checkInfo.LocationX += r.NextDouble() / 10000.0;
                    checkInfo.LocationY += r.NextDouble() / 10000.0;
                }

                //获取登录token
                string token = null;
                for (int i = 1; i <= 3; i++)
                {
                    //设置http代理
                    SetProxyIP();
                    token = GetToken(checkInfo);
                    if (token != null)
                    {
                        break;
                    }
                }
                if (checkInfo != null)
                {
                    //构造请求体
                    var data = new List<KeyValuePair<string, string>> {
                        new KeyValuePair<string,string>("token",token),
                        new KeyValuePair<string,string>("checkType",checkInfo.CheckType),
                        new KeyValuePair<string,string>("locationX", Math.Round(checkInfo.LocationX,6).ToString()),
                        new KeyValuePair<string,string>("locationY", Math.Round(checkInfo.LocationY,6).ToString()),
                        new KeyValuePair<string,string>("scale", checkInfo.Scale.ToString()),
                        new KeyValuePair<string,string>("label", checkInfo.Label),
                        new KeyValuePair<string,string>("mapType", checkInfo.MapType),
                        new KeyValuePair<string,string>("content", checkInfo.Content),
                        new KeyValuePair<string,string>("isAbnormal", checkInfo.IsAbnormal.ToString()),
                        new KeyValuePair<string,string>("isEvection", checkInfo.IsEvection.ToString()),
                        new KeyValuePair<string,string>("studentId", checkInfo.StudentId.ToString()),
                        new KeyValuePair<string,string>("internshipId", checkInfo.InternshipId.ToString()),
                    };

                    //转为form格式字符串
                    var content = new FormUrlEncodedContent(data);
                    //使用代理发送请求
                    using (var client = clientFactory.CreateClient("executer"))
                    {
                        var response = await client.PostAsync("/mobile/process/stu-location/save", content);
                        //获取签到结果
                        JObject json = JObject.Parse(response.Content.ReadAsStringAsync().Result);
                        //发送邮件
                        if (!string.IsNullOrWhiteSpace(checkInfo.Email))
                        {
                            SendMail(checkInfo,response.StatusCode,json);
                        }
                        //签到结果处理
                        //签到失败抛出异常进行重试
                        if (response.StatusCode != HttpStatusCode.OK || (json["code"].ToString() == "1" || json["code"].ToString() == "") || json["success"].ToString() == "False")
                        {
                            if(json["msg"]?.ToString() != "今天你已经完成签到任务，不必再签到")
                            {
                                throw new Exception($"{checkInfo.LoginName} 签到失败\n{json.ToString()}");
                            }
                        }
                        else
                        {
                            logger.LogInformation($"{checkInfo.LoginName} 签到成功\n{json.ToString()}");
                        }                
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message ?? ex?.InnerException?.Message);
                throw;
            }
        }

        /// <summary>
        /// 设置代理ip
        /// </summary>
        private void SetProxyIP()
        {
            try
            {
                HttpClient.DefaultProxy = new WebProxy();
                string api = configuration.GetSection("HttpProxyAPI").Value;
                using (var request = new HttpClient())
                {
                    var response = request.GetAsync(api).Result;
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string ip = response.Content.ReadAsStringAsync().Result;
                        var proxy = new WebProxy(ip);
                        HttpClient.DefaultProxy = proxy;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning($"代理IP获取失败\n{ex.Message ?? ex?.InnerException?.Message}");
            }
            
        }

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="checkInfo"></param>
        /// <param name="response"></param>
        private async void SendMail(CheckInReq checkInfo, HttpStatusCode statusCode, JObject resultJson)
        {
            try
            {
                //创建通知邮件
                var mail = new MailTemplate
                {
                    ToEmail = checkInfo.Email,
                    Subject = "[慧职教+] 签到任务"
                };
                if (statusCode == HttpStatusCode.OK)
                {
                    if ((resultJson["code"].ToString() == "1" || resultJson["code"].ToString() == "") && resultJson["msg"].ToString() != "今天你已经完成签到任务，不必再签到" && resultJson["success"].ToString() == "False") //签到失败
                    {
                        mail.Body = $@"执行结果：{resultJson["msg"]}，稍后进行重试操作...</br>
                                   执行时间：{DateTime.Now.ToLocalTime()}</br><hr/>
                                   登录<a href='http://internship.zhfsmy.cloud/' target='_blank'>慧职教+</a>查看详情";
                    }
                    else
                    {
                        if(resultJson["msg"].ToString() == "今天你已经完成签到任务，不必再签到")
                        {
                            mail.Body = $@"执行结果：{resultJson["msg"]}</br>
                                   执行时间：{DateTime.Now.ToLocalTime()}</br><hr/>
                                   登录<a href='http://internship.zhfsmy.cloud/' target='_blank'>慧职教+</a>查看详情";
                        }
                        else
                        {
                            mail.Body = $@"执行结果：签到成功</br>
                                执行时间：{DateTime.Now.ToLocalTime()}</br>
                                打卡位置：{checkInfo.Label}</br>
                                打卡坐标：X:{Math.Round(checkInfo.LocationX, 6)} Y:{Math.Round(checkInfo.LocationY, 6)}</br><hr/>
                                登录<a href='http://internship.zhfsmy.cloud/' target='_blank'>慧职教+</a>查看详情";
                        }
                    }
                }
                else //执行失败
                {
                    mail.Body = $@"执行结果：目标服务器响应超时，稍后进行重试操作...</br>
                               执行时间：{DateTime.Now.ToLocalTime()}</br>
                               状态码：{statusCode}</br>";
                }
                await Task.Run(() => mailHelper.SendEmail(mail));
            }
            catch (Exception ex)
            {
                logger.LogWarning($"{checkInfo.LoginName}邮件发送失败\n{ex.Message ?? ex.InnerException?.Message }");
            }
        }

        /// <summary>
        /// 获取token
        /// </summary>
        /// <returns></returns>
        private string GetToken(CheckInReq checkInfo)
        {
            try
            {
                //构造请求体
                var data = new List<KeyValuePair<string, string>> {
                    new KeyValuePair<string,string>("loginName",checkInfo.LoginName),
                    new KeyValuePair<string,string>("password",checkInfo.Password),
                    new KeyValuePair<string,string>("schoolId",checkInfo.SchoolId.ToString())
                };
                //转为form格式字符串
                var content = new FormUrlEncodedContent(data);
                //发送请求
                using (var client = clientFactory.CreateClient("executer"))
                {
                    var response = client.PostAsync("/mobile/login", content).Result;
                    //转换为Json对象
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        //转换为Json对象
                        JObject json = JObject.Parse(response.Content.ReadAsStringAsync().Result);
                        if (string.IsNullOrWhiteSpace(json["msg"].ToString()))
                        {
                            return json["data"]["sessionId"].ToString();
                        }
                        throw new Exception(json.ToString());
                    }
                    throw new Exception("网络错误");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning($"{checkInfo.LoginName}获取Token失败\n{ex.Message ?? ex.InnerException?.Message}");
                return null;
            }
        }
    }
}
