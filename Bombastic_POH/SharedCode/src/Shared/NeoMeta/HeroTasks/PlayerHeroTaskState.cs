using System;

namespace Shared.NeoMeta.HeroTasks
{
    public enum PlayerHeroTaskState : byte
    {
        NotStarted,
        Executing,
        Completed,
        Collected
    }

    public static class PlayerHeroTaskStateExtensions
    {
        public static TResult Match<TResult>(
            this PlayerHeroTaskState state,
            Func<TResult> onNotStarted,
            Func<TResult> onExecuting,
            Func<TResult> onCompleted,
            Func<TResult> onCollected)
        {
            switch (state)
            {
                case PlayerHeroTaskState.NotStarted:
                    return onNotStarted();
                case PlayerHeroTaskState.Executing:
                    return onExecuting();
                case PlayerHeroTaskState.Completed:
                    return onCompleted();
                case PlayerHeroTaskState.Collected:
                    return onCollected();
                default:
                    throw new ArgumentOutOfRangeException("state", state, null);
            }
        }
    }
}
