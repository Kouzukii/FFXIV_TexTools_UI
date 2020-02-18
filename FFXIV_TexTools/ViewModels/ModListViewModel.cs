﻿// FFXIV TexTools
// Copyright © 2019 Rafael Gonzalez - All Rights Reserved
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Models;
using FFXIV_TexTools.Resources;
using Newtonsoft.Json;
using SharpDX;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HelixToolkit.Wpf;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Items.DataContainers;
using xivModdingFramework.Materials.FileTypes;
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.Textures.DataContainers;
using xivModdingFramework.Textures.Enums;
using xivModdingFramework.Textures.FileTypes;
using Application = System.Windows.Application;

namespace FFXIV_TexTools.ViewModels
{
    public class ModListViewModel : INotifyPropertyChanged
    {
        private readonly Modding _modding;
        private string _modToggleText = UIStrings.Enable_Disable;
        private Visibility _listVisibility = Visibility.Visible, _infoGridVisibility = Visibility.Collapsed;
        private string _modPackTitle, _modPackModAuthorLabel, _modPackModCountLabel, _modPackModVersionLabel, _modPackContentList, _progressText;
        private int _progressValue;
        private ObservableCollection<Category> _categories;
        private IProgress<(int current, int total)> progress;
        private string _searchText;


        public ModListViewModel(Modding modding)
        {
            _modding = modding;

            progress = new Progress<(int current, int total)>((result) =>
            {
                ProgressValue = (int)(((float) result.current / (float) result.total) * 100);
                ProgressText = $"{result.current} / {result.total}";
            });

            GetCategoriesModPackFilter();
        }

        public string SearchText {
            get => _searchText;
            set {
                _searchText = string.IsNullOrWhiteSpace(value) ? null : value;
                OnPropertyChanged(nameof(SearchText));
            }
        }

        /// <summary>
        /// The collection of categories
        /// </summary>
        public ObservableCollection<Category> Categories
        {
            get => _categories;
            set
            {
                _categories = value;
                OnPropertyChanged(nameof(Categories));
            }
        }

        /// <summary>
        /// Gets the categoreis based on the mod pack filter
        /// </summary>
        private Task GetCategoriesModPackFilter()
        {
            Categories = new ObservableCollection<Category>();

            return Task.Run(() =>
            {
                var modList = _modding.GetModList();

                var modPackCatDict = new Dictionary<string, Category>();
                var modPackMainCatDict = new DoubleKeyDictionary<string, string, Category>();

                // Mod Packs

                var modPacksParent = new Category
                {
                    Name = "ModPacks"
                };

                var category = new Category
                {
                    Name = UIStrings.Standalone_Non_ModPack,
                    Categories = new ObservableCollection<Category>(),
                    CategoryList = new List<string>(),
                    ParentCategory = modPacksParent
                };

                modPackCatDict.Add(category.Name, category);

                var terms = _searchText != null
                    ? Regex.Replace(_searchText, @"(?:\W|^)(\w+)(?:\W+|$)", "$1 ")
                        .Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries)
                    : new string[0];

                foreach (var mod in modList.Mods) {
                    if (mod.modPack != null && (terms.Length == 0 || terms.All(t => mod.name.IndexOf(t, StringComparison.InvariantCultureIgnoreCase) >= 0 || mod.modPack.name.IndexOf(t, StringComparison.InvariantCultureIgnoreCase) >= 0))) {
                        if (!modPackMainCatDict.TryGetValue(mod.modPack.name, mod.category, out var cat)) {
                            if (!modPackCatDict.TryGetValue(mod.modPack.name, out var parent)) {
                                parent = new Category
                                {
                                    Name = mod.modPack.name,
                                    Categories = new ObservableCollection<Category>(),
                                    CategoryList = new List<string>(),
                                    ParentCategory = modPacksParent
                                };

                                modPackCatDict.Add(parent.Name, parent);
                            }
                            cat = new Category {
                                Name = mod.category,
                                Categories = new ObservableCollection<Category>(),
                                CategoryList = new List<string>(),
                                ParentCategory = parent
                            };
                            modPackMainCatDict.Add(mod.modPack.name, mod.category, cat);
                            parent.Categories.Add(cat);
                        }

                        if (cat.CategoryList.Contains(mod.name)) continue;

                        var categoryItem = new Category
                        {
                            Name = mod.name,
                            Item = MakeItemModel(mod),
                            ParentCategory = category
                        };

                        cat.Categories.Add(categoryItem);
                        cat.CategoryList.Add(mod.name);
                    }
                }

                Application.Current.Dispatcher.Invoke(() => Categories = new ObservableCollection<Category>(modPackCatDict.Values));
            });
        }


