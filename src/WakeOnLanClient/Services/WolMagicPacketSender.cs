// AI-code-start lines:308 tool:cursor ai生成
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using WakeOnLanClient.Config;
using WakeOnLanClient.Helpers;

namespace WakeOnLanClient.Services
{
    /// <summary>
    /// 向指定 MAC 地址发送 Wake-on-LAN 魔术包。
    /// </summary>
    public sealed class WolMagicPacketSender
    {
        private readonly ILogSink _logSink;

        public WolMagicPacketSender(ILogSink logSink)
        {
            _logSink = logSink ?? throw new ArgumentNullException(nameof(logSink));
        }

        /// <summary>
        /// 发送魔术包唤醒目标主机。
        /// </summary>
        /// <param name="macAddress">目标 MAC。</param>
        public void Send(string macAddress)
        {
            Send(macAddress, null);
        }

        /// <summary>
        /// 发送魔术包唤醒目标主机。
        /// 已知目标 IP 时同时按单播、子网定向广播、受限广播多路发送，
        /// 避免受限广播被交换机丢弃或只从单块网卡发出导致唤醒失败。
        /// </summary>
        /// <param name="macAddress">目标 MAC。</param>
        /// <param name="targetIpAddress">目标主机在局域网中的 IPv4，可为空。</param>
        public void Send(string macAddress, string targetIpAddress)
        {
            if (string.IsNullOrWhiteSpace(macAddress))
            {
                throw new ArgumentException("MAC 地址不能为空。", nameof(macAddress));
            }

            var macBytes = MacAddressHelper.Parse(macAddress);
            var packet = BuildMagicPacket(macBytes);
            var ports = BuildPorts();
            var targets = BuildTargets(targetIpAddress);
            var repeat = PowerCommandConfig.WolPacketRepeatCount < 1 ? 1 : PowerCommandConfig.WolPacketRepeatCount;

            var succeeded = new List<string>();
            for (var index = 0; index < targets.Count; index++)
            {
                var target = targets[index];
                try
                {
                    using (var client = CreateClient(target.LocalAddress))
                    {
                        for (var portIndex = 0; portIndex < ports.Count; portIndex++)
                        {
                            var endpoint = new IPEndPoint(target.RemoteAddress, ports[portIndex]);
                            for (var count = 0; count < repeat; count++)
                            {
                                client.Send(packet, packet.Length, endpoint);
                            }

                            succeeded.Add(target.Describe(ports[portIndex]));
                        }
                    }
                }
                catch (SocketException ex)
                {
                    _logSink.Warn($"WOL 魔术包发送失败: {target.Describe(0)}, error={ex.Message}");
                }
            }

            if (succeeded.Count == 0)
            {
                throw new InvalidOperationException("未能成功发送任何 WOL 魔术包，请检查本机网卡与网络配置。");
            }

            //_logSink.Info($"已发送 WOL 魔术包 -> MAC={macAddress}, 每个目标发送次数={repeat}, 目标=[{string.Join(", ", succeeded.ToArray())}]");
            _logSink.Info($"已发送 WOL 魔术包 -> MAC={macAddress}, 每个目标发送次数={repeat}, 目标={targetIpAddress}");
        }

        private static UdpClient CreateClient(IPAddress localAddress)
        {
            var client = localAddress == null
                ? new UdpClient(AddressFamily.InterNetwork)
                : new UdpClient(new IPEndPoint(localAddress, 0));
            client.EnableBroadcast = true;
            return client;
        }

        private static List<int> BuildPorts()
        {
            var ports = new List<int>();
            AddPort(ports, PowerCommandConfig.WolUdpPort);
            AddPort(ports, PowerCommandConfig.WolSecondaryUdpPort);
            if (ports.Count == 0)
            {
                ports.Add(9);
            }

            return ports;
        }

        private static void AddPort(ICollection<int> ports, int port)
        {
            if (port <= 0 || port > 65535 || ports.Contains(port))
            {
                return;
            }

            ports.Add(port);
        }

