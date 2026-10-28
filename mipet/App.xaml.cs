using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace mipet
{
    public partial class App : Application
    {
        private Mutex? _mutex;
        private bool _ownsMutex;
        private readonly Dictionary<string, MainWindow> _windows = new();
        private List<PetSettings> _pets = new();
        private CharacterListWindow? _list;
        private DispatcherTimer? _priorityTimer;

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

            _priorityTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _priorityTimer.Tick += (_, _) => ApplyPetPriority();
            _priorityTimer.Start();
            ApplyPetPriority();

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
            window.Activate();
            Save();
            ApplyPetPriority();
            _list?.RefreshPets(_pets);
        }

        public void HidePet(PetSettings pet)
        {
            pet.IsVisible = false;
            if (_windows.TryGetValue(pet.Id, out var window)) window.Hide();
            Save();
            ApplyPetPriority();
            _list?.RefreshPets(_pets);
            if (!_pets.Any(p => p.IsVisible)) Shutdown();
        }

        public void AddPet()
        {
            var pet = new PetSettings { Name = $"캐릭터 {_pets.Count + 1}", IsVisible = true };
            _pets.Add(pet);
            Save();
            ShowPet(pet);
        }

        public void RenamePet(PetSettings pet, string name)
        {
            if (!string.IsNullOrWhiteSpace(name)) pet.Name = name.Trim();
            Save();
        }

        public void DeletePet(PetSettings pet)
        {
            if (!_pets.Contains(pet)) return;

            var remainingPets = _pets.Where(p => p.Id != pet.Id).ToList();
            if (_windows.Remove(pet.Id, out var window)) window.Close();
            _pets.Remove(pet);
            Save();
            DeleteUnusedProfileImages(pet, remainingPets);
            _list?.RefreshPets(_pets);
            ApplyPetPriority();
        }

        private static void DeleteUnusedProfileImages(PetSettings deletedPet, IEnumerable<PetSettings> remainingPets)
        {
            string imageFolder = Path.GetFullPath(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "mipet", "images"));
            string imageRoot = Path.TrimEndingDirectorySeparator(imageFolder) + Path.DirectorySeparatorChar;

            foreach (string? path in GetImagePaths(deletedPet))
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(path)) continue;
                    string fullPath = Path.GetFullPath(path);
                    if (!fullPath.StartsWith(imageRoot, StringComparison.OrdinalIgnoreCase)) continue;

                    bool stillUsed = remainingPets
                        .SelectMany(GetImagePaths)
                        .Any(other => string.Equals(other, fullPath, StringComparison.OrdinalIgnoreCase));
                    if (!stillUsed && File.Exists(fullPath)) File.Delete(fullPath);
                }
                catch { }
            }
        }

        private static IEnumerable<string?> GetImagePaths(PetSettings pet)
        {
            yield return pet.SittingBodyPath;
            yield return pet.HeadOpenPath;
            yield return pet.HeadClosedPath;
            yield return pet.GrabbedImagePath;
            yield return pet.FallingImagePath;
            yield return pet.LandedImagePath;
        }

        public void ShowList()
        {
            EnsureList();
            _list!.RefreshPets(_pets);
            _list.Show();
            _list.Activate();
        }

        private void EnsureList()
        {
            if (_list != null) return;
            _list = new CharacterListWindow(
                _pets, ShowPet, HidePet, AddPet, RenamePet, DeletePet,
                () => { Save(); ApplyPetPriority(); });
        }

        private void ApplyPetPriority()
        {
            if (_list?.IsVisible == true) return;
            // 목록 첫 캐릭터가 맨 위에 오도록 뒤에서부터 차례로 올립니다.
            foreach (var pet in _pets.AsEnumerable().Reverse())
            {
                if (pet.IsVisible &&
                    _windows.TryGetValue(pet.Id, out var window) &&
                    window.IsVisible)
                {
                    TopmostHelper.BringToFront(window);
                }
            }
        }

        private void Save() => PetSettings.SaveAll(_pets);

        protected override void OnExit(ExitEventArgs e)
        {
            _priorityTimer?.Stop();
            Save();
            if (_ownsMutex) _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            base.OnExit(e);
        }
    }
}