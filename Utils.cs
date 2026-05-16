using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Utilities;
using TShockAPI;

namespace PointsSystem;

internal class Utils
{
    #region 随机器与颜色
    public static UnifiedRandom Random = Main.rand;
    public static Color color => new(240, 250, 150);          // 单色（淡黄）
    public static Color color2 => new(Random.Next(180, 250),   // 随机暖色
                                      Random.Next(180, 250),
                                      Random.Next(180, 250));
    #endregion

    #region 文本渐变色（逐字符）
    /// <summary>
    /// 对纯文本应用逐字符渐变色，若已含 [c/...] 标签则保留标签部分
    /// </summary>
    public static string TextGradient(string text, TSPlayer? plr = null)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // 先替换占位符
        text = ReplacePlaceholders(text, plr);

        // 已经包含颜色标签 → 混合处理
        if (text.Contains("[c/"))
            return MixedText(text);

        return ApplyGrad(text);
    }

    private static string ApplyGrad(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var sb = new StringBuilder();
        var start = new Color(166, 213, 234);
        var end = new Color(245, 247, 175);
        int cnt = 0;
        foreach (char c in text)
            if (c != '\n' && c != '\r') cnt++;
        if (cnt == 0) return text;

        int idx = 0;
        foreach (char c in text)
        {
            if (c == '\n' || c == '\r') { sb.Append(c); continue; }
            float ratio = (float)idx / (cnt - 1);
            var clr = Color.Lerp(start, end, ratio);
            sb.Append($"[c/{clr.Hex3()}:{c}]");
            idx++;
        }
        return sb.ToString();
    }

    private static string MixedText(string text)
    {
        var sb = new StringBuilder();
        var regex = new Regex(@"(\[c/([0-9a-fA-F]+):([^\]]+)\]|\[i(?:/s\d+)?:\d+\])");
        var matches = regex.Matches(text);
        if (matches.Count == 0) return ApplyGrad(text);

        int idx = 0;
        foreach (Match m in matches.Cast<Match>())
        {
            if (m.Index > idx)
                sb.Append(ApplyGrad(text.Substring(idx, m.Index - idx)));
            sb.Append(m.Value);
            idx = m.Index + m.Length;
        }
        if (idx < text.Length)
            sb.Append(ApplyGrad(text.Substring(idx)));
        return sb.ToString();
    }
    #endregion

    #region 占位符替换
    private static string ReplacePlaceholders(string text, TSPlayer? plr)
    {
        if (plr != null)
        {
            text = Regex.Replace(text, @"\{玩家名\}", plr.Name, RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\{账号\}", plr.Account.ID.ToString(), RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\{组名\}", plr.Account.Group, RegexOptions.IgnoreCase);
        }
        text = Regex.Replace(text, @"\{插件名\}", Plugin.PluginName, RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\{在线人数\}", TShock.Utils.GetActivePlayerCount().ToString(), RegexOptions.IgnoreCase);
        return text;
    }
    #endregion

    #region 密码哈希（SHA256）
    public static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }

    public static bool VerifyPassword(string password, string hash)
        => HashPassword(password) == hash;
    #endregion

    #region 获取物品图标字符串
    public static string ItemIcon(int itemID, int stack = 1) => $"[i/s{stack}:{itemID}]";
    #endregion
}