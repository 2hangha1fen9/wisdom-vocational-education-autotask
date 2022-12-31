namespace ScheduleTasks.Domain
{
    public class MailConfig
    {
        /// <summary>
        /// 发送者
        /// </summary>
        public string Mail { get; set; }
        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName { get; set; }
        /// <summary>
        /// 发送者授权码
        /// </summary>
        public string Password { get; set; }
        /// <summary>
        /// 发送者邮件服务器地址
        /// </summary>
        public string Host { get; set; }
        /// <summary>
        /// 发送者邮箱服务器端口
        /// </summary>
        public int Port { get; set; }
        
    }
}
