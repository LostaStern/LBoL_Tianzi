using System.Collections.Generic;
using System.Linq;
using LBoL.Base;
using LBoL.ConfigData;
using LBoL.Core;
using LBoL.Core.Battle;
using LBoL.Core.Battle.BattleActions;
using LBoL.Core.Battle.Interactions;
using LBoL.Core.Cards;
using LBoL.Core.Randoms;
using LBoLEntitySideloader.Attributes;
using TianziMod.Cards.Template;
using TianziMod.StatusEffects;

namespace TianziMod.Cards
{
    public sealed class TianziUnthinkableMaidenDef : TianziCardTemplate
    {
        /// <summary>
        /// 「一次性」= <c>Keyword.Disposable</c>，**1.8.1 新增**（值 0x4000000000000000）。
        ///
        /// 本工程编译引用的是 1.8.0 的 <c>LBoL.Base.dll</c>，那里还没有这个枚举值，
        /// 所以只能用字面量。这样做是安全的：flags 枚举是 <c>ulong</c> 常量，编译器会把它
        /// **内联成常数**，运行时不会去目标程序集里查这个名字 —— 在 1.8.1 上正常生效，
        /// 在 1.8.0 上只是一个未定义的位，不产生任何副作用。
        ///
        /// （引擎侧「一次性」的真正行为在 1.8.1 的 <c>DisposableCard</c> 里，
        /// 不在这个关键字本身 —— 关键字只影响 <c>CanBeDuplicated</c>。
        /// 本卡自行复刻了那两个 override，见下方 <c>AfterUseAction</c>。）
        /// </summary>
        public const Keyword DisposableKeyword = (Keyword)0x4000000000000000UL;

        public override CardConfig MakeConfig()
        {
            CardConfig config = GetDefaultCardConfig();

            // 红白混色
            config.Colors = new List<ManaColor>() { ManaColor.Red, ManaColor.White };
            // 3 费红白混色。引擎会在 Cost.Hybrid > 0 时自动补上 Keyword.HybridCost（Card.cs），
            // 所以这里不用手写那个关键字。
            config.Cost = ManaGroup.Hybrids(3, ManaColor.Red, ManaColor.White);
            // 升级后 2 费红白混合。
            // Card.cs 的 HybridCost 判定会同时看 Cost 和 UpgradedCost：
            //   if (IsUpgraded) { if (UpgradedCost.Hybrid > 0) keyword |= Keyword.HybridCost; }
            //   else if (Cost.Hybrid > 0) keyword |= Keyword.HybridCost;
            // ⇒ 升级前后都是混色，关键字自动生效，不用手写。
            config.UpgradedCost = ManaGroup.Hybrids(2, ManaColor.Red, ManaColor.White);

            // 中立牌。
            // TianziCardTemplate 的默认 config 会把 Owner 设成 mod 唯一 ID（= 天子专属），
            // 这里必须显式置 null。
            // 引擎侧依据（LBoL.Core/Cards/Card.cs 的 IsPlayerCard）：
            //   Owner == null  ⇒  IsPlayerCard = false，且在 CardWeightTable.WeightFor 里归入
            //   OwnerWeightTable.Neutral 档 —— 也就是"不属于任何角色"，任何角色都能刷到它。
            config.Owner = null;

            config.Rarity = Rarity.Uncommon;
            config.Type = CardType.Defense;
            config.TargetType = TargetType.Self;

            config.Shield = 15;
            config.UpgradedShield = 15;

            config.Value1 = 3;   // 天气持续回合
            config.UpgradedValue1 = 3;
            config.Value2 = 20;  // 从 N 张里选 1 张
            config.UpgradedValue2 = 20;

            config.Keywords = DisposableKeyword;
            config.UpgradedKeywords = DisposableKeyword;

            config.RelativeKeyword = Keyword.Shield;
            config.UpgradedRelativeKeyword = Keyword.Shield;
            config.RelativeEffects = TianziWeather.RelativeIds(includeForecast: true);
            config.UpgradedRelativeEffects = config.RelativeEffects;

            config.Illustrator = "カンパ";
            config.Index = CardIndexGenerator.GetUniqueIndex(config);
            return config;
        }
    }


    /// <summary>
    /// 非想非非想天之女：获得 15 点护盾；下回合释放一种天气，持续 {Value1} 回合；
    /// 从天子自己的卡池里随机取 {Value2} 张普通/罕见牌，选一张加入牌库。一次性。
    /// </summary>
    [EntityLogic(typeof(TianziUnthinkableMaidenDef))]
    public sealed class TianziUnthinkableMaiden : TianziCard
    {
        // ------------------------------------------------------------------
        //  一次性：打出后同时离开战斗与牌库
        //  复刻 1.8.1 LBoL.EntityLib 的 DisposableCard.AfterUseAction /
        //  AfterFollowPlayAction（这两个虚方法 1.8.0 就有，所以能直接 override）。
        // ------------------------------------------------------------------
        public override IEnumerable<BattleAction> AfterUseAction()
        {
            Card deckCard = base.GameRun.GetDeckCardByInstanceId(base.InstanceId);
            if (deckCard != null)
                base.GameRun.RemoveDeckCard(deckCard, triggerVisual: false);
            yield return new RemoveCardAction(this);
        }

        public override IEnumerable<BattleAction> AfterFollowPlayAction()
        {
            Card deckCard = base.GameRun.GetDeckCardByInstanceId(base.InstanceId);
            if (deckCard != null)
                base.GameRun.RemoveDeckCard(deckCard, triggerVisual: false);
            yield return new RemoveCardAction(this);
        }

