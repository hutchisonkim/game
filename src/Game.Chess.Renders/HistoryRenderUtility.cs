using System.Drawing;
using System.Drawing.Imaging;

namespace Game.Chess.Renders;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public static class HistoryRenderUtility
{
    /// <summary>
    /// Combine a list of bitmaps into a single animated GIF.
    /// Frame delays are applied in milliseconds. GIF loops infinitely.
    /// </summary>
    /// <param name="bitmaps">Frames to include in the GIF.</param>
    /// <param name="frameDelayMs">Delay per frame in milliseconds (default 800ms).</param>
    public static byte[] RenderGif(List<Bitmap> bitmaps, int frameDelayMs = 800)
    {
        if (bitmaps == null || bitmaps.Count == 0)
            throw new ArgumentException("No frames provided.", nameof(bitmaps));

        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("GIF encoding only supported on Windows.");

        var gifCodec = ImageCodecInfo.GetImageEncoders()
            .FirstOrDefault(c => c.MimeType == "image/gif")
            ?? throw new InvalidOperationException("GIF codec not available.");

        using var first = bitmaps[0];
        using var ms = new MemoryStream();


        try
        {
            // Create a proto PropertyItem from a dummy JPEG
            PropertyItem proto;
            using (var tmp = new Bitmap(1, 1))
            using (var tmpStream = new MemoryStream())
            {
                tmp.Save(tmpStream, ImageFormat.Jpeg);
                using var tmpImg = new Bitmap(tmpStream);
                proto = tmpImg.PropertyItems.First();
            }
            
            // Loop forever
            var loopProp = proto;
            loopProp.Id = 0x5101;
            loopProp.Type = 3;
            loopProp.Value = BitConverter.GetBytes((ushort)0);
            loopProp.Len = loopProp.Value.Length;
            first.SetPropertyItem(loopProp);

        }
        catch
        {
            // throw new InvalidOperationException("Failed to set GIF properties.");
        }
        try
        {
            // Create a proto PropertyItem from a dummy JPEG
            PropertyItem proto;
            using (var tmp = new Bitmap(1, 1))
            using (var tmpStream = new MemoryStream())
            {
                tmp.Save(tmpStream, ImageFormat.Jpeg);
                using var tmpImg = new Bitmap(tmpStream);
                proto = tmpImg.PropertyItems.First();
            }

            // Frame delays
            int frameCount = bitmaps.Count;
            short baseDelay = 12 * 4;
            var delays = new byte[4 * frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                short d = baseDelay;
                if (i == frameCount - 1) d = (short)(d + 10);
                var bytes = BitConverter.GetBytes((int)d);
                Array.Copy(bytes, 0, delays, i * 4, 4);
            }

            var delayProp = proto;
            delayProp.Id = 0x5100;
            delayProp.Type = 4;
            delayProp.Value = delays;
            delayProp.Len = delays.Length;
            first.SetPropertyItem(delayProp);
        }
        catch
        {
            // throw new InvalidOperationException("Failed to set GIF properties.");
        }

        // Save GIF
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
