using System.Text;
using Terraria;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace PointsSystem;

[ApiVersion(2, 1)]
public class Plugin : TerrariaPlugin
{
    #region 插件信息
    public const string PluginName = "积分系统";
    public override string Name => PluginName;
    public override string Author => "淦";
    public override Version Version => new(1, 0, 0);
    public override string Description => "签到 · 抽奖 · 掷骰子 · 猜数字 · 抢劫 · 回收 一体化积分系统";
    #endregion

    #region 文件路径
    public static readonly string MainPath = Path.Combine(TShock.SavePath, PluginName);
    public static readonly string ConfigPath = Path.Combine(MainPath, "配置文件.json");
    #endregion

    #region 静态实例
    internal static Configuration Config = new();
    internal static CacheData Cache = new();
    #endregion

    #region 构造函数
    public Plugin(Main game) : base(game) { }
    #endregion

    #region 初始化
    public override void Initialize()
    {
        LoadAllConfig();
        GeneralHooks.ReloadEvent += OnReload;

        // 注册所有指令
        RegisterCommands();

        // 事件钩子
        ServerApi.Hooks.GameUpdate.Register(this, OnGameUpdate);
    }

    private void RegisterCommands()
    {
        // ---- 账户 ----
        Commands.ChatCommands.Add(new Command("points.use", CmdRegister, "注册", "reg")
        { HelpText = "注册积分系统账户。用法: /注册 <密码>" });

        // ---- 签到 ----
        Commands.ChatCommands.Add(new Command("points.use", CmdSignIn, "签到", "sign")
        { HelpText = "每日签到获取积分，连续签到有额外奖励。" });

        // ---- 抽奖 ----
        Commands.ChatCommands.Add(new Command("points.use", CmdLottery, "抽奖", "lottery")
        { HelpText = "消耗积分抽取随机物品。" });

        // ---- 回收 ----
        Commands.ChatCommands.Add(new Command("points.use", CmdRecycle, "回收", "recycle")
        { HelpText = "回收手中物品换取积分。" });

        // ---- 掷骰子 ----
        Commands.ChatCommands.Add(new Command("points.use", CmdDice, "掷骰子", "dice")
        { HelpText = "掷骰子博弈。用法: /掷骰子" });

        // ---- 猜数字 ----
        Commands.ChatCommands.Add(new Command("points.use", CmdGuess, "猜数字", "guess")
        { HelpText = "猜数字赢积分。用法: /猜数字 <数字>" });

        // ---- 抢劫 ----
        Commands.ChatCommands.Add(new Command("points.rob", CmdRob, "抢劫", "rob")
        { HelpText = "抢劫其他玩家积分。用法: /抢劫 <玩家名>" });

        // ---- 查看 ----
        Commands.ChatCommands.Add(new Command("points.use", CmdProfile, "查看", "profile", "信息")
        { HelpText = "查看玩家信息。用法: /查看 [玩家名]" });

        // ---- 管理 ----
        Commands.ChatCommands.Add(new Command("points.admin", CmdAdminPoints, "积分管理", "pointsadmin")
        { HelpText = "管理积分。用法: /积分管理 <add|set|reset> <玩家名> [数量]" });
    }
    #endregion

