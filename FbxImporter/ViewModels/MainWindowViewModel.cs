using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ReactiveHistory;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SoulsFormats;
using FbxDataExtractor;

namespace FbxImporter.ViewModels
{
    public class MainWindowViewModel : ViewModelBase, ILoggable
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

            IObservable<bool> isFlverMeshSelected = this.WhenAnyValue(x => x.Flver!.SelectedMesh).Select(x => x is { });
            SaveFlverCommand = ReactiveCommand.Create(SaveFlver);
            OpenFlverCommand = ReactiveCommand.CreateFromTask(OpenFlverAsync);
            SaveFlverAsCommand = ReactiveCommand.CreateFromTask(SaveFlverAsAsync);
            ImportFbxCommand = ReactiveCommand.CreateFromTask(ImportFbxAsync);
            AddToFlverCommand = ReactiveCommand.CreateFromTask(AddToFlverAsync);

            UndoCommand = ReactiveCommand.Create(_history.Undo);
            RedoCommand = ReactiveCommand.Create(_history.Redo);

            this.WhenAnyValue(x => x.Flver,
                              y => y.Fbx!.SelectedMesh,
                              (x, y) => x is not null && y is not null)
               .ToPropertyEx(this, x => x.CanAddToFlver, initialValue: false);

            _history.CanUndo.ToPropertyEx(this, x => x.CanUndo);
            _history.CanRedo.ToPropertyEx(this, x => x.CanRedo);

            IsImporting = false;
            
            Logger.CurrentLoggable = this;
        }

        [Reactive] public FlverViewModel? Flver { get; set; }

        [Reactive] public FbxSceneDataViewModel? Fbx { get; set; }

        private string? FlverPath { get; set; }

        [ObservableAsProperty] public extern bool CanAddToFlver { get; }

        [ObservableAsProperty] public extern bool CanUndo { get; }

        [ObservableAsProperty] public extern bool CanRedo { get; }

        [Reactive] public bool IsImporting { get; set; }

        public Interaction<GetFilePathArgs, string?> GetFilePath { get; } = new();

        public Interaction<MeshImportOptionsViewModel, MeshImportOptions?> GetMeshImportOptions { get; } = new();

        public ReactiveCommand<Unit, Unit> SaveFlverCommand { get; }

        public ReactiveCommand<Unit, Unit> OpenFlverCommand { get; }

        public ReactiveCommand<Unit, Unit> SaveFlverAsCommand { get; }

        public ReactiveCommand<Unit, Unit> ImportFbxCommand { get; }

        public ReactiveCommand<Unit, Unit> AddToFlverCommand { get; }

        public ReactiveCommand<Unit,bool> RedoCommand { get; }

        public ReactiveCommand<Unit,bool> UndoCommand { get; }

        [Reactive] public string Log { get; set; } = "";

        private async Task AddToFlverAsync()
        {
            MeshImportOptionsViewModel optionsViewModel = new(Fbx!.SelectedMesh!.Name, Flver!.MaterialInfoBank);
            MeshImportOptions? options = await GetMeshImportOptions.Handle(optionsViewModel);
            if (options is null) return;
            
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
            IsImporting = true;
            
            List<FileTypeFilter> filters = new()
            {
                new FileTypeFilter("Autodesk Fbx Files", new List<string> {"fbx"}),
                new FileTypeFilter("All Files", new List<string> {"*"})
            };
            GetFilePathArgs args = new("Import Fbx", filters, GetPathMode.Open);
            string? fbxPath = await GetFilePath.Handle(args);
            if (fbxPath is null)
            {
                IsImporting = false;
                return;
            }

            List<FbxMeshDataViewModel> meshes = new();
            try
            {
                meshes = FbxMeshData.Import(fbxPath).Select(x => new FbxMeshDataViewModel(x)).ToList();
            }
            catch (Exception)
            {
                Logger.Log("Fbx Import Failed");
                throw;
            }

            FbxSceneDataViewModel? scene = new FbxSceneDataViewModel()
            {
                MeshData = new ObservableCollection<FbxMeshDataViewModel>(meshes)
            };
            
            Logger.Log($"Successfully imported {Path.GetFileName(fbxPath)}.");

            Fbx = scene;

            IsImporting = false;
        }

        private async Task OpenFlverAsync()
        {
            List<FileTypeFilter> filters = new()
            {
                new FileTypeFilter("Flver Files", new List<string> {"flver"}),
                new FileTypeFilter("All Files", new List<string> {"*"})
            };
            GetFilePathArgs args = new("Open Flver", filters, GetPathMode.Open);
            string? flverPath = await GetFilePath.Handle(args);
            if (flverPath is null) return;
            _history.Clear();
            Flver = new FlverViewModel(FLVER2.Read(flverPath), _history);
            FlverPath = flverPath;
        }

        private async Task SaveFlverAsAsync()
        {
            List<FileTypeFilter> filters = new()
            {
                new FileTypeFilter("Flver Files", new List<string> {"flver"}),
                new FileTypeFilter("All Files", new List<string> {"*"})
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