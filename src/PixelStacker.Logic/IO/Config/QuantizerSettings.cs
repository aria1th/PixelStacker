﻿using PixelStacker.Logic.Engine.Quantizer.Enums;
using System;
using System.ComponentModel;
using System.Linq;

namespace PixelStacker.Logic.IO.Config
{
    public class QuantizerSettings
    {
        public bool IsEnabled { get; set; } = false;

        [Category("Quantizing")]
        /// <summary>
        /// Color cache algorithm
        /// </summary>
        public string ColorCache { get; set; }

        [DisplayName("Max color count")]
        [Description(description: "The maximum color count for the quantizer algorithm.")]
        [Category("Quantizing")]
        /// <summary>
        /// Max number of colors.
        /// </summary>
        public int MaxColorCount { get; set; }

        [Category("Quantizing")]
        /// <summary>
        /// The quantizer algorithm to use.
        /// </summary>
        public string Algorithm { get; set; }

        [Category("Dithering")]
        /// <summary>
        /// Dithering method to use. (If any)
        /// </summary>
        public string DitherAlgorithm { get; set; }

        [Category("Dithering")]
        /// <summary>
        /// Acceptable values: 
        /// "1", 2, 4, 8, 16, 32, 64
        /// Number of parallel processes allowed to be used by the quantizer.
        /// </summary>
        public int MaxParallelProcesses { get; set; } = 1;

        public bool IsValid(QuantizerAlgorithmOptions opts, bool fixIfPossible = true)
        {
            if (!QuantizerAlgorithm.Values.Contains(Algorithm))
                return false;

            if (!opts.MaxParallelProcessesList.Contains(MaxParallelProcesses))
            {
                if (!fixIfPossible) return false;
                MaxParallelProcesses = 1;
            }

            if (null != DitherAlgorithm && !opts.DithererList.ContainsKey(DitherAlgorithm))
            {
                if (!fixIfPossible) return false;
                DitherAlgorithm = "No dithering";
            }

            if (ColorCache != null && !opts.ColorCacheList.ContainsKey(ColorCache))
            {
                if (!fixIfPossible) return false;
                ColorCache = opts.ColorCacheList.Keys.First();
            }

            if (!opts.MaxColorCountsList.Contains(MaxColorCount))
            {
                if (!fixIfPossible) return false;
                MaxColorCount = 256;
            }

            return true;
        }
    }
}
