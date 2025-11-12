using System.IO;
using System.Collections.Generic;
using Serializer.BinarySerializer;
using Serializer.Extensions.ID;
using Serializer.Tools;
using Shared.CommonData;

namespace Shared.Battle
{
    public class AiTemplateDescContainer : IDataStruct
    {
        public List<AiTemplateDescData> data = new List<AiTemplateDescData>();

        bool IDataStruct.Serialize(IBinarySerializer dst)
        {
            dst.Add(ref data);
            return true;
        }
    }

    public class AiTemplateDescData : IDataStruct, ISerializable
    {
        public const string RESOURCES_PATH = "Assets/LogicResources/Runtime/Data/Ai";
        public const string BLOB_PATH = "Assets/LogicResources/Runtime/Data/AiTemplates.bytes";
        public const string FN_TEMPLATE_LIST = "AiTemplatesList";
        public const string FN_TEMPLATE = "AiTemplate";

        public static IEnumerable<AiTemplateDescData> CreateFromBlob(string path)
        {
            if (File.Exists(path))
            {
                byte[] bytes = File.ReadAllBytes(path);
                AiTemplateDescContainer container = CommonDataConstructor<AiTemplateDescContainer, CommonResoucesFactory>.CreateFromBytes(bytes, true);
                return container.data;
            }
            else
            {
                Log.e("LoadDescriptions: can't find descriptions blob, {0}" + path);
                return null;
            }
        }

        public static IEnumerable<AiTemplateDescData> CreateFromBlob(byte[] bytes)
        {
            AiTemplateDescContainer container = CommonDataConstructor<AiTemplateDescContainer, CommonResoucesFactory>.CreateFromBytes(bytes, true);
            return container.data;
        }

        public static IEnumerable<AiTemplateDescData> CreateFromFolder(string path)
        {
            string[] files = Directory.GetFiles(path, "*.xml");

            List<AiTemplateDescData> templates = new List<AiTemplateDescData>();
            foreach (string file in files)
            {
                IEnumerable<AiTemplateDescData> fileTemplates = Create(file);
                if (fileTemplates != null)
                    templates.AddRange(fileTemplates);
            }
            return templates;
        }

        //TODO: Очень не нравится необходимость вообще создавать StorageFolder в этом случае, надо переписать!
        public static IEnumerable<AiTemplateDescData> Create(string path)
        {
            StorageFolder storageFolder = new StorageFolder();
            if (!CStorageSerializer.loadFromFile(path, storageFolder, false, false))
            {
                Log.e("Can't load Ai templates descriptions, path: \"{0}\"", path);
                return null;
            }

            return LoadDescriptions(storageFolder);
        }

        private static IEnumerable<AiTemplateDescData> LoadDescriptions(StorageFolder storageFolder)
        {
            if (storageFolder.Name != FN_TEMPLATE_LIST)
            {
                Log.e("Can't load Ai templates descriptions, wrong folder name : \"{0}\"", storageFolder.Name);
                return null;
            }

            Dictionary<int, AiTemplateDescData> descriptions = new Dictionary<int, AiTemplateDescData>();
            foreach (StorageFolder item in storageFolder.Items)
            {
                if (item.Name != FN_TEMPLATE)
                {
                    Log.e("Ai templates descriptions, wrong folder name : \"{0}\"", item.Name);
                    continue;
                }

                AiTemplateDescData templateDescription = new AiTemplateDescData();
                templateDescription.Deserialize(item);
                if (descriptions.ContainsKey(templateDescription.templateId))
                {
                    Log.e("Duplicate unit description Id: {0}, {1}. Conflicts with: {2}", templateDescription.templateId, templateDescription.name, descriptions[templateDescription.templateId].name);
                    continue;
                }
                descriptions.Add(templateDescription.templateId, templateDescription);
            }

            return descriptions.Values;
        }

        public enum MovementType : int
        {
            Stand = 0,
            FolowPatron = 1,
            MoveByWaypoints = 2,
        }

        public int templateId;

        public string name;
        public string patronTag;
        public float folowRange;

        public bool canAttack;
        public UnitClassType targetClassTypeMask;
        public float agroRange;
        public float agroRangeBase;
        public float moveRange;
        public float receiveSOSRange;
        public int agroTime;
        public int maxInvalidTime = 350;
        public int agroDpsDelay;

        public MovementType movementType;
        public bool loopedWp;
        public float wpSize;

        //++platformer
        public bool followTarget;

        public float patronOffsetX;
        public float patronOffsetY;
        //--platformer

