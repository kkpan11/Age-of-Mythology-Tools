﻿using AoMEngineLibrary.Graphics;
using AoMEngineLibrary.Graphics.Brg;
using AoMEngineLibrary.Graphics.Grn;
using AoMModelEditor.Services;
using AoMModelEditor.Models.Brg;
using AoMModelEditor.Models.Grn;
using AoMModelEditor.Settings;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using SharpGLTF.Schema2;
using SharpGLTF.Validation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows;

namespace AoMModelEditor.Models
{
    public class ModelsViewModel : ReactiveObject
    {
        private BrgFile? _brg;
        private GrnFile? _grn;

        private readonly object _modelLock = new object();
        private readonly AppSettings _appSettings;
        private readonly FileDialogService _fileDialogService;
        private readonly ILogger<ModelsViewModel> _logger;
        private readonly TextureManager _textureManager;
        private readonly BrgGltfImportSettingsViewModel _brgGltfImportSettingsViewModel;
        private readonly GrnGltfImportSettingsViewModel _grnGltfImportSettingsViewModel;

        public bool IsBrg => _brg != null;
        public bool IsGrn => _grn != null;

        private ObservableCollection<IModelObject> _modelObjects;
        public IEnumerable<IModelObject> ModelObjects => _modelObjects;

        public ReactiveCommand<Unit, Unit> ExportGltfCommand { get; }
        public ReactiveCommand<Unit, Unit> ImportGltfBrgCommand { get; }
        public ReactiveCommand<Unit, Unit> ImportGltfGrnCommand { get; }
        public ReactiveCommand<Unit, Unit> ExportBrgMtrlFilesCommand { get; }
        public ReactiveCommand<Unit, Unit> ApplyGrnAnimationCommand { get; }

        public Interaction<GltfImportSettingsViewModel, bool> ImportGltf { get; }

        public ModelsViewModel(AppSettings appSettings, FileDialogService fileDialogService, ILogger<ModelsViewModel> logger)
        {
            _appSettings = appSettings;
            _fileDialogService = fileDialogService;
            _logger = logger;
            _textureManager = new TextureManager(_appSettings.GameDirectory);
            _brgGltfImportSettingsViewModel = new BrgGltfImportSettingsViewModel();
            _grnGltfImportSettingsViewModel = new GrnGltfImportSettingsViewModel();
            _modelObjects = new ObservableCollection<IModelObject>();

            ExportGltfCommand = ReactiveCommand.Create(ExportGltf,
                this.WhenAnyValue(vm => vm.IsBrg, vm => vm.IsGrn, (b, g) => b || g));
            ImportGltfBrgCommand = ReactiveCommand.Create(ImportGltfToBrg);
            ImportGltfGrnCommand = ReactiveCommand.Create(ImportGltfToGrn);
            ExportBrgMtrlFilesCommand = ReactiveCommand.Create(ExportBrgMtrlFiles,
                this.WhenAnyValue(vm => vm.IsBrg));
            ApplyGrnAnimationCommand = ReactiveCommand.Create(ApplyGrnAnimation,
                this.WhenAnyValue(vm => vm.IsGrn));

            ImportGltf = new Interaction<GltfImportSettingsViewModel, bool>();
        }

        public BrgFile GetBrg()
        {
            return _brg ?? throw new InvalidOperationException("No brg file loaded.");
        }

        public GrnFile GetGrn()
        {
            return _grn ?? throw new InvalidOperationException("No grn file loaded.");
        }

        public void Load(string filePath)
        {
            if (Path.GetExtension(filePath).Equals(".grn", StringComparison.InvariantCultureIgnoreCase))
            {
                LoadGrn(filePath);
            }
            else
            {
                LoadBrg(filePath);
            }
        }
        private void LoadBrg(string filePath)
        {
            using (var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                LoadBrg(new BrgFile(fs));
            }
        }
        private void LoadBrg(BrgFile brg)
        {
            lock (_modelLock)
            {
                _logger.LogInformation("Loading brg file.");
                _modelObjects.Clear();
                _brg = brg;
                _grn = null;
                this.RaisePropertyChanged(nameof(IsBrg));
                this.RaisePropertyChanged(nameof(IsGrn));

                var statsVM = new BrgStatsViewModel(brg);
                _modelObjects.Add(statsVM);

                if (brg.Meshes.Count > 0)
                {
                    BrgMeshViewModel meshVM = new BrgMeshViewModel(brg, brg.Meshes[0]);
                    _modelObjects.Add(meshVM);
                }

                foreach (var mat in brg.Materials)
                {
                    _modelObjects.Add(new BrgMaterialViewModel(mat));
                }

                if (brg.Meshes.Count > 0)
                {
                    for (int i = 0; i < brg.Meshes[0].Dummies.Count; ++i)
                    {
                        _modelObjects.Add(new BrgDummyViewModel(brg.Meshes.Select(m => m.Dummies[i]).ToList()));
                    }
                }

                statsVM.IsSelected = true;

                _logger.LogInformation("Finished loading brg file.");
            }
        }
        private void LoadGrn(string filePath)
        {
            using (var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var grn = new GrnFile();
                grn.Read(fs);
                LoadGrn(grn);
            }
        }
        private void LoadGrn(GrnFile grn)
        {
            lock (_modelLock)
            {
                _logger.LogInformation("Loading grn file.");
                _modelObjects.Clear();
                _brg = null;
                _grn = grn;
                this.RaisePropertyChanged(nameof(IsBrg));
                this.RaisePropertyChanged(nameof(IsGrn));

                var statsVM = new GrnStatsViewModel(grn);
                _modelObjects.Add(statsVM);

                if (grn.Meshes.Count > 0)
                {
                    _modelObjects.Add(new GrnMeshesViewModel(grn.Meshes));
                }

                if (grn.Bones.Count > 0)
                {
                    _modelObjects.Add(new GrnBoneViewModel(grn, 0));
                }

                foreach (var mat in grn.Materials)
                {
                    _modelObjects.Add(new GrnMaterialViewModel(mat));
                }

                statsVM.IsSelected = true;

                _logger.LogInformation("Finished loading grn file.");
            }
        }

