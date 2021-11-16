﻿using PixelStacker.Extensions;
using PixelStacker.Logic.CanvasEditor.History;
using PixelStacker.Logic.IO.Config;
using PixelStacker.Logic.Model;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PixelStacker.Logic.CanvasEditor
{
    public partial class RenderedCanvasPainter
    {
        public EditHistory History { get; }
        public BufferedHistory HistoryBuffer { get; set; } = new BufferedHistory();

        public async Task DoRenderFromHistoryBuffer()
        {
            List<RenderRecord> records = HistoryBuffer.ToRenderInstructions(true);
            await DoProcessRenderRecords(records);
        }

        private List<bool[,]> GetChunksThatNeedReRendering(IEnumerable<PxPoint> chunks)
        {
            var output = new List<bool[,]>();
            for (int i = 0; i < Padlocks.Count; i++)
            {
                output.Add(new bool[Padlocks[i].GetLength(0), Padlocks[i].GetLength(1)]);
            }

            foreach (var chunk in chunks)
            {
                output[0][chunk.X, chunk.Y] = true;
            }

            for (int i = 1; i < output.Count; i++)
            {
                var curLayer = output[i];
                var upperLayer = output[i - 1];

                for (int x = 0; x < upperLayer.GetLength(0); x++)
                {
                    for (int y = 0; y < upperLayer.GetLength(1); y++)
                    {
                        curLayer[x / 2, y / 2] |= upperLayer[x, y];
                    }
                }
            }

            return output;
        }

        public Task DoProcessRenderRecords(List<RenderRecord> records)
        {
            if (records.Count == 0) return Task.CompletedTask;
            //var uniqueChunkIndexes = records.SelectMany(x => x.ChangedPixels).Distinct();
            var chunkIndexes = records.SelectMany(x => x.ChangedPixels.Select(cp => new
            {
                PaletteID = x.PaletteID,
                X = cp.X,
                Y = cp.Y
            })).GroupBy(cp => new PxPoint(GetChunkIndexX(cp.X), GetChunkIndexY(cp.Y)));

            var chunksThatNeedReRendering = GetChunksThatNeedReRendering(chunkIndexes.Select(x => x.Key));
            using SKPaint paint = new SKPaint() { BlendMode = SKBlendMode.Src };

            // Layer 0
            // Iterate over chunks
            bool isv = Data.IsSideView;
            foreach (var changeGroup in chunkIndexes)
            {
                // Get a lock on the current chunk's image and make a copy
                PxPoint chunkIndex = changeGroup.Key;
                SKBitmap bmCopied = null;
                lock (this.Padlocks[0][chunkIndex.X, chunkIndex.Y])
                {
                    bmCopied = Bitmaps[0][chunkIndex.X, chunkIndex.Y].Copy();
                }

                int offsetX = chunkIndex.X * BlocksPerChunk;
                int offsetY = chunkIndex.Y * BlocksPerChunk;
                // Modify the copied chunk
                using SKCanvas skCanvas = new SKCanvas(bmCopied);
                foreach (var pxToModify in changeGroup)
                {
                    MaterialCombination mc = Data.MaterialPalette[pxToModify.PaletteID];
                    int ix = Constants.TextureSize * (pxToModify.X - offsetX);
                    int iy = Constants.TextureSize * (pxToModify.Y - offsetY);
                    skCanvas.DrawBitmap(mc.GetImage(isv), ix, iy, paint);
                }

                lock (this.Padlocks[0][chunkIndex.X, chunkIndex.Y])
                {
                    var tmp = Bitmaps[0][chunkIndex.X, chunkIndex.Y];
                    Bitmaps[0][chunkIndex.X, chunkIndex.Y] = bmCopied;
                    tmp.DisposeSafely();
                }
            }

            // OTHER LAYERS 2.0
            {
                float pixelsPerHalfChunk = Constants.TextureSize * BlocksPerChunk / 2;
                for (int layerIndexToRender = 1; layerIndexToRender < chunksThatNeedReRendering.Count; layerIndexToRender++)
                {
                    int scaleDivide = (int)Math.Pow(2, layerIndexToRender);
                    var curLayer = chunksThatNeedReRendering[layerIndexToRender];
                    var upperLayer = chunksThatNeedReRendering[layerIndexToRender - 1];
                    for (int xIndexCurrentLayer = 0; xIndexCurrentLayer < curLayer.GetLength(0); xIndexCurrentLayer++)
                    {
                        for (int yIndexCurrentLayer = 0; yIndexCurrentLayer < curLayer.GetLength(1); yIndexCurrentLayer++)
                        {
                            // No need to re-render this chunk?
                            if (!curLayer[xIndexCurrentLayer, yIndexCurrentLayer]) continue;

                            SKBitmap bmToEdit = null;
                            lock (Padlocks[layerIndexToRender][xIndexCurrentLayer, yIndexCurrentLayer])
                            {
                                bmToEdit = Bitmaps[layerIndexToRender][xIndexCurrentLayer, yIndexCurrentLayer].Copy();
                            }

                            using SKCanvas g = new SKCanvas(bmToEdit);
                            //g.DrawRect(0, 0, bmToEdit.Width, bmToEdit.Height, new SKPaint() { Color = new SKColor(255, 0, 0, 255) });

                            int xUpper = xIndexCurrentLayer * 2;
                            int yUpper = yIndexCurrentLayer * 2;

                            // TL
                            if (upperLayer[xIndexCurrentLayer * 2, yIndexCurrentLayer * 2])
                            {
                                SKBitmap bmToCopy = Bitmaps[layerIndexToRender - 1][xUpper, yUpper];
                                var rect = new SKRect()
                                {
                                    Location = new SKPoint(0, 0),
                                    Size = new SKSize(bmToCopy.Width / 2, bmToCopy.Height / 2)
                                };

                                g.DrawBitmap(bmToCopy, rect, paint);
                            }

                            // TR
                            if (upperLayer.GetLength(0) > xUpper + 1
                                && upperLayer.GetLength(1) > yUpper
                                && upperLayer[xUpper + 1, yUpper])
                            {
                                SKBitmap bmToCopy = Bitmaps[layerIndexToRender - 1][xUpper + 1, yUpper];
                                var rect = new SKRect()
                                {
                                    Location = new SKPoint(pixelsPerHalfChunk, 0),
                                    Size = new SKSize(bmToCopy.Width / 2, bmToCopy.Height / 2)
                                };

                                g.DrawBitmap(bmToCopy, rect, paint);
                            }

                            // BL
                            if (upperLayer.GetLength(0) > xUpper
                                && upperLayer.GetLength(1) > yUpper + 1
                                && upperLayer[xUpper, yUpper + 1])
                            {
                                SKBitmap bmToCopy = Bitmaps[layerIndexToRender - 1][xUpper, yUpper + 1];
                                var rect = new SKRect()
                                {
                                    Location = new SKPoint(0, pixelsPerHalfChunk),
                                    Size = new SKSize(bmToCopy.Width / 2, bmToCopy.Height / 2)
                                };

                                g.DrawBitmap(bmToCopy, rect, paint);
                            }

                            // BR
                            if (upperLayer.GetLength(0) > xUpper + 1
                                && upperLayer.GetLength(1) > yUpper + 1
                                && upperLayer[xUpper + 1, yUpper + 1])
                            {
                                SKBitmap bmToCopy = Bitmaps[layerIndexToRender - 1][xUpper + 1, yUpper + 1];
                                var rect = new SKRect()
                                {
                                    Location = new SKPoint(pixelsPerHalfChunk, pixelsPerHalfChunk),
                                    Size = new SKSize(bmToCopy.Width / 2, bmToCopy.Height / 2)
                                };

                                g.DrawBitmap(bmToCopy, rect, paint);
                            }
                            //// Let's talk it out.
                            //// L0 is your base set of chunks. 
                            //// L1 has half as many chunks as L0.
                            //// given L1[x,y] which x,y coordinates would be from L0[x,y]?
                            //DrawTileOnLargerTile(layerIndexToRender, upperLayer, g, xUpper, yUpper, 0F, 0F, paint);
                            //DrawTileOnLargerTile(layerIndexToRender, upperLayer, g, xu, yu + 1, 0F, pixelsPerHalfChunk, paint);
                            //DrawTileOnLargerTile(layerIndexToRender, upperLayer, g, xu + 1, yu, pixelsPerHalfChunk, 0F, paint);
                            //DrawTileOnLargerTile(layerIndexToRender, upperLayer, g, xu + 1, yu + 1, pixelsPerHalfChunk, pixelsPerHalfChunk, paint);

                            lock (Padlocks[layerIndexToRender][xIndexCurrentLayer, yIndexCurrentLayer])
                            {
                                var tmp = Bitmaps[layerIndexToRender][xIndexCurrentLayer, yIndexCurrentLayer];
                                Bitmaps[layerIndexToRender][xIndexCurrentLayer, yIndexCurrentLayer] = bmToEdit;
                                tmp.DisposeSafely();
                            }
                        }
                    }
                }
                // THIS is where the slow down happens.
                // Why do we copy every tile in the entire set every time any change occurs? FIX IT.
                #region OTHER LAYERS
                //{
                //    for (int l = 1; l < Bitmaps.Count; l++)
                //    {
                //        SKBitmap[,] bmSet = Bitmaps[l];
                //        int scaleDivide = (int)Math.Pow(2, l);
                //        int numChunksWide = bmSet.GetLength(0);
                //        int numChunksHigh = bmSet.GetLength(1);
                //        int srcPixelsPerChunk = BlocksPerChunk * scaleDivide;
                //        int dstPixelsPerChunk = Constants.TextureSize * srcPixelsPerChunk / scaleDivide;

                //        for (int x = 0; x < bmSet.GetLength(0); x++)
                //        {
                //            for (int y = 0; y < bmSet.GetLength(1); y++)
                //            {
                //                SKSize dstSize;
                //                lock (Padlocks[l][x, y])
                //                {
                //                    dstSize = bmSet[x, y].Info.Size;
                //                }

                //                var bm = new SKBitmap((int)dstSize.Width, (int)dstSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
                //                using SKCanvas g = new SKCanvas(bm);

                //                // tiles within the chunk. We iterate over the main src image to get our content for our chunk data.
                //                for (int xWithinDownsizedChunk = 0; xWithinDownsizedChunk < scaleDivide; xWithinDownsizedChunk++)
                //                {
                //                    for (int yWithinDownsizedChunk = 0; yWithinDownsizedChunk < scaleDivide; yWithinDownsizedChunk++)
                //                    {
                //                        int xIndexOIfL0Chunk = xWithinDownsizedChunk + scaleDivide * x;
                //                        int yIndexOfL0Chunk = yWithinDownsizedChunk + scaleDivide * y;
                //                        if (xIndexOIfL0Chunk > Bitmaps[0].GetLength(0) - 1 || yIndexOfL0Chunk > Bitmaps[0].GetLength(1) - 1)
                //                            continue;

                //                        lock (Padlocks[l][x, y])
                //                        {
                //                            var bmToPaint = Bitmaps[0][xIndexOIfL0Chunk, yIndexOfL0Chunk];
                //                            var rect = new SKRect()
                //                            {
                //                                Location = new SKPoint((float)(xWithinDownsizedChunk * dstPixelsPerChunk / scaleDivide),
                //                                    (float)(yWithinDownsizedChunk * dstPixelsPerChunk / scaleDivide)),
                //                                Size = new SKSize(dstPixelsPerChunk / scaleDivide, dstPixelsPerChunk / scaleDivide)
                //                            };

                //                            g.DrawBitmap(
                //                            bmToPaint,
                //                            rect);
                //                        }
                //                    }
                //                }

                //                lock (Padlocks[l][x, y])
                //                {
                //                    Bitmaps[l][x, y].DisposeSafely();
                //                    Bitmaps[l][x, y] = bm;
                //                }
                //            }
                //        }
                //    }
                //}
                #endregion OTHER LAYERS


                return Task.CompletedTask;
            }

            void DrawTileOnLargerTile(int layerToRender, bool[,] upperLayer, SKCanvas g, int xu, int yu, float xOffset, float yOffset, SKPaint paint)
            {
                try
                {
                    if (upperLayer.GetLength(0) <= xu) return;
                    if (upperLayer.GetLength(1) <= yu) return;
                    if (Padlocks[layerToRender - 1].GetLength(0) <= xu) return;
                    if (Padlocks[layerToRender - 1].GetLength(1) <= yu) return;
                    if (Padlocks[layerToRender].GetLength(0) <= xu / 2) return;
                    if (Padlocks[layerToRender].GetLength(1) <= yu / 2) return;
                    if (upperLayer[xu, yu])
                    {
                        lock (Padlocks[layerToRender][xu, yu])
                        {
                            SKBitmap bmToCopy = Bitmaps[layerToRender - 0][xu, yu];
                            var rect = new SKRect()
                            {
                                Location = new SKPoint(xOffset, yOffset),
                                Size = new SKSize(bmToCopy.Width / 2, bmToCopy.Height / 2)
                            };

                            g.DrawBitmap(bmToCopy, rect, paint);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                }
            }
        }
    }
}
