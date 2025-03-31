using System.Collections.Generic;
using System.Windows.Controls;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Resources.Controls
{
    public partial class InstalledPluginSearchDelayCombobox
    {
        public InstalledPluginSearchDelayCombobox()
        {
            InitializeComponent();
            LoadDelayOptions();
            Loaded += InstalledPluginSearchDelayCombobox_Loaded;
        }

        private void InstalledPluginSearchDelayCombobox_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is ViewModel.PluginViewModel viewModel)
            {
                // 초기 값 설정
                int currentDelayMs = GetCurrentDelayMs(viewModel);
                foreach (DelayOption option in cbDelay.Items)
                {
                    if (option.Value == currentDelayMs)
                    {
                        cbDelay.SelectedItem = option;
                        break;
                    }
                }
            }
        }

        private int GetCurrentDelayMs(ViewModel.PluginViewModel viewModel)
        {
            // SearchDelayTime enum 값을 int로 변환
            SearchDelayTime? delayTime = viewModel.PluginPair.Metadata.SearchDelayTime;
            if (delayTime.HasValue)
            {
                return (int)delayTime.Value;
            }
            return 0; // 기본값
        }

        private void LoadDelayOptions()
        {
            // 검색 지연 시간 옵션들 (SearchDelayTime enum 값에 맞춰야 함)
            var delayOptions = new List<DelayOption>
            {
                new DelayOption { Display = "0 ms", Value = 0 },
                new DelayOption { Display = "50 ms", Value = 50 },
                new DelayOption { Display = "100 ms", Value = 100 },
                new DelayOption { Display = "150 ms", Value = 150 },
                new DelayOption { Display = "200 ms", Value = 200 },
                new DelayOption { Display = "250 ms", Value = 250 },
                new DelayOption { Display = "300 ms", Value = 300 },
                new DelayOption { Display = "350 ms", Value = 350 },
                new DelayOption { Display = "400 ms", Value = 400 },
                new DelayOption { Display = "450 ms", Value = 450 },
                new DelayOption { Display = "500 ms", Value = 500 },
            };

            cbDelay.ItemsSource = delayOptions;
        }

        private void CbDelay_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is ViewModel.PluginViewModel viewModel && cbDelay.SelectedItem is DelayOption selectedOption)
            {
                // int 값을 SearchDelayTime enum으로 변환
                int delayValue = selectedOption.Value;
                viewModel.PluginPair.Metadata.SearchDelayTime = (SearchDelayTime)delayValue;

                // 설정 저장
                //How to save?
            }
        }
    }

    public class DelayOption
    {
        public string Display { get; set; }
        public int Value { get; set; }
    }
}
