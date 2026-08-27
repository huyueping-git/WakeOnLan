// AI-code-start lines:28 tool:cursor ai生成
namespace WakeOnLanClient.Services
{
    /// <summary>
    /// 日志输出抽象，供 UI 与后台服务解耦。
    /// </summary>
    public interface ILogSink
    {
        /// <summary>
        /// 写入一条日志。
        /// </summary>
        /// <param name="message">日志内容。</param>
        void Info(string message);

        /// <summary>
        /// 写入一条警告日志。
        /// </summary>
        /// <param name="message">日志内容。</param>
        void Warn(string message);

        /// <summary>
        /// 写入一条错误日志。
        /// </summary>
        /// <param name="message">日志内容。</param>
        void Error(string message);
    }
}
// AI-code-end