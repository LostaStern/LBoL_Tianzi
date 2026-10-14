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

    public sealed class TianziQuakeWorldDef : TianziCardTemplate
    {
        public override CardConfig MakeConfig()
        {
            CardConfig config = GetDefaultCardConfig();
            config.GunName = GunNameID.GetGunFromId(6162);
            config.GunNameBurst = GunNameID.GetGunFromId(6162);
            config.Colors = new List<ManaColor>() { ManaColor.Red };
            config.Cost = new ManaGroup() { Any = 1, Red = 1 };
            config.UpgradedCost = new ManaGroup() { Any = 2 };
            config.Rarity = Rarity.Uncommon;
            config.Type = CardType.Attack;
            config.TargetType = TargetType.SingleEnemy;
            config.Damage = 13;
            config.UpgradedDamage = 17;
            config.Mana = new ManaGroup() { Red = 1 };
            config.Illustrator = "かすおかすと";
            config.Index = CardIndexGenerator.GetUniqueIndex(config);
            config.Keywords = Keyword.Accuracy;
            config.UpgradedKeywords = Keyword.Accuracy;
            return config;
        }
    }


    [EntityLogic(typeof(TianziQuakeWorldDef))]
    public sealed class TianziQuakeWorld : TianziCard
    {
        protected override void OnEnterBattle(BattleController battle)
        {
            base.OnEnterBattle(battle);
            base.HandleBattleEvent<CardUsingEventArgs>(
                battle.CardUsed,
                new GameEventHandler<CardUsingEventArgs>(this.OnCardUsed));
            base.HandleBattleEvent<CardsEventArgs>(
                battle.CardsAddedToHand,
                new GameEventHandler<CardsEventArgs>(this.OnAddedToHand));
        }

        private void OnCardUsed(CardUsingEventArgs args)
        {
            if (args.Card == null || args.Card == this || args.Card.CardType != CardType.Attack)
                return;
            if (this.Zone != CardZone.Hand)
                return;
            this.DecreaseTurnCost(ManaGroup.Anys(1));
        }

        private void OnAddedToHand(CardsEventArgs args)
        {
            if (args.Cards == null || !args.Cards.Contains(this))
                return;
            int n = this.CountTurnPlayed(CardType.Attack);
            for (int i = 0; i < n; i++)
                this.DecreaseTurnCost(ManaGroup.Anys(1));
        }

        protected override IEnumerable<BattleAction> Actions(
            UnitSelector selector, ManaGroup consumingMana, Interaction precondition)
        {
            yield return base.AttackAction(selector);
            yield return new GainManaAction(new ManaGroup() { Red = 1 });
        }
    }
}
