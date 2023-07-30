using System.Threading.RateLimiting;
using Hangfire;
using Hangfire.Redis;
using Hangfire.Server;
using Hangfire.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ScheduleTasks.Domain;
using ScheduleTasks.Jobs;
using StackExchange.Redis;

namespace ScheduleTasks.Controllers
{
    [Route("[controller]/[action]")]
    [ApiController]
    public class ExtController : ControllerBase
    {
        private readonly ILogger<CheckInJob> logger;
        private readonly IConnectionMultiplexer redisConnection;

        public ExtController(ILogger<CheckInJob> logger,IConnectionMultiplexer redisConnection)
        {
            this.logger = logger;
            this.redisConnection = redisConnection;
        }


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
                Cron.Daily(checkInReq.StartTime[0], checkInReq.StartTime[1]),TimeZoneInfo.Local
            );
            logger.LogInformation($"{checkInReq.LoginName}订阅签到任务\n{reqJson}");
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
            logger.LogInformation($"{loginName}取消订阅签到任务");
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

        /// <summary>
        /// 获取最佳签到时间
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public int[] GetAppropriateTime()
        {
            using (var con = JobStorage.Current.GetConnection())
            {
                //根据时间进行分组查询
                var jobGroups = con.GetRecurringJobs().GroupBy(job => job.Cron).OrderByDescending(job => job.Count()).ToList();
                var baseCron = "";
                //如果没有任何计划任务给出默认crontab表达式 0 7 * * *
                if (jobGroups.Count() == 0)
                {
                    baseCron = Cron.Daily(7, 0);
                }
                else 
                {
                    //没有满足的Cron表达式则从7：00点遍历，时间向后推5分钟
                    var tempCronTime = new DateTime(2023, 1, 1, 7, 0, 0);
                    var tempCron = Cron.Daily(tempCronTime.Hour, tempCronTime.Minute);
                    while (jobGroups.FirstOrDefault(jb => jb.Key == tempCron) != null && jobGroups.FirstOrDefault(jb => jb.Key == tempCron).Count() > 5)
                    {
                        tempCronTime = tempCronTime.AddMinutes(5);
                        tempCron = Cron.Daily(tempCronTime.Hour, tempCronTime.Minute);
                    }
                    baseCron = tempCron;
                }
                var baseCronArray = baseCron.Split(' ');
                return new int[] { Convert.ToInt32(baseCronArray[1]), Convert.ToInt32(baseCronArray[0]) };
            }
        }

        /// <summary>
        /// 获取所有附件
        /// </summary>
        /// <param name="loginName"></param>
        /// <returns></returns>
        [HttpGet]
        public List<Attach> ListAttach([FromQuery] string loginName)
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
        /// 添加附件
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public IActionResult AddAttach(Attach attach)
        {
            try
            {
                var database = redisConnection.GetDatabase(1);
                var key = $"{attach.LoginName}:{attach.Id}";
                var stringSet = database.StringSet(key, JsonConvert.SerializeObject(attach));
                if (stringSet)
                {
                    return Ok();
                }
                return BadRequest();
            }
            catch (Exception e)
            {
                logger.LogError(e.Message);
                return BadRequest();
            }
        }

        /// <summary>
        /// 删除附件
        /// </summary>
        /// <param name="loginName"></param>
        /// <param name="attach"></param>
        /// <returns></returns>
        [HttpDelete]
        public IActionResult DeleteAttach(Attach attach)
        {
            try
            {
                var database = redisConnection.GetDatabase(1);
                var key = $"{attach.LoginName}:{attach.Id}";
                var stringSet = database.KeyDelete(key);
                if (stringSet)
                {
                    return Ok();
                }
                return BadRequest();
            }
            catch (Exception e)
            {
                logger.LogError(e.Message);
                return BadRequest();
            }
        }
    }
}
