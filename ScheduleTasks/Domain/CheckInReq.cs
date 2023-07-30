namespace ScheduleTasks.Domain
{
    public class CheckInReq
    {
        /// <summary>
        /// 登录名
        /// </summary>
        public string LoginName { get; set; }
        /// <summary>
        /// 密码
        /// </summary>
        public string Password { get; set; }
        /// <summary>
        /// 学校ID
        /// </summary>
        public int SchoolId { get; set; }
        /// <summary>
        /// 学生ID
        /// </summary>
        public int StudentId { get; set; }
        /// <summary>
        /// 课程ID
        /// </summary>
        public int InternshipId { get; set; }
        /// <summary>
        /// 签到地址名称
        /// </summary>
        public string Label { get; set; }
        /// <summary>
        /// X坐标
        /// </summary>
        public double LocationX { get; set; }
        /// <summary>
        /// Y坐标
        /// </summary>
        public double LocationY { get; set; }
        /// <summary>
        /// 浮动位置
        /// </summary>
        public bool? lFloat { get; set; } = false;
        /// <summary>
        /// 地图缩放层级
        /// </summary>
        public int? Scale { get; set; } = 16;
        /// <summary>
        /// 地图类型
        /// </summary>
        public string? MapType { get; set; } = "baidu";
        /// <summary>
        /// 是否出差签到
        /// </summary>
        public int? IsEvection { get; set; } = 0;
        /// <summary>
        /// 签到类型 签到CHECKIN/SINGOFF
        /// </summary>
        public string? CheckType { get; set; } = "CHECKIN";
        /// <summary>
        /// 是否异常
        /// </summary>
        public bool? IsAbnormal { get; set; } = false;
        /// <summary>
        /// 签到备注
        /// </summary>
        public string? Content { get; set; }
        /// <summary>
        /// 通知邮箱
        /// </summary>
        public string? Email { get; set; }
        /// <summary>
        /// 执行时间 时,分,周六,周天
        /// </summary>
        public int[] StartTime { get; set; } = new int[] { 7, 0,0,0 };
        /// <summary>
        /// 时间浮动
        /// </summary>
        public bool? tFloat { get; set; } = false;
        /// <summary>
        /// 附件ID
        /// </summary>
        public List<int>? AttachIds { get; set; }
        /// <summary>
        /// 是否随机上传已有附件
        /// </summary>
        public bool? RandomAttach { get; set; } = false;
    }
}
