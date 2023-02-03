using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ScheduleTasks.Domain;
using StackExchange.Redis;
using System.Net;

namespace ScheduleTasks.Utils
{
    //动态获取代理HttpClient
    public class DynamicProxyHttpClientFactory : IHttpClientFactory
    {
        /// <summary>
        /// 基本地址
        /// </summary>
        public Uri BaseAddress { get; set; }
        /// <summary>
        /// 默认MIME类型
        /// </summary>
        public string DefaultContentType { get; set; }
        /// <summary>
        /// 代理IP获取API
        /// </summary>
        public string ProxyAPI { get; set; }
        /// <summary>
        /// 超时时间
        /// </summary>
        public TimeSpan Timeout { get; set; }
        /// <summary>
        /// 代理IP池
        /// </summary>
        private static List<ProxyInfo> proxyInfos = new List<ProxyInfo>();

        /// <summary>
        /// 创建HttpClient
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public HttpClient CreateClient(string name)
        {
            lock(proxyInfos)
            {
                ProxyInfo proxy = GetProxyIp();
                var client = new HttpClient(new HttpClientHandler
                {
                    Proxy = new WebProxy($"{proxy.Ip}:{proxy.Port}"),
                    UseProxy = true,
                });
                client.Timeout = Timeout;
                client.BaseAddress = BaseAddress;
                client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", DefaultContentType);
                
                return client;
            }
        }

        private ProxyInfo GetProxyIp()
        {
            lock(proxyInfos) 
            {
                ProxyInfo proxy = null;
                //ip获取重试次数
                int retryCount = 3;
                while(retryCount > 0 && proxy == null)
                {
                    //没有寻找到合适IP则查看剩余IP数量，如果没有了在填充一次进行下一次重试
                    if(proxyInfos.Count <= 0)
                    {
                        FillProxyIP();
                    } 

                    //循环获取代理信息
                    while (proxyInfos.Count > 0)
                    {
                        DateTime nowTime = DateTime.Now;
                        ProxyInfo temp = proxyInfos.FirstOrDefault();
                        proxyInfos.Remove(temp);
                        if (temp != null && temp.EndTime > nowTime)
                        {
                            Console.WriteLine($"取出IP： {temp.Ip}:{temp.Port} 剩余：{proxyInfos.Count}");
                            proxy = temp;
                            break;
                        }
                    }

                    retryCount--;
                }
                return proxy;
            }
        }

        /// <summary>
        /// 填充代理IP池
        /// </summary>
        private void FillProxyIP()
        {
            int retryCount = 5; //请求重试次数
            using (var client = new HttpClient())
            {
                var response = new HttpResponseMessage(HttpStatusCode.BadGateway);
                while (retryCount > 0)
                {
                    try
                    {
                        response = client.GetAsync(ProxyAPI).Result;
                        response.EnsureSuccessStatusCode(); //确保请求成功响应
                        string ips = response.Content.ReadAsStringAsync().Result;

                        //序列化请求结果
                        ProxyResponse resp = JsonConvert.DeserializeObject<ProxyResponse>(ips);
                        //正常响应将存储获取到的代理IP
                        if (resp.Success && resp.Code == 0)
                        {
                            Console.WriteLine($"提取IP\n{ips}");
                            proxyInfos.AddRange(resp.Data);
                        }
                        else
                        {
                            throw new Exception();
                        }
        
                        break;
                    }
                    catch (Exception)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(2));
                    }
                    finally
                    {
                        retryCount--;
                    }
                }
            }
        }
    }
}
