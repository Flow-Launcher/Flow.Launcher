using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Flow.Launcher.Core.Resource;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Flow.Launcher.Resources.Pages
{
    public partial class WelcomePage3 : Page
    {
        public WelcomePage3()
        {
            InitializeComponent();
        }
       
        public string strHotkey { get; set; }
        public string strHotkeyAction { get; set; }
        private List<WelcomePage3> HotkeyListData()
        {
            List<WelcomePage3> list = new List<WelcomePage3>();

            list.Add(new WelcomePage3()
            {
                strHotkey = string.Format(InternationalizationManager.Instance.GetTranslation("Hotkey01")),
                strHotkeyAction = string.Format(InternationalizationManager.Instance.GetTranslation("Hotkey01Action"))
            });
            list.Add(new WelcomePage3()
            {
                strHotkey = string.Format(InternationalizationManager.Instance.GetTranslation("Hotkey02")),
                strHotkeyAction = string.Format(InternationalizationManager.Instance.GetTranslation("Hotkey02Action"))
            });
            list.Add(new WelcomePage3()
            {
                strHotkey = string.Format(InternationalizationManager.Instance.GetTranslation("Hotkey03")),
                strHotkeyAction = string.Format(InternationalizationManager.Instance.GetTranslation("Hotkey03Action"))
            });
            list.Add(new WelcomePage3()
            {
                strHotkey = string.Format(InternationalizationManager.Instance.GetTranslation("Hotkey04")),
                strHotkeyAction = string.Format(InternationalizationManager.Instance.GetTranslation("Hotkey04Action"))
            });
            list.Add(new WelcomePage3()
            {
                strHotkey = string.Format(InternationalizationManager.Instance.GetTranslation("Hotkey05")),
                strHotkeyAction = string.Format(InternationalizationManager.Instance.GetTranslation("Hotkey05Action"))
            });
            list.Add(new WelcomePage3()
            {
                strHotkey = string.Format(InternationalizationManager.Instance.GetTranslation("Hotkey07")),
                strHotkeyAction = string.Format(InternationalizationManager.Instance.GetTranslation("Hotkey07Action"))
            });
            list.Add(new WelcomePage3()
            {
                strHotkey = string.Format(InternationalizationManager.Instance.GetTranslation("Hotkey08")),
                strHotkeyAction = string.Format(InternationalizationManager.Instance.GetTranslation("Hotkey08Action"))
            });

            return list;
        }

    }
}
