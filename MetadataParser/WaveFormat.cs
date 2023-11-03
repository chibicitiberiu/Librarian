using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetadataParser
{
    public enum WaveFormatType
    {
        Unknown,
        PCM,
        MS_ADPCM,
        IEEEFloatingPoint,
        ALAW = 6,
        MULAW,
        IMA_ADPCM = 0x11,
        GSM610 = 0x31,
        MPEG = 0x50,
        MPEGLAYER3 = 0x55,
    }

    public class WaveFormat
    {
        public WaveFormatType FormatTag { get; set; } = WaveFormatType.Unknown;

        public ushort Channels { get; set; }

        /// <summary>
        /// Sample frequency
        /// </summary>
        public uint SamplesPerSecond { get; set; }

        /// <summary>
        /// Used to estimate buffer size
        /// </summary>
        public uint AverageBytesPerSecond { get; set; }

        public ushort BlockAlign { get; set; }

        public ushort BitsPerSample { get; set; }
    }
}
