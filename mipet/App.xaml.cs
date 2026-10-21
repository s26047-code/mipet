using System.Threading;
using System.Windows;

namespace mipet
{
    public partial class App : Application
    {
        private Mutex? _mutex;
        private bool _ownsMutex;
        private readonly Dictionary<string, MainWindow> _windows = new();
        private List<PetSettings> _pets = new();
        private CharacterListWindow? _list;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            _mutex = new Mutex(true, "mipet.SingleInstance", out bool first);
            if (!first) { _mutex.Dispose(); _mutex = null; Shutdown(); return; }
            _ownsMutex = true;
            _pets = PetSettings.LoadAll();
            if (_pets.Count == 0) _pets.Add(new PetSettings { Name = "캐릭터 1" });
            PetSettings.SaveAll(_pets);
            foreach (var pet in _pets.Where(p => p.IsVisible).ToArray()) ShowPet(pet);
            EnsureList();
            if (!_pets.Any(p => p.IsVisible)) ShowList();
        }

        public void ShowPet(PetSettings pet)
        {
            pet.IsVisible = true;
            if (!_windows.TryGetValue(pet.Id, out var window))
            {
                window = new MainWindow(pet, Save, ShowList, HidePet);
                _windows[pet.Id] = window;
                window.Closed += (_, _) => _windows.Remove(pet.Id);
            }
            if (!window.IsVisible) window.Show();
            window.Activate(); Save(); _list?.RefreshPets(_pets);
        }
        public void HidePet(PetSettings pet)
        {
            pet.IsVisible = false;
            if (_windows.TryGetValue(pet.Id, out var window)) window.Hide();
            Save(); _list?.RefreshPets(_pets);
            if (!_pets.Any(p => p.IsVisible)) Shutdown();
        }
        public void AddPet()
        {
            var pet = new PetSettings { Name = $"캐릭터 {_pets.Count + 1}", IsVisible = true };
            _pets.Add(pet); Save(); ShowPet(pet);
        }
        public void RenamePet(PetSettings pet, string name)
        {
            if (!string.IsNullOrWhiteSpace(name)) pet.Name = name.Trim();
            Save();
        }
        public void ShowList()
        {
            EnsureList(); _list!.RefreshPets(_pets); _list.Show(); _list.Activate();
        }
        private void EnsureList()
        {
            if (_list != null) return;
            _list = new CharacterListWindow(_pets, ShowPet, HidePet, AddPet, RenamePet);
        }
        private void Save() => PetSettings.SaveAll(_pets);
        protected override void OnExit(ExitEventArgs e)
        {
            Save();
            if (_ownsMutex) _mutex?.ReleaseMutex();
            _mutex?.Dispose(); base.OnExit(e);
        }
    }
}
