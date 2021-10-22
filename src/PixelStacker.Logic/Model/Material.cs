﻿using PixelStacker.Extensions;
using PixelStacker.IO.Config;
using PixelStacker.Resources;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Threading;

namespace PixelStacker.Logic
{
    [Serializable]
    public class Material
    {
        public string PixelStackerID { get; set; }
        public string Label { get; set; }
        public int BlockID { get; set; }
        public int Data { get; set; }
        public Bitmap SideImage { get; private set; }
        public Bitmap TopImage { get; private set; }
        public string Category { get; set; }
        public string SchematicaMaterialName { get; set; }
        public bool IsAdvanced { get; set; } = false;

        /// <summary>
        /// minecraft:stone_1
        /// </summary>
        private string TopBlockName { get; set; }

        /// <summary>
        /// minecraft:stone_1[axis=y]
        /// </summary>
        private string SideBlockName { get; set; }

        private Color? _averageColor = null;
        private Color? _averageColorSide = null;

        /// <summary>
        /// List of words or phrases ppl can search for to get these materials
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        private static readonly string[] ValidMinecraftVersions = new string[] {
            "NEW", "1.7", // 1.7 and 1.12 will contain inaccuracies where I was originally "rounding" up or down.
            "1.8", "1.9", "1.10",
            "1.12", "1.13", "1.14", "1.15", "1.16",
            "1.17"
        };

        private string _minimumSupportedMinecraftVersion = "NEW";
        public string MinimumSupportedMinecraftVersion
        {
            get => this._minimumSupportedMinecraftVersion;
            set
            {
                if (string.IsNullOrWhiteSpace(value) || !ValidMinecraftVersions.Contains(value))
                {
                    throw new ArgumentNullException($"Invalid MC Version provided. Given '{value}' Expected: {string.Join(", ", Material.ValidMinecraftVersions)}");
                }

                _minimumSupportedMinecraftVersion = value;
            }
        }

        public Material(string minMcVersion, bool isAdvancedMaterial, string category, string pixelStackerID, string label, int blockID, int data, byte[] topImage, byte[] sideImage, string topBlockName, string sideBlockName, string schematicaMaterialName)
           : this(minMcVersion, isAdvancedMaterial, category, pixelStackerID, label, blockID, data, topImage.ToBitmap(),sideImage.ToBitmap(), topBlockName, sideBlockName, schematicaMaterialName)
        {}

        public Material(string minMcVersion, bool isAdvancedMaterial, string category, string pixelStackerID, string label, int blockID, int data, Bitmap topImage, Bitmap sideImage, string topBlockName, string sideBlockName, string schematicaMaterialName)
        {
            this.MinimumSupportedMinecraftVersion = minMcVersion;
            this.IsAdvanced = isAdvancedMaterial;
            this.PixelStackerID = pixelStackerID;
            this.Label = label;
            this.BlockID = blockID;
            this.Data = data;
            this.TopImage = topImage.To32bppBitmap();
            this.SideImage = (sideImage ?? topImage).To32bppBitmap();
            this.Category = category;
            this.TopBlockName = topBlockName;
            this.SideBlockName = sideBlockName;
            this.SchematicaMaterialName = schematicaMaterialName;
        }

        public string ToConstructorString()
        {
            ResourceSet resources = Textures.ResourceManager.GetResourceSet(CultureInfo.CurrentCulture, false, true);
            DictionaryEntry? topImageResource = null;
            DictionaryEntry? sideImageResource = null;

            foreach (DictionaryEntry resource in resources)
            {
                if (resource.Value is Bitmap)
                {
                    if (topImageResource == null)
                    {
                        if ((resource.Value as Bitmap).AreEqual(this.TopImage))
                        {
                            topImageResource = resource;
                        }
                    }

                    if (sideImageResource == null)
                    {
                        if ((resource.Value as Bitmap).AreEqual(this.SideImage))
                        {
                            sideImageResource = resource;
                        }
                    }
                }
            }

            if (topImageResource == null)
            {
                throw new Exception("Top image not found in list");
            }

            if (sideImageResource == null)
            {
                throw new Exception("Side image not found in list");
            }

            string topBlockName = this.TopBlockName.Replace("minecraft:" + topImageResource.Value.Key.ToString(), "minecraft:{nameof(Textures." + topImageResource.Value.Key.ToString() + ")}");
            string sideBlockName = this.SideBlockName.Replace("minecraft:" + topImageResource.Value.Key.ToString(), "minecraft:{nameof(Textures." + topImageResource.Value.Key.ToString() + ")}");
       
            return $"new Material("
                + $"\"{this.MinimumSupportedMinecraftVersion}\", "
                + $"{this.IsAdvanced.ToString().ToLowerInvariant()}, "
                + $"\"{this.Category}\", "
                + $"\"{this.PixelStackerID}\", "
                + $"\"{this.Label}\", "
                + $"{this.BlockID}, "
                + $"{this.Data}, "
                + $"Textures.{topImageResource.Value.Key}, "
                + $"Textures.{sideImageResource.Value.Key}, "
                + $"$\"{topBlockName}\", "
                + $"$\"{sideBlockName}\", "
                + $"\"{this.SchematicaMaterialName}\""
                + "),";
        }

