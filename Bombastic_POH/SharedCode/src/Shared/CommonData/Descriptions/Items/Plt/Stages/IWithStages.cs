namespace Shared.CommonData.Plt
{
    public interface IWithStages
    {
        short StartStageId { get; }

        StageDescription[] Stages { get; }
    }

    public interface IWithStages<out TStageDescription> : IWithStages
        where TStageDescription : StageDescription
    {
        new TStageDescription[] Stages { get; }
    }

    public static class IWithStagesExtensions
    {
        public static TStageDescription GetStageDescription<TStageDescription>(this IWithStages self, short stageId)
            where TStageDescription : StageDescription
        {
            for (int index = 0; index < self.Stages.Length; ++index)
            {
                var eachStageDescription = self.Stages[index];

                if (eachStageDescription.Id == stageId)
                {
                    return eachStageDescription as TStageDescription;
                }
            }

            return null;
        }

        public static TStageDescription GetStageDescription<TStageDescription>(this IWithStages<TStageDescription> self, short stageId)
            where TStageDescription : StageDescription
        {
            return GetStageDescription<TStageDescription>(self as IWithStages, stageId);
        }
    }
}