    #region 释放
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            GeneralHooks.ReloadEvent -= OnReload;
            ServerApi.Hooks.GameUpdate.Deregister(this, OnGameUpdate);
            Cache.Save(CachePath);
        }
        base.Dispose(disposing);
    }
    #endregion

    #region 配置加载
    private static string CachePath => Path.Combine(MainPath, "数据缓存.json");

    private static void LoadAllConfig()
    {
        try
        {
            if (!Directory.Exists(MainPath))
                Directory.CreateDirectory(MainPath);

            Config = Configuration.Read(ConfigPath, out var cache);
            Cache = cache;
            Config.Write(ConfigPath, Cache);
            TShock.Log.ConsoleInfo($"[{PluginName}] 配置加载成功。");
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[{PluginName}] 配置加载失败：{ex.Message}");
        }
    }

    private void OnReload(ReloadEventArgs args)
    {
        LoadAllConfig();
        // 使用明确的重载：SendMessage(string, byte, byte, byte)
        args.Player.SendMessage(
            $"[c/AAFFAA:{PluginName}] 配置已重新加载。",
            Utils.color.R, Utils.color.G, Utils.color.B);
    }
    #endregion

    #region 定时保存
    private short _tick;
    private short _sec;
    private void OnGameUpdate(EventArgs args)
    {
        if (!Config.Enabled) return;

        _tick++;
        if (_tick >= 60)   // 每秒
        {
            _sec++;
            _tick = 0;
        }
        if (_sec >= Config.SyncIntervalSec)
        {
            Cache.Save(CachePath);
            _sec = 0;
        }
    }
    #endregion

    // ======================== 指令实现 ============================

    #region 注册 /reg
    private void CmdRegister(CommandArgs args)
    {
        var plr = args.Player;
        if (!plr.IsLoggedIn)
        {
            plr.SendErrorMessage("请先登录 TShock 账户再注册积分系统。");
            return;
        }
        if (args.Parameters.Count < 1)
        {
            plr.SendErrorMessage("用法: /注册 <密码>");
            return;
        }

        var pwd = args.Parameters[0];
        if (pwd.Length < 3)
        {
            plr.SendErrorMessage("密码至少需要 3 个字符。");
            return;
        }

        var data = Cache.GetOrCreate(plr.Name);
        if (data.IsRegistered)
        {
            plr.SendErrorMessage("你已经注册过了！如需重置密码请联系管理员。");
            return;
        }

        data.PasswordHash = Utils.HashPassword(pwd);
        Cache.Save(CachePath);

        // 使用 byte 重载避免歧义
        plr.SendMessage(
            Utils.TextGradient($"[{PluginName}] 注册成功！欢迎 {plr.Name}，现在你可以使用签到、抽奖等功能了。", plr),
            Utils.color.R, Utils.color.G, Utils.color.B);
        TShock.Log.ConsoleInfo($"[{PluginName}] 玩家 {plr.Name} 注册了积分账户。");
    }
    #endregion

    #region 签到 /sign
    private void CmdSignIn(CommandArgs args)
    {
        var plr = args.Player;
        if (!CheckRegistered(plr)) return;

        var data = Cache.GetOrCreate(plr.Name);
        var today = DateTime.UtcNow.Date;

        // 检查是否今天已签到
        if (data.LastSignInDate.HasValue && data.LastSignInDate.Value.Date == today)
        {
            plr.SendErrorMessage("你今天已经签到过了，明天再来吧！");
            return;
        }

        // 判断连续签到
        if (data.LastSignInDate.HasValue &&
            data.LastSignInDate.Value.Date == today.AddDays(-1))
        {
            // 昨天签到了 → 连续
            data.ConsecutiveSignIns++;
        }
        else
        {
            // 中断 → 重置
            data.ConsecutiveSignIns = 1;
        }

        data.TotalSignIns++;
        data.LastSignInDate = DateTime.UtcNow;

        // 计算奖励：基础 + min(连续次数-1, 最大额外次数) × 连续奖励
        int extraDays = Math.Min(data.ConsecutiveSignIns - 1,
            Config.SignMaxConsecutiveBonus / Math.Max(1, Config.SignConsecutiveBonus));
        int bonus = extraDays * Config.SignConsecutiveBonus;
        int earned = Config.SignBasePoints + bonus;

        data.Points += earned;
        Cache.Save(CachePath);

        var sb = new StringBuilder();
        sb.AppendLine($"[{PluginName}] 签到成功！");
        sb.AppendLine($"  基础积分: +{Config.SignBasePoints}");
        if (bonus > 0)
            sb.AppendLine($"  连续签到奖励: +{bonus} (连续 {data.ConsecutiveSignIns} 天)");
        sb.AppendLine($"  本次获得: [c/FFD700:+{earned} 积分]");
        sb.AppendLine($"  当前积分: {data.Points}");
        sb.AppendLine($"  累计签到: {data.TotalSignIns} 次");

        plr.SendMessage(Utils.TextGradient(sb.ToString(), plr),
            Utils.color.R, Utils.color.G, Utils.color.B);
    }
    #endregion

    #region 抽奖 /lottery
    private void CmdLottery(CommandArgs args)
    {
        var plr = args.Player;
        if (!CheckRegistered(plr)) return;

        var data = Cache.GetOrCreate(plr.Name);

        if (Config.LotteryItems == null || Config.LotteryItems.Count == 0)
        {
            plr.SendErrorMessage("抽奖池为空，请联系管理员配置。");
            return;
        }
        if (data.Points < Config.LotteryCost)
        {
            plr.SendErrorMessage($"积分不足！抽奖需要 {Config.LotteryCost} 积分，你当前有 {data.Points} 积分。");
            return;
        }

        // 扣积分
        data.Points -= Config.LotteryCost;

        // 按权重抽选物品
        int totalWeight = Config.LotteryItems.Sum(i => i.Weight);
        int roll = Utils.Random.Next(totalWeight);
        int cumulative = 0;
        Configuration.LotteryEntry? won = null;
        foreach (var entry in Config.LotteryItems)
        {
            cumulative += entry.Weight;
            if (roll < cumulative)
            {
                won = entry;
                break;
            }
        }
        won ??= Config.LotteryItems[0]; // 保险

        // 给予物品
        plr.GiveItem(won.ItemID, won.Stack, won.Prefix);
        Cache.Save(CachePath);

        var itemName = Lang.GetItemNameValue(won.ItemID) ?? $"物品#{won.ItemID}";
        plr.SendMessage(
            Utils.TextGradient(
                $"[{PluginName}] 抽奖花费 {Config.LotteryCost} 积分，" +
                $"获得了 {Utils.ItemIcon(won.ItemID, won.Stack)} [c/FFD700:{itemName}]" +
                $"{(won.Stack > 1 ? $" x{won.Stack}" : "")}！剩余积分: {data.Points}", plr),
            Utils.color.R, Utils.color.G, Utils.color.B);
    }
    #endregion

    #region 回收 /recycle
    private void CmdRecycle(CommandArgs args)
    {
        var plr = args.Player;
        if (!CheckRegistered(plr)) return;

        var item = plr.SelectedItem;
        if (item == null || item.type == ItemID.None || item.stack <= 0)
        {
            plr.SendErrorMessage("请手持要回收的物品。");
            return;
        }
        if (item.value < Config.RecycleMinValue)
        {
            plr.SendErrorMessage($"该物品价值太低（{item.value} 铜币），不可回收。最低要求: {Config.RecycleMinValue} 铜币。");
            return;
        }

        int pointsEarned = (int)(item.value * Config.RecycleRate * item.stack);
        if (pointsEarned <= 0)
        {
            plr.SendErrorMessage("该物品回收价值为 0，无法回收。");
            return;
        }

        var data = Cache.GetOrCreate(plr.Name);
        string itemName = Lang.GetItemNameValue(item.type) ?? $"物品#{item.type}";

        // 回收整组
        int stack = item.stack;
        plr.TPlayer.inventory[plr.TPlayer.selectedItem].TurnToAir();
        plr.SendData(PacketTypes.PlayerSlot, "", plr.Index, plr.TPlayer.selectedItem);

        data.Points += pointsEarned;
        Cache.Save(CachePath);

        plr.SendMessage(
            Utils.TextGradient(
                $"[{PluginName}] 回收了 {Utils.ItemIcon(item.type, stack)} [c/FFD700:{itemName}] x{stack}，" +
                $"获得 +{pointsEarned} 积分。当前积分: {data.Points}", plr),
            Utils.color.R, Utils.color.G, Utils.color.B);
    }
    #endregion

    #region 掷骰子 /dice
    private void CmdDice(CommandArgs args)
    {
        var plr = args.Player;
        if (!CheckRegistered(plr)) return;

        var data = Cache.GetOrCreate(plr.Name);

        // 冷却检查
        if (data.LastDiceTime.HasValue)
        {
            var elapsed = (DateTime.UtcNow - data.LastDiceTime.Value).TotalSeconds;
            if (elapsed < Config.DiceCooldownSec)
            {
                int remain = (int)(Config.DiceCooldownSec - elapsed);
                plr.SendErrorMessage($"掷骰子冷却中，请等待 {FormatTime(remain)}。");
                return;
            }
        }
        if (data.Points < Config.DiceCost)
        {
            plr.SendErrorMessage($"积分不足！掷骰子需要 {Config.DiceCost} 积分，你当前有 {data.Points} 积分。");
            return;
        }

        data.LastDiceTime = DateTime.UtcNow;
        data.Points -= Config.DiceCost;

        bool win = Utils.Random.NextDouble() < Config.DiceWinProbability;
        int dice1 = Utils.Random.Next(1, 7);
        int dice2 = Utils.Random.Next(1, 7);
        int total = dice1 + dice2;
        string diceIcon = $"🎲 {dice1} + {dice2} = {total}";

        string msg;
        if (win)
        {
            data.Points += Config.DiceReward;
            msg = $"[{PluginName}] {diceIcon} [c/AAFFAA:你赢了！] +{Config.DiceReward} 积分。当前积分: {data.Points}";
        }
        else
        {
            msg = $"[{PluginName}] {diceIcon} [c/FF8888:你输了！] -{Config.DiceCost} 积分。当前积分: {data.Points}";
        }

        plr.SendMessage(Utils.TextGradient(msg, plr),
            Utils.color.R, Utils.color.G, Utils.color.B);
        Cache.Save(CachePath);
    }
    #endregion

    #region 猜数字 /guess
    private void CmdGuess(CommandArgs args)
    {
        var plr = args.Player;
        if (!CheckRegistered(plr)) return;

        var data = Cache.GetOrCreate(plr.Name);

        if (args.Parameters.Count < 1 || !int.TryParse(args.Parameters[0], out int guess))
        {
            plr.SendErrorMessage($"用法: /猜数字 <{Config.GuessRangeMin}~{Config.GuessRangeMax}>");
            return;
        }
        if (guess < Config.GuessRangeMin || guess > Config.GuessRangeMax)
        {
            plr.SendErrorMessage($"猜测数字必须在 {Config.GuessRangeMin} ~ {Config.GuessRangeMax} 之间。");
            return;
        }

        // 冷却检查
        if (data.LastGuessTime.HasValue)
        {
            var elapsed = (DateTime.UtcNow - data.LastGuessTime.Value).TotalSeconds;
            if (elapsed < Config.GuessCooldownSec)
            {
                int remain = (int)(Config.GuessCooldownSec - elapsed);
                plr.SendErrorMessage($"猜数字冷却中，请等待 {FormatTime(remain)}。");
                return;
            }
        }
        if (data.Points < Config.GuessCost)
        {
            plr.SendErrorMessage($"积分不足！猜数字需要 {Config.GuessCost} 积分，当前有 {data.Points} 积分。");
            return;
        }

        data.LastGuessTime = DateTime.UtcNow;
        data.Points -= Config.GuessCost;

        int answer = Utils.Random.Next(Config.GuessRangeMin, Config.GuessRangeMax + 1);
        bool win = guess == answer && Utils.Random.NextDouble() < Config.GuessWinProbability;

        string msg;
        if (win)
        {
            data.Points += Config.GuessReward;
            msg = $"[{PluginName}] 你猜了 {guess}，答案是 [c/AAFFAA:{answer}] —— [c/AAFFAA:猜中了！]" +
                  $" +{Config.GuessReward} 积分。当前积分: {data.Points}";
        }
        else
        {
            msg = $"[{PluginName}] 你猜了 {guess}，答案是 [c/FF8888:{answer}] —— [c/FF8888:没猜中……]" +
                  $" -{Config.GuessCost} 积分。当前积分: {data.Points}";
        }

        plr.SendMessage(Utils.TextGradient(msg, plr),
            Utils.color.R, Utils.color.G, Utils.color.B);
        Cache.Save(CachePath);
    }
    #endregion

    #region 抢劫 /rob
    private void CmdRob(CommandArgs args)
    {
        var plr = args.Player;
        if (!CheckRegistered(plr)) return;

        var data = Cache.GetOrCreate(plr.Name);

        if (args.Parameters.Count < 1)
        {
            plr.SendErrorMessage("用法: /抢劫 <玩家名>");
            return;
        }

        string targetName = args.Parameters[0];

        // 【修复】手动查找玩家，替代 TShock.Utils.FindPlayer（TShock 6.x API 变更）
        var targets = TShock.Players
            .Where(p => p != null && p.Active && p.Name != null
                        && p.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (targets.Count == 0)
        {
            plr.SendErrorMessage($"找不到在线玩家 \"{targetName}\"。");
            return;
        }
        if (targets.Count > 1)
        {
            plr.SendErrorMessage($"找到多个匹配玩家: {string.Join(", ", targets.Select(p => p.Name))}。请更精确指定。");
            return;
        }

        var target = targets[0];
        if (target.Name == plr.Name)
        {
            plr.SendErrorMessage("你不能抢劫自己！");
            return;
        }

        var targetData = Cache.GetOrCreate(target.Name);
        if (!targetData.IsRegistered)
        {
            plr.SendErrorMessage($"玩家 {target.Name} 尚未注册积分系统，无法抢劫。");
            return;
        }

        // 冷却检查
        if (data.LastRobTime.HasValue)
        {
            var elapsed = (DateTime.UtcNow - data.LastRobTime.Value).TotalSeconds;
            if (elapsed < Config.RobCooldownSec)
            {
                int remain = (int)(Config.RobCooldownSec - elapsed);
                plr.SendErrorMessage($"抢劫冷却中，请等待 {FormatTime(remain)}。");
                return;
            }
        }

        data.LastRobTime = DateTime.UtcNow;

        // 随机抢劫金额
        int stealAmount = Utils.Random.Next(Config.RobMinPoints, Config.RobMaxPoints + 1);
        stealAmount = Math.Min(stealAmount, targetData.Points); // 不超过目标积分

        if (stealAmount <= 0)
        {
            plr.SendErrorMessage($"{target.Name} 积分不足，无法抢劫。");
            return;
        }

        bool success = Utils.Random.NextDouble() < Config.RobSuccessProbability;

        if (success)
        {
            targetData.Points -= stealAmount;
            data.Points += stealAmount;

            plr.SendMessage(
                Utils.TextGradient(
                    $"[{PluginName}] [c/AAFFAA:抢劫成功！]从 {target.Name} 处抢得 {stealAmount} 积分。当前积分: {data.Points}", plr),
                Utils.color.R, Utils.color.G, Utils.color.B);
            target.SendMessage(
                Utils.TextGradient(
                    $"[{PluginName}] [c/FF8888:{plr.Name} 抢走了你 {stealAmount} 积分！]当前积分: {targetData.Points}", target),
                Utils.color.R, Utils.color.G, Utils.color.B);
        }
        else
        {
            int penalty = (int)(stealAmount * Config.RobFailurePenaltyRate);
            penalty = Math.Max(1, penalty);
            penalty = Math.Min(penalty, data.Points); // 不超过抢劫者积分

            data.Points -= penalty;
            targetData.Points += penalty;

            plr.SendMessage(
                Utils.TextGradient(
                    $"[{PluginName}] [c/FF8888:抢劫失败！]你被反抢，扣除 {penalty} 积分给 {target.Name}。当前积分: {data.Points}", plr),
                Utils.color.R, Utils.color.G, Utils.color.B);
            target.SendMessage(
                Utils.TextGradient(
                    $"[{PluginName}] [c/AAFFAA:{plr.Name} 试图抢劫你但失败了！]你获得 {penalty} 积分。当前积分: {targetData.Points}", target),
                Utils.color.R, Utils.color.G, Utils.color.B);
        }

        Cache.Save(CachePath);
    }
    #endregion

    #region 查看 /profile
    private void CmdProfile(CommandArgs args)
    {
        var plr = args.Player;

        string targetName;
        if (args.Parameters.Count >= 1)
            targetName = args.Parameters[0];
        else
            targetName = plr.Name;

        // 【修复】手动查找在线玩家
        var matches = TShock.Players
            .Where(p => p != null && p.Active && p.Name != null
                        && p.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        TSPlayer? targetPlr = null;
        if (matches.Count == 1)
        {
            targetPlr = matches[0];
            targetName = targetPlr.Name;
        }
        else if (matches.Count > 1)
        {
            plr.SendErrorMessage($"找到多个匹配玩家: {string.Join(", ", matches.Select(p => p.Name))}。请更精确指定。");
            return;
        }

        // 如果不在线，尝试从缓存中查找
        if (targetPlr == null)
        {
            var key = Cache.Players.Keys.FirstOrDefault(
                k => k.Equals(targetName, StringComparison.OrdinalIgnoreCase));
            if (key != null)
                targetName = key;
            else
            {
                plr.SendErrorMessage($"找不到玩家 \"{targetName}\" 的数据。");
                return;
            }
        }

        if (!Cache.TryGet(targetName, out var data) || !data.IsRegistered)
        {
            plr.SendErrorMessage($"玩家 {targetName} 尚未注册积分系统。");
            return;
        }

        // 获取在线状态
        bool online = TShock.Players.Any(p => p != null && p.Active
            && p.Name != null && p.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase));
        string status = online ? "[c/AAFFAA:在线]" : "[c/AAAAAA:离线]";

        var sb = new StringBuilder();
        sb.AppendLine($"══════ [c/FFD700:{targetName}] 的信息 ══════");
        sb.AppendLine($"  状态      : {status}");
        sb.AppendLine($"  积分      : [c/FFD700:{data.Points}]");
        sb.AppendLine($"  累计签到  : {data.TotalSignIns} 次");
        sb.AppendLine($"  连续签到  : {data.ConsecutiveSignIns} 天");
        if (data.LastSignInDate.HasValue)
            sb.AppendLine($"  上次签到  : {data.LastSignInDate.Value:yyyy-MM-dd HH:mm}");
        if (online && targetPlr != null)
        {
            sb.AppendLine($"  手持物品  : {Utils.ItemIcon(targetPlr.SelectedItem.type)} " +
                $"{Lang.GetItemNameValue(targetPlr.SelectedItem.type)}");
        }
        sb.AppendLine($"══════════════════════════════");

        plr.SendMessage(Utils.TextGradient(sb.ToString(), plr),
            Utils.color.R, Utils.color.G, Utils.color.B);
    }
    #endregion

    #region 管理指令 /pointsadmin
    private void CmdAdminPoints(CommandArgs args)
    {
        var plr = args.Player;
        if (args.Parameters.Count < 2)
        {
            plr.SendErrorMessage("用法: /积分管理 <add|set|reset> <玩家名> [数量]");
            return;
        }

        string action = args.Parameters[0].ToLower();
        string targetName = args.Parameters[1];

        // 大小写不敏感查找缓存中的玩家
        var key = Cache.Players.Keys.FirstOrDefault(
            k => k.Equals(targetName, StringComparison.OrdinalIgnoreCase));
        if (key == null)
        {
            plr.SendErrorMessage($"找不到玩家 \"{targetName}\" 的积分数据。");
            return;
        }
        var data = Cache.GetOrCreate(key);

        switch (action)
        {
            case "add":
                if (args.Parameters.Count < 3 || !int.TryParse(args.Parameters[2], out int addAmt))
                {
                    plr.SendErrorMessage("请提供要增加的积分数量。");
                    return;
                }
                data.Points += addAmt;
                plr.SendMessage(
                    Utils.TextGradient($"[{PluginName}] 已为 {key} 增加 {addAmt} 积分，当前积分: {data.Points}"),
                    Utils.color.R, Utils.color.G, Utils.color.B);
                break;

            case "set":
                if (args.Parameters.Count < 3 || !int.TryParse(args.Parameters[2], out int setAmt))
                {
                    plr.SendErrorMessage("请提供要设置的积分数量。");
                    return;
                }
                data.Points = Math.Max(0, setAmt);
                plr.SendMessage(
                    Utils.TextGradient($"[{PluginName}] 已将 {key} 的积分设置为 {data.Points}"),
                    Utils.color.R, Utils.color.G, Utils.color.B);
                break;

            case "reset":
                data.Points = 0;
                data.TotalSignIns = 0;
                data.ConsecutiveSignIns = 0;
                data.LastSignInDate = null;
                plr.SendMessage(
                    Utils.TextGradient($"[{PluginName}] 已重置 {key} 的积分和签到数据。"),
                    Utils.color.R, Utils.color.G, Utils.color.B);
                break;

            default:
                plr.SendErrorMessage("未知操作。可用: add, set, reset");
                return;
        }

        Cache.Save(CachePath);
        TShock.Log.ConsoleInfo($"[{PluginName}] 管理员 {plr.Name} 执行了 {action} 操作于玩家 {key}。");
    }
    #endregion

    // ======================== 辅助方法 ============================

    #region 检查注册状态
    private bool CheckRegistered(TSPlayer plr)
    {
        var data = Cache.GetOrCreate(plr.Name);
        if (!data.IsRegistered)
        {
            plr.SendErrorMessage("你尚未注册积分系统！请使用 /注册 <密码> 进行注册。");
            return false;
        }
        return true;
    }
    #endregion

    #region 格式化秒数为可读时间
    private static string FormatTime(int totalSec)
    {
        if (totalSec < 60) return $"{totalSec}秒";
        int min = totalSec / 60;
        int sec = totalSec % 60;
        if (min < 60) return $"{min}分{sec}秒";
        int hour = min / 60;
        min %= 60;
        return $"{hour}小时{min}分";
    }
    #endregion
}