        public readonly List<AiTriggerAbilityData> abilityTriggers = new List<AiTriggerAbilityData>();

        public AiTemplateDescData() { }

        public bool Serialize(IBinarySerializer dst)
        {
            int mType = (int)movementType;

            dst.Add(ref templateId);
            dst.Add(ref name);
            dst.Add(ref patronTag);
            dst.Add(ref folowRange);
            dst.Add(ref canAttack);

            int cType = (int)targetClassTypeMask;
            dst.Add(ref cType);
            targetClassTypeMask = (UnitClassType)cType;

            dst.Add(ref agroRange);
            dst.Add(ref agroRangeBase);
            dst.Add(ref moveRange);
            dst.Add(ref receiveSOSRange);
            dst.Add(ref agroTime);
            dst.Add(ref maxInvalidTime);
            dst.Add(ref agroDpsDelay);
            dst.Add(ref mType);
            dst.Add(ref loopedWp);
            dst.Add(ref wpSize);

            dst.Add(ref followTarget);

            dst.Add(ref patronOffsetX);
            dst.Add(ref patronOffsetY);

            movementType = (MovementType)mType;

            AiTriggerAbilityData[] triggers = abilityTriggers.ToArray();
            abilityTriggers.Clear();

            dst.Add(ref triggers);

            abilityTriggers.AddRange(triggers);

            return true;
        }

        private const string FN_TEMPLATE_ID = "templateId";
        private const string FN_NAME = "name";
        private const string FN_PATRON = "patronTag";
        private const string FN_PFOLOW_RANGE = "folowRange";
        private const string FN_CAN_ATTACK = "canAttack";
        private const string FN_CLASS_TYPE = "targetClassType";
        private const string FN_AGRO_RANGE = "agroRange";
        private const string FN_AGRO_RANGE_BASE = "agroRangeBase";
        private const string FN_MOVE_RANGE = "moveRange";
        private const string FN_SOS_RANGE = "receiveSOSRange";
        private const string FN_AGRO_TIME = "agroTime";
        private const string FN_MAX_INVALID_TIME = "maxTargetInvalidTime";
        private const string FN_AGRO_DPS_DELAY = "agroDpsDelay";
        private const string FN_MOVE_BY_WP = "movementType";
        private const string FN_LOOPED_WP = "loopedWp";
        private const string FN_WP_SIZE = "wpSize";
        private const string FN_ABILITY_TRIGGERS = "AbilityTriggers";

        private const string FN_FOLLOW_TARGET = "followTarget";

        private const string FN_PATRON_OFFSET_X = "patronOffsetX";
        private const string FN_PATRON_OFFSET_Y = "patronOffsetY";

        public void Deserialize(StorageFolder from)
        {
            templateId = from.GetItemAsInt(FN_TEMPLATE_ID);
            name = from.GetItemAsString(FN_NAME);
            patronTag = from.GetItemAsString(FN_PATRON);
            folowRange = from.GetItemAsFloat(FN_PFOLOW_RANGE);
            canAttack = from.GetItemAsBool(FN_CAN_ATTACK);
            targetClassTypeMask = (UnitClassType)from.GetItemAsInt(FN_CLASS_TYPE, byte.MaxValue);
            agroRange = from.GetItemAsFloat(FN_AGRO_RANGE);
            agroRangeBase = from.GetItemAsFloat(FN_AGRO_RANGE_BASE);
            moveRange = from.GetItemAsFloat(FN_MOVE_RANGE);
            receiveSOSRange = from.GetItemAsFloat(FN_SOS_RANGE);
            agroTime = from.GetItemAsInt(FN_AGRO_TIME);
            maxInvalidTime = from.GetItemAsInt(FN_MAX_INVALID_TIME, maxInvalidTime);
            agroDpsDelay = from.GetItemAsInt(FN_AGRO_DPS_DELAY);
            movementType = (MovementType)from.GetItemAsInt(FN_MOVE_BY_WP);
            loopedWp = from.GetItemAsBool(FN_LOOPED_WP);
            wpSize = from.GetItemAsFloat(FN_WP_SIZE, 0.01f/*Shared.Battle.Ai.AiConsts.EPS*/);

            followTarget = from.GetItemAsBool(FN_FOLLOW_TARGET);

            patronOffsetX = from.GetItemAsFloat(FN_PATRON_OFFSET_X);
            patronOffsetY = from.GetItemAsFloat(FN_PATRON_OFFSET_Y);

            abilityTriggers.Clear();
            StorageFolder triggersFolder = from.GetFolder(FN_ABILITY_TRIGGERS);
            if (triggersFolder != null)
            {
                foreach (StorageFolder trFolder in triggersFolder.Items)
                {
                    if (trFolder.Name != AiTriggerAbilityData.FN_TRIGGER)
                    {
                        Log.e("Ai templates descriptions, wrong trigger folder name : \"{0}\"", trFolder.Name);
                        continue;
                    }

                    AiTriggerAbilityData trData = new AiTriggerAbilityData();
                    trData.Deserialize(trFolder);
                    abilityTriggers.Add(trData);
                }
            }
        }

