using System.Buffers.Binary;
using SkiaSharp;

namespace Aptison.Booking.Infrastructure.Images;

/// <summary>
/// The orientation in a PNG's <c>eXIf</c> chunk. Skia's PNG codec does not read that chunk and
/// reports every PNG as upright; it does read a JPEG's.
/// </summary>
internal static class PngExifOrientation
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private const ushort OrientationTag = 0x0112;

    public static SKEncodedOrigin Read(ReadOnlySpan<byte> png)
    {
        if (!png.StartsWith(Signature))
        {
            return SKEncodedOrigin.TopLeft;
        }

        // Each chunk: 4 bytes length, 4 bytes type, the data, 4 bytes CRC. eXIf must precede IDAT.
        for (var offset = Signature.Length; offset + 8 <= png.Length;)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png[offset..]);
            var type = png.Slice(offset + 4, 4);

            if (length < 0 || offset + 12 + length > png.Length || type.SequenceEqual("IDAT"u8))
            {
                break;
            }

            if (type.SequenceEqual("eXIf"u8))
            {
                return ReadTiffOrientation(png.Slice(offset + 8, length));
            }

            offset += 12 + length;
        }

        return SKEncodedOrigin.TopLeft;
    }

    /// <summary>The first IFD of a TIFF block, either byte order.</summary>
    private static SKEncodedOrigin ReadTiffOrientation(ReadOnlySpan<byte> tiff)
    {
        if (tiff.Length < 8)
        {
            return SKEncodedOrigin.TopLeft;
        }

        var bigEndian = tiff[0] == (byte)'M';
        var directory = ReadUInt32(tiff, 4, bigEndian);

        if (directory + 2 > tiff.Length)
        {
            return SKEncodedOrigin.TopLeft;
        }

        var entryCount = ReadUInt16(tiff, (int)directory, bigEndian);

        for (var index = 0; index < entryCount; index++)
        {
            var entry = (int)directory + 2 + (index * 12);

            if (entry + 12 > tiff.Length)
            {
                break;
            }

            if (ReadUInt16(tiff, entry, bigEndian) == OrientationTag)
            {
                var value = ReadUInt16(tiff, entry + 8, bigEndian);
                return value is >= 1 and <= 8 ? (SKEncodedOrigin)value : SKEncodedOrigin.TopLeft;
            }
        }

        return SKEncodedOrigin.TopLeft;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> tiff, int at, bool bigEndian) =>
        bigEndian ? BinaryPrimitives.ReadUInt16BigEndian(tiff[at..]) : BinaryPrimitives.ReadUInt16LittleEndian(tiff[at..]);

    private static uint ReadUInt32(ReadOnlySpan<byte> tiff, int at, bool bigEndian) =>
        bigEndian ? BinaryPrimitives.ReadUInt32BigEndian(tiff[at..]) : BinaryPrimitives.ReadUInt32LittleEndian(tiff[at..]);
}
