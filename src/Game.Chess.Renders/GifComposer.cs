using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace Game.Chess.Renders
{
    internal static class GifComposer
    {
        public static byte[] CombineBitmapsToGif(List<Bitmap> bitmaps)
        {
            if (bitmaps == null || bitmaps.Count == 0) throw new ArgumentException("No frames provided.", nameof(bitmaps));

            var gifCodec = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.MimeType == "image/gif") ?? throw new InvalidOperationException("GIF codec not available.");
            using var first = bitmaps[0];
            using var ms = new MemoryStream();

            try
            {
                var existing = first.PropertyItems;
                if (existing != null && existing.Length > 0)
                {
                    var proto = existing[0];
                    PropertyItem? loopProp = null;
                    try { loopProp = first.GetPropertyItem(proto.Id); } catch { loopProp = proto; }
                    if (loopProp != null)
                    {
                        loopProp.Id = 0x5101; // PropertyTagLoopCount
                        loopProp.Type = 3; // SHORT
                        loopProp.Value = BitConverter.GetBytes((short)0); // 0 -> infinite loop
                        loopProp.Len = loopProp.Value.Length;
                        try { first.SetPropertyItem(loopProp); } catch { }
                    }
                }
            }
            catch { }

            var ep = new EncoderParameters(1);
            ep.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.MultiFrame);
            first.Save(ms, gifCodec, ep);

            for (int i = 1; i < bitmaps.Count; i++)
            {
                var ep2 = new EncoderParameters(1);
                ep2.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.FrameDimensionTime);
                first.SaveAdd(bitmaps[i], ep2);
                bitmaps[i].Dispose();
            }

            var epFlush = new EncoderParameters(1);
            epFlush.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.Flush);
            first.SaveAdd(epFlush);

            return ms.ToArray();
        }

        public static byte[] CombinePngPairsToGif(List<(byte[] fromPng, byte[] toPng)> pngPairs)
        {
            if (pngPairs == null || pngPairs.Count == 0) throw new ArgumentException("No PNG pairs provided.", nameof(pngPairs));
            var bitmaps = new List<Bitmap>();
            foreach (var (fromPng, toPng) in pngPairs)
            {
                try
                {
                    using var fromStream = new MemoryStream(fromPng);
                    using var toStream = new MemoryStream(toPng);
                    var fromBmp = new Bitmap(fromStream);
                    var toBmp = new Bitmap(toStream);
                    bitmaps.Add(fromBmp);
                    bitmaps.Add(toBmp);
                }
                catch { }
            }

            if (bitmaps.Count == 0) throw new InvalidOperationException("No valid frames available to render GIF.");
            return CombineBitmapsToGif(bitmaps);
        }
    }
}
