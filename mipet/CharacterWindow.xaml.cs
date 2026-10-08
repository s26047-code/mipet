using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace mipet
{
    public partial class CharacterWindow : Window
    {
        private PetSettings _settings;
        private Action _onChanged;

        // 고른 이미지를 복사해서 보관하는 폴더: %AppData%\mipet\images
        private static string ImageFolder
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "mipet", "images");
            }
        }

        public CharacterWindow(PetSettings settings, Action onChanged)
        {
            InitializeComponent();
            _settings = settings;
            _onChanged = onChanged;
            UpdateLabels();
        }

        private void UpdateLabels()
        {
            SittingLabel.Text = Short(_settings.SittingBodyPath);
            HeadOpenLabel.Text = Short(_settings.HeadOpenPath);
            HeadClosedLabel.Text = Short(_settings.HeadClosedPath);
            GrabbedLabel.Text = Short(_settings.GrabbedImagePath);
            FallingLabel.Text = Short(_settings.FallingImagePath);
            LandedLabel.Text = Short(_settings.LandedImagePath);
        }

        private string Short(string? path)
        {
            return string.IsNullOrWhiteSpace(path) ? "(기본)" : "사용자 이미지";
        }

        private string? GetPath(string key)
        {
            switch (key)
            {
                case "Sitting": return _settings.SittingBodyPath;
                case "HeadOpen": return _settings.HeadOpenPath;
                case "HeadClosed": return _settings.HeadClosedPath;
                case "Grabbed": return _settings.GrabbedImagePath;
                case "Falling": return _settings.FallingImagePath;
                case "Landed": return _settings.LandedImagePath;
            }
            return null;
        }

        private void Pick_Click(object sender, RoutedEventArgs e)
        {
            string key = (string)((Button)sender).Tag;

            var dialog = new OpenFileDialog
            {
                Title = "이미지 선택",
                Filter = "이미지 파일|*.png;*.jpg;*.jpeg;*.bmp;*.gif|모든 파일|*.*"
            };

            if (dialog.ShowDialog(this) != true) return;

            try
            {
                // 원본을 앱 폴더로 복사 (원본을 옮기거나 지워도 유지되게)
                Directory.CreateDirectory(ImageFolder);
                string ext = Path.GetExtension(dialog.FileName);
                string newPath = Path.Combine(ImageFolder,
                    $"{key}_{DateTime.Now:yyyyMMddHHmmssfff}{ext}");
                File.Copy(dialog.FileName, newPath, true);

                SetPath(key, newPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("이미지를 저장하지 못했습니다.\n" + ex.Message, "mipet");
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            SetPath((string)((Button)sender).Tag, null);
        }

        private void SetPath(string key, string? path)
        {
            string? old = GetPath(key);

            switch (key)
            {
                case "Sitting": _settings.SittingBodyPath = path; break;
                case "HeadOpen": _settings.HeadOpenPath = path; break;
                case "HeadClosed": _settings.HeadClosedPath = path; break;
                case "Grabbed": _settings.GrabbedImagePath = path; break;
                case "Falling": _settings.FallingImagePath = path; break;
                case "Landed": _settings.LandedImagePath = path; break;
            }

            UpdateLabels();
            _onChanged();          // 화면에 반영 (새 이미지를 읽음)
            _settings.Save();      // 바로 저장

            DeleteOldCopy(old);    // 이전에 복사해 둔 파일 정리
        }

        private void ResetAll_Click(object sender, RoutedEventArgs e)
        {
            string?[] olds =
            {
                _settings.SittingBodyPath, _settings.HeadOpenPath, _settings.HeadClosedPath,
                _settings.GrabbedImagePath, _settings.FallingImagePath, _settings.LandedImagePath
            };

            _settings.SittingBodyPath = null;
            _settings.HeadOpenPath = null;
            _settings.HeadClosedPath = null;
            _settings.GrabbedImagePath = null;
            _settings.FallingImagePath = null;
            _settings.LandedImagePath = null;

            UpdateLabels();
            _onChanged();
            _settings.Save();

            foreach (var o in olds) DeleteOldCopy(o);
        }

        // 앱 폴더에 복사해 둔 파일만 삭제 (사용자의 원본 파일은 건드리지 않음)
        private void DeleteOldCopy(string? path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) return;
                string full = Path.GetFullPath(path);
                if (full.StartsWith(Path.GetFullPath(ImageFolder), StringComparison.OrdinalIgnoreCase)
                    && File.Exists(full))
                {
                    File.Delete(full);
                }
            }
            catch { }
        }
    }
}