        // ------------------------------------------------------------------
        //  选牌（{Value2} 选 1）—— 必须放在 Actions() 里做，**不能**放 Precondition()
        //
        //  原因（读引擎源码确认，不是猜的）：
        //    · LBoL.Core/Battle/BattleActions/UseCardAction.cs
        //        从手牌打出时会硬编码
        //        this.React(new InteractionAction(this._precondition, true), null, null);
        //    · LBoL.Core/Battle/BattleActions/InteractionAction.cs
        //        构造函数会 **覆盖** 掉卡里自己设的值：
        //        this._interaction.CanCancel = canCancel;
        //  ⇒ 走 Precondition() 弹出的面板永远带「取消」，卡里怎么设都没用。
        //    取消发生在扣费之前 ⇒ 牌回手、法力不退不扣，再打一次就重新摇 20 张（刷新漏洞）。
        //
        //  官方卡的统一做法（Cirno.CallFriends / Opt / ChooseFriend / IceWing…）：
        //    在 Actions() 里自建 interaction，然后
        //        yield return new InteractionAction(interaction, false);
        //    UI 侧 SelectCardPanel.ViewSelectCard 用 payload.CanCancel 控制取消按钮显隐，
        //    传 false 时取消按钮直接不显示。
        //
        //  所以这里必须返回 null，否则框架会在 Precondition 阶段再弹一次可取消的面板。
        // ------------------------------------------------------------------
        public override Interaction Precondition()
        {
            return null;
        }

        /// <summary>
        /// 摇出候选池：只取「法力适配」的天子牌。
        ///
        /// 法力适配 = 官方「奖励卡牌池」的规则 1 + 规则 3
        /// （出处：<c>zh-Hans/Common.yaml</c> 的 <c>Explain.RollCard</c>）：
        ///   · 规则 1：基础法术力的全额足够打出这张牌；
        ///   · 规则 3：基础法术力的颜色里，必须包含这张牌的每一种颜色（Any 费用不参与颜色判定）。
        /// 引擎里这两条就是 <c>GameRunController.CreateValidCardsPool</c> 的
        /// <c>manaLimit</c> + <c>colorLimit</c> 两个开关，所以这里不自己重写，
        /// 直接走 <c>GameRun.RollCards(manaLimit: BaseMana, colorLimit: true)</c>，
        /// 保证和游戏本体的取牌规则永远一致。
        ///
        /// ★ 彩色（= <c>ManaColor.Philosophy</c>，贤者之石给的那种）走**官方语义**：
        ///   规则 3 原文写「彩色法力不会被视作其他颜色」⇒ 彩色只帮付费用
        ///   （<c>ManaGroup.CanAfford</c> 会把 Philosophy 拿去补缺口），
        ///   但**不满足颜色要求**，所以 colorLimit 不受它影响，恒定生效。
        /// </summary>
        private List<Card> RollPool()
        {
            CardWeightTable table = new CardWeightTable(
                new RarityWeightTable(1f, 1f, 0f, 0f), // 只要 普通 / 罕见
                // 本卡已中立化，打出它的角色未必是天子 ⇒ 不能用 OnlyPlayer，
                // 那取的是"当前角色"的卡池。用 AllOnes 放开 owner 档位，
                // 再靠下面的 filter 精确锁死天子的牌（见 CardWeightTable.WeightFor 的分类逻辑）。
                OwnerWeightTable.AllOnes,
                CardTypeWeightTable.CanBeLoot,          // 排除状态/厄运等不可掉落牌
                false);

            // 玩家基础法力。取 GameRun.BaseMana（展品加成已计入，比如贤者之石会加彩色），
            // 和 GameRun.RollCards 内部用的是同一个值，保证判定一致。
            // （BattleController 上只有转发的 BaseTurnMana，用 GameRun 的直接来源更清楚。）
            ManaGroup baseMana = base.GameRun.BaseMana;

            return base.GameRun
                .RollCards(
                    base.GameRun.BattleCardRng,
                    table,
                    base.Value2,
                    baseMana,        // 规则 1：基础法力全额得够打出它
                    true,            // 规则 3：卡牌的每种颜色都要在基础法力里（彩色不算数）
                    false,           // applyFactors
                    true,            // battleRolling
                    false,           // ensureCount
                    cfg => cfg.Owner == BepinexPlugin.modUniqueID && cfg.Id != base.Id)
                .ToList();
        }

        protected override IEnumerable<BattleAction> Actions(
            UnitSelector selector,
            ManaGroup consumingMana,
            Interaction precondition
        )
        {
            yield return base.DefenseAction(true);

            // 下回合释放一种天气，持续 {Value1} 回合
            yield return BuffAction<TianziWeatherForecastSe>(0, base.Value1, 0, 0, 0.2f);

            // 选牌：自己 yield InteractionAction 并传 canCancel: false ⇒ 面板无「取消」按钮。
            // 这一步已经过了扣费阶段，所以也不存在"取消退费再刷新"的路径。
            List<Card> pool = this.RollPool();
            if (pool.Count > 0)
            {
                SelectCardInteraction select = new SelectCardInteraction(
                    1, 1, pool, SelectedCardHandling.DoNothing)
                {
                    // 官方卡的硬性要求：自己 new 出来的 interaction 必须手动挂 Source。
                    // 走 Precondition() 那条路时是框架替我们设的，这里没人设。
                    Source = this,
                };
                yield return new InteractionAction(select, false);

                if (select.SelectedCards != null && select.SelectedCards.Count > 0)
                {
                    Card chosen = select.SelectedCards[0];
                    if (chosen != null)
                        yield return new AddCardsToDeckAction(new Card[] { chosen });
                }
            }
            yield break;
        }
    }
}
