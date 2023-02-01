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
        private static List<string> proxyIPs = new List<string>();

        /// <summary>
        /// 创建HttpClient
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public HttpClient CreateClient(string name)
        {
            if (proxyIPs.Count == 0)
            {
                FillProxyIP();
            }
            string ip = proxyIPs.FirstOrDefault();
            var client = new HttpClient(new HttpClientHandler
            {
                Proxy = new WebProxy(ip),
                UseProxy = true,
            });
            client.Timeout = Timeout;
            client.BaseAddress = BaseAddress;
            client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", DefaultContentType);

            proxyIPs.Remove(ip);
            return client;
        }

        /// <summary>
        /// 填充代理IP池
        /// </summary>
        private void FillProxyIP()
        {
            int retryCount = 3; //请求重试次数
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
                        proxyIPs.AddRange(ips.Split(','));
                        Console.WriteLine("填充IP");
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