        public ObservableCollection<ModListModel> ModListPreviewList { get; set; } = new ObservableCollection<ModListModel>();

        /// <summary>
        /// Makes an generic item model from a mod item
        /// </summary>
        /// <param name="modItem">The mod item</param>
        /// <returns>The mod item as a XivGenericItemModel</returns>
        private static XivGenericItemModel MakeItemModel(Mod modItem)
        {
            var fullPath = modItem.fullPath;

            var item = new XivGenericItemModel
            {
                Name = modItem.name,
                ItemCategory = modItem.category,
                DataFile = XivDataFiles.GetXivDataFile(modItem.datFile)
            };

            try
            {
                if (modItem.fullPath.Contains("chara/equipment") || modItem.fullPath.Contains("chara/accessory"))
                {
                    item.Category = XivStrings.Gear;
                    item.ModelInfo = new XivModelInfo
                    {
                        ModelID = int.Parse(fullPath.Substring(17, 4))
                    };
                }

                if (modItem.fullPath.Contains("chara/weapon"))
                {
                    item.Category = XivStrings.Gear;
                    item.ModelInfo = new XivModelInfo
                    {
                        ModelID = int.Parse(fullPath.Substring(14, 4))
                    };
                }

                if (modItem.fullPath.Contains("chara/human"))
                {
                    item.Category = XivStrings.Character;


                    if (item.Name.Equals(XivStrings.Body))
                    {
                        item.ModelInfo = new XivModelInfo
                        {
                            ModelID = int.Parse(
                                fullPath.Substring(fullPath.IndexOf("/body", StringComparison.Ordinal) + 7, 4))
                        };
                    }
                    else if (item.Name.Equals(XivStrings.Hair))
                    {
                        item.ModelInfo = new XivModelInfo
                        {
                            ModelID = int.Parse(
                                fullPath.Substring(fullPath.IndexOf("/hair", StringComparison.Ordinal) + 7, 4))
                        };
                    }
                    else if (item.Name.Equals(XivStrings.Face))
                    {
                        item.ModelInfo = new XivModelInfo
                        {
                            ModelID = int.Parse(
                                fullPath.Substring(fullPath.IndexOf("/face", StringComparison.Ordinal) + 7, 4))
                        };
                    }
                    else if (item.Name.Equals(XivStrings.Tail))
                    {
                        item.ModelInfo = new XivModelInfo
                        {
                            ModelID = int.Parse(
                                fullPath.Substring(fullPath.IndexOf("/tail", StringComparison.Ordinal) + 7, 4))
                        };
                    }
                }

                if (modItem.fullPath.Contains("chara/common"))
                {
                    item.Category = XivStrings.Character;

                    if (item.Name.Equals(XivStrings.Face_Paint))
                    {
                        item.ModelInfo = new XivModelInfo
                        {
                            ModelID = int.Parse(
                                fullPath.Substring(fullPath.LastIndexOf("_", StringComparison.Ordinal) + 1, 1))
                        };
                    }
                    else if (item.Name.Equals(XivStrings.Equipment_Decals))
                    {
                        item.ModelInfo = new XivModelInfo();

                        if (!fullPath.Contains("_stigma"))
                        {
                            item.ModelInfo.ModelID = int.Parse(
                                fullPath.Substring(fullPath.LastIndexOf("_", StringComparison.Ordinal) + 1, 3));
                        }
                    }
                }

                if (modItem.fullPath.Contains("chara/monster"))
                {
                    item.Category = XivStrings.Companions;

                    item.ModelInfo = new XivModelInfo
                    {
                        ModelID = int.Parse(fullPath.Substring(15, 4)),
                        Body = int.Parse(fullPath.Substring(fullPath.IndexOf("/body", StringComparison.Ordinal) + 7, 4))
                    };
                }

                if (modItem.fullPath.Contains("chara/demihuman"))
                {
                    item.Category = XivStrings.Companions;

                    item.ModelInfo = new XivModelInfo
                    {
                        Body = int.Parse(fullPath.Substring(17, 4)),
                        ModelID = int.Parse(
                            fullPath.Substring(fullPath.IndexOf("t/e", StringComparison.Ordinal) + 3, 4))
                    };
                }

                if (modItem.fullPath.Contains("ui/"))
                {
                    item.Category = XivStrings.UI;

                    if (modItem.fullPath.Contains("ui/uld") || modItem.fullPath.Contains("ui/map") || modItem.fullPath.Contains("ui/loadingimage"))
                    {
                        item.ModelInfo = new XivModelInfo
                        {
                            ModelID = 0
                        };
                    }
                    else
                    {
                        item.ModelInfo = new XivModelInfo
                        {
                            ModelID = int.Parse(fullPath.Substring(fullPath.LastIndexOf("/", StringComparison.Ordinal) + 1,
                                6))
                        };
                    }
                }

                if (modItem.fullPath.Contains("/hou/"))
                {
                    item.Category = XivStrings.Housing;

                    item.ModelInfo = new XivModelInfo
                    {
                        ModelID = int.Parse(fullPath.Substring(fullPath.LastIndexOf("_m", StringComparison.Ordinal) + 2,
                            4))
                    };
                }
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format(UIMessages.ModelDataErrorMessage, modItem.name, modItem.fullPath));
            }

