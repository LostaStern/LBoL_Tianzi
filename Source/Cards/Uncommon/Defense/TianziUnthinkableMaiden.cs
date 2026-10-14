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
            // 升级后 0 费（混色也随之消失 ⇒ HybridCost 关键字自动不再附加）
            config.UpgradedCost = new ManaGroup() { };

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
        //  选牌（{Value2} 选 1）
        // ------------------------------------------------------------------
        public override Interaction Precondition()
        {
            List<Card> pool = this.RollPool();
            if (pool.Count == 0)
                return null;
            return new SelectCardInteraction(1, 1, pool, SelectedCardHandling.DoNothing);
        }

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
            return base.Battle
                .RollCardsWithoutManaLimit(
                    table,
                    base.Value2,
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

            SelectCardInteraction select = precondition as SelectCardInteraction;
            if (select != null && select.SelectedCards != null && select.SelectedCards.Count > 0)
            {
                Card chosen = select.SelectedCards[0];
                if (chosen != null)
                    yield return new AddCardsToDeckAction(new Card[] { chosen });
            }
            yield break;
        }
    }
}
