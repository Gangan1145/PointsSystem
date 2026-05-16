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
    public override Version Version => new(1, 1, 0);
    public override string Description => "签到 · 抽奖 · 转账 · 仓库 · 掷骰子 · 猜数字 · 抢劫 · 回收 一体化积分系统";
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
        RegisterCommands();
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

        // ---- 抽奖（物品存入仓库）----
        Commands.ChatCommands.Add(new Command("points.use", CmdLottery, "抽奖", "lottery")
        { HelpText = "消耗积分抽取随机物品，存入仓库。" });

        // ---- 仓库查看 ----
        Commands.ChatCommands.Add(new Command("points.use", CmdStorage, "仓库", "storage")
        { HelpText = "查看抽奖仓库中的物品。" });

        // ---- 领取物品 ----
        Commands.ChatCommands.Add(new Command("points.use", CmdClaim, "取物品", "claim")
        { HelpText = "从仓库中领取物品。用法: /取物品 <序号|all>" });

        // ---- 回收仓库物品 ----
        Commands.ChatCommands.Add(new Command("points.use", CmdRecycle, "回收", "recycle")
        { HelpText = "回收仓库中的物品换取积分。用法: /回收 <序号|all>" });

        // ---- 转账 ----
        Commands.ChatCommands.Add(new Command("points.use", CmdTransfer, "转账", "transfer", "pay")
        { HelpText = "向其他玩家转账积分。用法: /转账 <玩家名> <数量>" });

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
        if (_tick >= 60) { _sec++; _tick = 0; }
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

        if (data.LastSignInDate.HasValue && data.LastSignInDate.Value.Date == today)
        {
            plr.SendErrorMessage("你今天已经签到过了，明天再来吧！");
            return;
        }

        if (data.LastSignInDate.HasValue && data.LastSignInDate.Value.Date == today.AddDays(-1))
            data.ConsecutiveSignIns++;
        else
            data.ConsecutiveSignIns = 1;

        data.TotalSignIns++;
        data.LastSignInDate = DateTime.UtcNow;

        int extraDays = Math.Min(data.ConsecutiveSignIns - 1,
            Config.SignMaxConsecutiveBonus / Math.Max(1, Config.SignConsecutiveBonus));
        int bonus = extraDays * Config.SignConsecutiveBonus;
        int earned = Config.SignBasePoints + bonus;
        data.Points += earned;
        Cache.Save(CachePath);

        var sb = new StringBuilder();
        sb.AppendLine($"[{PluginName}] 签到成功！");
        sb.AppendLine($"  基础积分: +{Config.SignBasePoints}");
        if (bonus > 0) sb.AppendLine($"  连续签到奖励: +{bonus} (连续 {data.ConsecutiveSignIns} 天)");
        sb.AppendLine($"  本次获得: [c/FFD700:+{earned} 积分]");
        sb.AppendLine($"  当前积分: {data.Points}");
        sb.AppendLine($"  累计签到: {data.TotalSignIns} 次 / 连续签到: {data.ConsecutiveSignIns} 天");

        plr.SendMessage(Utils.TextGradient(sb.ToString(), plr),
            Utils.color.R, Utils.color.G, Utils.color.B);
    }
    #endregion

        #region 抽奖 /lottery（物品存入仓库）
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

        // 按权重抽选
        int totalWeight = Config.LotteryItems.Sum(i => i.Weight);
        int roll = Utils.Random.Next(totalWeight);
        int cumulative = 0;
        Configuration.LotteryEntry? won = null;
        foreach (var entry in Config.LotteryItems)
        {
            cumulative += entry.Weight;
            if (roll < cumulative) { won = entry; break; }
        }
        won ??= Config.LotteryItems[0];

        // 存入仓库
        var stored = new CacheData.StoredItem
        {
            Id = Cache.NextStorageId(data),
            ItemID = won.ItemID,
            Stack = won.Stack,
            Prefix = won.Prefix,
            ObtainedAt = DateTime.UtcNow
        };
        data.LotteryStorage.Add(stored);
        Cache.Save(CachePath);

        var itemName = Lang.GetItemNameValue(won.ItemID) ?? $"物品#{won.ItemID}";
        int recycleValue = CalcRecycleValue(won.ItemID, won.Stack);

        // ★ 修复：分别发送文本和带图标的行，避免颜色标签干扰 [i:...] 渲染
        plr.SendMessage(
            $"[c/FFD700:{PluginName}] 抽奖花费 {Config.LotteryCost} 积分，获得了：",
            Utils.color.R, Utils.color.G, Utils.color.B);
        plr.SendMessage(
            $"  {Utils.ItemIcon(won.ItemID, won.Stack)} [c/FFD700:{itemName}]{(won.Stack > 1 ? $" x{won.Stack}" : "")}  (序号 #{stored.Id})",
            Utils.color.R, Utils.color.G, Utils.color.B);
        plr.SendMessage(
            $"  回收价值: {recycleValue} 积分 | 剩余积分: {data.Points}",
            Utils.color.R, Utils.color.G, Utils.color.B);
    }
    #endregion

    #region 仓库 /storage
    private void CmdStorage(CommandArgs args)
    {
        var plr = args.Player;
        if (!CheckRegistered(plr)) return;

        var data = Cache.GetOrCreate(plr.Name);

        if (data.LotteryStorage.Count == 0)
        {
            // 标题仍可使用颜色，因为没有物品图标
            plr.SendMessage(
                $"[c/FFD700:{PluginName}] 你的抽奖仓库是空的。使用 /抽奖 获取物品吧！",
                Utils.color.R, Utils.color.G, Utils.color.B);
            return;
        }

        // 标题行
        plr.SendMessage(
            $"══════ [c/FFD700:{plr.Name} 的抽奖仓库] ══════",
            Utils.color.R, Utils.color.G, Utils.color.B);

        // ★ 逐行发送物品信息 — 不使用 TextGradient，确保 [i:...] 正常渲染
        foreach (var item in data.LotteryStorage.OrderBy(i => i.Id))
        {
            var itemName = Lang.GetItemNameValue(item.ItemID) ?? $"物品#{item.ItemID}";
            int recycleVal = CalcRecycleValue(item.ItemID, item.Stack);
            string prefixStr = item.Prefix > 0 ? $" [前缀:{item.Prefix}]" : "";
            string stackStr = item.Stack > 1 ? $" x{item.Stack}" : "";

            // 每件物品单独一行，不使用任何颜色标签包裹图标部分
            plr.SendMessage(
                $"  #{item.Id} {Utils.ItemIcon(item.ItemID, item.Stack)} {itemName}{stackStr}{prefixStr} | 回收: {recycleVal} 积分",
                Utils.color.R, Utils.color.G, Utils.color.B);
        }

        // 底部信息
        plr.SendMessage(
            $"══════════════════════════════",
            Utils.color.R, Utils.color.G, Utils.color.B);
        plr.SendMessage(
            $"共 {data.LotteryStorage.Count} 件 | 使用 /取物品 <序号|all> 领取 | /回收 <序号|all> 兑换积分",
            Utils.color.R, Utils.color.G, Utils.color.B);
    }
    #endregion

    #region 取物品 /claim
    private void CmdClaim(CommandArgs args)
    {
        var plr = args.Player;
        if (!CheckRegistered(plr)) return;

        var data = Cache.GetOrCreate(plr.Name);

        if (data.LotteryStorage.Count == 0)
        {
            plr.SendErrorMessage("你的抽奖仓库是空的。");
            return;
        }
        if (args.Parameters.Count < 1)
        {
            plr.SendErrorMessage("用法: /取物品 <序号|all>");
            return;
        }

        string param = args.Parameters[0].ToLower();

        if (param == "all")
        {
            // 领取全部
            int count = data.LotteryStorage.Count;
            foreach (var item in data.LotteryStorage)
                plr.GiveItem(item.ItemID, item.Stack, item.Prefix);

            data.LotteryStorage.Clear();
            Cache.Save(CachePath);

            plr.SendMessage(
                Utils.TextGradient($"[{PluginName}] 已领取全部 {count} 件物品到背包。"),
                Utils.color.R, Utils.color.G, Utils.color.B);
        }
        else if (int.TryParse(param, out int id))
        {
            var item = data.LotteryStorage.FirstOrDefault(i => i.Id == id);
            if (item == null)
            {
                plr.SendErrorMessage($"找不到序号 #{id} 的物品。使用 /仓库 查看你的物品列表。");
                return;
            }
            plr.GiveItem(item.ItemID, item.Stack, item.Prefix);
            data.LotteryStorage.Remove(item);
            Cache.Save(CachePath);

            var itemName = Lang.GetItemNameValue(item.ItemID) ?? $"物品#{item.ItemID}";
            plr.SendMessage(
                Utils.TextGradient(
                    $"[{PluginName}] 已领取 #{id} {Utils.ItemIcon(item.ItemID, item.Stack)} [c/FFD700:{itemName}]" +
                    $"{(item.Stack > 1 ? $" x{item.Stack}" : "")}。"),
                Utils.color.R, Utils.color.G, Utils.color.B);
        }
        else
        {
            plr.SendErrorMessage("无效参数，请输入序号数字或 \"all\"。");
        }
    }
    #endregion

    #region 回收 /recycle（仅限仓库物品）
    private void CmdRecycle(CommandArgs args)
    {
        var plr = args.Player;
        if (!CheckRegistered(plr)) return;

        var data = Cache.GetOrCreate(plr.Name);

        if (data.LotteryStorage.Count == 0)
        {
            plr.SendErrorMessage("你的抽奖仓库是空的，没有可回收的物品。");
            return;
        }
        if (args.Parameters.Count < 1)
        {
            plr.SendErrorMessage("用法: /回收 <序号|all>");
            return;
        }

        string param = args.Parameters[0].ToLower();

        if (param == "all")
        {
            int totalPoints = 0;
            int count = 0;
            foreach (var item in data.LotteryStorage)
            {
                int val = CalcRecycleValue(item.ItemID, item.Stack);
                if (val > 0)
                {
                    totalPoints += val;
                    count++;
                }
            }
            if (totalPoints <= 0)
            {
                plr.SendErrorMessage("仓库中没有可回收的物品（物品价值低于最低门槛）。");
                return;
            }

            data.Points += totalPoints;
            data.LotteryStorage.Clear();
            Cache.Save(CachePath);

            plr.SendMessage(
                Utils.TextGradient(
                    $"[{PluginName}] 已回收全部 {count} 件物品，获得 +{totalPoints} 积分。当前积分: {data.Points}"),
                Utils.color.R, Utils.color.G, Utils.color.B);
        }
        else if (int.TryParse(param, out int id))
        {
            var item = data.LotteryStorage.FirstOrDefault(i => i.Id == id);
            if (item == null)
            {
                plr.SendErrorMessage($"找不到序号 #{id} 的物品。使用 /仓库 查看你的物品列表。");
                return;
            }

            int val = CalcRecycleValue(item.ItemID, item.Stack);
            if (val <= 0)
            {
                plr.SendErrorMessage($"该物品回收价值为 0（低于最低门槛 {Config.RecycleMinValue} 铜币），无法回收。");
                return;
            }

            data.Points += val;
            data.LotteryStorage.Remove(item);
            Cache.Save(CachePath);

            var itemName = Lang.GetItemNameValue(item.ItemID) ?? $"物品#{item.ItemID}";
            plr.SendMessage(
                Utils.TextGradient(
                    $"[{PluginName}] 已回收 #{id} {Utils.ItemIcon(item.ItemID, item.Stack)} [c/FFD700:{itemName}]，" +
                    $"获得 +{val} 积分。当前积分: {data.Points}"),
                Utils.color.R, Utils.color.G, Utils.color.B);
        }
        else
        {
            plr.SendErrorMessage("无效参数，请输入序号数字或 \"all\"。");
        }
    }
    #endregion

    #region 转账 /transfer
    private void CmdTransfer(CommandArgs args)
    {
        var plr = args.Player;
        if (!CheckRegistered(plr)) return;

        if (args.Parameters.Count < 2)
        {
            plr.SendErrorMessage("用法: /转账 <玩家名> <数量>");
            return;
        }

        string targetName = args.Parameters[0];
        if (!int.TryParse(args.Parameters[1], out int amount) || amount <= 0)
        {
            plr.SendErrorMessage("请输入有效的正整数量。");
            return;
        }
        if (amount < Config.TransferMinPoints)
        {
            plr.SendErrorMessage($"单次转账最少 {Config.TransferMinPoints} 积分。");
            return;
        }

        var senderData = Cache.GetOrCreate(plr.Name);
        if (senderData.Points < amount)
        {
            plr.SendErrorMessage($"积分不足！你当前有 {senderData.Points} 积分，需要 {amount} 积分。");
            return;
        }

        // 手续费
        int fee = (int)(amount * Config.TransferFeeRate);
        int totalCost = amount + fee;

        if (senderData.Points < totalCost)
        {
            plr.SendErrorMessage($"积分不足（含手续费 {fee}）！需要 {totalCost} 积分，当前 {senderData.Points} 积分。");
            return;
        }

        // 大小写不敏感查找目标
        var key = Cache.Players.Keys.FirstOrDefault(
            k => k.Equals(targetName, StringComparison.OrdinalIgnoreCase));
        if (key == null)
        {
            plr.SendErrorMessage($"找不到玩家 \"{targetName}\" 的积分数据。对方可能尚未注册。");
            return;
        }
        if (key.Equals(plr.Name, StringComparison.OrdinalIgnoreCase))
        {
            plr.SendErrorMessage("你不能给自己转账！");
            return;
        }

        var targetData = Cache.GetOrCreate(key);
        if (!targetData.IsRegistered)
        {
            plr.SendErrorMessage($"玩家 {key} 尚未注册积分系统，无法接收转账。");
            return;
        }

        // 执行转账
        senderData.Points -= totalCost;
        targetData.Points += amount;
        Cache.Save(CachePath);

        // 通知发送方
        var sbSender = new StringBuilder();
        sbSender.AppendLine($"[{PluginName}] 转账成功！");
        sbSender.AppendLine($"  向 [c/FFD700:{key}] 转账: -{amount} 积分");
        if (fee > 0) sbSender.AppendLine($"  手续费: -{fee} 积分");
        sbSender.AppendLine($"  当前积分: {senderData.Points}");
        plr.SendMessage(Utils.TextGradient(sbSender.ToString(), plr),
            Utils.color.R, Utils.color.G, Utils.color.B);

        // 通知接收方（如果在线）
        var targetPlr = TShock.Players.FirstOrDefault(
            p => p != null && p.Active && p.Name != null
                 && p.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (targetPlr != null)
        {
            targetPlr.SendMessage(
                Utils.TextGradient(
                    $"[{PluginName}] [c/AAFFAA:{plr.Name}] 向你转账了 {amount} 积分！当前积分: {targetData.Points}", targetPlr),
                Utils.color.R, Utils.color.G, Utils.color.B);
        }

        TShock.Log.ConsoleInfo($"[{PluginName}] {plr.Name} → {key} 转账 {amount} 积分 (手续费 {fee})。");
    }
    #endregion

    #region 掷骰子 /dice
    private void CmdDice(CommandArgs args)
    {
        var plr = args.Player;
        if (!CheckRegistered(plr)) return;
        var data = Cache.GetOrCreate(plr.Name);

        if (data.LastDiceTime.HasValue)
        {
            var elapsed = (DateTime.UtcNow - data.LastDiceTime.Value).TotalSeconds;
            if (elapsed < Config.DiceCooldownSec)
            {
                plr.SendErrorMessage($"掷骰子冷却中，请等待 {FormatTime((int)(Config.DiceCooldownSec - elapsed))}。");
                return;
            }
        }
        if (data.Points < Config.DiceCost)
        {
            plr.SendErrorMessage($"积分不足！需要 {Config.DiceCost} 积分，当前 {data.Points}。");
            return;
        }

        data.LastDiceTime = DateTime.UtcNow;
        data.Points -= Config.DiceCost;

        bool win = Utils.Random.NextDouble() < Config.DiceWinProbability;
        int dice1 = Utils.Random.Next(1, 7);
        int dice2 = Utils.Random.Next(1, 7);
        string diceIcon = $"🎲 {dice1} + {dice2} = {dice1 + dice2}";

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
        if (data.LastGuessTime.HasValue)
        {
            var elapsed = (DateTime.UtcNow - data.LastGuessTime.Value).TotalSeconds;
            if (elapsed < Config.GuessCooldownSec)
            {
                plr.SendErrorMessage($"猜数字冷却中，请等待 {FormatTime((int)(Config.GuessCooldownSec - elapsed))}。");
                return;
            }
        }
        if (data.Points < Config.GuessCost)
        {
            plr.SendErrorMessage($"积分不足！需要 {Config.GuessCost} 积分，当前 {data.Points}。");
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
            msg = $"[{PluginName}] 你猜了 {guess}，答案是 [c/AAFFAA:{answer}] —— 猜中了！+{Config.GuessReward} 积分。当前积分: {data.Points}";
        }
        else
        {
            msg = $"[{PluginName}] 你猜了 {guess}，答案是 [c/FF8888:{answer}] —— 没猜中…… -{Config.GuessCost} 积分。当前积分: {data.Points}";
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
            plr.SendErrorMessage($"找到多个匹配玩家: {string.Join(", ", targets.Select(p => p.Name))}。");
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
        if (data.LastRobTime.HasValue)
        {
            var elapsed = (DateTime.UtcNow - data.LastRobTime.Value).TotalSeconds;
            if (elapsed < Config.RobCooldownSec)
            {
                plr.SendErrorMessage($"抢劫冷却中，请等待 {FormatTime((int)(Config.RobCooldownSec - elapsed))}。");
                return;
            }
        }

        data.LastRobTime = DateTime.UtcNow;
        int stealAmount = Utils.Random.Next(Config.RobMinPoints, Config.RobMaxPoints + 1);
        stealAmount = Math.Min(stealAmount, targetData.Points);

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
                Utils.TextGradient($"[{PluginName}] [c/AAFFAA:抢劫成功！]从 {target.Name} 处抢得 {stealAmount} 积分。当前积分: {data.Points}", plr),
                Utils.color.R, Utils.color.G, Utils.color.B);
            target.SendMessage(
                Utils.TextGradient($"[{PluginName}] [c/FF8888:{plr.Name} 抢走了你 {stealAmount} 积分！]当前积分: {targetData.Points}", target),
                Utils.color.R, Utils.color.G, Utils.color.B);
        }
        else
        {
            int penalty = Math.Max(1, Math.Min((int)(stealAmount * Config.RobFailurePenaltyRate), data.Points));
            data.Points -= penalty;
            targetData.Points += penalty;
            plr.SendMessage(
                Utils.TextGradient($"[{PluginName}] [c/FF8888:抢劫失败！]被反抢，扣除 {penalty} 积分。当前积分: {data.Points}", plr),
                Utils.color.R, Utils.color.G, Utils.color.B);
            target.SendMessage(
                Utils.TextGradient($"[{PluginName}] [c/AAFFAA:{plr.Name} 试图抢劫你但失败了！]+{penalty} 积分。当前积分: {targetData.Points}", target),
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
            plr.SendErrorMessage($"找到多个匹配玩家: {string.Join(", ", matches.Select(p => p.Name))}。");
            return;
        }

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

        bool online = TShock.Players.Any(p => p != null && p.Active
            && p.Name != null && p.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase));
        string status = online ? "[c/AAFFAA:在线]" : "[c/AAAAAA:离线]";

        var sb = new StringBuilder();
        sb.AppendLine($"══════ [c/FFD700:{targetName}] 的信息 ══════");
        sb.AppendLine($"  状态       : {status}");
        sb.AppendLine($"  积分       : [c/FFD700:{data.Points}]");
        sb.AppendLine($"  抽奖仓库   : {data.LotteryStorage.Count} 件物品");
        sb.AppendLine($"  累计签到   : {data.TotalSignIns} 次");
        sb.AppendLine($"  连续签到   : {data.ConsecutiveSignIns} 天");
        if (data.LastSignInDate.HasValue)
            sb.AppendLine($"  上次签到   : {data.LastSignInDate.Value:yyyy-MM-dd HH:mm}");
        if (online && targetPlr != null)
            sb.AppendLine($"  手持物品   : {Utils.ItemIcon(targetPlr.SelectedItem.type)} {Lang.GetItemNameValue(targetPlr.SelectedItem.type)}");
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
                { plr.SendErrorMessage("请提供要增加的积分数量。"); return; }
                data.Points += addAmt;
                plr.SendMessage(
                    Utils.TextGradient($"[{PluginName}] 已为 {key} 增加 {addAmt} 积分，当前: {data.Points}"),
                    Utils.color.R, Utils.color.G, Utils.color.B);
                break;

            case "set":
                if (args.Parameters.Count < 3 || !int.TryParse(args.Parameters[2], out int setAmt))
                { plr.SendErrorMessage("请提供要设置的积分数量。"); return; }
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
                data.LotteryStorage.Clear();
                plr.SendMessage(
                    Utils.TextGradient($"[{PluginName}] 已重置 {key} 的积分、签到和仓库数据。"),
                    Utils.color.R, Utils.color.G, Utils.color.B);
                break;

            default:
                plr.SendErrorMessage("未知操作。可用: add, set, reset");
                return;
        }

        Cache.Save(CachePath);
        TShock.Log.ConsoleInfo($"[{PluginName}] 管理员 {plr.Name} 执行 {action} → {key}。");
    }
    #endregion

    // ======================== 辅助方法 ============================

    #region 计算回收价值（基于物品基础价值 × 回收比例 × 堆叠数）
    private static int CalcRecycleValue(int itemID, int stack)
    {
        try
        {
            // 通过 SetDefaults 创建临时 Item 获取其基础价值
            Item tempItem = new Item();
            tempItem.SetDefaults(itemID);
            if (tempItem.value < Config.RecycleMinValue)
                return 0;
            return (int)(tempItem.value * Config.RecycleRate * stack);
        }
        catch
        {
            return 0;
        }
    }
    #endregion

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

    #region 格式化秒数
    private static string FormatTime(int totalSec)
    {
        if (totalSec < 60) return $"{totalSec}秒";
        int min = totalSec / 60, sec = totalSec % 60;
        if (min < 60) return $"{min}分{sec}秒";
        int hour = min / 60; min %= 60;
        return $"{hour}小时{min}分";
    }
    #endregion
}