using System;
namespace Shared.CommonData.Plt
{
    public static class RequirementsExtensions
    {
        public static short GetHeroClassDescId(this Requirement[] requirements)
        {
            var classDescId = TryGetHeroClassDescId(requirements);
            if (!classDescId.HasValue)
            {
                throw new Exception("ClassId not found");
            }

            return classDescId.Value;
        }

        public static short? TryGetHeroClassDescId(this Requirement[] requirements)
        {
            for (int i = 0; i < requirements.Length; i++)
            {
                var requirement = requirements[i];
                var heroClassRequirement = requirement as HeroClassItemRequirement;
                if (heroClassRequirement != null)
                {
                    return heroClassRequirement.HeroClassDescriptionId;
                }
            }

            return null;
        }
    }
}
