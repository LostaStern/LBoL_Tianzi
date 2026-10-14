using System.Collections.Generic;
using System.Linq;
using LBoL.Base;
using LBoL.ConfigData;
using LBoL.Core;
using LBoL.Core.Battle;
using LBoL.Core.Battle.BattleActions;
using LBoL.Core.Cards;
using LBoL.Core.StatusEffects;
using LBoL.Core.Units;
using LBoLEntitySideloader.Attributes;
using TianziMod.Cards.Template;
using TianziMod.GunName;
using TianziMod.Keywords;
using TianziMod.StatusEffects;

namespace TianziMod.Cards
{
    public sealed class TianziBlazingSwordDef : TianziCardTemplate
    {
        public override CardConfig MakeConfig()
        {
            CardConfig config = GetDefaultCardConfig();
            config.GunName = GunNameID.GetGunFromId(510);
            config.GunNameBurst = GunNameID.GetGunFromId(510);
            config.Colors = new List<ManaColor>() { ManaColor.Red };
            // X 费的最低需求不能含 Any：BattleManaPanel 会对 XCostRequiredMana 调 CanAfford，
            // 而 CanAfford 禁止 receiver 带 Any（升级「任意1」会直接报错）。
            config.Cost = new ManaGroup() { Red = 1 };
            config.UpgradedCost = new ManaGroup() { Red = 1 };
            config.IsXCost = true;
            config.Rarity = Rarity.Uncommon;
            config.Type = CardType.Attack;
            config.TargetType = TargetType.SingleEnemy;
            config.Damage = 5;
            config.UpgradedDamage = 6;
            config.UpgradedKeywords = Keyword.Accuracy;
            config.UpgradedRelativeEffects = config.RelativeEffects;
            config.Illustrator = "竜崎いち";
            config.Index = CardIndexGenerator.GetUniqueIndex(config);
            return config;
        }
    }


    [EntityLogic(typeof(TianziBlazingSwordDef))]
    public sealed class TianziBlazingSword : TianziCard
    {
        protected override IEnumerable<BattleAction> Actions(
            UnitSelector selector, ManaGroup consumingMana, Interaction precondition)
        {
            // 额外支付的费用点数（不计颜色）：超出 X 费最低需求的每一费 +1 段
            int paid = consumingMana.Amount;
            int required = base.XCostRequiredMana.Amount;
            int extra = paid - required;
            if (extra < 0)
                extra = 0;
            int hits = 1 + extra;
            base.CardGuns = new Guns(base.GunName, hits, true);
            foreach (GunPair gunPair in base.CardGuns.GunPairs)
                yield return base.AttackAction(selector, gunPair);
            
        }
    }
}