        public void Save(string filePath)
        {
            if (IsBrg)
            {
                SaveBrg(filePath);
            }
            else
            {
                SaveGrn(filePath);
            }
        }
        private void SaveBrg(string filePath)
        {
            _logger.LogInformation("Saving brg file.");
            var brg = GetBrg();

            using (var fs = File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                brg.Write(fs);
            }
            _logger.LogInformation("Finished saving brg file.");
        }
        private void SaveGrn(string filePath)
        {
            _logger.LogInformation("Saving grn file.");
            var grn = GetGrn();

            using (var fs = File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                grn.Write(fs);
            }
            _logger.LogInformation("Finished saving grn file.");
        }

        private void ExportGltf()
        {
            try
            {
                var sfd = _fileDialogService.GetModelSaveFileDialog();
                sfd.Filter = "Glb files (*.glb)|*.glb|Gltf files (*.gltf)|*.gltf|All files (*.*)|*.*";

                var dr = sfd.ShowDialog();
                if (dr.HasValue && dr == true)
                {
                    _fileDialogService.SetLastModelFilePath(sfd.FileName);
                    if (IsBrg)
                    {
                        ExportBrgToGltf(sfd.FileName);
                    }
                    else
                    {
                        ExportGrnToGltf(sfd.FileName);
                    }

                    MessageBus.Current.SendMessage(new ModelFileChangedArgs { FilePath = sfd.FileName });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export gltf file.");
                MessageBox.Show($"Failed to export gltf file.{Environment.NewLine}{ex.Message}", Properties.Resources.AppTitleLong,
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
            }
        }
        private void ExportBrgToGltf(string filePath)
        {
            _logger.LogInformation("Exporting gltf file as brg.");
            var brg = GetBrg();
            var parms = new BrgGltfParameters()
            {
                ModelName = Path.GetFileNameWithoutExtension(filePath)
            };

            BrgGltfConverter conv = new BrgGltfConverter();
            var gltf = conv.Convert(brg, parms, _textureManager);
            gltf.Save(filePath, new WriteSettings() { Validation = ValidationMode.Strict });
            _logger.LogInformation("Finished exporting gltf file as brg.");
        }
        private void ExportGrnToGltf(string filePath)
        {
            _logger.LogInformation("Exporting gltf file as grn.");
            var grn = GetGrn();

            GrnGltfConverter conv = new GrnGltfConverter();
            var gltf = conv.Convert(grn, _textureManager);
            gltf.Save(filePath, new WriteSettings() { Validation = ValidationMode.Strict });
            _logger.LogInformation("Finished exporting gltf file as grn.");
        }

        private async void ImportGltfToBrg()
        {
            try
            {
                var ofd = _fileDialogService.GetModelOpenFileDialog();
                ofd.Filter =
                    "Glb/Gltf files (*.glb, *.gltf)|*.glb;*.gltf|Glb files (*.glb)|*.glb|Gltf files (*.gltf)|*.gltf|All files (*.*)|*.*";

                // Load gltf file
                var dr = ofd.ShowDialog();
                if (dr != true) return;

                _fileDialogService.SetLastModelFilePath(ofd.FileName);
                _logger.LogInformation("Loading gltf file.");
                var gltf = ModelRoot.Load(ofd.FileName, new ReadSettings() { Validation = ValidationMode.Strict });
                _logger.LogInformation("Finished loading gltf file.");

                // Get settings and convert
                _brgGltfImportSettingsViewModel.SetGltfModel(gltf);
                var importResult = await ImportGltf.Handle(_brgGltfImportSettingsViewModel);
                if (!importResult) return;
                var settings = _brgGltfImportSettingsViewModel.CreateGltfBrgParameters();

                _logger.LogInformation("Converting gltf file to brg.");
                var conv = new GltfBrgConverter();
                var brg = conv.Convert(gltf, settings);
                _logger.LogInformation("Finished converting gltf file to brg.");

                LoadBrg(brg);
                MessageBus.Current.SendMessage(new ModelFileChangedArgs { FilePath = ofd.FileName });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import gltf file as brg.");
                MessageBox.Show($"Failed to import gltf file as brg.{Environment.NewLine}{ex.Message}",
                    Properties.Resources.AppTitleLong,
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK,
                    MessageBoxOptions.DefaultDesktopOnly);
            }
        }

        private async void ImportGltfToGrn()
        {
            try
            {
                var ofd = _fileDialogService.GetModelOpenFileDialog();
                ofd.Filter =
                    "Glb/Gltf files (*.glb, *.gltf)|*.glb;*.gltf|Glb files (*.glb)|*.glb|Gltf files (*.gltf)|*.gltf|All files (*.*)|*.*";

                // Load gltf file
                var dr = ofd.ShowDialog();
                if (dr != true) return;

                _fileDialogService.SetLastModelFilePath(ofd.FileName);
                _logger.LogInformation("Loading gltf file.");
                var gltf = ModelRoot.Load(ofd.FileName, new ReadSettings() { Validation = ValidationMode.Strict });
                _logger.LogInformation("Finished loading gltf file.");

                // Get settings and convert
                _grnGltfImportSettingsViewModel.SetGltfModel(gltf);
                var importResult = await ImportGltf.Handle(_grnGltfImportSettingsViewModel);
                if (!importResult) return;
                var settings = _grnGltfImportSettingsViewModel.CreateGltfGrnParameters();

                _logger.LogInformation("Converting gltf file to grn.");
                var conv = new GltfGrnConverter();
                var grn = conv.Convert(gltf, settings);
                _logger.LogInformation("Finished converting gltf file to grn.");

                LoadGrn(grn);
                MessageBus.Current.SendMessage(new ModelFileChangedArgs { FilePath = ofd.FileName });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import gltf file as grn.");
                MessageBox.Show($"Failed to import gltf file as grn.{Environment.NewLine}{ex.Message}",
                    Properties.Resources.AppTitleLong,
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK,
                    MessageBoxOptions.DefaultDesktopOnly);
            }
        }

        private void ExportBrgMtrlFiles()
        {
            try
            {
                var brg = GetBrg();

                var ofd = _fileDialogService.GetModelOpenFileDialog();
                var sfd = _fileDialogService.GetModelSaveFileDialog();
                var brgFilePath = sfd.FileName ?? ofd.FileName ?? string.Empty;
                var brgFolderPath = Path.GetDirectoryName(brgFilePath);
                var fbd = _fileDialogService.GetModelFolderBrowserDialog();

                if (!string.IsNullOrEmpty(_appSettings.MtrlFolderDialogDirectory) &&
                    Directory.Exists(_appSettings.MtrlFolderDialogDirectory))
                {
                    fbd.SelectedPath = _appSettings.MtrlFolderDialogDirectory;
                }
                else if (!string.IsNullOrEmpty(brgFolderPath) &&
                    Directory.Exists(brgFolderPath))
                {
                    fbd.SelectedPath = brgFolderPath;
                }

                if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    // Assuming single-threaded brg will not be null here since IsBrg already checked
                    var brgFileNameNoExt = Path.GetFileNameWithoutExtension(brgFilePath);
                    for (int i = 0; i < brg.Materials.Count; i++)
                    {
                        MtrlFile mtrl = new MtrlFile(brg.Materials[i]);
                        using (var fs = File.Open(Path.Combine(fbd.SelectedPath, brgFileNameNoExt + "_" + i + ".mtrl"),
                            FileMode.Create, FileAccess.Write, FileShare.Read))
                        {
                            mtrl.Write(fs);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export brg mtrl files.");
                MessageBox.Show($"Failed to export brg mtrl files.{Environment.NewLine}{ex.Message}", Properties.Resources.AppTitleLong,
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
            }
        }

        private void ApplyGrnAnimation()
        {
            try
            {
                var grn = GetGrn();

                var ofd = _fileDialogService.GetModelOpenFileDialog();
                ofd.Filter = "Grn files (*.grn)|*.grn|All files (*.*)|*.*";

                var dr = ofd.ShowDialog();
                if (dr.HasValue && dr == true)
                {
                    _fileDialogService.SetLastModelFilePath(ofd.FileName);
                    using (var fs = File.Open(ofd.FileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var animGrn = new GrnFile();
                        animGrn.Read(fs);
                        grn.SetAnimation(animGrn);
                    }

                    MessageBus.Current.SendMessage(new ModelFileChangedArgs { FilePath = ofd.FileName });

                    var statsVM = _modelObjects.FirstOrDefault(x => x is GrnStatsViewModel) as GrnStatsViewModel;
                    statsVM?.Update();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply grn animation.");
                MessageBox.Show($"Failed to apply grn animation.{Environment.NewLine}{ex.Message}", Properties.Resources.AppTitleLong,
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
            }
        }
    }
}
