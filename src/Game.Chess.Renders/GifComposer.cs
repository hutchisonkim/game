using System.Drawing;
using System.Drawing.Imaging;

namespace Game.Chess.Renders;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
internal static class GifComposer
{
    /// <summary>
    /// Combine a list of bitmaps into a single animated GIF.
    /// If <paramref name="frameDelaysMs"/> is provided and a PropertyItem template
    /// can be found on one of the images, the GIF frame delays will be set (in milliseconds).
    /// Note: GIF frame delays are stored in 1/100th second units; values will be rounded and
    /// a minimum of 1 (0.01s) will be used when applicable.
    /// </summary>
    /// <param name="bitmaps">Frames to include in the GIF. The first bitmap will be used as the primary image and disposed at the end.</param>
    /// <param name="frameDelaysMs">Optional per-frame delays in milliseconds. If non-null and length >= frame count, delays will be applied. Otherwise delays are not modified.</param>
    public static byte[] CombineBitmapsToGif(List<Bitmap> bitmaps, int frameDelaysMs = 0)
    {
        if (bitmaps == null || bitmaps.Count == 0) throw new ArgumentException("No frames provided.", nameof(bitmaps));

        var gifCodec = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.MimeType == "image/gif") ?? throw new InvalidOperationException("GIF codec not available.");
        using var first = bitmaps[0];
        using var ms = new MemoryStream();

        try
        {
            // Try to find a PropertyItem template from any of the bitmaps so we can set GIF properties
            PropertyItem? proto = null;
            foreach (var bmp in bitmaps)
            {
                var items = bmp.PropertyItems;
                if (items != null && items.Length > 0)
                {
                    proto = items[0];
                    break;
                }
            }

            if (proto != null)
            {
                // Set loop count -> 0 (infinite loop)
                try
                {
                    var loopProp = proto;
                    loopProp.Id = 0x5101; // PropertyTagLoopCount
                    loopProp.Type = 3; // SHORT
                    loopProp.Value = BitConverter.GetBytes((short)0); // 0 -> infinite loop
                    loopProp.Len = loopProp.Value.Length;
                    try { first.SetPropertyItem(loopProp); } catch { }
                }
                catch { }

                // If caller provided per-frame delays, attempt to set PropertyTagFrameDelay (0x5100)
                try
                {
                    if (frameDelaysMs != 0)
                    {
                        // GIF frame delays are stored as an array of 4-byte unsigned ints (1/100th seconds)
                        var delays = new byte[4 * bitmaps.Count];
                        for (int i = 0; i < bitmaps.Count; i++)
                        {
                            // Convert milliseconds to 1/100th seconds and ensure minimum of 1
                            var hundredths = Math.Max(1, (int)Math.Round(frameDelaysMs / 10.0));
                            var dbytes = BitConverter.GetBytes(hundredths);
                            Buffer.BlockCopy(dbytes, 0, delays, i * 4, 4);
                        }

                        var delayProp = proto;
                        delayProp.Id = 0x5100; // PropertyTagFrameDelay
                        delayProp.Type = 4; // LONG
                        delayProp.Value = delays;
                        delayProp.Len = delays.Length;
                        try { first.SetPropertyItem(delayProp); } catch { }
                    }
                }
                catch { }
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

    public static byte[] CombinePngPairsToGif(List<(byte[] fromPng, byte[] toPng)> pngPairs, int frameDelaysMs = 0)
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
        return CombineBitmapsToGif(bitmaps, frameDelaysMs);
    }
}