        public void Serialize(StorageFolder to)
        {
            to.AddItem(new StorageInt(FN_TEMPLATE_ID, templateId));
            to.AddItem(new StorageString(FN_NAME, name));
            to.AddItem(new StorageString(FN_PATRON, patronTag));
            to.AddItem(new StorageFloat(FN_PFOLOW_RANGE, folowRange));
            to.AddItem(new StorageBool(FN_CAN_ATTACK, canAttack));
            to.AddItem(new StorageInt(FN_CLASS_TYPE, (int)targetClassTypeMask));
            to.AddItem(new StorageFloat(FN_AGRO_RANGE, agroRange));
            to.AddItem(new StorageFloat(FN_AGRO_RANGE_BASE, agroRangeBase));
            to.AddItem(new StorageFloat(FN_MOVE_RANGE, moveRange));
            to.AddItem(new StorageFloat(FN_SOS_RANGE, receiveSOSRange));
            to.AddItem(new StorageInt(FN_AGRO_TIME, agroTime));
            to.AddItem(new StorageInt(FN_MAX_INVALID_TIME, maxInvalidTime));
            to.AddItem(new StorageInt(FN_AGRO_DPS_DELAY, agroDpsDelay));
            to.AddItem(new StorageInt(FN_MOVE_BY_WP, (int)movementType));
            to.AddItem(new StorageBool(FN_LOOPED_WP, loopedWp));
            to.AddItem(new StorageFloat(FN_WP_SIZE, wpSize));

            to.AddItem(new StorageBool(FN_FOLLOW_TARGET, followTarget));

            to.AddItem(new StorageFloat(FN_PATRON_OFFSET_X, patronOffsetX));
            to.AddItem(new StorageFloat(FN_PATRON_OFFSET_Y, patronOffsetY));

            StorageFolder triggersFolder = new StorageFolder(FN_ABILITY_TRIGGERS);
            if (abilityTriggers != null)
            {
                foreach (AiTriggerAbilityData trigger in abilityTriggers)
                {
                    StorageFolder trFolder = new StorageFolder(AiTriggerAbilityData.FN_TRIGGER);
                    trigger.Serialize(trFolder);
                    triggersFolder.AddItem(trFolder);
                }
                to.AddItem(triggersFolder);
            }
        }
    }

    public class AiTriggerAbilityData : IDataStruct
    {
        public const string FN_TRIGGER = "AiAbilityTrigger";

        private const string FN_TARGET_TYPE = "targetType";
        private const string FN_CLASS_TYPE = "classType";
        private const string FN_SLOT_ID = "slotId";

        private const string FN_TRY_CAST_COOLDOWN = "tryCastCooldown";
        private const string FN_START_COOLDOWN = "startCooldown";

        private const string FN_NEED_ROTATE_TO_CAST_TARGET = "needRotateToCastTarget";

        private const string FN_MIN_HP = "minHpPercent";
        private const string FN_MAX_HP = "maxHpPercent";
        private const string FN_MIN_RANGE = "minRange";
        private const string FN_MAX_RANGE = "maxRange";
        private const string FN_MAX_RANGEFROM_ABILITY = "maxRangeFromAbility";
        private const string FN_TARGETS_COUNT = "targetsCount";

        private const string FN_IS_LOCKED_ANGLE_AIM = "isLockedAngleAim";
        private const string FN_CAN_FLIP = "canFlip";
        private const string FN_ANGLE_STEP = "angleStep";

        private const string FN_IS_BASE_ABILITY = "isBaseAbility";

        public enum TargetType : int
        {
            Self,
            CurrentTarget,
            Any,
            AutoAttack,
        }

        public TargetType targetType;
        public UnitClassType classTypeMask;
        public IDSByte<IAbilitySlotId> slotId;
        public int tryCastCooldown;
        public int startCooldown;
        public bool needRotateToCastTarget;
        public float minHpPercent;
        public float maxHpPercent;
        public float minRange;
        public float maxRange;
        public bool maxRangeFromAbility;
        public int targetsCount;

