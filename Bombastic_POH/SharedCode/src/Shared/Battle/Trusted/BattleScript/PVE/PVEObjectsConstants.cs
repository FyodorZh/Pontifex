namespace Shared.Battle
{
    public static class MapDataBehaviourConstants
    {
        public const string SPAWNERS_FOLDER_NAME = "Spawners";
        public const string TRIGGERS_FOLDER_NAME = "Triggers";
    }

    public static class ScenarioTriggerConstants
    {
        public const string FOLDER_NAME = "Trigger";
        public const string ACTIONS_FOLDER_NAME = "Actions";
        public const string TYPE = "Type";
        public const string ID = "ID";
        public const string RUN_ONCE = "RunOnce";
        public const string NAME = "Name";

        public const string TAG = "Tag";
        public const string POSITION = "Position";
        public const string COOLDOWN = "Cooldown";
        public const string HIERARCHY_PATH = "HierarhyPath";

        public const float DEFAULT_COOLDOWN = 1.0f;

        public static string GetFunctionString(string name)
        {
            return name + "_string";
        }

        public static string GetFunctionFolder(string name)
        {
            return name + "_folder";
        }
    }

    public static class PVEQuestConstants
    {
        public const string FOLDER_NAME = "Quest";
        public const string DONE_CONDITIONS = "Done";
        public const string FAIL_CONDITIONS = "Fail";
        public const string MAIN_CONDITION_ID = "MainCondID";
        public const string ID = "ID";
        public const string STARS = "Stars";
        public const string PROGRESS_MESSAGE = "ProgressMessage";
        public const string NAME = "Name";

        public static string GetFunctionString(string name)
        {
            return name + "_string";
        }

        public static string GetFunctionFolder(string name)
        {
            return name + "_folder";
        }
    }

    public static class PVEQuestConditionConstants
    {
        public const string FOLDER_NAME = "Condition";
        public const string NAME = "Name";
        public const string ID = "Id";
        public const string TRIGGER_ID = "TriggerId";
        public const string COUNT = "Count";
    }

    public static class ScenarioTriggerTimerConstants
    {
        public const string ACTIVATION_TIME = "ACTIVATION_TIME";
        public const string USE_PURE_GAME_TIME = "USE_PURE_GAME_TIME";
    }

    public static class ScenarioTriggerUnitDeathConstants
    {
        public const string TARGET_UNIT_TAG = "TargetUnitTag";
        public const string KILLER_UNIT_TAG = "KillertUnitTag";

        public const string TARGET_UNIT_TAG_FUNC = "TargetUnitTagFunction";
        public const string KILLER_UNIT_TAG_FUNC = "KillerUnitTagFunction";
        public const string TARGET_SPAWN_POINTS_FOLDER_NAME = "TargetSpawnPoints";
        public const string LOCAL_UNIT = "LocalUnit";
    }

    public static class ScenarioTriggerSpawnerCompleteConstants
    {
        public const string SPAWNERS = "Spawners";
        public const string SPAWNER_ID = "SpawnerID";
        public const string SPAWNER_GROUIP_INDEX = "SpawnerGroupIndex";
    }

    public static class ScenarioTriggerStatusAddedConstants
    {
        public const string CASTER_TAG = "CasterTag";
        public const string TARGETS_FOLDER_NAME = "Targets";
        public const string TARGET_TAG = "TargetTag";
        public const string ABILITY_NAME = "Ability";
        public const string STATUS_NAME = "Status";
        public const string STATUS_STACKS = "Stacks";
        public const string STATUS_FLAGS = "Flags";

        public const string CASTER_TAG_FUNC = "CasterTag_Function";
        public const string TARGET_TAG_FUNC = "TargetTag_Function";

        public const string CAN_MULTIPLE_INVOKE_IN_ONE_FRAME = "CanMultipleInvokeInOneFrame";
    }

    public static class ScenarioActionConstants
    {
        public const string FOLDER_NAME = "Action";
        public const string TYPE = "Type";
        public const string NAME = "Name";
        public const string POSITION = "Position";
        public const string ROTATION = "Rotation";
        public const string SCALE = "Scale";
        public const string TIME_OFFSET = "TimeOffset";
        public const string USE_PURE_GAME_TIME = "usePureGameTime";
    }

    public static class ScenarioActionLogMessageConstants
    {
        public const string MESSAGE_TEXT = "MessageText";
    }

    public static class ScenarioActionSetDelaySpawnerConstants
    {
        public const int MAX_TIME = 999999;
        public const string SPAWNER_ID = "SpawnerID";
        public const string START_DELAY = "StartDelay";
    }

    public static class ScenarioActionActivateSpawnerConstants
    {
        public const string SPAWNER_ID = "SpawnerID";
        public const string ACTIVATE = "Activate";
    }

    public static class ScenarioActionActivateSpawnGroupConstants
    {
        public const string SPAWNER_ID = "SpawnerID";
        public const string GROUP_NAME = "GroupName";
        public const string ACTIVATE = "Activate";
        public const string WAIT_WAVE_COMPLETE = "WaitWaveComplete";
    }

    public static class ScenarioActionRestoreSpawnerConstants
    {
        public const string SPAWNER_ID = "SpawnerID";
        public const string GROUP_NAME = "GroupName";
    }

    public static class ScenarioActionHudChangeVisibility
    {
        public const string ELEMENT_ID = "ElementID";
        public const string ACTIVATE = "Activate";
    }

    public static class ScenarioActionHudChangeInteraction
    {
        public const string ELEMENT_ID = "ElementID";
        public const string ACTIVATE = "Activate";
    }

    public static class ScenarioActionSetPassabilityConstants {
        public const string COLLIDER_ID = "ColliderID";
        public const string PLAYER_ACTION = "PlayerAction";
        public const string ENEMIES_ACTION = "EnemiesAction";
        public const string PROJECTILES_ACTION = "ProjectilesAction";
    }

    public enum ScenarioActionSetPassabilityAction
    {
        DoNotChange,
        Passable,
        NotPassable
    }

    public static class PVEActionHudElementHiglightConstants
    {
        public const string HOLD_WORLD = "HoldWorld";
        public const string ELEMENT_ID = "ElementID";
        public const string ID = "ID";
    }

    public static class PVEActionHudElementHiglightOffConstants
    {
        public const string ELEMENT_ID = "ElementID";
        public const string ID = "ID";
    }

    public static class PVEActionTooltipShowConstants
    {
        public const string ID = "ID";
        public const string Text = "Text";
    }

    public static class PVEActionTooltipHideConstants
    {
        public const string ID = "ID";
    }

    public static class ActionCastAbilityConstants
    {
        public const string ABILITY_SLOT_ID = "AbilitySlotId";
        public const string TARGET_TYPE = "TargetType";
        public const string TARGET_TAG = "TargetTag";
        public const string TARGET_POSITION = "TargetPosition";
        public const string _X = "X";
        public const string _Y = "Y";
        public const string _R = "R";

        public enum TargetType
        {
            Tag = 0,
            Position = 1,
            PositionDirection = 2
        }
    }

    public static class PVEActionCreateFXConstants
    {
        public const string ID = "ID";
        public const string PATH = "Path";
        public const string FX_POSITION = "FXPosition";
        public const string FX_ROTATION = "FXRotation";
        public const string FX_SCALE = "FXScale";
    }

    public static class PVEActionFreezeResumeConstants
    {
        public const string STRATEGIES = "Strategies";
        public const string TIMER = "Timer";
    }

    public static class PVEActionDestroyFXConstants
    {
        public const string CREATE_FX_ID = "ID";
        public const string FADE_OUT_DURATION = "FadeOutDuration";
    }


    public static class ScenarioActionEndBattleConstants
    {
        public const string RESULT = "Result";
        public const string SHOULD_SET_STRATEGIES = "ShouldSetStrategies";
        public const string ANIMATION_OFFSET = "AnimationOffset";
    }

    public static class PVEActionShowDialogConstants
    {
        public const string ITEMS = "Items";
        public const string CHARACTER_NAME = "Character";
        public const string TEXT = "Text";
        public const string IMAGE_PATH = "Image";
        public const string IMAGE_ALIGN = "Align";
        public const string ID = "ID";
        public const string CONTROLLED_UNIT = "ControlledUnit";
    }

    public static class PVEActionChooseHeroConstants
    {
        public const string HERO_ID = "HeroID";
    }

    public static class ScenarioTriggerAreaReachedConstants
    {
        public const string TRIGGER_AREA_ID = "TriggerAreaId";
        public const string UNIT_TAG_FUNC = "UnitTagFunction";
        public const string LOCAL_UNIT = "LocalUnit";
    }

    public static class PVETriggerDialogEndedConstants
    {
        public const string DIALOG_ID = "DialogID";
    }

    public static class PVETriggerBattleHelpWindowHiddenConstants
    {
        public const string WINDOW_ID = "WindowID";
    }

    public static class ScenarioActionStartMoveCameraConstants
    {
        public const string CAMERA_LOOK_POSITION = "CameraLookPosition";
        public const string DISTANCE = "Distance";
        public const string MOVE_TO_TARGET_TIME = "MoveToTargetTime";
        public const string IS_RETURN_TO_PLAYER = "ReturnToPlayer";
        public const string STAY_AT_TARGET_TIME = "StayAtTargetTime";
        public const string RETURN_TIME = "ReturnTime";
        public const string HOLD_WORLD = "HoldWorld";
    }

    public static class ScenarioActionPlayAnimationOnCameraConstants
    {
        public const string CLIP_PATH = "ClipPath";
        public const string ANIMATION_DURATION = "AnimationDuration";
        public const string IS_RETURN_TO_PLAYER = "ReturnToPlayer";
        public const string STAY_AT_TARGET_TIME = "StayAtTargetTime";
        public const string RETURN_TIME = "ReturnTime";
        public const string HOLD_WORLD = "HoldWorld";
        public const string CURTAINS_OPEN_DELAY = "CurtainsOpenDelay";
    }

    public static class ScenarioActionChangeStrategyConstants
    {
        public const string TAG = "Tag";
        public const string STRATEGY_TYPE = "StrategyType";
        public const string STRATEGY_TEMPLATE = "StrategyTemplate";
        public const string WAY_POINTS = "WayPoints";
        public const string OVERRIDED = "Overrided";
    }

    public static class ScenarioActionReturnCameraToPlayerConstants
    {
        public const string DURATION = "dURATION";
        public const string HOLD_WORLD = "HoldWorld";
    }

    public static class ScenarioActionActivateTriggerConstants
    {
        public const string TRIGGER_ID = "TriggerID";
        public const string VALUE = "Value";
    }

    public static class ScenarioActionRestartTriggerConstants
    {
        public const string TRIGGER_ID = "TriggerID";
    }

    public static class ScenarioTriggerSpawnerActivatedConstants
    {
        public const string SPAWNER_ID = "SpawnerId";
    }

    public static class ScenarioTriggerUnitSpawnedConstants
    {
        public const string TARGET_UNIT_TAG_FUNC = "TargetUnitTagFunction";
    }

    public static class ScenarioActionNotifyFunnelStepConstants
    {
        public const string FUNNEL_STEP_ID = "FunnelStepId";
    }


    public static class PVETriggerHiglightEndedConstants
    {
        public const string HIGHLIGHT_ID = "HighlightID";
    }

    public static class PVEActionShowBattleHelpWindowConstants
    {
        public const string BATTLE_HELP_ID = "BattleHelpId";
        public const string BATTLE_HELP_IMAGE_PATH = "BattleHelpImagePath";
    }

    public static class ScenarioActionCompleteActionConstants
    {
        public const string TRIGGER_ID = "TriggerID";
        public const string ACTION_ID = "ActionID";
    }

    public static class TriggerAreaConstants
    {
        public const string FOLDER_NAME = "TriggerArea";
        public const string SHAPES = "Shapes";
        public const string ID = "ID";
        public const string NAME = "Name";
    }

    public static class TriggerAreaShapeConstants
    {
        public const string FOLDER_NAME = "Shape";
        public const string TYPE = "Type";
        public const string POSITION = "Position";
    }

    public static class TriggerAreaShapeCircleConstants
    {
        public const string RADIUS = "Radius";
    }

    public static class TriggerAreaShapeRectConstants
    {
        public const string SIZE = "Size";
        public const string ROTATION = "Rotation";
    }

    public static class TriggerAreaShapePolyConstants
    {
        public const string POLYS_FOLDER = "Polys";
        public const string POLY = "Poly";
    }

    public enum TriggerShapeType
    {
        Circle,
        Rect,
        Poly
    }
}
