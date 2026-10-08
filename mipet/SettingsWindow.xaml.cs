using System;
using System.Windows;

namespace mipet
{
    public partial class SettingsWindow : Window
    {
        private PetSettings _settings;
        private Action _onChanged;
        private bool _loading = true;

        public SettingsWindow(PetSettings settings, Action onChanged)
        {
            InitializeComponent();
            _settings = settings;
            _onChanged = onChanged;
            LoadToControls();
            _loading = false;
        }

        private void LoadToControls()
        {
            ScaleSlider.Value = _settings.Scale;
            FlingSlider.Value = _settings.FlingPower;
            MaxAngleSlider.Value = _settings.MaxAngle;
            ReverseCheck.IsChecked = _settings.ReverseFling;
            AutoStartCheck.IsChecked = AutoStart.IsEnabled();   // 레지스트리의 실제 상태
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading) return;

            _settings.Scale = ScaleSlider.Value;
            _settings.FlingPower = FlingSlider.Value;
            _settings.MaxAngle = MaxAngleSlider.Value;
            _onChanged();
        }

        private void Check_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;

            _settings.ReverseFling = ReverseCheck.IsChecked == true;
            _onChanged();
        }

        private void AutoStart_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;

            AutoStart.SetEnabled(AutoStartCheck.IsChecked == true);
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            var d = new PetSettings();
            _settings.Scale = d.Scale;
            _settings.FlingPower = d.FlingPower;
            _settings.MaxAngle = d.MaxAngle;
            _settings.ReverseFling = d.ReverseFling;

            _loading = true;
            LoadToControls();
            _loading = false;
            _onChanged();
        }
    }
}