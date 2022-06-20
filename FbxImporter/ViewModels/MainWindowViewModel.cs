using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using FbxImporter.Models;
using FbxImporter.Util;
using Newtonsoft.Json;
using Reactive.Bindings.Extensions;
using ReactiveHistory;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SoulsFormats;

namespace FbxImporter.ViewModels
{
    public class MainWindowViewModel : ViewModelBase, ILoggable, IDisposable
    {
        public MainWindowViewModel()
        {
            Disposable = new CompositeDisposable();
            History = new StackHistory();
            
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            MaterialLibraryPath = Path.Join(baseDirectory, "Res", "MaterialLibrary.json");
            if (File.Exists(MaterialLibraryPath))
            {
                string materialLibrary = File.ReadAllText(MaterialLibraryPath);
                MaterialLibrary = JsonConvert.DeserializeObject<MaterialLibrary>(materialLibrary) ??
                                  throw new InvalidDataException("MaterialLibrary could not be read.");
            }
            else
            {
                MaterialLibrary = new MaterialLibrary("Dark Souls 3");
            }

            IObservable<bool> isFlverMeshSelected = this.WhenAnyValue(x => x.Flver!.SelectedMesh).Select(x => x is { });
            SaveFlverCommand = ReactiveCommand.Create(SaveFlver);
            OpenFlverCommand = ReactiveCommand.CreateFromTask(OpenFlverAsync);
            SaveFlverAsCommand = ReactiveCommand.CreateFromTask(SaveFlverAsAsync);
            ImportFbxCommand = ReactiveCommand.CreateFromTask(ImportFbxAsync);
            AddToFlverCommand = ReactiveCommand.CreateFromTask(AddToFlverAsync);
            SaveMaterialInfoCommand = ReactiveCommand.CreateFromTask(SaveMaterialInfoAsync, isFlverMeshSelected);

            UndoCommand = ReactiveCommand.Create(History.Undo);
            RedoCommand = ReactiveCommand.Create(History.Redo);

            this.WhenAnyValue(x => x.Flver,
                    y => y.Fbx!.SelectedMesh,
                    (x, y) => x is not null && y is not null)
                .ToPropertyEx(this, x => x.CanAddToFlver, initialValue: false)
                .AddTo(Disposable);

            History.CanUndo.ToPropertyEx(this, x => x.CanUndo).AddTo(Disposable);
            History.CanRedo.ToPropertyEx(this, x => x.CanRedo).AddTo(Disposable);

            IsImporting = false;
            
            Logger.CurrentLoggable = this;
        }

        private CompositeDisposable Disposable { get; }

        private IHistory History { get; }
        
        private MaterialLibrary MaterialLibrary { get; set; }

        private string MaterialLibraryPath { get; set; }

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
        
        public ReactiveCommand<Unit, Unit> SaveMaterialInfoCommand { get; }
        
        public ReactiveCommand<Unit,bool> RedoCommand { get; }

        public ReactiveCommand<Unit,bool> UndoCommand { get; }

        public void Dispose() => Disposable.Dispose();

        [Reactive] public string Log { get; set; } = "";

        private async Task AddToFlverAsync()
        {
            MeshImportOptionsViewModel optionsViewModel = new(MaterialLibrary);
            MeshImportOptions? options = await GetMeshImportOptions.Handle(optionsViewModel);
            if (options is null) return;
            
            AddToFlverWithHistory(options);
        }
        
        private void AddToFlverWithHistory(MeshImportOptions options)
        {
            int meshIndex = Flver!.Meshes.Count;
            int boneIndex = Flver.Flver.Bones.Count;
            bool addedBone = Flver.Flver.Bones.All(x => x.Name != Fbx!.SelectedMesh!.Name) && options.CreateDefaultBone;

            History.Snapshot(Undo, Redo);
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
            if (fbxPath is null) return;
            
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            ProcessStartInfo startInfo = new()
            {
                WorkingDirectory = Path.Join(baseDirectory, "Dependencies", "FbxMeshDataExtractor"),
                Arguments = $"\"{fbxPath}\"",
                FileName = Path.Join(baseDirectory, "Dependencies", "FbxMeshDataExtractor", "FbxMeshDataExtractor.exe"),
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process fbxMeshDataExtractor = new()
            {
                StartInfo = startInfo
            };
            
            fbxMeshDataExtractor.OutputDataReceived += (_, eventArgs) =>
            {
                if (eventArgs.Data is null) return;
                Logger.Log(eventArgs.Data);
            };
            
            Logger.Log($"Importing {Path.GetFileName(fbxPath)}...");
            
            fbxMeshDataExtractor.Start();
            fbxMeshDataExtractor.BeginOutputReadLine();
            await fbxMeshDataExtractor.WaitForExitAsync();
            fbxMeshDataExtractor.Close();

            string meshDataPath = Path.Join(baseDirectory, "Dependencies/FbxMeshDataExtractor/Temp/MeshData.json");

            if (!File.Exists(meshDataPath))
            {
                Logger.Log("Fbx Import Failed");
                return;
            }

            FbxSceneDataViewModel? scene = JsonConvert.DeserializeObject<FbxSceneDataViewModel>(await File.ReadAllTextAsync(meshDataPath));
            if (scene is null)
            {
                Logger.Log("Fbx Import Failed");
                return;
            }
            
            Directory.Delete( Path.Join(baseDirectory, "Dependencies/FbxMeshDataExtractor/Temp"), true);
            
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
            History.Clear();
            Flver = new FlverViewModel(FLVER2.Read(flverPath), History);
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
        
        private async Task SaveMaterialInfoAsync()
        {
            FlverMeshViewModel selectedMesh = Flver!.SelectedMesh!;
            if (MaterialLibrary.Materials.Any(x => x.MtdPath == selectedMesh.Material.MTD))
            {
                Logger.Log($"The material library already contains {selectedMesh.Material.Name}");
                return;
            }

            List<FLVER2.BufferLayout> bufferLayouts = selectedMesh.Mesh.VertexBuffers
                .Select(vertexBuffer => Flver.Flver.BufferLayouts[vertexBuffer.LayoutIndex])
                .Select(FlverUtils.Clone)
                .ToList();

            MaterialLibrary.Materials.Add(new MaterialInfo(selectedMesh.Material.Clone(), bufferLayouts, selectedMesh.GxList.Clone()));

            JsonSerializerSettings settings = new()
            {
                Formatting = Formatting.Indented
            };
            JsonSerializer serializer = JsonSerializer.Create(settings);
            await using StreamWriter sw = new(MaterialLibraryPath);
            using JsonWriter writer = new JsonTextWriter(sw)
            {
                Indentation = 4
            };
            serializer.Serialize(writer, MaterialLibrary);
            Logger.Log($"Successfully added material {selectedMesh.Material.Name} to the material library.");
        }

        public enum GetPathMode
        {
            Open,
            Save
        }

        public record GetFilePathArgs(string Title, List<FileTypeFilter> Filters, GetPathMode Mode);

        public record FileTypeFilter(string Name, List<string> Extensions);
    }
}