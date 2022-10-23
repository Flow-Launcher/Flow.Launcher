using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Flow.Launcher.Core.Resource;

namespace Flow.Launcher.Converters
{
    public class TextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var ID = value.ToString();
            switch(ID)
            {
                case "NewRelease":
                    return InternationalizationManager.Instance.GetTranslation("pluginStore_NewRelease");
                    break;
                case "RecentlyUpdated":
                    return InternationalizationManager.Instance.GetTranslation("pluginStore_RecentlyUpdated");
                    break;
                case "None":
                    return InternationalizationManager.Instance.GetTranslation("pluginStore_None");
                    break;
                case "installed":
                    return InternationalizationManager.Instance.GetTranslation("pluginStore_Installed");
                    break;
                default:
                    return ID;
                    break;
            }

            
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, CultureInfo culture) => throw new System.InvalidOperationException();
    }
}
