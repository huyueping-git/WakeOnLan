namespace WakeOnLanClient.Models
{
    /// <summary>
    /// 电源操作类型。
    /// </summary>
    public enum PowerAction
    {
        /// <summary>
        /// 远程开机（Wake-on-LAN）。
        /// </summary>
        PowerOn = 0,

        /// <summary>
        /// 远程关机。
        /// </summary>
        PowerOff = 1
    }
}
