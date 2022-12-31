using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using ScheduleTasks.Domain;

namespace ScheduleTasks.Utils
{
    public class MailHelper
    {
        /// <summary>
        /// 邮件配置
        /// </summary>
        private readonly MailConfig config;

        public MailHelper(MailConfig config)
        {
            this.config = config;
        }

        public bool SendEmail(MailTemplate template)
        {
            //设置邮件
            var mime = new MimeMessage();
            //设置发件人
            mime.From.Add(MailboxAddress.Parse(config.Mail));
            mime.Sender = MailboxAddress.Parse(config.Mail);
            //设置收件人
            mime.To.Add(MailboxAddress.Parse(template.ToEmail));
            //设置标题
            mime.Subject = template.Subject;
            //设置正文
            var body = new BodyBuilder();
            body.HtmlBody = template.Body;
            mime.Body = body.ToMessageBody();
            //发送邮箱
            using (var smtp = new SmtpClient())
            {
                //设置邮件服务器地址
                smtp.Connect(config.Host, config.Port, SecureSocketOptions.StartTls);
                //设置权限
                smtp.Authenticate(config.Mail, config.Password);
                //发送邮件
                smtp.Send(mime);
            }
            //存入redis中
            return true;
        }
    }
}

