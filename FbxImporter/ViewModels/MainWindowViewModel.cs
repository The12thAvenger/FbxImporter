using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveHistory;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SoulsFormats;
using FbxDataExtractor;

namespace FbxImporter.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public enum GetPathMode
        {
            Open,
            Save
        }

        private readonly IHistory _history;

        public MainWindowViewModel()
        {
            _history = new StackHistory();

            IObservable<bool> isFlverLoaded = this.WhenAnyValue(x => x.Flver).Select(x => x is not null);
            IObservable<bool> isFbxLoaded = this.WhenAnyValue(x => x.Fbx).Select(x => x is not null);
            IObservable<bool> canAddToFlver = isFlverLoaded.Zip(isFbxLoaded, (isFlver, isFbx) => isFlver && isFbx);
            SaveFlverCommand = ReactiveCommand.Create(SaveFlver, isFlverLoaded);
            OpenFlverCommand = ReactiveCommand.CreateFromTask(OpenFlverAsync);
            SaveFlverAsCommand = ReactiveCommand.CreateFromTask(SaveFlverAsAsync, isFlverLoaded);
            ImportFbxCommand = ReactiveCommand.CreateFromTask(ImportFbxAsync);
            AddToFlverCommand = ReactiveCommand.CreateFromTask(AddToFlverAsync, canAddToFlver);

            ImportFbxCommand.ThrownExceptions.Subscribe(e =>
            {
                Logger.Log(e is InvalidDataException ? e.Message : e.ToString());
            });
            AddToFlverCommand.ThrownExceptions.Subscribe(e =>
            {
                Logger.Log(e is InvalidDataException ? e.Message : e.ToString());
            });

            UndoCommand = ReactiveCommand.Create(_history.Undo, _history.CanUndo);
            RedoCommand = ReactiveCommand.Create(_history.Redo, _history.CanRedo);

            this.WhenAnyValue(x => x.Flver,
                y => y.Fbx!.SelectedMesh,
                (x, y) => x is not null && y is not null);
        }

        [Reactive] public FlverViewModel? Flver { get; set; }

        [Reactive] public FbxSceneDataViewModel? Fbx { get; set; }

        public MeshImportOptions? MeshImportOptionsCache { get; set; }

        private string? FlverPath { get; set; }

        public ObservableCollection<string> Log => Logger.Instance.Lines;

        public Interaction<GetFilePathArgs, string?> GetFilePath { get; } = new();

        public Interaction<Unit, string?> GetTargetGame { get; } = new();

        public Interaction<MeshImportOptionsViewModel, MeshImportOptions?> GetMeshImportOptions { get; } = new();

        public ReactiveCommand<Unit, Unit> SaveFlverCommand { get; }

        public ReactiveCommand<Unit, Unit> OpenFlverCommand { get; }

        public ReactiveCommand<Unit, Unit> SaveFlverAsCommand { get; }

        public ReactiveCommand<Unit, Unit> ImportFbxCommand { get; }

        public ReactiveCommand<Unit, Unit> AddToFlverCommand { get; }

        public ReactiveCommand<Unit, bool> RedoCommand { get; }

        public ReactiveCommand<Unit, bool> UndoCommand { get; }

        private async Task AddToFlverAsync()
        {
            MeshImportOptionsViewModel optionsViewModel =
                new(Fbx!.SelectedMesh!.MTD, Flver!.MaterialInfoBank, MeshImportOptionsCache);
            MeshImportOptions? options = await GetMeshImportOptions.Handle(optionsViewModel);
            if (options is null) return;

            MeshImportOptionsCache = options;

            AddToFlverWithHistory(options);
        }

        private void AddToFlverWithHistory(MeshImportOptions options)
        {
            int meshIndex = Flver!.Meshes.Count;
            int boneIndex = Flver.Flver.Bones.Count;
            bool addedBone = Flver.Flver.Bones.All(x => x.Name != Fbx!.SelectedMesh!.Name) && options.CreateDefaultBone;

            _history.Snapshot(Undo, Redo);
            Redo();

            void Undo()
            {
                Flver.Meshes.RemoveAt(meshIndex);
                if (addedBone)
                {
                    Flver.Flver.Bones.RemoveAt(boneIndex);
                }
            }

            void Redo()
            {
                Flver.Meshes.Insert(meshIndex, Fbx!.SelectedMesh!.ToFlverMesh(Flver.Flver, options));
            }
        }

        private void SaveFlver()
        {
            Flver!.Write(FlverPath!);
            Logger.Log("Saved File");
        }

        private async Task ImportFbxAsync()
        {
            List<FileTypeFilter> filters = new()
            {
                new FileTypeFilter("Autodesk Fbx Files", new List<string> { "fbx" }),
                new FileTypeFilter("All Files", new List<string> { "*" })
            };
            GetFilePathArgs args = new("Import Fbx", filters, GetPathMode.Open);
            string? fbxPath = await GetFilePath.Handle(args);
            if (fbxPath is null) return;

            Logger.Log($"Importing {Path.GetFileName(fbxPath)}...");
            List<FbxMeshDataViewModel> meshes;
            try
            {
                meshes = await Task.Run(() =>
                    FbxMeshData.Import(fbxPath).Select(x => new FbxMeshDataViewModel(x)).ToList());
            }
            catch (Exception)
            {
                Logger.Log("Fbx Import Failed");
                throw;
            }

            FbxSceneDataViewModel scene = new()
            {
                MeshData = new ObservableCollection<FbxMeshDataViewModel>(meshes)
            };

            Logger.Log("Import successful.");

            Fbx = scene;
        }

        private async Task OpenFlverAsync()
        {
            List<FileTypeFilter> filters = new()
            {
                new FileTypeFilter("Flver Files", new List<string> { "flver" }),
                new FileTypeFilter("All Files", new List<string> { "*" })
            };
            GetFilePathArgs args = new("Open Flver", filters, GetPathMode.Open);
            string? flverPath = await GetFilePath.Handle(args);
            if (flverPath is null) return;
        
            FLVER2? flver = await Task.Run(() =>
            {

                if (FLVER2.IsRead(flverPath, out FLVER2 flver)) return flver;
                Logger.Log($"{flverPath} is not a flver file");
                return null;
            });
            if (flver is null) return;

            FlverViewModel.FlverVersion? optionalVersion = flver.Header.Version switch
            {
                131092 => FlverViewModel.FlverVersion.DS3,
                131098 when flver.Materials.Any(x => x.MTD.Contains(".matxml")) => FlverViewModel.FlverVersion.ER,
                131098 => await GetTargetGame.Handle(Unit.Default) switch
                {
                    "Elden Ring" => FlverViewModel.FlverVersion.ER,
                    "Sekiro" => FlverViewModel.FlverVersion.SDT,
                    null => null,
                    _ => throw new ArgumentOutOfRangeException()
                },
                131099 => FlverViewModel.FlverVersion.AC6,
                _ => throw new InvalidDataException("Invalid Flver Version")
            };

            if (optionalVersion is not { } version)
            {
                return;
            }

            _history.Clear();
            Flver = new FlverViewModel(flver, version, _history);
            FlverPath = flverPath;
        }

        private async Task SaveFlverAsAsync()
        {
            List<FileTypeFilter> filters = new()
            {
                new FileTypeFilter("Flver Files", new List<string> { "flver" }),
                new FileTypeFilter("All Files", new List<string> { "*" })
            };
            GetFilePathArgs args = new("Save Flver As...", filters, GetPathMode.Save);
            string? flverPath = await GetFilePath.Handle(args);
            if (flverPath is null) return;
            Flver!.Write(flverPath);
            FlverPath = flverPath;
        }

        public record GetFilePathArgs(string Title, List<FileTypeFilter> Filters, GetPathMode Mode);

        public record FileTypeFilter(string Name, List<string> Extensions);
    }
}