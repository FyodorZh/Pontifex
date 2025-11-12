namespace MetaGame
{
    public enum MetaCommand : byte
    {
        Undefined = 0,

        Battle,

        /// <summary>
        /// Действия над параметрами игрока
        /// </summary>
        Player,

        /// <summary>
        /// Действия над рунами и наборами рун героя
        /// </summary>
        Runes,

        /// <summary>
        /// Команды, присылаемые серверами при обновлении контекста игрока
        /// </summary>
        PlayerContext,

        /// <summary>
        /// Команды для управления квестами
        /// </summary>
        Quests,

        /// <summary>
        /// Команды управления инвентарём и экипировкой
        /// </summary>
        Inventory,

        Common,
        Cheat,
        PromoCodes,

        /// <summary>
        /// Команды на совершение покупок
        /// </summary>
        Purchases,

        /// <summary>
        /// Команды для просмотра и получения регулярных наград
        /// </summary>
        RewardQueue,

        Offer,

        Rewards,

        Profile,
        Sparks,

        /// <summary>
        /// Обработчик логина игрока
        /// </summary>
        EnterPlayer,

        /// <summary>
        /// Рамки аватаров
        /// </summary>
        AvatarFrames,

        /// <summary>
        /// Лиги
        /// </summary>
        League,

        /// <summary>
        /// Почта
        /// </summary>
        Mail,

        /// <summary>
        /// Боевые итема
        /// </summary>
        BattleItems,

        /// <summary>
        /// Карма
        /// </summary>
        Karma,

        /// <summary>
        /// Команды платформера
        /// </summary>
        Plt,

        max
    }

    public enum BattleSubCommand : byte
    {
        Undefined = 0,

        JoinBattle,
        JoinGroupBattle,
        SetAnyGeoNode,
        LeaveBattle,
        MatchTeams,
        AcceptMatch,
        AcceptStateFail,
        GetBattleState,
        SelectHero,
        AcceptHero,
        RejectHero,
        SelectRunes,
        SelectBattleItemsBuild,

        SelectSkin,
        SelectSpark,
        GetBattleNode,
        CheckBattleResult,
        SaveBattleResult,
        CompletePveMission,
        CompleteTutorialMission,
        NotifyAsyncBattleStarted,
        NotifyAsyncBattleBreak,
        MatcheMakerKeepAlive,

        max
    }

    public enum CommonSubCommand : byte
    {
        Undefined = 0,

        MatcheMakerStatistics,
        ServerTime,
        GetGeoNodes,
        PlayerBaned,

        /// <summary>
        /// Sends push token and user id to meta server
        /// </summary>
        PushInfo,
        NewInboxMessage,
        StatisticId,
        ClientInfo,
        HeroRotation,
        OperatorMessage,

        max
    }

    public enum PlayerSubCommand : byte
    {
        Undefined = 0,

        FinishRegistration,

        /// <summary>
        /// Переименование игрока
        /// </summary>
        Rename,

        /// <summary>
        /// Предложить дружбу
        /// </summary>
        OfferFriendship,

        /// <summary>
        /// Подтвердить запрос на дружбу
        /// </summary>
        ConfirmFriendship,

        /// <summary>
        /// Отклонить запрос на дружбу
        /// </summary>
        RejectFriendship,

        /// <summary>
        /// Прекратить дружбу
        /// </summary>
        BreakFriendships,

        /// <summary>
        /// Получить часть списка друзей
        /// </summary>
        GetFriendListPart,

        /// <summary>
        /// Получить данные о количестве друзей онлайн и т.п.
        /// </summary>
        GetFriendStatistic,

        /// <summary>
        /// Найти игрока по имени
        /// </summary>
        FindPlayerByName,

        /// <summary>
        /// Создать группу для пвп
        /// </summary>
        FriendGroupCreate,

        /// <summary>
        /// Распустить группу для пвп
        /// </summary>
        FriendGroupDissolve,

        /// <summary>
        /// Приглашение игрока в группу
        /// </summary>
        FriendGroupInvite,

        /// <summary>
        /// Исключить игрока из группы
        /// </summary>
        FriendGroupDrop,

        /// <summary>
        /// Покинуть группу
        /// </summary>
        FriendGroupLeave,

        /// <summary>
        /// Получить информацию о группе
        /// </summary>
        FriendGroupInfo,

        /// <summary>
        /// Подтверждение запроса на вступление в группу
        /// </summary>
        FriendGroupConfirmInvite,

        /// <summary>
        /// Отказ от вступления в группу
        /// </summary>
        FriendGroupRejectInvite,

        /// <summary>
        /// запрос данных по recent played
        /// </summary>
        GetRecentPlayed,

        /// <summary>
        /// Запрос на получение данных по игроку
        /// </summary>
        GetPlayerInfo,

        /// <summary>
        /// Запрос на получение онлайн статусов игроков
        /// </summary>
        GetOnlineStatus,

        /// <summary>
        /// Регистрация(привязка) Facebook аккаунта
        /// </summary>
        SetFacebookAccount,

        /// <summary>
        /// Выбор скина для героя.
        /// </summary>
        ChangeHeroSkin,

        /// <summary>
        /// Сохранение дополнительных данных клиента, для последующего использования в onesignal
        /// </summary>
        SetExtraData,

        /// <summary>
        /// Получить данные игрока для последующего мержинга аккаунтов
        /// </summary>
        GetPlayerDataForMerge,

        /// <summary>
        /// Уведомление с сервера о результате обработка покупки во внешнем магазине
        /// </summary>
        InAppPurchaseProcessing,

        /// <summary>
        /// Команда с клиента на сбор начисленного добра
        /// </summary>
        CollectAcquisitions,

        /// <summary>
        /// Команда с клиента забрать награду за прохождение всех миссий акта на 3 звезды
        /// </summary>
        CollectPveActReward,

        /// <summary>
        /// Команда с клиента на проверки просроченных расходников
        /// </summary>
        UpdateSubscriptions,

        /// <summary>
        /// Установка предпочитаемой гео-ноды для боя
        /// </summary>
        SetPreferableGeoNode,

        /// <summary>
        /// Сбор награды текущей завершенной стадии указанного достижения
        /// </summary>
        CollectAchievementReward,

        SetAvatarActive,

        max
    }

    public enum RunesSubCommand : byte
    {
        Undefined = 0,

        /// <summary>
        /// Активация руны для героя. После активации руна может быть использована в одном из наборов рун.
        /// Альт: Покупка руны
        /// </summary>
        //ActivateRune,
        /// <summary>
        /// Добавление нового набора рун герою.
        /// </summary>
        //AddRuneSet,
        /// <summary>
        /// Обновление существующего набора рун.
        /// </summary>
        UpdateRuneSet,

        /// <summary>
        /// Запрос всех рун и рунсетов героя.
        /// </summary>
        GetHeroRunes,

        max
    }

    public enum PlayerContextSubCommand : byte
    {
        Undefined = 0,

        /// <summary>
        /// Изменения в общих свойствах игрока.
        /// </summary>
        UpdateCommonData,

        /// <summary>
        /// Обновление списка героев и параметров конкретных героев.
        /// </summary>
        UpdateHeroes,
        //RemoveHeroes,

        /// <summary>
        /// Обновление списка наборов рун и параметров конкретных наборов, включая список рун.
        /// </summary>
        UpdateRuneSets,
        //RemoveRuneSets,

        /// <summary>
        /// Стартовые данные.
        /// </summary>
        StartupData,

        /// <summary>
        /// Данные о рунах и рунсетах героя.
        /// </summary>
        HeroRunesData,

        /// <summary>
        /// Данные о квестах игрока
        /// </summary>
        UpdateQuests,

        /// <summary>
        /// Данные о пройденных миссиях
        /// </summary>
        UpdatePveMissions,

        /// <summary>
        /// Данные об инвентаре игрока
        /// </summary>
        UpdateInventory,

        /// <summary>
        /// Данные об индивидуальном итеме из инвентаря игрока
        /// </summary>
        UpdateInventoryItem,

        /// <summary>
        /// Данные о достижениях игрока
        /// </summary>
        UpdateAchievements,

        /// <summary>
        /// Данные об спарках игрока
        /// </summary>
        UpdateSparks,

        /// <summary>
        /// Данные о пройденных туториальных миссиях
        /// </summary>
        UpdateTutorialMissions,

        /// <summary>
        /// Данные о состоянии регулярных наград
        /// </summary>
        UpdateRewardQueues,

        /// <summary>
        /// Данные о пройденных актах
        /// </summary>
        UpdatePveActs,

        /// <summary>
        /// Данные об активных расходниках игрока
        /// </summary>
        UpdateSubscriptions,

        UpdateRewards,

        /// <summary>
        /// Обновления боевой статистики игрока
        /// </summary>
        UpdateRates,

        UpdatePlayerAvatars,
        UpdatePlayerAvatarFrames,
        UpdateSelectedRuneset,

        /// <summary>
        /// Данные об активных расходниках игрока
        /// </summary>
        UpdateConsumables,

        UpdateSparkResearches,

        /// <summary>
        /// Данные об активных временных героях
        /// </summary>
        UpdateTemporaryHeroes,

        UpdateSkins,

        /// <summary>
        /// Данные об активных временных героях
        /// </summary>
        UpdateTemporarySkins,

        /// <summary>
        /// Данные об озменении общих данных
        /// </summary>
        UpdateSharedData,

        /// <summary>
        /// Данные об изменении статы в лиге
        /// </summary>
        UpdateLeagueStats,

        /// <summary>
        /// Обновления боевых итемов
        /// </summary>
        UpdateBattleItems,

        /// <summary>
        /// Обновился оффер
        /// </summary>
        UpdateOffer,
        
        PltUpdateItem,
        PltDeleteItem,
        PltUpdatePlayerData,
        InAppPurchase,
        AutoLevelUp,
        StoreItemAutoPurchase,

        max
    }

    public enum InventorySubCommand : byte
    {
        Undefined = 0,

        /// <summary>
        /// Утилизация компонента
        /// </summary>
        InventoryItemUtilize,

        /// <summary>
        /// Открытие предмета-контейнера
        /// </summary>
        InventoryItemOpen,

        /// <summary>
        /// Использование предмета на героя
        /// </summary>
        InventoryItemUseForHero,

        max
    }

    public enum QuestsSubCommand : byte
    {
        Undefined = 0,

        GetSlotsInfo,
        CheckSlots,
        CheckDailyChest,

        max
    }

    public enum PromoCodesSubCommand : byte
    {
        Undefined = 0,

        Activate,

        max
    }

    public enum CheatSubCommand : byte
    {
        Undefined = 0,

        StrCommand,

        max
    }

    public enum PurchasesSubCommand : byte
    {
        Undefined = 0,

        GetAllProducts,
        PurchaseProduct,
        PurchaseHero,

        PurchaseSkin,
        PurchaseEquipmentLevelUp,
        PurchaseQuestSkip,
        PurchaseSlotCooldownReset,
        PurchasePlayerAvatar,
        PurchasePlayerRename,
        PurchaseSparkResearchInstantComplete,
        PurchaseSparkUpgrade,
        PurchaseSparkUnlock,

        max
    }

    public enum RewardQueueSubCommand : byte
    {
        Undefined = 0,

        /// <summary>
        /// Запросить обновление активной награды
        /// </summary>
        CurrentItem,

        /// <summary>
        /// Забрать награду
        /// </summary>
        UtilizeReward,

        max
    }

    public enum OfferSubCommand : byte
    {
        Undefined = 0,

        AcceptSpecialOffer,
        BuyLimitedOffer,

        max
    }

    public enum RewardsSubCommand : byte
    {
        Undefined = 0,

        Collect,

        max
    }

    public enum ProfileSubCommand : byte
    {
        Undefined = 0,

        GetRates,
        GetHeroes,
        GetBattles,
        GetFullBattle,
        GetAchievements,
        GetAccount,
        GetKarma,

        max
    }

    public enum EnterSubCommand : byte
    {
        Undefined = 0,

        Successfully,

        max
    }

    public enum SparksSubCommand : byte
    {
        Undefined = 0,

        StartResearch,
        CancelResearch,
        CollectResearch,
        UpdateResearches,

        max
    }

    public enum ChatSubCommand : byte
    {
        Undefined = 0,
        JoinGlobalChat,
        SendPrivateMessage,
        ReceivePrivateMessage,
        GetLatestPrivateMessages,
        SendPartyMessage,
        ReceivePartyMessage,
        SendBattleMessage,
        ReceiveBattleMessage,
        SetPrivateLastReadTime,
        GetUnreadPrivateMessagesCounts,
        GetAllRoomTypes,
        Max
    }

    public enum BlackListSubCommand : byte
    {
        Undefined = 0,
        GetBlackList,
        AddUser,
        RemoveUser,
        Max
    }

    public enum AvatarFramesSubCommand : byte
    {
        Undefined = 0,

        SetActive,

        Max
    }

    public enum MailSubCommand : byte
    {
        Undefined = 0,

        MessageReceived,
        MessageModified,
        MessageDeleted,
        GiftToFriendSent,

        SendGiftToFriend,
        MarkAsRead,
        Delete,
        CollectReward,
        FriendGiftRemainReceiveCount,

        Max
    }

    public enum LeaguesSubCommand : byte
    {
        Undefined = 0,

        TakeDailyReward,
        Update,
        GetTop100,
        Max
    }

    public enum BattleItemsSubCommand : byte
    {
        Undefined = 0,

        SetItem,
        SaveBuild,
        SelectBuild,
        RenameBuild,
        GetTopBuilds,

        Max
    }

    public enum KarmaSubCommand : byte
    {
        Undefined = 0,
        SendReport,
        Max
    }

    public enum PltSubCommand : byte
    {
        Other = 0
    }

    public static class MetaCommandHelper
    {
        public static string GetSubCommandName(MetaCommand command, byte subCommand)
        {
            switch (command)
            {
                case MetaCommand.Battle:
                    return ((BattleSubCommand)subCommand).ToString();
                case MetaCommand.Player:
                    return ((PlayerSubCommand)subCommand).ToString();
                case MetaCommand.Runes:
                    return ((RunesSubCommand)subCommand).ToString();
                case MetaCommand.PlayerContext:
                    return ((PlayerContextSubCommand)subCommand).ToString();
                case MetaCommand.Quests:
                    return ((QuestsSubCommand)subCommand).ToString();
                case MetaCommand.Inventory:
                    return ((InventorySubCommand)subCommand).ToString();
                case MetaCommand.Common:
                    return ((CommonSubCommand)subCommand).ToString();
                case MetaCommand.Cheat:
                    return ((CheatSubCommand)subCommand).ToString();
                case MetaCommand.PromoCodes:
                    return ((PromoCodesSubCommand)subCommand).ToString();
                case MetaCommand.Purchases:
                    return ((PurchasesSubCommand)subCommand).ToString();
                case MetaCommand.RewardQueue:
                    return ((RewardQueueSubCommand)subCommand).ToString();
                case MetaCommand.Offer:
                    return ((OfferSubCommand)subCommand).ToString();
                case MetaCommand.Profile:
                    return ((ProfileSubCommand)subCommand).ToString();
                case MetaCommand.Rewards:
                    return ((RewardsSubCommand)subCommand).ToString();
                case MetaCommand.Sparks:
                    return ((SparksSubCommand)subCommand).ToString();
                case MetaCommand.EnterPlayer:
                    return ((EnterSubCommand)subCommand).ToString();
                case MetaCommand.League:
                    return ((LeaguesSubCommand)subCommand).ToString();
                case MetaCommand.AvatarFrames:
                    return ((AvatarFramesSubCommand)subCommand).ToString();
                case MetaCommand.Mail:
                    return ((MailSubCommand)subCommand).ToString();
                case MetaCommand.BattleItems:
                    return ((BattleItemsSubCommand)subCommand).ToString();
                case MetaCommand.Karma:
                    return ((KarmaSubCommand) subCommand).ToString();

                case MetaCommand.Plt:
                    return ((PltSubCommand)subCommand).ToString();

                default:
                    Log.e("Unknown command type {0}", command);
                    break;
            }
            return subCommand.ToString();
        }
    }
}
