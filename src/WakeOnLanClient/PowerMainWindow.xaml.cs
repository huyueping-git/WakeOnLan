using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using WakeOnLanClient.Helpers;
using WakeOnLanClient.Models;
using WakeOnLanClient.Services;

namespace WakeOnLanClient
{
    /// <summary>
    /// 主窗口：按界面输入执行开机或关机。
    /// </summary>
    public partial class PowerMainWindow : Window, ILogSink
    {
        private const int MaxUiLogChars = 200000;
        private const int TrimUiLogChars = 120000;

        private readonly PowerControlService _powerControl;
        private int _uiLogLength;
        private bool _isExecuting;

        public PowerMainWindow()
        {
            InitializeComponent();
            _powerControl = new PowerControlService(new WolMagicPacketSender(this), this);
            UpdateCredentialFields();
        }

        public void Info(string message)
        {
            AppendLog("INFO", message);
        }

        public void Warn(string message)
        {
            AppendLog("WARN", message);
        }

        public void Error(string message)
        {
            AppendLog("ERROR", message);
        }

        private void PowerAction_Checked(object sender, RoutedEventArgs e)
        {
            UpdateCredentialFields();
        }

        private async void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            if (_isExecuting)
            {
                return;
            }

            var action = RdoPowerOff.IsChecked == true ? PowerAction.PowerOff : PowerAction.PowerOn;
            var ipAddress = (TxtIpAddress.Text ?? string.Empty).Trim();
            var macAddress = (TxtMacAddress.Text ?? string.Empty).Trim();
            var userName = (TxtUserName.Text ?? string.Empty).Trim();
            var password = TxtPassword.Password ?? string.Empty;

            if (!TryValidate(action, ipAddress, macAddress, userName, password, out var error))
            {
                MessageBox.Show(this, error, "无法执行", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _isExecuting = true;
            BtnExecute.IsEnabled = false;
            TxtStatus.Text = action == PowerAction.PowerOn ? "正在开机..." : "正在关机...";

            try
            {
                await Task.Run(() => _powerControl.Execute(action, macAddress, ipAddress, ipAddress, userName, password));
                TxtStatus.Text = "执行完成";
                Info(action == PowerAction.PowerOn ? "开机指令已发送。" : "关机指令已发送。");
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "执行失败";
                Error(ex.Message);
                MessageBox.Show(this, ex.Message, "执行失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isExecuting = false;
                BtnExecute.IsEnabled = true;
            }
        }

        private static bool TryValidate(
            PowerAction action,
            string ipAddress,
            string macAddress,
            string userName,
            string password,
            out string error)
        {
            error = null;

            if (action == PowerAction.PowerOn)
            {
                if (string.IsNullOrWhiteSpace(macAddress) || !MacAddressHelper.IsValid(macAddress))
                {
                    error = "开机需要填写有效的 MAC 地址。";
                    return false;
                }

                return true;
            }

            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                error = "关机需要填写目标电脑的 IP 地址。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(userName))
            {
                error = "关机需要填写目标电脑用户名。";
                return false;
            }

            if (string.IsNullOrEmpty(password))
            {
                error = "关机需要填写目标电脑密码。";
                return false;
            }

            return true;
        }

        private void UpdateCredentialFields()
        {
            // XAML 解析 RdoPowerOn 的 IsChecked 时就会触发 Checked，此时后面的控件还未创建。
            if (RdoPowerOff == null || TxtUserName == null || TxtPassword == null)
            {
                return;
            }

            var powerOff = RdoPowerOff.IsChecked == true;
            TxtUserName.IsEnabled = powerOff;
            TxtPassword.IsEnabled = powerOff;
        }

        private void AppendLog(string level, string message)
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
            FileLogWriter.Write(line);
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            {
                return;
            }

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => WriteLine(line)));
                return;
            }

            WriteLine(line);
        }

        private void WriteLine(string line)
        {
            if (_uiLogLength > 0)
            {
                TxtLog.AppendText(Environment.NewLine);
                _uiLogLength += Environment.NewLine.Length;
            }

            TxtLog.AppendText(line);
            _uiLogLength += line.Length;
            TrimUiLogIfNeeded();
            TxtLog.ScrollToEnd();
        }

        private void TrimUiLogIfNeeded()
        {
            if (_uiLogLength <= MaxUiLogChars)
            {
                return;
            }

            var text = TxtLog.Text;
            var keepFrom = text.Length - TrimUiLogChars;
            if (keepFrom < 0)
            {
                keepFrom = 0;
            }

            var lineBreak = text.IndexOf('\n', keepFrom);
            if (lineBreak >= 0 && lineBreak + 1 < text.Length)
            {
                keepFrom = lineBreak + 1;
            }

            var trimmed = "…… 更早的界面日志已截断，完整内容见 log 目录。"
                + Environment.NewLine
                + text.Substring(keepFrom);
            TxtLog.Text = trimmed;
            _uiLogLength = trimmed.Length;
        }
    }
}
