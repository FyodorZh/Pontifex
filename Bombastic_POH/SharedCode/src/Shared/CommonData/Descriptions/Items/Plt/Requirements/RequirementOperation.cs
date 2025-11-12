using System;

namespace Shared.CommonData.Plt
{
    public enum RequirementOperation : byte
    {
        Equal = 0,
        GreaterThanOrEqual = 1,
        LessThanOrEqual = 2
    }

//    public static class RequirementOperationExtensions
//    {
//        public static bool IsGreatenThan(this RequirementOperation self)
//        {
//            return (self & RequirementOperation.GreaterThan) == RequirementOperation.GreaterThan;
//        }
//
//        public static bool IsLessThan(this RequirementOperation self)
//        {
//            return (self & RequirementOperation.LessThan) == RequirementOperation.LessThan;
//        }
//
//        public static bool IsEquals(this RequirementOperation self)
//        {
//            return (self & RequirementOperation.Equals) == RequirementOperation.Equals;
//        }
//    }
}
