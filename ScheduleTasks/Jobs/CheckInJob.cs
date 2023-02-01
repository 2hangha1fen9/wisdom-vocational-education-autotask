
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ScheduleTasks.Domain;
using ScheduleTasks.Utils;
using System.Net;

namespace ScheduleTasks.Jobs
{
    public class CheckInJob
    {
        private readonly DynamicProxyHttpClientFactory clientFactory;
        private readonly MailHelper mailHelper;
        private readonly ILogger<CheckInJob> logger;

        public CheckInJob(DynamicProxyHttpClientFactory clientFactory, MailHelper mailHelper, ILogger<CheckInJob> logger)
        {
            this.clientFactory = clientFactory;
            this.mailHelper = mailHelper;
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
                string token = await GetToken(checkInfo);
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

                    using (var httpClient = clientFactory.CreateClient("executer"))
                    {
                        var response = await httpClient.PostAsync("/mobile/process/stu-location/save", content);
                        //获取签到结果
                        JObject json = new JObject();
                        if (response.IsSuccessStatusCode)
                        {
                            var respContent = await response.Content.ReadAsStringAsync();
                            json = JObject.Parse(respContent);
                        }

                        //发送邮件
                        if (!string.IsNullOrWhiteSpace(checkInfo.Email))
                        {
                            SendMail(checkInfo, response.StatusCode, json);
                        }

                        //签到失败抛出异常进行重试
                        if (!response.IsSuccessStatusCode || (json["code"].ToString() == "1" || json["code"].ToString() == "") || json["success"].ToString() == "False")
                        {
                            if (json["msg"]?.ToString() != "今天你已经完成签到任务，不必再签到")
                            {
                                throw new Exception($"{checkInfo.LoginName} 签到失败\n{json}");
                            }
                        }
                        else
                        {
                            logger.LogInformation($"{checkInfo.LoginName} 签到成功\n{json}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"{ex.Message ?? ex?.InnerException?.Message}");
                throw;
            }
        }

        /// <summary>
        /// 获取token
        /// </summary>
        /// <returns></returns>
        private async Task<string> GetToken(CheckInReq checkInfo)
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

                //登录获取Token
                using (var httpClient = clientFactory.CreateClient("executer"))
                {
                    var response = new HttpResponseMessage(HttpStatusCode.BadGateway);
                    response = await httpClient.PostAsync("/mobile/login", content);
                    response.EnsureSuccessStatusCode();

                    //将响应转换为Json获取Token
                    var respContent = await response.Content.ReadAsStringAsync();
                    JObject json = JObject.Parse(respContent);
                    if (string.IsNullOrWhiteSpace(json["msg"].ToString()))
                    {
                        return json["data"]["sessionId"].ToString();
                    }

                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning($"{checkInfo.LoginName}获取Token失败\n{ex.Message ?? ex.InnerException?.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="checkInfo"></param>
        /// <param name="response"></param>
        private void SendMail(CheckInReq checkInfo, HttpStatusCode statusCode, JObject resultJson)
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
                        if (resultJson["msg"].ToString() == "今天你已经完成签到任务，不必再签到")
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
                Task.Run(() => mailHelper.SendEmail(mail));
            }
            catch (Exception ex)
            {
                logger.LogWarning($"{checkInfo.LoginName}邮件发送失败\n{ex.Message ?? ex.InnerException?.Message}");
            }
        }
    }
}