        private static List<SendTarget> BuildTargets(string targetIpAddress)
        {
            var targets = new List<SendTarget>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            IPAddress targetIp = null;
            if (!string.IsNullOrWhiteSpace(targetIpAddress)
                && IPAddress.TryParse(targetIpAddress.Trim(), out var parsedTarget)
                && parsedTarget.AddressFamily == AddressFamily.InterNetwork)
            {
                targetIp = parsedTarget;
            }

            // 与 PowerShell 手工验证一致：直接单播到目标 IP，成功率最高。
            AddTarget(targets, seen, null, targetIp);

            foreach (var local in EnumerateLocalIPv4())
            {
                if (targetIp != null && IsSameSubnet(local.Address, local.Mask, targetIp))
                {
                    AddTarget(targets, seen, local.Address, targetIp);
                }

                AddTarget(targets, seen, local.Address, GetBroadcastAddress(local.Address, local.Mask));
                AddTarget(targets, seen, local.Address, IPAddress.Broadcast);
            }

            if (IPAddress.TryParse(PowerCommandConfig.WolBroadcastAddress ?? string.Empty, out var configuredBroadcast)
                && configuredBroadcast.AddressFamily == AddressFamily.InterNetwork)
            {
                AddTarget(targets, seen, null, configuredBroadcast);
            }

            if (targets.Count == 0)
            {
                AddTarget(targets, seen, null, IPAddress.Broadcast);
            }

            return targets;
        }

        private static void AddTarget(ICollection<SendTarget> targets, ISet<string> seen, IPAddress localAddress, IPAddress remoteAddress)
        {
            if (remoteAddress == null)
            {
                return;
            }

            var key = (localAddress == null ? "*" : localAddress.ToString()) + "|" + remoteAddress;
            if (!seen.Add(key))
            {
                return;
            }

            targets.Add(new SendTarget(localAddress, remoteAddress));
        }

        private static IEnumerable<LocalInterface> EnumerateLocalIPv4()
        {
            var result = new List<LocalInterface>();
            NetworkInterface[] adapters;
            try
            {
                adapters = NetworkInterface.GetAllNetworkInterfaces();
            }
            catch (NetworkInformationException)
            {
                return result;
            }

            for (var index = 0; index < adapters.Length; index++)
            {
                var adapter = adapters[index];
                if (adapter.OperationalStatus != OperationalStatus.Up
                    || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback
                    || adapter.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                foreach (var unicast in adapter.GetIPProperties().UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork)
                    {
                        continue;
                    }

                    result.Add(new LocalInterface(unicast.Address, unicast.IPv4Mask));
                }
            }

            return result;
        }

        private static IPAddress GetBroadcastAddress(IPAddress address, IPAddress mask)
        {
            if (mask == null || mask.AddressFamily != AddressFamily.InterNetwork)
            {
                return null;
            }

            var addressBytes = address.GetAddressBytes();
            var maskBytes = mask.GetAddressBytes();
            var broadcastBytes = new byte[4];
            var maskIsEmpty = true;
            for (var index = 0; index < 4; index++)
            {
                if (maskBytes[index] != 0)
                {
                    maskIsEmpty = false;
                }

                broadcastBytes[index] = (byte)(addressBytes[index] | (byte)~maskBytes[index]);
            }

            return maskIsEmpty ? null : new IPAddress(broadcastBytes);
        }

        private static bool IsSameSubnet(IPAddress localAddress, IPAddress mask, IPAddress targetAddress)
        {
            if (mask == null || mask.AddressFamily != AddressFamily.InterNetwork)
            {
                return false;
            }

            var localBytes = localAddress.GetAddressBytes();
            var targetBytes = targetAddress.GetAddressBytes();
            var maskBytes = mask.GetAddressBytes();
            for (var index = 0; index < 4; index++)
            {
                if ((localBytes[index] & maskBytes[index]) != (targetBytes[index] & maskBytes[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static byte[] BuildMagicPacket(byte[] macBytes)
        {
            // 6 字节 0xFF + MAC 重复 16 次
            var packet = new byte[6 + (16 * 6)];
            for (var index = 0; index < 6; index++)
            {
                packet[index] = 0xFF;
            }

            for (var count = 0; count < 16; count++)
            {
                Buffer.BlockCopy(macBytes, 0, packet, 6 + (count * 6), 6);
            }

            return packet;
        }

        private sealed class LocalInterface
        {
            public LocalInterface(IPAddress address, IPAddress mask)
            {
                Address = address;
                Mask = mask;
            }

            public IPAddress Address { get; }

            public IPAddress Mask { get; }
        }

        private sealed class SendTarget
        {
            public SendTarget(IPAddress localAddress, IPAddress remoteAddress)
            {
                LocalAddress = localAddress;
                RemoteAddress = remoteAddress;
            }

            public IPAddress LocalAddress { get; }

            public IPAddress RemoteAddress { get; }

            public string Describe(int port)
            {
                var remote = port > 0 ? RemoteAddress + ":" + port : RemoteAddress.ToString();
                return LocalAddress == null ? remote : remote + "(via " + LocalAddress + ")";
            }
        }
    }
}
// AI-code-end