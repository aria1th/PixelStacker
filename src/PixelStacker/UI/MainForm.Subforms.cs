﻿using PixelStacker.Logic.Utilities;
using System;
using System.Linq;

namespace PixelStacker.UI
{
    public partial class MainForm
    {
        private MaterialSelectWindow MaterialOptions { get; set; } = null;

        private void selectMaterialsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.MaterialOptions == null)
            {
                this.MaterialOptions = new MaterialSelectWindow(this.Options);

                this.MaterialOptions.OnColorPaletteRecompileRequested = (token) => {
                    ProgressX.Report(40, Resources.Text.Progress_CompilingColorMap);
                    this.ColorMapper.SetSeedData(this.Palette.ToValidCombinationList(this.Options), this.Palette, this.Options.Preprocessor.IsSideView);
                    ProgressX.Report(100, Resources.Text.Progress_CompiledColorMap);
                };
            }

            this.MaterialOptions.ShowDialog(this);
        }

        private void sizingToolStripMenuItem_Click(object sender, System.EventArgs e)
        {
            var form = new SizeForm(this.Options);
            form.ShowDialog(this);
        }
    }
}
