using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace mipet
{
    public sealed class CharacterListWindow : Window
    {
        private readonly StackPanel _items = new();
        private readonly Action<PetSettings> _show;
        private readonly Action<PetSettings> _hide;
        private readonly Action _add;
        private readonly Action<PetSettings, string> _rename;
        private readonly Action<PetSettings> _delete;

        public CharacterListWindow(List<PetSettings> pets, Action<PetSettings> show, Action<PetSettings> hide, Action add, Action<PetSettings, string> rename, Action<PetSettings> delete)
        {
            Title = "캐릭터 리스트"; Width = 500; SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen; Topmost = true; ResizeMode = ResizeMode.NoResize;
            _show = show; _hide = hide; _add = add; _rename = rename; _delete = delete;
            var root = new StackPanel { Margin = new Thickness(18) };
            root.Children.Add(new TextBlock { Text = "캐릭터 리스트", FontSize = 18, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 12) });
            root.Children.Add(_items);
            var addButton = new Button { Content = "캐릭터 추가하기", Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(0, 12, 0, 0) };
            addButton.Click += (_, _) => _add(); root.Children.Add(addButton); Content = root;
            RefreshPets(pets);
            Closing += (_, e) => { e.Cancel = true; Hide(); };
        }

        public void RefreshPets(IEnumerable<PetSettings> pets)
        {
            _items.Children.Clear();
            int i = 0;
            foreach (var pet in pets)
            {
                i++;
                var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var name = new TextBox { Text = pet.Name, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), ToolTip = "이름을 수정하려면 입력한 뒤 Enter 또는 다른 곳을 클릭하세요." };
                name.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) { _rename(pet, name.Text); Keyboard.ClearFocus(); } };
                name.LostFocus += (_, _) => _rename(pet, name.Text);
                Grid.SetColumn(name, 0); row.Children.Add(name);
                var toggle = new Button { Content = pet.IsVisible ? "숨기기" : "보이기", MinWidth = 76, Padding = new Thickness(8, 3, 8, 3) };
                toggle.Click += (_, _) => { if (pet.IsVisible) _hide(pet); else _show(pet); };
                Grid.SetColumn(toggle, 1); row.Children.Add(toggle); _items.Children.Add(row);

                var delete = new Button { Content = "삭제", MinWidth = 60, Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(6, 0, 0, 0) };
                delete.Click += (_, _) =>
                {
                    var answer = MessageBox.Show(this,
                        $"'{pet.Name}' 캐릭터를 삭제할까요?\n설정과 앱에 복사된 이미지 파일이 삭제됩니다.",
                        "캐릭터 삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (answer == MessageBoxResult.Yes) _delete(pet);
                };
                Grid.SetColumn(delete, 2); row.Children.Add(delete);
            }
        }
    }
}
