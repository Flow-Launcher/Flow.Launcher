﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.Everything.Everything
{
    public enum SortOption : uint
    {
        NAME_ASCENDING = 1u,
        NAME_DESCENDING = 2u,
        PATH_ASCENDING = 3u,
        PATH_DESCENDING = 4u,
        SIZE_ASCENDING = 5u,
        SIZE_DESCENDING = 6u,
        EXTENSION_ASCENDING = 7u,
        EXTENSION_DESCENDING = 8u,
        TYPE_NAME_ASCENDING = 9u,
        TYPE_NAME_DESCENDING = 10u,
        DATE_CREATED_ASCENDING = 11u,
        DATE_CREATED_DESCENDING = 12u,
        DATE_MODIFIED_ASCENDING = 13u,
        DATE_MODIFIED_DESCENDING = 14u,
        ATTRIBUTES_ASCENDING = 15u,
        ATTRIBUTES_DESCENDING = 16u,
        FILE_LIST_FILENAME_ASCENDING = 17u,
        FILE_LIST_FILENAME_DESCENDING = 18u,
        RUN_COUNT_ASCENDING = 19u,
        RUN_COUNT_DESCENDING = 20u,
        DATE_RECENTLY_CHANGED_ASCENDING = 21u,
        DATE_RECENTLY_CHANGED_DESCENDING = 22u,
        DATE_ACCESSED_ASCENDING = 23u,
        DATE_ACCESSED_DESCENDING = 24u,
        DATE_RUN_ASCENDING = 25u,
        DATE_RUN_DESCENDING = 26u
    }
}
