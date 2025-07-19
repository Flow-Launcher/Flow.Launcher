using Flow.Launcher.Localization.Attributes;

namespace Flow.Launcher.Plugin.Explorer.Search.Everything
{
    [EnumLocalize]
    public enum EverythingSortOption : uint
    {
        [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_everything_sort_by_name_ascending))]
        NAME_ASCENDING = 1u,
        [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_everything_sort_by_name_descending))]
        NAME_DESCENDING = 2u,
        [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_everything_sort_by_path_ascending))]
        PATH_ASCENDING = 3u,
        [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_everything_sort_by_path_descending))]
        PATH_DESCENDING = 4u,
        [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_everything_sort_by_size_ascending))]
        SIZE_ASCENDING = 5u,
        [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_everything_sort_by_size_descending))]
        SIZE_DESCENDING = 6u,
        [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_everything_sort_by_extension_ascending))]
        EXTENSION_ASCENDING = 7u,
        [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_everything_sort_by_extension_descending))]
        EXTENSION_DESCENDING = 8u,
        [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_everything_sort_by_type_name_ascending))]
        TYPE_NAME_ASCENDING = 9u,
        [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_everything_sort_by_type_name_descending))]
        TYPE_NAME_DESCENDING = 10u,
        [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_everything_sort_by_date_created_ascending))]
        DATE_CREATED_ASCENDING = 11u,
        [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_everything_sort_by_date_created_descending))]
        DATE_CREATED_DESCENDING = 12u,
        [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_everything_sort_by_date_modified_ascending))]
        DATE_MODIFIED_ASCENDING = 13u,
        [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_everything_sort_by_date_modified_descending))]
        DATE_MODIFIED_DESCENDING = 14u,
        [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_everything_sort_by_attributes_ascending))]
        ATTRIBUTES_ASCENDING = 15u,
        [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_everything_sort_by_attributes_descending))]
        ATTRIBUTES_DESCENDING = 16u,
        [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_everything_sort_by_file_list_filename_ascending))]
        FILE_LIST_FILENAME_ASCENDING = 17u,
        [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_everything_sort_by_file_list_filename_descending))]
        FILE_LIST_FILENAME_DESCENDING = 18u,
        [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_everything_sort_by_run_count_descending))]
        RUN_COUNT_DESCENDING = 20u,
        [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_everything_sort_by_date_recently_changed_ascending))]
        DATE_RECENTLY_CHANGED_ASCENDING = 21u,
        [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_everything_sort_by_date_recently_changed_descending))]
        DATE_RECENTLY_CHANGED_DESCENDING = 22u,
        [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_everything_sort_by_date_accessed_ascending))]
        DATE_ACCESSED_ASCENDING = 23u,
        [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_everything_sort_by_date_accessed_descending))]
        DATE_ACCESSED_DESCENDING = 24u,
        [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_everything_sort_by_date_run_ascending))]
        DATE_RUN_ASCENDING = 25u,
        [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_everything_sort_by_date_run_descending))]
        DATE_RUN_DESCENDING = 26u
    }
}
