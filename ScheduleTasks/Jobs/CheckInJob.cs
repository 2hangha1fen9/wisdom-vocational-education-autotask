
using Hangfire;
using Hangfire.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ScheduleTasks.Domain;
using ScheduleTasks.Utils;
using System.Net;
using StackExchange.Redis;

namespace ScheduleTasks.Jobs
{
    public class CheckInJob
    {
        private readonly DynamicProxyHttpClientFactory clientFactory;
        private readonly MailHelper mailHelper;
        private readonly ILogger<CheckInJob> logger;
        private readonly IConnectionMultiplexer redisConnection;

        public CheckInJob(DynamicProxyHttpClientFactory clientFactory, MailHelper mailHelper, ILogger<CheckInJob> logger,IConnectionMultiplexer redisConnection)
        {
            this.clientFactory = clientFactory;
            this.mailHelper = mailHelper;
            this.logger = logger;
            this.redisConnection = redisConnection;
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
                
                Random r = new Random();
                //参数处理
                if (checkInfo.tFloat.Value)
                {
                    Thread.Sleep(r.Next(5 * 60) * 1000);
                }
                if (checkInfo.lFloat.Value)
                {
                    checkInfo.LocationX += r.NextDouble() / 10000.0;
                    checkInfo.LocationY += r.NextDouble() / 10000.0;
                }
                //获取附件列表
                var attachs = GetAttachs(checkInfo.LoginName);
                if (checkInfo.RandomAttach.Value && attachs.Count > 0)
                {
                    // 随机选择的附件数量
                    int count = new Random().Next(1, attachs.Count); // 随机选择3、4、5或6个附件
                    
                    int n = attachs.Count;
                    while (n > 1)
                    {
                        n--;
                        int k = new Random().Next(n + 1);
                        (attachs[k], attachs[n]) = (attachs[n], attachs[k]);
                    }

                    // 选择前 numAttachments 个附件
                    attachs = attachs.Take(count).ToList();
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
                    if (attachs.Count > 0)
                    {
                        data.Add(new("attachIds",string.Join(',',attachs.Select(a => a.Id))));
                    }
                    
                    //转为form格式字符串
                    var content = new FormUrlEncodedContent(data);

                    int retryCount = 3;
                    //获取签到结果
                    var response = new HttpResponseMessage(HttpStatusCode.BadGateway);
                    JObject json = new JObject();
                    while (retryCount > 0)
                    {
                        using (var httpClient = clientFactory.CreateClient("executer"))
                        {
                            try
                            {
                                response = await httpClient.PostAsync("/mobile/process/stu-location/save", content);
                                response.EnsureSuccessStatusCode();
                                var respContent = await response.Content.ReadAsStringAsync();
                                json = JObject.Parse(respContent);
                                break;
                            }
                            catch (Exception ex)
                            {
                                logger.LogError($"{checkInfo.LoginName} 请求签到失败，进行第{retryCount}次重试\n原因：{ex.Message ?? ex?.InnerException?.Message}");
                                Thread.Sleep(TimeSpan.FromSeconds(2));
                            }
                            finally { retryCount--; }
                        }
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
                            throw new Exception($"{checkInfo.LoginName} 签到失败\n结果：{json}");
                        }
                    }
                    else
                    {
                        logger.LogInformation($"{checkInfo.LoginName} 签到成功\n结果：{json}");
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
            //构造请求体
            var data = new List<KeyValuePair<string, string>> {
                    new KeyValuePair<string,string>("loginName",checkInfo.LoginName),
                    new KeyValuePair<string,string>("password",checkInfo.Password),
                    new KeyValuePair<string,string>("schoolId",checkInfo.SchoolId.ToString())
                };
            //转为form格式字符串
            var content = new FormUrlEncodedContent(data);

            string token = string.Empty;
            int retryCount = 3;
            while (retryCount > 0)
            {
                try
                {
                    //登录获取Token
                    using (var httpClient = clientFactory.CreateClient("executer"))
                    {
                        var response = await httpClient.PostAsync("/mobile/login", content);
                        response.EnsureSuccessStatusCode();
                        //将响应转换为Json获取Token
                        var respContent = await response.Content.ReadAsStringAsync();
                        JObject json = JObject.Parse(respContent);
                        if (string.IsNullOrWhiteSpace(json["msg"].ToString()))
                        {
                            token = json["data"]["sessionId"].ToString();
                        }
                        break;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning($"{checkInfo.LoginName} 请求获取Token失败，进行第{retryCount}次重试\n原因：{ex.Message ?? ex.InnerException?.Message}");
                    Thread.Sleep(TimeSpan.FromSeconds(2));
                }
                finally { retryCount--; }
            }
            return token;
        }
        
        /// <summary>
        /// 获取附件列表
        /// </summary>
        /// <param name="loginName"></param>
        /// <returns></returns>
        public List<Attach> GetAttachs(string loginName)
        {
            var attachList = new List<Attach>();
            try
            {
                var database = redisConnection.GetDatabase(1);
                var redisValue = database.StringGet(loginName);
                var result = database.ScriptEvaluate(LuaScript.Prepare($"local res = redis.call('KEYS','*{loginName}*') return res"));
                if (!result.IsNull)
                {
                    RedisKey[] keys = (RedisKey[])result;
                    foreach (var key in keys)
                    {
                        var value = database.StringGet(key);
                        attachList.Add(JsonConvert.DeserializeObject<Attach>(value));
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogError(e.Message);
                
            }
            return attachList;
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
                var canSend = true;
                //创建通知邮件
                var mail = new MailTemplate
                {
                    ToEmail = checkInfo.Email,
                    Subject = "[慧职教+] 签到任务"
                };

                //签到失败
                if (statusCode != HttpStatusCode.OK || (resultJson["code"]?.ToString() == "1" || resultJson["code"]?.ToString() == "") || resultJson["success"]?.ToString() == "False")
                {
                    //签到失败
                    if (resultJson["msg"]?.ToString() != "今天你已经完成签到任务，不必再签到")
                    {
                        mail.Body = $@"执行结果：签到失败，请登录<a href='http://internship.zhfsmy.cloud/' target='_blank'>慧职教+</a>手动签到</br>
                               执行时间：{DateTime.Now.ToLocalTime()}</br>
                               状态码：{statusCode}</br>";
                        //重试次数大于10次才发送邮箱通知
                        using (var con = JobStorage.Current.GetConnection())
                        {
                            var recurringJob = con.GetRecurringJobs().Where(j => j.Id == checkInfo.LoginName).FirstOrDefault();
                            var retryCount = Convert.ToInt32(con.GetJobParameter(recurringJob.LastJobId, "RetryCount"));
                            if (retryCount >= 10)
                            {
                                canSend = true;
                            }
                            else
                            {
                                canSend = false;
                            }
                        }
                    }
                    //重复签到
                    else
                    {
                        mail.Body = $@"执行结果：{resultJson["msg"]}</br>
                                   执行时间：{DateTime.Now.ToLocalTime()}</br><hr/>
                                   登录<a href='http://internship.zhfsmy.cloud/' target='_blank'>慧职教+</a>查看详情";
                    }
                }
                //签到成功
                else
                {
                    mail.Body = $@"执行结果：签到成功</br>
                                执行时间：{DateTime.Now.ToLocalTime()}</br>
                                打卡位置：{checkInfo.Label}</br>
                                打卡坐标：X:{Math.Round(checkInfo.LocationX, 6)} Y:{Math.Round(checkInfo.LocationY, 6)}</br><hr/>
                                登录<a href='http://internship.zhfsmy.cloud/' target='_blank'>慧职教+</a>查看详情";
                }
                if (canSend)
                {
                    Task.Run(() => mailHelper.SendEmail(mail));
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning($"{checkInfo.LoginName}邮件发送失败\n原因：{ex.Message ?? ex.InnerException?.Message}");
            }
        }
    }
}
