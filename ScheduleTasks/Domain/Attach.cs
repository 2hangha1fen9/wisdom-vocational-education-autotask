namespace ScheduleTasks.Domain;

/// <summary>
/// 文件附件
/// </summary>
public class Attach
{
    /// <summary>
    /// 用户名
    /// </summary>
    public string LoginName { get; set; }
    /// <summary>
    /// 文件ID
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// 文件路径
    /// </summary>
    public string Path { get; set; }
}