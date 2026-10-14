using System.Collections.Generic;
using LBoL.Base;
using LBoL.ConfigData;
using LBoL.Core;
using LBoL.Core.Battle;
using LBoL.Core.Battle.BattleActions;
using LBoL.Core.Battle.Interactions;
using LBoL.Core.Cards;
using LBoL.Core.StatusEffects;
using LBoL.Core.Units;
using LBoL.EntityLib.StatusEffects.Basic;
using LBoLEntitySideloader.Attributes;
using TianziMod.Cards.Template;
using TianziMod.Keywords;
using TianziMod.StatusEffects;

namespace TianziMod.Cards
{

    // ------------------------------------------------------------------ 晴空万里
    public sealed class TianziClearSkyDef : TianziCardTemplate
    {
        public override CardConfig MakeConfig()
        {
            CardConfig config = GetDefaultCardConfig();

            config.Colors = new List<ManaColor>() { ManaColor.White };
            config.Cost = new ManaGroup() { White = 1 };
            config.UpgradedCost = new ManaGroup() { Any = 1 };
            config.Rarity = Rarity.Uncommon;

            config.Type = CardType.Defense;
            config.TargetType = TargetType.Self;

            // Defense 牌必须在 config 里带 Block 或 Shield，否则 Card.Verify() 会
            // Debug.LogError("<Id> must set block or shield in config")。
            // 这张卡是「每放逐一张牌获得护盾」，把【单张护盾值】直接放在 Shield 上，
            // 卡面 {Shield} 显示的数值就和实际每张给的护盾一致。
            config.Shield = 7;
            config.UpgradedShield = 8;
            config.Value1 = 1; // 最多放逐张数

            config.Illustrator = "鱼鱼鱼鱼花";
            config.RelativeKeyword = Keyword.Shield | Keyword.Exile;
            config.UpgradedRelativeKeyword = Keyword.Shield | Keyword.Exile;
            config.Index = CardIndexGenerator.GetUniqueIndex(config);
            return config;
        }
    }


    /// <summary>
    /// 晴空万里：从手牌、抽牌堆和弃牌堆中，选择至多 {Value2} 张牌放逐。
    /// 每放逐一张牌，获得 {Value1} 点护盾。（放逐 / 回响）
    /// </summary>
    [EntityLogic(typeof(TianziClearSkyDef))]
    public sealed class TianziClearSky : TianziCard
    {
        // 混合 zone 的选牌池官方写法（见 Cirno 的 FreezeToIce）：
        //  · 池子在 Precondition() 里构建，而不是在 Actions() 里另开一个 InteractionAction；
        //  · 必须排除 this；
        //  · 抽牌堆要用 DrawZoneToShow（UI 能显示的那一份）；
        //  · 多张牌一起放逐用 ExileManyCardAction，而不是在 foreach 里逐张 ExileCardAction
        //    —— 后者会在 play-area / 手牌控件簿记上留下脏状态，进而让之后每次出牌都抛 NRE。
        public override Interaction Precondition()
        {
            List<Card> pool = new List<Card>();
            foreach (Card c in base.Battle.HandZone)
                if (c != null && c != this) pool.Add(c);
            foreach (Card c in base.Battle.DrawZoneToShow)
                if (c != null && c != this) pool.Add(c);
            foreach (Card c in base.Battle.DiscardZone)
                if (c != null) pool.Add(c);
            if (pool.Count == 0)
                return null;
            return new SelectCardInteraction(0, base.Value1, pool, SelectedCardHandling.DoNothing);
        }

        protected override IEnumerable<BattleAction> Actions(
            UnitSelector selector,
            ManaGroup consumingMana,
            Interaction precondition
        )
        {
            SelectCardInteraction interaction = precondition as SelectCardInteraction;
            if (interaction == null || interaction.SelectedCards.Count == 0)
                yield break;

            List<Card> picks = new List<Card>(interaction.SelectedCards);
            yield return new ExileManyCardAction(picks);

            // 每放逐一张牌「单独结算一次」护盾，而不是把总量一次给完 ——
            // 这样「灵力」（Spirit）和「神恩」（Grace）会按张数各加上它们的层数。
            // ⚠ 必须用 BlockShieldType.Normal：Spirit / Grace 的 BlockShieldGaining
            //   守卫是 `if (args.Type != BlockShieldType.Direct)`，写 Direct 会被整段跳过。
            int perCard = base.ConfigShield;
            if (perCard > 0)
            {
                for (int i = 0; i < picks.Count; i++)
                {
                    yield return new CastBlockShieldAction(
                        base.Battle.Player,
                        base.Battle.Player,
                        0,
                        perCard,
                        BlockShieldType.Normal,
                        false
                    );
                }
            }
            yield break;
        }
    }
}
