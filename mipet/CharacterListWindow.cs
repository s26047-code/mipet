using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace mipet
{
    public sealed class CharacterListWindow : Window
    {
        private readonly StackPanel _items = new();
        private readonly List<PetSettings> _pets;
        private readonly Action<PetSettings> _show;
        private readonly Action<PetSettings> _hide;
        private readonly Action _add;
        private readonly Action<PetSettings, string> _rename;
        private readonly Action<PetSettings> _delete;
        private readonly Action _saveOrder;
        private Point _dragStart;

        public CharacterListWindow(
            List<PetSettings> pets,
            Action<PetSettings> show,
            Action<PetSettings> hide,
            Action add,
            Action<PetSettings, string> rename,
            Action<PetSettings> delete,
            Action saveOrder)
        {
            _pets = pets;
            _show = show;
            _hide = hide;
            _add = add;
            _rename = rename;
            _delete = delete;
            _saveOrder = saveOrder;

            Title = "캐릭터 리스트";
            Width = 500;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;

            var root = new StackPanel { Margin = new Thickness(18) };
            root.Children.Add(new TextBlock
            {
                Text = "캐릭터 리스트",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 12)
            });
            root.Children.Add(_items);

            var addButton = new Button
            {
                Content = "캐릭터 추가하기",
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 12, 0, 0)
            };
            addButton.Click += (_, _) => _add();
            root.Children.Add(addButton);

            Content = root;
            RefreshPets(_pets);
            Closing += (_, e) => { e.Cancel = true; Hide(); };
            Deactivated += (_, _) => Hide();
        }

        public void RefreshPets(IEnumerable<PetSettings> pets)
        {
            _items.Children.Clear();

            foreach (var pet in pets)
            {
                var row = new Grid
                {
                    Margin = new Thickness(0, 3, 0, 3),
                    Background = Brushes.Transparent,
                    AllowDrop = true
                };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var nameArea = new Grid { Margin = new Thickness(0, 0, 8, 0) };
                var nameText = new TextBlock
                {
                    Text = pet.Name,
                    VerticalAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(5, 5, 5, 5),
                    Cursor = Cursors.Hand,
                    ToolTip = "클릭: 이름 수정 / 누른 채 위아래로 드래그: 순서 변경"
                };
                var nameEditor = new TextBox
                {
                    Text = pet.Name,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Visibility = Visibility.Collapsed
                };

                bool dragOccurred = false;

                void CommitName()
                {
                    if (nameEditor.Visibility != Visibility.Visible) return;
                    _rename(pet, nameEditor.Text);
                    nameText.Text = pet.Name;
                    nameEditor.Visibility = Visibility.Collapsed;
                    nameText.Visibility = Visibility.Visible;
                }

                nameText.MouseLeftButtonDown += (_, e) =>
                {
                    _dragStart = e.GetPosition(nameText);
                    dragOccurred = false;
                };
                nameText.MouseMove += (_, e) =>
                {
                    if (e.LeftButton != MouseButtonState.Pressed) return;

                    Point current = e.GetPosition(nameText);
                    if (Math.Abs(current.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
                        Math.Abs(current.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
                        return;

                    dragOccurred = true;
                    DragDrop.DoDragDrop(nameText, pet, DragDropEffects.Move);
                    e.Handled = true;
                };
                nameText.MouseLeftButtonUp += (_, e) =>
                {
                    if (dragOccurred)
                    {
                        dragOccurred = false;
                        return;
                    }

                    nameEditor.Text = pet.Name;
                    nameText.Visibility = Visibility.Collapsed;
                    nameEditor.Visibility = Visibility.Visible;
                    nameEditor.Focus();
                    nameEditor.SelectAll();
                    e.Handled = true;
                };

                nameEditor.KeyDown += (_, e) =>
                {
                    if (e.Key == Key.Enter)
                    {
                        CommitName();
                        Keyboard.ClearFocus();
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Escape)
                    {
                        nameEditor.Text = pet.Name;
                        nameEditor.Visibility = Visibility.Collapsed;
                        nameText.Visibility = Visibility.Visible;
                        Keyboard.ClearFocus();
                        e.Handled = true;
                    }
                };
                nameEditor.LostFocus += (_, _) => CommitName();

                nameArea.Children.Add(nameText);
                nameArea.Children.Add(nameEditor);
                Grid.SetColumn(nameArea, 0);
                row.Children.Add(nameArea);

                row.DragOver += (_, e) =>
                {
                    e.Effects = e.Data.GetDataPresent(typeof(PetSettings))
                        ? DragDropEffects.Move
                        : DragDropEffects.None;
                    e.Handled = true;
                };
                row.Drop += (_, e) =>
                {
                    if (e.Data.GetData(typeof(PetSettings)) is not PetSettings dragged) return;

                    int fromIndex = _pets.IndexOf(dragged);
                    int toIndex = _pets.IndexOf(pet);
                    if (fromIndex < 0 || toIndex < 0 || fromIndex == toIndex) return;

                    _pets.RemoveAt(fromIndex);
                    _pets.Insert(Math.Min(toIndex, _pets.Count), dragged);
                    _saveOrder();
                    RefreshPets(_pets);
                    e.Handled = true;
                };

                var toggle = new Button
                {
                    Content = pet.IsVisible ? "숨기기" : "보이기",
                    MinWidth = 76,
                    Padding = new Thickness(8, 3, 8, 3)
                };
                toggle.Click += (_, _) =>
                {
                    if (pet.IsVisible) _hide(pet);
                    else _show(pet);
                };
                Grid.SetColumn(toggle, 1);
                row.Children.Add(toggle);

                var delete = new Button
                {
                    Content = "삭제",
                    MinWidth = 60,
                    Padding = new Thickness(8, 3, 8, 3),
                    Margin = new Thickness(6, 0, 0, 0)
                };
                delete.Click += (_, _) =>
                {
                    var answer = MessageBox.Show(
                        this,
                        $"'{pet.Name}' 캐릭터를 삭제할까요?\n설정과 앱에 복사된 이미지 파일이 삭제됩니다.",
                        "캐릭터 삭제 확인",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    if (answer == MessageBoxResult.Yes) _delete(pet);
                };
                Grid.SetColumn(delete, 2);
                row.Children.Add(delete);

                _items.Children.Add(row);
            }
        }
    }
}