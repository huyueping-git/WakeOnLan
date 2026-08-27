// AI-code-start lines:81 tool:cursor ai生成
namespace WakeOnLanClient.Config
{
    /// <summary>
    /// 开关机命令配置。
    /// 若安全审计部门提供专用命令，只需修改本类中的模板与参数即可，无需改动业务逻辑。
    /// </summary>
    public static class PowerCommandConfig
    {
        /// <summary>
        /// Wake-on-LAN 魔术包目标 UDP 端口。
        /// 常见值为 7 或 9。
        /// </summary>
        public static int WolUdpPort { get; set; } = 9;

        /// <summary>
        /// 备用 UDP 端口，部分网卡只监听 7。设为 0 表示不发送。
        /// </summary>
        public static int WolSecondaryUdpPort { get; set; } = 7;

        /// <summary>
        /// 魔术包广播地址。默认全网广播；也可改为子网广播如 192.168.1.255。
        /// 实际发送时还会自动附加目标 IP 单播与各网卡的子网定向广播。
        /// </summary>
        public static string WolBroadcastAddress { get; set; } = "255.255.255.255";

        /// <summary>
        /// 同一 MAC 连续发送魔术包的次数，提高唤醒成功率。
        /// </summary>
        public static int WolPacketRepeatCount { get; set; } = 3;

        /// <summary>
        /// 远程关机命令模板。
        /// 占位符：{0}=目标 IP 或主机名，{1}=倒计时秒数。
        /// 审计部门若提供自定义命令，请替换此模板。
        /// </summary>
        public static string ShutdownCommandTemplate { get; set; } = "shutdown /s /m \\\\{0} /t {1} /f";

        /// <summary>
        /// 远程重启命令模板（预留）。
        /// 占位符：{0}=目标 IP 或主机名，{1}=倒计时秒数。
        /// </summary>
        public static string RestartCommandTemplate { get; set; } = "shutdown /r /m \\\\{0} /t {1} /f";

        /// <summary>
        /// 关机倒计时秒数。
        /// </summary>
        public static int ShutdownTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// 执行关机命令的进程文件名。
        /// </summary>
        public static string CommandProcessFileName { get; set; } = "cmd.exe";

        /// <summary>
        /// 进程参数模板。占位符：{0}=完整关机/重启命令字符串。
        /// </summary>
        public static string CommandProcessArgumentsTemplate { get; set; } = "/c {0}";

        /// <summary>
        /// 根据模板生成远程关机命令。
        /// </summary>
        /// <param name="targetHost">局域网可达 IP 或主机名。</param>
        /// <returns>可直接交给 cmd 执行的关机命令。</returns>
        public static string BuildShutdownCommand(string targetHost)
        {
            return string.Format(ShutdownCommandTemplate, targetHost, ShutdownTimeoutSeconds);
        }

        /// <summary>
        /// 根据模板生成远程重启命令。
        /// </summary>
        /// <param name="targetHost">局域网可达 IP 或主机名。</param>
        /// <returns>可直接交给 cmd 执行的重启命令。</returns>
        public static string BuildRestartCommand(string targetHost)
        {
            return string.Format(RestartCommandTemplate, targetHost, ShutdownTimeoutSeconds);
        }
    }
}
// AI-code-end