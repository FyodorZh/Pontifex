using System;
using Geom2d;
using Shared.Battle.Common;

namespace Shared.Battle
{
    [Serializable]
    public class UnitPreset : ISerializable
    {
        public short UnitDescriptionID;
        public short SkinID;
        public Vector PositionOffset;
        public float ViewAngle;
        public StrategyType StrategyType;
        public int StrategyTemplateId;
        public string Tag;
        public string CustomIdleAnimation;
        public InitRestrictions Restrictions = new InitRestrictions();

        /// <summary>
        /// Это мега костыль только для того, чтобы эта фигня сериализовалась в префаб Unity
        /// </summary>
        public uint _RestrictionFlags;
        public int _RestrictionsLifetime = DeltaTime.Infinity.MilliSeconds;

        private const string FN_DESCRIPTION_ID = "DescriptionId";
        private const string FN_SKIN_ID = "SkinID";
        private const string FN_POSITION_X = "PositionX";
        private const string FN_POSITION_Y = "PositionY";
        private const string FN_VIEW_ANGLE = "ViewAngle";
        private const string FN_STRATEGY_TYPE = "StrategyType";
        private const string FN_STRATEGY_TEMPLATE_ID = "StrategyTemplateId";
        private const string UNITS_TAG = "Tag";
        private const string UNITS_CUSTOM_IDLE_ANIMATION_TAG = "CustomIdleAnimation";
        private const string FN_RESTRICTIONS = "Restrictions";
        private const string FN_RESTRICTIONS_LIFETIME = "RestrictionsLifetime";

        public void Deserialize(StorageFolder from)
        {
            int desc = from.GetItemAsInt(FN_DESCRIPTION_ID);
            UnitDescriptionID = (short)desc;

            int skin = from.GetItemAsInt(FN_SKIN_ID, 0);
            SkinID = (short)skin;

            float posX = from.GetItemAsFloat(FN_POSITION_X);
            float posY = from.GetItemAsFloat(FN_POSITION_Y);
            PositionOffset = new Vector(posX, posY);
            ViewAngle = from.GetItemAsFloat(FN_VIEW_ANGLE);

            StrategyType = (StrategyType)from.GetItemAsByte(FN_STRATEGY_TYPE);
            StrategyTemplateId = from.GetItemAsInt(FN_STRATEGY_TEMPLATE_ID);
            Tag = from.GetItemAsString(UNITS_TAG);

            CustomIdleAnimation = from.GetItemAsString(UNITS_CUSTOM_IDLE_ANIMATION_TAG);

            Restrictions.Init(from.GetItemAsUInt(FN_RESTRICTIONS), DeltaTime.FromMiliseconds(from.GetItemAsInt(FN_RESTRICTIONS_LIFETIME)));
        }

        public void Serialize(StorageFolder to)
        {
            if (UnitDescriptionID == 1001)
            {
                Log.e("RESRICT TO USE SUPERVISOR!!!");
            }

            if (UnitDescriptionID == 1050 && (StrategyType != StrategyType.TemplateStrategy || StrategyTemplateId != 6632))
            {
                Log.e("Lizard Sniper has wrong strategy [{0}, {1}]", StrategyType, StrategyTemplateId);
            }

            if (UnitDescriptionID != 1050 && StrategyType == StrategyType.TemplateStrategy && StrategyTemplateId == 6632)
            {
                Log.e("Wrong unit {2} has Lizard Sniper strategy [{0}, {1}]", StrategyType, StrategyTemplateId, UnitDescriptionID);
            }

            to.AddItem(new StorageInt(FN_DESCRIPTION_ID, UnitDescriptionID));

            if (SkinID > 0)
            {
                to.AddItem(new StorageInt(FN_SKIN_ID, SkinID));
            }

            to.AddItem(new StorageFloat(FN_POSITION_X, PositionOffset.x));
            to.AddItem(new StorageFloat(FN_POSITION_Y, PositionOffset.y));

            to.AddItem(new StorageFloat(FN_VIEW_ANGLE, ViewAngle));
            to.AddItem(new StorageByte(FN_STRATEGY_TYPE, (byte)StrategyType));
            to.AddItem(new StorageInt(FN_STRATEGY_TEMPLATE_ID, StrategyTemplateId));

            if (!string.IsNullOrEmpty(Tag))
            {
                to.AddItem(new StorageString(UNITS_TAG, Tag));
            }

            if (!string.IsNullOrEmpty(CustomIdleAnimation))
            {
                to.AddItem(new StorageString(UNITS_CUSTOM_IDLE_ANIMATION_TAG, CustomIdleAnimation));
            }

            Restrictions.Init(_RestrictionFlags, DeltaTime.FromMiliseconds(_RestrictionsLifetime));

            if (Restrictions.HasRestrictions)
            {
                to.AddItem(new StorageUInt(FN_RESTRICTIONS, Restrictions.Flags));
                to.AddItem(new StorageInt(FN_RESTRICTIONS_LIFETIME, Restrictions.LifeTime.MilliSeconds));
            }
        }
    }
}