namespace Shared.CommonData.Plt
{
    public static class ItemsConstants
    {
        public static class ItemDescriptionId
        {
            public const short Account = 49;
            public const short RewardedVideo = 260;
            public const short RewardedVideoSteppedContainerItem = 281;
            public const short CoopEvent = 377;

            public static class Currency
            {
                public const short Hard = 51;
                public const short HardBonus = 12;
                public const short HardReal = 50;
                public const short Soft = 13;
                public const short AccountExperience = 48;
                public const short Dust = 57;
                public const short Rubedo = 58;
                public const short TowerMissions = 106;
                public const short DogTag = 217;
                public const short HeroExperience = DogTag;
                public const short BlackMarket = 258;
                public const short AsyncPvpTicket = 261;
                public const short RubedoCommon = 263;
                public const short RubedoUncommon = 264;
                public const short RubedoRare = 265;
                public const short RubedoEpic = 266;
            }

            public static class Building
            {
                public const short Story = 14;
                public const short Hero = 16;
                public const short Weapon = 17;
                public const short Tasks = 15;
                public const short Daily = 25;
                public const short Containers = 52;
                public const short Core = 3;
                public const short MiningDust = 103;
                public const short MiningGold = 104;
                public const short Tower = 105;
                public const short AsyncPvpArena = 148;
                public const short Shop = 147;
                public const short Craft = 259;
                public const short GameEvents = 334;
            }

            public static class Weapon
            {
                public const short AssaultRifle = 2;
                public const short SniperRifle_1Star = 22;
                public const short SniperRifle_2Star = 6;
            }

            public static class Hero
            {
                public const short Jeff = 1;
                public const short Sniper = 4;
                public const short Atom = 41;
                public const short XieMei = 43;
                public const short Tnt = 107;
                public const short Android = 109;
                public const short Contra = 111;
                public const short Fox = 112;
                public const short Serpent = 149;
                public const short Succubus = 321;
                public const short Kid = 336;
            }

            public class HeroShard
            {
                public const short Jeff = 7;
                public const short Sniper = 8;
                public const short Atom = 37;
                //public const short XieMei = 43;
            }

            public static class Offers
            {
                public const short AnyOffer = 129;
            }
        }

        public static class HeroClass
        {
            public const short Assault = 1;
            public const short Sniper = 2;
            public const short Heavy = 3;
            public const short Assassin = 4;
        }

        public static class EquipmentTypeId
        {
            public const short Weapon = 1;
            public const short Shoes = 2;
            public const short Hat = 3;
            public const short Bag = 4;
            public const short Shirt = 5;
            public const short Gloves = 6;
        }

        public static class OfferTypeId
        {
            public const short FoundersPack = 92;
        }

        public static class RpgParamDescriptionId
        {
            public const short Power = 1;
        }

        public static class Tooltips
        {
            public static class Timer
            {
                public const short Container = 2;
                public const short DailyMission = 15;
                public const short DailyMissionReplace = 3;
                public const short RefreshTasks = 10;
                public const short CompleteTask = 11;
            }

            public static class Power
            {
                public const short Sum = 1;
                public const short Hero = 4;
                public const short Weapon = 5;
                public const short Recommended = 6;
            }

            public static class Rarity
            {
                public const short Weapon = 8;
                public const short Hero = 16;
            }

            public static class AsyncPvp
            {
                public const short Reward = 12;
                public const short Place = 13;
                public const short Score = 14;
            }

            public const short HeroStars = 9;
            public const short LevelWeapon = 7;
        }

        public static class Prices
        {
            public const short PlayerRename = 1;
            public const short FreeHard = 2;
        }

        public static class Store
        {
            public static class Item
            {
                public const short FreeBox = 14;
            }

            public static class Shelves
            {
                public const short CurrencyExchange = 3;
            }
        }

        public static class ContainerPack
        {
            public const short FreeBox = 0;
            public const short PremiumBox = 1;
            public const short MagicFreeBox = 2;
            public const short RewardedVideoBoxVisual = 3;
            public const short MediumBox = 4;
        }

        public static class HeroTaskDifficultyTypeId
        {
            public const short Ads = 3;
        }

        public static class EquipmentRarity
        {
            public const short Common = 1;
            public const short Uncommon = 2;
            public const short Rare = 3;
            public const short Epic = 4;
            public const short Legendary = 5;
            public const short Unique = 6;
        }
    }
}