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
using TianziMod.StatusEffects;

namespace TianziMod.Cards
{

    // ------------------------------------------------------------------ 天界之镜
    public sealed class TianziHeavenMirrorDef : TianziCardTemplate
    {
        public override CardConfig MakeConfig()
        {
            CardConfig config = GetDefaultCardConfig();

            config.Colors = new List<ManaColor>() { ManaColor.White };
            config.Cost = new ManaGroup() { White = 2 };
            config.UpgradedCost = new ManaGroup() { White = 2 };
            config.Rarity = Rarity.Rare;

            config.Type = CardType.Skill;
            config.TargetType = TargetType.Self;

            config.Keywords = Keyword.Exile;
            config.UpgradedKeywords = Keyword.Exile | Keyword.Echo;

            config.Illustrator = "亜音";
            config.UpgradedRelativeKeyword = Keyword.Exile;
            config.Index = CardIndexGenerator.GetUniqueIndex(config);
            return config;
        }
    }


    /// <summary>
    /// 天界之镜：将本回合上一张打出的牌的一张复制置入手中，该复制费用为 0 且放逐。
    /// 若本回合尚未打出过其他牌，则抽 {Value1} 张牌。（放逐）
    /// </summary>
    [EntityLogic(typeof(TianziHeavenMirrorDef))]
    public sealed class TianziHeavenMirror : TianziCard
    {
        protected override IEnumerable<BattleAction> Actions(
            UnitSelector selector,
            ManaGroup consumingMana,
            Interaction precondition
        )
        {
            Card previous = this.PreviousPlayedCard;
            if (previous == null)
                yield break;

            Card copy = previous.CloneBattleCard();
            copy.SetTurnCost(ManaGroup.Empty);
            copy.IsExile = true;
            yield return new AddCardsToHandAction(new Card[] { copy }, AddCardsType.Normal, false);
            yield break;
        }
    }
}
