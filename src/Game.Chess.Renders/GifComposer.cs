using System.Drawing;
using System.Drawing.Imaging;

namespace Game.Chess.Renders;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
internal static class GifComposer
{
    /// <summary>
    /// Combine a list of bitmaps into a single animated GIF.
    /// Frame delays are applied in milliseconds. GIF loops infinitely.
    /// </summary>
    /// <param name="bitmaps">Frames to include in the GIF.</param>
    /// <param name="frameDelayMs">Delay per frame in milliseconds (default 400ms).</param>
    public static byte[] Combine(List<Bitmap> bitmaps, int frameDelayMs = 400)
    {
        if (bitmaps == null || bitmaps.Count == 0)
            throw new ArgumentException("No frames provided.", nameof(bitmaps));

        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("GIF encoding only supported on Windows.");

        var gifCodec = ImageCodecInfo.GetImageEncoders()
            .FirstOrDefault(c => c.MimeType == "image/gif")
            ?? throw new InvalidOperationException("GIF codec not available.");

        if (bitmaps.Count == 0) throw new InvalidOperationException("No valid frames available to render GIF.");

        using var first = bitmaps[0];
        using var ms = new MemoryStream();

        // Try to set GIF loop and frame delays by cloning an existing PropertyItem if available.
        // This avoids FormatterServices.GetUninitializedObject and uses existing PropertyItem instances.
        var existing = first.PropertyItems;
        if (existing != null && existing.Length > 0)
        {
            var proto = existing[0];
            PropertyItem? loopProp = null;
            try { loopProp = first.GetPropertyItem(proto.Id); }
            catch
            {
                throw new InvalidOperationException("Failed to get frame loop property on GIF.");
            }
            if (loopProp != null)
            {
                loopProp.Id = 0x5101; // PropertyTagLoopCount
                loopProp.Type = 3; // SHORT
                loopProp.Value = BitConverter.GetBytes((short)0); // Infinite loop
                loopProp.Len = loopProp.Value.Length;
                try { first.SetPropertyItem(loopProp); }
                catch
                {
                    throw new InvalidOperationException("Failed to set frame loop on GIF.");
                }
            }

            int frameCount = bitmaps.Count;
            short baseDelay = 24 * 4 * 100;
            var delays = new byte[4 * frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                short d = baseDelay;
                if (i == frameCount - 1) d = (short)(d + 10);
                var bytes = BitConverter.GetBytes((int)d);
                Array.Copy(bytes, 0, delays, i * 4, 4);
            }

            PropertyItem? delayProp = null;
            try { delayProp = first.GetPropertyItem(proto.Id); }
            catch
            {
                throw new InvalidOperationException("Failed to get frame delay property on GIF.");
            }
            if (delayProp != null)
            {
                delayProp.Id = 0x5100; // PropertyTagFrameDelay
                delayProp.Type = 4; // LONG
                delayProp.Value = delays;
                delayProp.Len = delays.Length;
                try { first.SetPropertyItem(delayProp); }
                catch
                {
                    throw new InvalidOperationException("Failed to set frame delays on GIF.");
                }
            }
        }

        var ep = new EncoderParameters(1);
        ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.SaveFlag, (long)EncoderValue.MultiFrame);
        first.Save(ms, gifCodec, ep);

        for (int i = 1; i < bitmaps.Count; i++)
        {
            var ep2 = new EncoderParameters(1);
            ep2.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.SaveFlag, (long)EncoderValue.FrameDimensionTime);
            first.SaveAdd(bitmaps[i], ep2);
            bitmaps[i].Dispose();
        }

        var epFlush = new EncoderParameters(1);
        epFlush.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.SaveFlag, (long)EncoderValue.Flush);
        first.SaveAdd(epFlush);

        return ms.ToArray();
    }
}
