using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace PointsSystem;

/// <summary>
/// 插件配置文件
/// </summary>
internal class Configuration
{
    #region 基础设置
    [JsonProperty("插件开关", Order = 0)]
    public bool Enabled { get; set; } = true;

    [JsonProperty("数据同步秒数", Order = 1)]
    public int SyncIntervalSec { get; set; } = 60;

    [JsonProperty("离服清理缓存", Order = 2)]
    public bool ClearOnLeave { get; set; } = false;
    #endregion

    #region 签到设置
    [JsonProperty("签到基础积分", Order = 10)]
    public int SignBasePoints { get; set; } = 10;

    [JsonProperty("签到连续奖励", Order = 11)]
    public int SignConsecutiveBonus { get; set; } = 5;

    [JsonProperty("签到最大连续奖励", Order = 12)]
    public int SignMaxConsecutiveBonus { get; set; } = 50;
    #endregion

    #region 掷骰子设置
    [JsonProperty("掷骰子冷却秒数", Order = 20)]
    public int DiceCooldownSec { get; set; } = 300;

    [JsonProperty("掷骰子获胜概率", Order = 21)]
    public double DiceWinProbability { get; set; } = 0.45;

    [JsonProperty("掷骰子积分消耗", Order = 22)]
    public int DiceCost { get; set; } = 10;

    [JsonProperty("掷骰子获胜奖励", Order = 23)]
    public int DiceReward { get; set; } = 25;
    #endregion

    #region 猜数字设置
    [JsonProperty("猜数字冷却秒数", Order = 30)]
    public int GuessCooldownSec { get; set; } = 600;

    [JsonProperty("猜数字获胜概率", Order = 31)]
    public double GuessWinProbability { get; set; } = 0.1;

    [JsonProperty("猜数字最小值", Order = 32)]
    public int GuessRangeMin { get; set; } = 1;

    [JsonProperty("猜数字最大值", Order = 33)]
    public int GuessRangeMax { get; set; } = 100;

    [JsonProperty("猜数字积分消耗", Order = 34)]
    public int GuessCost { get; set; } = 5;

    [JsonProperty("猜数字获胜奖励", Order = 35)]
    public int GuessReward { get; set; } = 50;
    #endregion

    #region 抢劫设置
    [JsonProperty("抢劫冷却秒数", Order = 40)]
    public int RobCooldownSec { get; set; } = 1800;

    [JsonProperty("抢劫成功概率", Order = 41)]
    public double RobSuccessProbability { get; set; } = 0.4;

    [JsonProperty("抢劫最小积分", Order = 42)]
    public int RobMinPoints { get; set; } = 5;

    [JsonProperty("抢劫最大积分", Order = 43)]
    public int RobMaxPoints { get; set; } = 50;

    [JsonProperty("抢劫失败惩罚比例", Order = 44)]
    public double RobFailurePenaltyRate { get; set; } = 0.5;
    #endregion

    #region 抽奖设置
    [JsonProperty("抽奖积分消耗", Order = 50)]
    public int LotteryCost { get; set; } = 20;

    [JsonProperty("抽奖物品列表", Order = 51)]
    public List<LotteryEntry> LotteryItems { get; set; } = new()
    {
        new LotteryEntry { ItemID = 73,  Weight = 5 },
        new LotteryEntry { ItemID = 155, Weight = 3 },
        new LotteryEntry { ItemID = 65,  Weight = 2 },
        new LotteryEntry { ItemID = 125, Weight = 1 },
    };
    #endregion

    #region 回收设置（仅限抽奖仓库中的物品）
    [JsonProperty("回收比例", Order = 60)]
    public double RecycleRate { get; set; } = 0.5;

    [JsonProperty("回收最小价值(铜币)", Order = 61)]
    public int RecycleMinValue { get; set; } = 100;
    #endregion

    #region 转账设置
    [JsonProperty("转账最小积分", Order = 70)]
    public int TransferMinPoints { get; set; } = 1;

    [JsonProperty("转账手续费比例", Order = 71)]
    public double TransferFeeRate { get; set; } = 0.0; // 0 = 免手续费
    #endregion

    #region 抽奖物品条目
    public class LotteryEntry
    {
        [JsonProperty("物品ID")]
        public int ItemID { get; set; }

        [JsonProperty("权重")]
        public int Weight { get; set; } = 1;

        [JsonProperty("数量")]
        public int Stack { get; set; } = 1;

        [JsonProperty("前缀")]
        public int Prefix { get; set; } = 0;
    }
    #endregion

    #region 文件读写
    public void Write(string path, CacheData cache)
    {
        string json = JsonConvert.SerializeObject(this, Formatting.Indented);
        File.WriteAllText(path, json);
        cache.Save(GetCachePath(path));
    }

    public static Configuration Read(string path, out CacheData cache)
    {
        if (!File.Exists(path))
        {
            var cfg = new Configuration();
            cache = new CacheData();
            cfg.Write(path, cache);
            return cfg;
        }
        try
        {
            string json = File.ReadAllText(path);
            var cfg = JsonConvert.DeserializeObject<Configuration>(json)!;
            cache = CacheData.Load(GetCachePath(path));
            return cfg;
        }
        catch (JsonReaderException ex)
        {
            string json = File.ReadAllText(path);
            string[] lines = json.Split('\n');
            int line = ex.LineNumber;
            int idx = Math.Max(0, Math.Min(line - 2, lines.Length - 1));
            string text = lines[idx].Trim();
            throw new Exception(
                $"配置文件格式错误！\n" +
                $"位置: 第 {line - 1} 行\n" +
                $"内容: {text ?? string.Empty}\n" +
                $"路径: {FormatJsonPath(ex.Path ?? string.Empty)}", ex);
        }
    }

    private static string GetCachePath(string configPath)
    {
        var dir = Path.GetDirectoryName(configPath)!;
        return Path.Combine(dir, "数据缓存.json");
    }

    private static string FormatJsonPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        return Regex.Replace(path, @"\[(\d+)\]", m =>
        {
            int index = int.Parse(m.Groups[1].Value);
            return $":第{index + 1}项";
        });
    }
    #endregion
}