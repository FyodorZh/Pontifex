namespace Shared.Battle
{
    public enum StrategyType : byte
    {
        Undefined = 0,

        NOP = 1,

        BrownianMotion = 2,
        ReturnToOrigin = 3,

        TemplateStrategy = 9,
        DebugReturnToOrigin = 16,
    }
}