namespace ScheduleTasks.Domain
{
    public class ProxyResponse
    {
        public int Code { get; set; }
        public List<ProxyInfo> Data { get; set; }
        public string Msg { get; set; }
        public bool Success { get; set; }
    }

    public class ProxyInfo
    {
        public string Ip { get; set; }
        public int Port { get; set; }  
        public DateTime EndTime { get; set; }
    }
}
