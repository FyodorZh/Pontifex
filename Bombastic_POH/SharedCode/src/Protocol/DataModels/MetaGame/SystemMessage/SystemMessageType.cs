namespace Shared.Protocol
{
    public enum SystemMessageType : byte
    {
        ChangeOnlineStatus,
        Ban,
        Unban,
        Admin,
        GracefulShutdown,
        DisconnectAll,
        ReloadBots,
        MatcheMakerKeepAliveTimeout,
        OperatorMessageUpdate,

        max
    }
}