        public bool isLockedAngleAim;
        public bool canFlip;
        public float angleDegreesStep;

        public bool isBaseAbility;

        public bool Serialize(IBinarySerializer dst)
        {
            int tType = (int)targetType;
            int cType = (int)classTypeMask;

            dst.Add(ref tType);
            dst.Add(ref cType);
            dst.AddId(ref slotId);
            dst.Add(ref tryCastCooldown);
            dst.Add(ref startCooldown);
            dst.Add(ref needRotateToCastTarget);
            dst.Add(ref minHpPercent);
            dst.Add(ref maxHpPercent);
            dst.Add(ref minRange);
            dst.Add(ref maxRange);
            dst.Add(ref maxRangeFromAbility);
            dst.Add(ref targetsCount);

            dst.Add(ref isLockedAngleAim);
            dst.Add(ref canFlip);
            dst.Add(ref angleDegreesStep);

            dst.Add(ref isBaseAbility);

            targetType = (TargetType)tType;
            classTypeMask = (UnitClassType)cType;

            return true;
        }

        public void Deserialize(StorageFolder from)
        {
            targetType = (TargetType)from.GetItemAsInt(FN_TARGET_TYPE);
            classTypeMask = (UnitClassType)from.GetItemAsInt(FN_CLASS_TYPE, byte.MaxValue);
            slotId = IDSByte<IAbilitySlotId>.DeserializeFrom((sbyte)from.GetItemAsInt(FN_SLOT_ID));

            tryCastCooldown = from.GetItemAsInt(FN_TRY_CAST_COOLDOWN);
            startCooldown = from.GetItemAsInt(FN_START_COOLDOWN);

            needRotateToCastTarget = from.GetItemAsBool(FN_NEED_ROTATE_TO_CAST_TARGET);

            minHpPercent = from.GetItemAsFloat(FN_MIN_HP);
            maxHpPercent = from.GetItemAsFloat(FN_MAX_HP);
            minRange = from.GetItemAsFloat(FN_MIN_RANGE);
            maxRange = from.GetItemAsFloat(FN_MAX_RANGE);
            maxRangeFromAbility = from.GetItemAsBool(FN_MAX_RANGEFROM_ABILITY);
            targetsCount = from.GetItemAsInt(FN_TARGETS_COUNT);

            isLockedAngleAim = from.GetItemAsBool(FN_IS_LOCKED_ANGLE_AIM);
            canFlip = from.GetItemAsBool(FN_CAN_FLIP);
            angleDegreesStep = from.GetItemAsFloat(FN_ANGLE_STEP);

            isBaseAbility = from.GetItemAsBool(FN_IS_BASE_ABILITY);
        }
        public void Serialize(StorageFolder to)
        {
            to.AddItem(new StorageInt(FN_TARGET_TYPE, (int)targetType));
            to.AddItem(new StorageInt(FN_CLASS_TYPE, (int)classTypeMask));
            to.AddItem(new StorageInt(FN_SLOT_ID, slotId.SerializeTo()));

            to.AddItem(new StorageInt(FN_TRY_CAST_COOLDOWN, tryCastCooldown));
            to.AddItem(new StorageInt(FN_START_COOLDOWN, startCooldown));

            to.AddItem(new StorageBool(FN_NEED_ROTATE_TO_CAST_TARGET, needRotateToCastTarget));

            to.AddItem(new StorageFloat(FN_MIN_HP, minHpPercent));
            to.AddItem(new StorageFloat(FN_MAX_HP, maxHpPercent));
            to.AddItem(new StorageFloat(FN_MIN_RANGE, minRange));
            to.AddItem(new StorageFloat(FN_MAX_RANGE, maxRange));
            to.AddItem(new StorageBool(FN_MAX_RANGEFROM_ABILITY, maxRangeFromAbility));
            to.AddItem(new StorageInt(FN_TARGETS_COUNT, targetsCount));

            to.AddItem(new StorageBool(FN_IS_LOCKED_ANGLE_AIM, isLockedAngleAim));
            to.AddItem(new StorageBool(FN_CAN_FLIP, canFlip));
            to.AddItem(new StorageFloat(FN_ANGLE_STEP, angleDegreesStep));

            to.AddItem(new StorageBool(FN_IS_BASE_ABILITY, isBaseAbility));
        }
    }
}
