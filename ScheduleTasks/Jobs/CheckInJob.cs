using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ScheduleTasks.Domain;
using ScheduleTasks.Utils;
using System.ComponentModel;

namespace ScheduleTasks.Jobs
{
    public class CheckInJob
    {
        private readonly IHttpClientFactory clientFactory;
        private readonly MailHelper mailHelper;
        public CheckInJob(IHttpClientFactory clientFactory, MailHelper mailHelper) 
        { 
            this.clientFactory = clientFactory;
            this.mailHelper = mailHelper;
        }

        /// <summary>
        /// 执行签到
        /// </summary>
        /// <returns></returns>
        public void CheckIn(string checkInfoStr)
       {
            if(string.IsNullOrWhiteSpace(checkInfoStr))
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
                checkInfo.LocationX += Math.Round(r.NextDouble() / 10000,6);
                checkInfo.LocationY += Math.Round(r.NextDouble() / 10000,6);
            }


            var token = GetToken(checkInfo);
            if (token != null && checkInfo != null)
            {
                //构造请求体
                var data = new List<KeyValuePair<string, string>> {
                    new KeyValuePair<string,string>("token",token),
                    new KeyValuePair<string,string>("checkType",checkInfo.CheckType),
                    new KeyValuePair<string,string>("locationX", checkInfo.LocationX.ToString()),
                    new KeyValuePair<string,string>("locationY", checkInfo.LocationY.ToString()),
                    new KeyValuePair<string,string>("scale", checkInfo.Scale.ToString()),
                    new KeyValuePair<string,string>("label", checkInfo.Label),
                    new KeyValuePair<string,string>("mapType", checkInfo.MapType),
                    new KeyValuePair<string,string>("content", checkInfo.Content),
                    new KeyValuePair<string,string>("content", checkInfo.Content),
                    new KeyValuePair<string,string>("isAbnormal", checkInfo.IsAbnormal.ToString()),
                    new KeyValuePair<string,string>("isEvection", checkInfo.IsEvection.ToString()),
                    new KeyValuePair<string,string>("studentId", checkInfo.StudentId.ToString()),
                    new KeyValuePair<string,string>("internshipId", checkInfo.InternshipId.ToString()),
                };
                
                //转为form格式字符串
                var content = new FormUrlEncodedContent(data);
                //发送请求
                var client = clientFactory.CreateClient("executer");
                var response = client.PostAsync("/mobile/process/stu-location/save", content).Result;

                //创建通知邮件
                var mail = new MailTemplate
                {
                    ToEmail = checkInfo.Email,
                    Subject = "[慧职教+] 签到任务"
                };
                //转换为Json对象
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    //转换为Json对象
                    JObject json = JObject.Parse(response.Content.ReadAsStringAsync().Result);
                    if (!string.IsNullOrWhiteSpace(checkInfo.Email))
                    {
                        
                        if (json["code"].ToString() == "1") //签到失败
                        {
                            mail.Body = $@"执行结果：{json["msg"]}</br>
                                           执行时间：{DateTime.Now.ToLocalTime()}</br>";
                        }
                        else
                        {
                            mail.Body = $@"执行结果：签到成功</br>
                                           执行时间：{DateTime.Now.ToLocalTime()}</br>
                                           打卡位置：{checkInfo.Label}</br>
                                           打卡坐标：X:{checkInfo.LocationX} Y:{checkInfo.LocationY}";
                        }
                    }
                }
                else
                {
                    mail.Body = $@"执行结果：目标服务器错误</br>
                               执行时间：{DateTime.Now.ToLocalTime()}</br>
                               状态码：{response.StatusCode}</br>";
                }
                mailHelper.SendEmail(mail);
            }
        }

        /// <summary>
        /// 获取token
        /// </summary>
        /// <returns></returns>
        private string GetToken(CheckInReq checkInfo)
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
            var client = clientFactory.CreateClient("executer");
            var response =  client.PostAsync("/mobile/login", content).Result;
            //转换为Json对象
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                //转换为Json对象
                JObject json = JObject.Parse(response.Content.ReadAsStringAsync().Result);
                if (string.IsNullOrWhiteSpace(json["msg"].ToString()))
                {
                    return json["data"]["sessionId"].ToString();
                }
                return null;
            }
            else
            {
                return null;
            }
        }
    }
}
