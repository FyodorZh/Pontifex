using System;
namespace Shared
{
    namespace CommonData
    {
        public sealed class UnitDescriptionResources
        {
            private const string ASSASSIN_ROLE_ICON_PATH = "Assets/_CONTENT/GUI/UI/Shared/SD/gui_hero_role_assassin.png";
            private const string SUPPORT_ROLE_ICON_PATH = "Assets/_CONTENT/GUI/UI/Shared/SD/gui_hero_role_support.png";
            private const string WARRIOR_ROLE_ICON_PATH = "Assets/_CONTENT/GUI/UI/Shared/SD/gui_hero_role_warrior.png";
            private const string MARKSMAN_ROLE_ICON_PATH = "Assets/_CONTENT/GUI/UI/Shared/SD/gui_hero_role_marksman.png";
            private const string MAGE_ROLE_ICON_PATH = "Assets/_CONTENT/GUI/UI/Shared/SD/gui_hero_role_mage.png";

            private const string PATH_TO_UNITS_FOLDER = "Assets/_DATA/UNITS";
            private const string SKIN_FOLDER_FORMAT = "Skin_{0}";
            private const string ICONS_FOLDER_NAME = "Icons";
            private const string ICON_64_NAME = "unit_portrait_64x64.png";
            private const string ICON_116_NAME = "unit_portrait_116x116.png";
            private const string ICON_220_NAME = "unit_portrait_220x220.png";
            private const string ICON_FULL_PORTRAIT = "gui_hero_portrait_card.png";

            private const string PREFAB_NAME = "{0}_prefab.prefab";
            private const string LOBBY_PREFAB_NAME = "{0}_lobby_prefab.prefab";
            private const string COMBINE_PATH = "{0}/{1}";
            private const string ABILITY_ICONS_FOLDER_NAME = "AbilityIcons";
            private const string ABILITY_ICON_FILE_FORMAT = "ability_icon_{0}.png";
            private const string RUNE_ICONS_FOLDER_NAME = "RuneIcons";
            private const string RUNE_ICONS_100_FOLDER_NAME = "100x100";
            private const string RUNE_ICONS_50_FOLDER_NAME = "50x50";
            private const string RUNE_ICON_FILE_FORMAT = "rune_icon_{0}_{1}.png";

            private readonly string mPathToFolder;
            private readonly string mFolderName;
            private string mModelPath;
            private string mLobbyModelPath;
            private string mIcon64X64Path;
            private string mIcon128X128Path;
            private string mIcon220X220Path;
            private string mIconFullPortrait;

            public UnitDescriptionResources(string folderName)
            {
                mFolderName = folderName;
                mPathToFolder = string.Format(COMBINE_PATH, PATH_TO_UNITS_FOLDER, mFolderName);
            }

        }
    }

}