        private string SettingsKey { get { return string.Format("BLOCK_{0}", this.PixelStackerID); } }

        public bool IsVisibleF(Options opts)
        {
            if (this.BlockID == 0)
            {
                return false;
            }

            if (!opts.IsAdvancedModeEnabled && this.IsAdvanced)
            {
                return false;
            }

            return true;
        }

        [Obsolete("IsVisible uses a static reference to Options.Get. Avoid this if you can.")]
        public bool IsVisible
        {
            get
            {
                if (this.BlockID == 0)
                {
                    return false;
                }

                if (!Options.Get.IsAdvancedModeEnabled && this.IsAdvanced)
                {
                    return false;
                }

                return true;
            }
        }

        public bool IsEnabledF(Options opts)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            opts ??= Options.Get;
#pragma warning restore CS0618 // Type or member is obsolete
            if (this.BlockID == 0)
            {
                return false;
            }

            if (this.IsAdvanced && !opts.IsAdvancedModeEnabled)
            {
                return false;
            }

            if (!opts.EnableStates.ContainsKey(SettingsKey))
            {
                opts.EnableStates[SettingsKey] = !this.IsAdvanced;
            }

            return opts.EnableStates[SettingsKey];
        }

        public void IsEnabledF(Options opts, bool val)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            opts ??= Options.Get;
#pragma warning restore CS0618 // Type or member is obsolete
            opts.EnableStates[SettingsKey] = val;
        }

        [Obsolete("IsEnabled uses a static reference to Options.Get. Avoid this if you can.", true)]
        public bool IsEnabled
        {
            get
            {
                if (this.BlockID == 0)
                {
                    return false;
                }

                if (this.IsAdvanced && !Options.Get.IsAdvancedModeEnabled)
                {
                    return false;
                }

                if (!Options.Get.EnableStates.ContainsKey(SettingsKey))
                {
                    Options.Get.EnableStates[SettingsKey] = !this.IsAdvanced;
                }

                return Options.Get.EnableStates[SettingsKey];
            }
            set
            {
            }
        }

        public string GetBlockNameAndData(bool isSide)
        {
            return isSide ? this.SideBlockName : this.TopBlockName;
        }

        public Bitmap GetImage(bool isSide)
        {
            if (isSide)
            {
                return this.SideImage;
            }
            else
            {
                return this.TopImage;
            }
        }
        
        public Color GetAverageColor(bool isSide)
        {
            if (isSide)
            {
                if (_averageColorSide == null)
                {
                    _averageColorSide = GetAverageColor(this.SideImage);
                }
                return _averageColorSide.Value;
            }
            else
            {
                if (_averageColor == null)
                {
                    _averageColor = GetAverageColor(this.TopImage);
                }
                return _averageColor.Value;
            }
        }

        private Color GetAverageColor(Bitmap src, int rgbFragmentSize = 1)
        {
            long r = 0;
            long g = 0;
            long b = 0;
            long a = 0;
            long total = src.Width * src.Height;

            src.ToViewStreamParallel(null, (int x, int y, Color c) =>
            {
                Interlocked.Add(ref r, c.R);
                Interlocked.Add(ref g, c.G);
                Interlocked.Add(ref b, c.B);
                Interlocked.Add(ref a, c.A);
            });

            r /= total;
            g /= total;
            b /= total;
            a /= total;

            if (a > 128)
            {
                a = 255;
            }

            return Color.FromArgb((int)a, (int)r, (int)g, (int)b).Normalize(rgbFragmentSize);
        }

        public override string ToString()
        {
            return this.Label;
        }

        public override bool Equals(object obj)
        {
            var material = obj as Material;
            return material != null &&
                   Label == material.Label;
        }

        public override int GetHashCode()
        {
            return 981597221 + EqualityComparer<string>.Default.GetHashCode(Label);
        }

        public static bool operator ==(Material left, Material right)
        {
            return left?.ToString() == right?.ToString();
        }

        public static bool operator !=(Material left, Material right)
        {
            return left?.ToString() != right?.ToString();
        }
    }
}
