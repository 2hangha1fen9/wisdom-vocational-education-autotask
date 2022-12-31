using Hangfire;
using Hangfire.Server;
using Hangfire.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ScheduleTasks.Domain;
using ScheduleTasks.Jobs;

namespace ScheduleTasks.Controllers
{
    [Route("[controller]/[action]")]
    [ApiController]
    public class ExtController : ControllerBase
    {
        /// <summary>
        /// 订阅定时签到任务
        /// </summary>
        /// <param name="checkInReq"></param>
        /// <returns></returns>
        [HttpPost]
        public IActionResult SubscribeCheckin(CheckInReq checkInReq)
        {
            string reqJson = JsonConvert.SerializeObject(checkInReq);
            //添加到任务队列
            RecurringJob.AddOrUpdate<CheckInJob>(
                checkInReq.LoginName,
                job => job.CheckIn(reqJson),
                Cron.Daily(checkInReq.StartTime[0], checkInReq.StartTime[1])
            );
            return Ok();
        }

        /// <summary>
        /// 取消定时签到任务
        /// </summary>
        /// <param name="loginName"></param>
        /// <returns></returns>
        [HttpDelete]
        public IActionResult UnsubscribeCheckin([FromQuery]string loginName)
        {
            RecurringJob.RemoveIfExists(loginName);
            return Ok();
        }

        /// <summary>
        /// 获取定时签到任务信息
        /// </summary>
        /// <param name="loginName"></param>
        /// <returns></returns>
        [HttpGet]
        public CheckInReq SubscribeInfo([FromQuery] string loginName)
        {
            using (var con = JobStorage.Current.GetConnection())
            {
                var job = con.GetRecurringJobs().Where(j => j.Id == loginName).FirstOrDefault();
                if(job != null)
                {
                    return JsonConvert.DeserializeObject<CheckInReq>(job.Job.Args[0].ToString());
                }
                else
                {
                    return null;
                }
            }
        }
    }
}