            return item;
        }

        /// <summary>
        /// Update the mod list entries
        /// </summary>
        /// <param name="selectedItem">The selected item to update the entries for</param>
        public Task UpdateList(Category category, CancellationTokenSource cts)
        {
            var updateLock = new object();
            ListVisibility = Visibility.Visible;
            InfoGridVisibility = Visibility.Collapsed;
            ModListPreviewList.Clear();

            ProgressValue = 0;
            ProgressText = string.Empty;
            Tex tex;

            return Task.Run(async () =>
            {
                var selectedItem = category.Item as XivGenericItemModel;
                if (selectedItem == null) return;

                var mtrl = new Mtrl(_modding, selectedItem.DataFile, GetLanguage());
                var modList = _modding.GetModList();

                var modItems = new List<Mod>();

                var modPackCategory = category;

                while (!modPackCategory.ParentCategory.Name.Equals("ModPacks"))
                {
                    modPackCategory = modPackCategory.ParentCategory;
                }

                foreach (var mod in modList.Mods)
                {
                    if (!mod.name.Equals(selectedItem.Name)) continue;

                    if (mod.modPack != null)
                    {
                        if (mod.modPack.name == modPackCategory.Name)
                        {
                            modItems.Add(mod);
                        }
                    }
                    else
                    {
                        modItems.Add(mod);
                    }
                }

                if (modItems.Count > 10)
                {
                    tex = new Tex(_modding, selectedItem.DataFile);
                    await tex.GetIndexFileDictionary();
                }
                else
                {
                    tex = new Tex(_modding);
                }

                var modNum = 0;

                await Task.Run(async () =>
                {
                    foreach (var modItem in modItems)
                    {
                        var itemPath = modItem.fullPath;

                        var modListModel = new ModListModel
                        {
                            ModItem = modItem
                        };

                        // Race
                        if (selectedItem.Category.Equals(XivStrings.Gear))
                        {
                            if (modItem.fullPath.Contains("equipment"))
                            {
                                string raceCode;
                                if (itemPath.Contains("/v"))
                                {
                                    raceCode = itemPath.Substring(itemPath.LastIndexOf("_c") + 2, 4);
                                }
                                else
                                {
                                    raceCode = itemPath.Substring(itemPath.LastIndexOf("/c") + 2, 4);
                                }

                                modListModel.Race = XivRaces.GetXivRace(raceCode).GetDisplayName();
                            }
                            else
                            {
                                modListModel.Race = XivStrings.All;
                            }
                        }
                        else if (selectedItem.Category.Equals(XivStrings.Character))
                        {
                            if (!modItem.fullPath.Contains("chara/common"))
                            {
                                var raceCode = itemPath.Substring(itemPath.IndexOf("n/c") + 3, 4);
                                modListModel.Race = XivRaces.GetXivRace(raceCode).GetDisplayName();
                            }
                            else
                            {
                                modListModel.Race = XivStrings.All;
                            }

                        }
                        else if (selectedItem.Category.Equals(XivStrings.Companions))
                        {
                            modListModel.Race = XivStrings.Monster;
                        }
                        else if (selectedItem.Category.Equals(XivStrings.UI))
                        {
                            modListModel.Race = XivStrings.All;
                        }
                        else if (selectedItem.Category.Equals(XivStrings.Housing))
                        {
                            modListModel.Race = XivStrings.All;
                        }

                        XivTexType? xivTexType = null;
                        // Map
                        if (itemPath.Contains("_d."))
                        {
                            xivTexType = XivTexType.Diffuse;
                            modListModel.Map = xivTexType.ToString();
                        }
                        else if (itemPath.Contains("_n."))
                        {
                            xivTexType = XivTexType.Normal;
                            modListModel.Map = xivTexType.ToString();
                        }
                        else if (itemPath.Contains("_s."))
                        {
                            xivTexType = XivTexType.Specular;
                            modListModel.Map = xivTexType.ToString();
                        }
                        else if (itemPath.Contains("_m."))
                        {
                            xivTexType = XivTexType.Multi;
                            modListModel.Map = xivTexType.ToString();
                        }
                        else if (itemPath.Contains("material"))
                        {
                            xivTexType = XivTexType.ColorSet;
                            modListModel.Map = xivTexType.ToString();
                        }
                        else if (itemPath.Contains("decal"))
                        {
                            xivTexType = XivTexType.Mask;
                            modListModel.Map = xivTexType.ToString();
                        }
                        else if (itemPath.Contains("vfx"))
                        {
                            xivTexType = XivTexType.Vfx;
                            modListModel.Map = xivTexType.ToString();
                        }
                        else if (itemPath.Contains("ui/"))
                        {
                            if (itemPath.Contains("icon"))
                            {
                                xivTexType = XivTexType.Icon;
                                modListModel.Map = xivTexType.ToString();
                            }
                            else if (itemPath.Contains("map"))
                            {
                                xivTexType = XivTexType.Map;
                                modListModel.Map = xivTexType.ToString();
                            }
                            else
                            {
                                modListModel.Map = "UI";
                            }
                        }
                        else if (itemPath.Contains(".mdl"))
                        {
                            modListModel.Map = "3D";
                        }
                        else
                        {
                            modListModel.Map = "--";
                        }

                        // Part
                        if (itemPath.Contains("_b_")|| itemPath.Contains("_b."))
                        {
                            modListModel.Part = "b";
                        }
                        else if (itemPath.Contains("_c_")|| itemPath.Contains("_c."))
                        {
                            modListModel.Part = "c";
                        }
                        else if (itemPath.Contains("_d_")|| itemPath.Contains("_d."))
                        {
                            modListModel.Part = "d";
                        }
                        else if (itemPath.Contains("decal"))
                        {
                            modListModel.Part = itemPath.Substring(itemPath.LastIndexOf('_') + 1,
                                itemPath.LastIndexOf('.') - (itemPath.LastIndexOf('_') + 1));
                        }
                        else
                        {
                            modListModel.Part = "a";
                        }

                        // Type
                        if (itemPath.Contains("_iri_"))
                        {
                            modListModel.Type = XivStrings.Iris;
                        }
                        else if (itemPath.Contains("_etc_"))
                        {
                            modListModel.Type = XivStrings.Etc;
                        }
                        else if (itemPath.Contains("_fac_"))
                        {
                            modListModel.Type = XivStrings.Face;
                        }
                        else if (itemPath.Contains("_hir_"))
                        {
                            modListModel.Type = XivStrings.Hair;
                        }
                        else if (itemPath.Contains("_acc_"))
                        {
                            modListModel.Type = XivStrings.Accessory;
                        }
                        else if (itemPath.Contains("demihuman"))
                        {
                            modListModel.Type = itemPath.Substring(itemPath.LastIndexOf('_') - 3, 3);
                        }
                        else
                        {
                            modListModel.Type = "--";
                        }

                        // Image
                        if (itemPath.Contains("material"))
                        {
                            var dxVersion = int.Parse(Properties.Settings.Default.DX_Version);

                            var offset = modItem.enabled ? modItem.data.modOffset : modItem.data.originalOffset;

                            try
                            {
                                mtrl.DataFile = XivDataFiles.GetXivDataFile(modItem.datFile);

                                var mtrlData = await mtrl.GetMtrlData(offset, modItem.fullPath, dxVersion);

                                var floats = Half.ConvertToFloat(mtrlData.ColorSetData.ToArray());

                                var floatArray = Utilities.ToByteArray(floats);

                                if (floatArray.Length > 0)
                                {
                                    using (var img = Image.LoadPixelData<RgbaVector>(floatArray, 4, 16))
                                    {
                                        img.Mutate(x => x.Opacity(1));

                                        BitmapImage bmp;

                                        using (var ms = new MemoryStream())
                                        {
                                            img.Save(ms, new BmpEncoder());

                                            bmp = new BitmapImage();
                                            bmp.BeginInit();
                                            bmp.StreamSource = ms;
                                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                                            bmp.EndInit();
                                            bmp.Freeze();
                                        }

                                        modListModel.Image =
                                            Application.Current.Dispatcher.Invoke(() => bmp);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                FlexibleMessageBox.Show(
                                    string.Format(UIMessages.MaterialFileReadErrorMessage, modItem.fullPath,
                                        ex.Message),
                                    UIMessages.MaterialDataReadErrorTitle,
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }

                        }
                        else if (itemPath.Contains(".mdl"))
                        {
                            modListModel.Image = Application.Current.Dispatcher.Invoke(() => new BitmapImage(
                                new Uri("pack://application:,,,/FFXIV_TexTools;component/Resources/3DModel.png")));
                        }
                        else
                        {
                            var ttp = new TexTypePath
                            {
                                Type = xivTexType.GetValueOrDefault(),
                                DataFile = XivDataFiles.GetXivDataFile(modItem.datFile),
                                Path = modItem.fullPath
                            };

                            XivTex texData;
                            try
                            {
                                if (modItems.Count > 10)
                                {
                                    texData = await tex.GetTexDataPreFetchedIndex(ttp);
                                }
                                else
                                {
                                    texData = await tex.GetTexData(ttp);
                                }

                            }
                            catch (Exception ex)
                            {
                                FlexibleMessageBox.Show(
                                    string.Format(UIMessages.TextureFileReadErrorMessage, ttp.Path, ex.Message),
                                    UIMessages.TextureDataReadErrorTitle,
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }

                            var mapBytes = await tex.GetImageData(texData);

                            using (var img = Image.LoadPixelData<Rgba32>(mapBytes, texData.Width, texData.Height))
                            {
                                img.Mutate(x => x.Opacity(1));

                                BitmapImage bmp;

                                using (var ms = new MemoryStream())
                                {
                                    img.Save(ms, new BmpEncoder());

                                    bmp = new BitmapImage();
                                    bmp.BeginInit();
                                    bmp.StreamSource = ms;
                                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                                    bmp.EndInit();
                                    bmp.Freeze();
                                }

                                modListModel.Image =
                                    Application.Current.Dispatcher.Invoke(() => bmp);
                            }
                        }

                        // Status
                        if (modItem.enabled)
                        {
                            modListModel.ActiveBorder = Brushes.Green;
                            modListModel.Active = Brushes.Transparent;
                            modListModel.ActiveOpacity = 1;
                        }
                        else
                        {
                            modListModel.ActiveBorder = Brushes.Red;
                            modListModel.Active = Brushes.Gray;
                            modListModel.ActiveOpacity = 0.5f;
                        }

                        cts.Token.ThrowIfCancellationRequested();

                        lock (updateLock)
                        {
                            progress.Report((++modNum, modItems.Count));
                        }

                        if (!cts.IsCancellationRequested)
                        {
                            Application.Current.Dispatcher.Invoke(() => ModListPreviewList.Add(modListModel));
                        }
                    }
                }, cts.Token);
            }, cts.Token);
        }

        /// <summary>
        /// Update the info grid
        /// </summary>
        /// <remarks>
        /// The info grid shows mod pack details
        /// </remarks>
        /// <param name="category">The category to update the info grid for</param>
        public void UpdateInfoGrid(Category category)
        {
            ListVisibility = Visibility.Collapsed;
            InfoGridVisibility = Visibility.Visible;
            ModPackContentList = string.Empty;
            var enabledCount = 0;
            var disabledCount = 0;

            ProgressValue = 0;
            ProgressText = string.Empty;

            var modList = _modding.GetModList();
            List<Mod> modPackModList = null;

            if (category.Name.Equals(UIStrings.Standalone_Non_ModPack))
            {
                modPackModList = (from items in modList.Mods
                    where !items.name.Equals(string.Empty) && items.modPack == null
                    select items).ToList();

                ModPackModAuthorLabel = "[ N/A ]";
                ModPackModVersionLabel = "[ N/A ]";
            }
            else
            {
                var modPackData = (from data in modList.ModPacks
                    where data.name == category.Name
                    select data).FirstOrDefault();

                modPackModList = (from items in modList.Mods
                    where (items.modPack != null && items.modPack.name == category.Name)
                    select items).ToList();

                ModPackModAuthorLabel = modPackData.author;
                ModPackModVersionLabel = modPackData.version;
            }

            ModPackTitle = category.Name;
            ModPackModCountLabel = modPackModList.Count.ToString();

            var modNameDict = new Dictionary<string, int>();

            foreach (var mod in modPackModList)
            {
                if (mod.enabled)
                {
                    enabledCount++;
                }
                else
                {
                    disabledCount++;
                }

                if (!modNameDict.ContainsKey(mod.name))
                {
                    modNameDict.Add(mod.name, 1);
                }
                else
                {
                    modNameDict[mod.name] += 1;
                }
            }

            foreach (var mod in modNameDict)
            {
                ModPackContentList += $"[{ mod.Value}] {mod.Key}\n";
            }

            ModToggleText = enabledCount > disabledCount ? UIStrings.Disable : UIStrings.Enable;
        }

        /// <summary>
        /// Clears the list of mods 
        /// </summary>
        public void ClearList()
        {
            ListVisibility = Visibility.Visible;
            InfoGridVisibility = Visibility.Collapsed;
            ModListPreviewList.Clear();
        }

        /// <summary>
        /// Removes an item from the list when deleted
        /// </summary>
        /// <param name="item">The mod item to remove</param>
        /// <param name="category">The Category object for the item</param>
        public void RemoveItem(ModListModel item, Category category)
        {
            var modList = _modding.GetModList();

            var remainingList = (from items in modList.Mods
                                where items.name == item.ModItem.name
                                select items).ToList();

            if (remainingList.Count == 0)
            {
                Category parentCategory = null;
                foreach (var modPackCategory in Categories)
                {
                    parentCategory = (from parent in modPackCategory.Categories
                        where parent.Name.Equals(item.ModItem.category)
                        select parent).FirstOrDefault();

                    if (parentCategory != null)
                    {
                        break;
                    }
                }

                if (parentCategory != null)
                {
                    parentCategory.Categories.Remove(category);

                    if (parentCategory.Categories.Count == 0)
                    {
                        Categories.Remove(parentCategory);
                    }
                }
            }

            ModListPreviewList.Remove(item);
        }

        /// <summary>
        /// Refreshes the view after a mod pack is deleted
        /// </summary>
        public void RemoveModPack() {
            GetCategoriesModPackFilter();
        }

        public void UpdateSearch() {
            GetCategoriesModPackFilter();
        }

        /// <summary>
        /// The text for the mod toggle button
        /// </summary>
        public string ModToggleText
        {
            get => _modToggleText;
            set
            {
                _modToggleText = value;
                OnPropertyChanged(nameof(ModToggleText));
            }
        }

        /// <summary>
        /// The visibility of the mod list item view
        /// </summary>
        public Visibility ListVisibility
        {
            get => _listVisibility;
            set
            {
                _listVisibility = value;
                OnPropertyChanged(nameof(ListVisibility));
            }
        }

        /// <summary>
        /// THe visibility of the info grid view
        /// </summary>
        public Visibility InfoGridVisibility
        {
            get => _infoGridVisibility;
            set
            {
                _infoGridVisibility = value;
                OnPropertyChanged(nameof(InfoGridVisibility));
            }
        }

        /// <summary>
        /// The mod pack title
        /// </summary>
        public string ModPackTitle
        {
            get => _modPackTitle;
            set
            {
                _modPackTitle = value;
                OnPropertyChanged(nameof(ModPackTitle));
            }
        }

        /// <summary>
        /// The label for the mod pack author in the info grid
        /// </summary>
        public string ModPackModAuthorLabel
        {
            get => _modPackModAuthorLabel;
            set
            {
                _modPackModAuthorLabel = value;
                OnPropertyChanged(nameof(ModPackModAuthorLabel));
            }
        }

        /// <summary>
        /// The label for the mod pack mod count in the info grid
        /// </summary>
        public string ModPackModCountLabel
        {
            get => _modPackModCountLabel;
            set
            {
                _modPackModCountLabel = value;
                OnPropertyChanged(nameof(ModPackModCountLabel));
            }
        }

        /// <summary>
        /// the label for the mod pack version in the info grid
        /// </summary>
        public string ModPackModVersionLabel
        {
            get => _modPackModVersionLabel;
            set
            {
                _modPackModVersionLabel = value;
                OnPropertyChanged(nameof(ModPackModVersionLabel));
            }
        }

        /// <summary>
        /// The content of the mod pack as a string
        /// </summary>
        public string ModPackContentList
        {
            get => _modPackContentList;
            set
            {
                _modPackContentList = value;
                OnPropertyChanged(nameof(ModPackContentList));
            }
        }

        public int ProgressValue
        {
            get => _progressValue;
            set
            {
                _progressValue = value;
                OnPropertyChanged(nameof(ProgressValue));
            }
        }

        public string ProgressText
        {
            get => _progressText;
            set
            {
                _progressText = value;
                OnPropertyChanged(nameof(ProgressText));
            }
        }

        public void Dispose()
        {
            Categories = null;
            ModListPreviewList = null;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public class ModListModel : INotifyPropertyChanged
        {
            private SolidColorBrush _active, _activeBorder;
            private float _opacity;

            /// <summary>
            /// The race of the modded item
            /// </summary>
            public string Race { get; set; }

            /// <summary>
            /// The texture map of the modded item
            /// </summary>
            public string Map { get; set; }

            /// <summary>
            /// The part of the modded item
            /// </summary>
            public string Part { get; set; }

            /// <summary>
            /// The type of the modded item
            /// </summary>
            public string Type { get; set; }

            /// <summary>
            /// The brush color reflecting the active status of the modded item
            /// </summary>
            public SolidColorBrush Active
            {
                get => _active;
                set
                {
                    _active = value;
                    OnPropertyChanged(nameof(Active));
                }
            }

            /// <summary>
            /// The opacity reflecting the active status of the modded item
            /// </summary>
            public float ActiveOpacity
            {
                get => _opacity;

                set
                {
                    _opacity = value;
                    OnPropertyChanged(nameof(ActiveOpacity));
                }
            }

            /// <summary>
            /// The border brush color reflecting the active status of the modded item
            /// </summary>
            public SolidColorBrush ActiveBorder
            {
                get => _activeBorder;
                set
                {
                    _activeBorder = value;
                    OnPropertyChanged(nameof(ActiveBorder));
                }
            }

            /// <summary>
            /// The mod item
            /// </summary>
            public Mod ModItem { get; set; }

            /// <summary>
            /// The image of the modded item
            /// </summary>
            public BitmapSource Image { get; set; }


            public event PropertyChangedEventHandler PropertyChanged;

            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        /// <summary>
        /// Gets the language for the application
        /// </summary>
        /// <returns>The application language as XivLanguage</returns>
        private static XivLanguage GetLanguage()
        {
            return XivLanguages.GetXivLanguage(Properties.Settings.Default.Application_Language);
        }
    }
}