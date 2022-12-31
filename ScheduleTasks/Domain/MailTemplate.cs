namespace ScheduleTasks.Domain
{
    public class MailTemplate
    {
        /// <summary>
        /// 接收者
        /// </summary>
        public string ToEmail { get; set; }
        /// <summary>
        /// 标题
        /// </summary>
        public string Subject { get; set; }
        /// <summary>
        /// 内容
        /// </summary>
        public string Body { get; set; }
    }
}
