using Newtonsoft.Json;
using System.Collections.Concurrent;

namespace PointsSystem;

/// <summary>
/// 玩家数据缓存（持久化为 JSON）
/// </summary>
internal class CacheData
{
    [JsonProperty("玩家数据", Order = 0)]
    public ConcurrentDictionary<string, PlayerCache> Players { get; set; } = new();

    /// <summary>
    /// 单个玩家的持久化数据
    /// </summary>
    public class PlayerCache
    {
        /// <summary>密码的 SHA256 哈希</summary>
        [JsonProperty("密码哈希", Order = 0)]
        public string PasswordHash { get; set; } = "";

        /// <summary>积分余额</summary>
        [JsonProperty("积分", Order = 1)]
        public int Points { get; set; } = 0;

        /// <summary>累计签到次数</summary>
        [JsonProperty("累计签到", Order = 2)]
        public int TotalSignIns { get; set; } = 0;

        /// <summary>当前连续签到次数</summary>
        [JsonProperty("连续签到", Order = 3)]
        public int ConsecutiveSignIns { get; set; } = 0;

        /// <summary>最近一次签到日期（UTC）</summary>
        [JsonProperty("上次签到日期", Order = 4)]
        public DateTime? LastSignInDate { get; set; } = null;

        /// <summary>上次掷骰子时间</summary>
        [JsonProperty("上次掷骰子", Order = 5)]
        public DateTime? LastDiceTime { get; set; } = null;

        /// <summary>上次猜数字时间</summary>
        [JsonProperty("上次猜数字", Order = 6)]
        public DateTime? LastGuessTime { get; set; } = null;

        /// <summary>上次抢劫时间</summary>
        [JsonProperty("上次抢劫", Order = 7)]
        public DateTime? LastRobTime { get; set; } = null;

        /// <summary>抽奖仓库 — 抽中的物品暂存于此，可领取或回收</summary>
        [JsonProperty("抽奖仓库", Order = 8)]
        public List<StoredItem> LotteryStorage { get; set; } = new();

        /// <summary>是否已注册（密码非空即已注册）</summary>
        [JsonIgnore]
        public bool IsRegistered => !string.IsNullOrEmpty(PasswordHash);
    }

    /// <summary>
    /// 仓库中暂存的物品条目
    /// </summary>
    public class StoredItem
    {
        /// <summary>唯一序号（用于玩家指令引用）</summary>
        [JsonProperty("序号", Order = 0)]
        public int Id { get; set; }

        /// <summary>物品 ID</summary>
        [JsonProperty("物品ID", Order = 1)]
        public int ItemID { get; set; }

        /// <summary>堆叠数量</summary>
        [JsonProperty("数量", Order = 2)]
        public int Stack { get; set; } = 1;

        /// <summary>前缀</summary>
        [JsonProperty("前缀", Order = 3)]
        public int Prefix { get; set; } = 0;

        /// <summary>获得时间</summary>
        [JsonProperty("获得时间", Order = 4)]
        public DateTime ObtainedAt { get; set; } = DateTime.UtcNow;
    }

    #region 数据管理方法
    public PlayerCache GetOrCreate(string name) => Players.GetOrAdd(name, _ => new PlayerCache());

    public bool TryGet(string name, out PlayerCache data) => Players.TryGetValue(name, out data!);

    /// <summary>
    /// 为玩家仓库中的物品分配唯一序号（基于当前最大序号+1）
    /// </summary>
    public int NextStorageId(PlayerCache data)
    {
        if (data.LotteryStorage.Count == 0) return 1;
        return data.LotteryStorage.Max(i => i.Id) + 1;
    }
    #endregion

    #region 文件读写
    public void Save(string path)
    {
        string json = JsonConvert.SerializeObject(this, Formatting.Indented);
        File.WriteAllText(path, json);
    }

    public static CacheData Load(string path)
    {
        if (!File.Exists(path))
        {
            var fresh = new CacheData();
            fresh.Save(path);
            return fresh;
        }
        string json = File.ReadAllText(path);
        return JsonConvert.DeserializeObject<CacheData>(json) ?? new CacheData();
    }
    #endregion
}