// AI-code-start lines:243 tool:cursor ai生成
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using WakeOnLanClient.Config;
using WakeOnLanClient.Models;

namespace WakeOnLanClient.Services
{
    /// <summary>
    /// 统一执行开机（WOL）与关机命令。
    /// </summary>
    public sealed class PowerControlService
    {
        private readonly WolMagicPacketSender _wolSender;
        private readonly ILogSink _logSink;

        public PowerControlService(WolMagicPacketSender wolSender, ILogSink logSink)
        {
            _wolSender = wolSender ?? throw new ArgumentNullException(nameof(wolSender));
            _logSink = logSink ?? throw new ArgumentNullException(nameof(logSink));
        }

        /// <summary>
        /// 按动作类型执行电源操作。
        /// </summary>
        /// <param name="action">开机或关机。</param>
        /// <param name="macAddress">目标 MAC（开机必需）。</param>
        /// <param name="ipAddress">目标 IP（关机必需）。</param>
        /// <param name="computerName">目标名称，仅用于日志。</param>
        /// <param name="userName">目标电脑用户名（关机时用于远程鉴权）。</param>
        /// <param name="password">目标电脑密码（关机时用于远程鉴权）。</param>
        public void Execute(
            PowerAction action,
            string macAddress,
            string ipAddress,
            string computerName,
            string userName,
            string password)
        {
            var displayName = string.IsNullOrWhiteSpace(computerName) ? (ipAddress ?? macAddress ?? "未知主机") : computerName;

            if (action == PowerAction.PowerOn)
            {
                if (string.IsNullOrWhiteSpace(macAddress))
                {
                    throw new ArgumentException("开机操作需要有效的 MAC 地址。", nameof(macAddress));
                }

                _logSink.Info($"执行开机: {displayName}, MAC={macAddress}, IP={ipAddress}");
                _wolSender.Send(macAddress, ipAddress);
                return;
            }

            if (action == PowerAction.PowerOff)
            {
                if (string.IsNullOrWhiteSpace(ipAddress))
                {
                    throw new ArgumentException("关机操作需要有效的局域网 IP 地址。", nameof(ipAddress));
                }

                var targetHost = ipAddress.Trim();
                _logSink.Info($"执行关机: {displayName}, IP={targetHost}");

                var sessionOpened = TryOpenAdminSession(targetHost, userName, password);
                try
                {
                    RunRemoteCommand(PowerCommandConfig.BuildShutdownCommand(targetHost));
                }
                finally
                {
                    if (sessionOpened)
                    {
                        CloseAdminSession(targetHost);
                    }
                }

                return;
            }

            throw new NotSupportedException($"不支持的电源动作: {action}");
        }

        /// <summary>
        /// 用配置的管理员凭据连接目标机 IPC$，否则 shutdown 会以当前进程账号鉴权而被拒绝。
        /// </summary>
        private bool TryOpenAdminSession(string targetHost, string userName, string password)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                return false;
            }

            var share = BuildIpcShare(targetHost);
            var user = userName.Trim();

            // 已有的同主机连接会导致 1219 多重连接冲突，先断开再建立。
            RunProcess($"net use {share} /delete /y", $"net use {share} /delete /y");

            var command = $"net use {share} /user:{user} \"{password ?? string.Empty}\"";
            var display = $"net use {share} /user:{user} ******";
            var result = RunProcess(command, display);
            if (result.ExitCode != 0)
            {
                _logSink.Warn($"建立远程管理连接失败（将以当前账号继续尝试关机）: {result.DescribeFailure()}");
                return false;
            }

            _logSink.Info($"已以 {user} 建立远程管理连接: {share}");
            return true;
        }

        private void CloseAdminSession(string targetHost)
        {
            var share = BuildIpcShare(targetHost);
            RunProcess($"net use {share} /delete /y", $"net use {share} /delete /y");
        }

        private static string BuildIpcShare(string targetHost)
        {
            return "\\\\" + targetHost + "\\IPC$";
        }

        private void RunRemoteCommand(string commandText)
        {
            if (string.IsNullOrWhiteSpace(commandText))
            {
                throw new InvalidOperationException("关机命令模板结果为空，请检查 PowerCommandConfig。");
            }

            var result = RunProcess(commandText, commandText);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"关机命令退出码异常: {result.ExitCode}{BuildExitCodeHint(result.ExitCode)}");
            }
        }

        private static string BuildExitCodeHint(int exitCode)
        {
            switch (exitCode)
            {
                case 5:
                    return "（拒绝访问：执行账号在目标机上不具备远程关机权限。"
                        + "请确认界面填写的是目标机管理员账号和密码；工作组环境还需在目标机启用 LocalAccountTokenFilterPolicy=1）";
                case 53:
                    return "（找不到网络路径：目标机可能已关机、IP 变更或未开放文件与打印机共享）";
                case 1219:
                    return "（多重连接冲突：本机已用其他账号连接该目标机，请先执行 net use /delete）";
                case 1722:
                    return "（RPC 服务器不可用：目标机防火墙未放行远程管理相关端口）";
                default:
                    return string.Empty;
            }
        }

        private ProcessResult RunProcess(string commandText, string displayCommandText)
        {
            var arguments = string.Format(PowerCommandConfig.CommandProcessArgumentsTemplate, commandText);
            var displayArguments = string.Format(PowerCommandConfig.CommandProcessArgumentsTemplate, displayCommandText);
            _logSink.Info($"调用命令进程: {PowerCommandConfig.CommandProcessFileName} {displayArguments}");

            var startInfo = new ProcessStartInfo
            {
                FileName = PowerCommandConfig.CommandProcessFileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    throw new InvalidOperationException("无法启动命令进程。");
                }

                var standardOutput = string.Empty;
                var standardError = string.Empty;
                var outputTask = Task.Run(() => process.StandardOutput.ReadToEnd());
                var errorTask = Task.Run(() => process.StandardError.ReadToEnd());

                if (!process.WaitForExit(120000))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        // 进程已退出则忽略
                    }

                    throw new TimeoutException("命令进程超时（120 秒），已终止。");
                }

                if (!Task.WaitAll(new Task[] { outputTask, errorTask }, 5000))
                {
                    throw new TimeoutException("读取命令输出超时。");
                }

                standardOutput = outputTask.Result;
                standardError = errorTask.Result;

                if (!string.IsNullOrWhiteSpace(standardOutput))
                {
                    _logSink.Info($"命令输出: {standardOutput.Trim()}");
                }

                if (!string.IsNullOrWhiteSpace(standardError))
                {
                    _logSink.Warn($"命令错误输出: {standardError.Trim()}");
                }

                return new ProcessResult(process.ExitCode, standardOutput, standardError);
            }
        }

        private sealed class ProcessResult
        {
            public ProcessResult(int exitCode, string standardOutput, string standardError)
            {
                ExitCode = exitCode;
                StandardOutput = standardOutput;
                StandardError = standardError;
            }

            public int ExitCode { get; }

            public string StandardOutput { get; }

            public string StandardError { get; }

            public string DescribeFailure()
            {
                var detail = string.IsNullOrWhiteSpace(StandardError) ? StandardOutput : StandardError;
                return $"exitCode={ExitCode}, detail={(detail ?? string.Empty).Trim()}";
            }
        }
    }
}
// AI-code-end