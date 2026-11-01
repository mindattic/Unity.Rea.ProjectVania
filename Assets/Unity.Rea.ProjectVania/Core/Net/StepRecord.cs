using System;
using System.Collections.Generic;
using System.IO;
using Unity.Rea.ProjectVania.Core.Input;

namespace Unity.Rea.ProjectVania.Core.Net
{
    /// <summary>
    /// Everything one step of an online game took from outside the world (U.VAN-T31): each seated player's input frame,
    /// in seat order, and whether the authority's song had ended before it. The authority makes one per step it runs; a
    /// replica that applies the same records to a world built from the same seed holds the same game, because
    /// <see cref="Simulation.GameWorld"/> changes only through the commands these frames route to.
    /// </summary>
    public sealed class StepRecord
    {
        readonly List<SeatFrame> _seats = new List<SeatFrame>();

        public StepRecord(long tick, bool songEnded)
        {
            Tick = tick;
            SongEnded = songEnded;
        }

        /// <summary>The world's <see cref="Simulation.GameWorld.Tick"/> as the step starts.</summary>
        public long Tick { get; }

        /// <summary>The authority's song ran to its end before this step (its <c>EndSong</c>, applied first).</summary>
        public bool SongEnded { get; }

        /// <summary>Each seat's frame for the step, in ascending seat order.</summary>
        public IReadOnlyList<SeatFrame> Seats => _seats;

        public void Add(int seat, InputFrame frame)
        {
            if (seat < 0 || seat > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(seat));
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (_seats.Count > 0 && _seats[_seats.Count - 1].Seat >= seat) throw new ArgumentException("Seats are added in ascending order, once each.", nameof(seat));
            _seats.Add(new SeatFrame(seat, frame));
        }
    }

    /// <summary>One seat's input frame in a <see cref="StepRecord"/>.</summary>
    public readonly struct SeatFrame
    {
        public SeatFrame(int seat, InputFrame frame)
        {
            Seat = seat;
            Frame = frame;
        }

        public int Seat { get; }
        public InputFrame Frame { get; }
    }

    /// <summary>
    /// The wire form of consecutive <see cref="StepRecord"/>s and of a client's own input frames (U.VAN-T31). A seat's
    /// frame that reads the same as its last one in the same message is one byte, so a held or idle control costs
    /// almost nothing.
    /// </summary>
    public static class StepCodec
    {
        const byte Version = 1;
        const byte Same = 0;
        const byte Changed = 1;

        /// <summary>Encodes <paramref name="records"/>, which must be consecutive ticks.</summary>
        public static byte[] EncodeSteps(IReadOnlyList<StepRecord> records)
        {
            if (records == null) throw new ArgumentNullException(nameof(records));
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                writer.Write(records.Count);
                writer.Write(records.Count > 0 ? records[0].Tick : 0L);
                var last = new Dictionary<int, InputFrame>();
                for (var i = 0; i < records.Count; i++)
                {
                    var record = records[i];
                    if (record.Tick != records[0].Tick + i) throw new ArgumentException("The records must be consecutive steps.", nameof(records));
                    writer.Write(record.SongEnded);
                    writer.Write((byte)record.Seats.Count);
                    foreach (var seat in record.Seats)
                    {
                        writer.Write((byte)seat.Seat);
                        WriteFrame(writer, seat.Seat, seat.Frame, last);
                    }
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>The records <see cref="EncodeSteps"/> wrote, or null for bytes that are not such a message.</summary>
        public static List<StepRecord> DecodeSteps(byte[] bytes)
        {
            if (bytes == null) return null;
            try
            {
                using (var reader = new BinaryReader(new MemoryStream(bytes)))
                {
                    if (reader.ReadByte() != Version) return null;
                    var count = reader.ReadInt32();
                    if (count < 0) return null;
                    var first = reader.ReadInt64();
                    var records = new List<StepRecord>(Math.Min(count, 4096));
                    var last = new Dictionary<int, InputFrame>();
                    for (var i = 0; i < count; i++)
                    {
                        var record = new StepRecord(first + i, reader.ReadBoolean());
                        var seats = reader.ReadByte();
                        for (var s = 0; s < seats; s++)
                        {
                            int seat = reader.ReadByte();
                            record.Add(seat, ReadFrame(reader, seat, last));
                        }

                        records.Add(record);
                    }

                    return reader.BaseStream.Position == bytes.Length ? records : null;
                }
            }
            catch (Exception e) when (e is EndOfStreamException || e is ArgumentException || e is IOException)
            {
                return null;
            }
        }

        /// <summary>Encodes a client's own frames, one per step it sampled, oldest first.</summary>
        public static byte[] EncodeFrames(IReadOnlyList<InputFrame> frames)
        {
            if (frames == null) throw new ArgumentNullException(nameof(frames));
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                writer.Write(frames.Count);
                var last = new Dictionary<int, InputFrame>();
                foreach (var frame in frames)
                {
                    WriteFrame(writer, 0, frame, last);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>The frames <see cref="EncodeFrames"/> wrote, or null for bytes that are not such a message.</summary>
        public static List<InputFrame> DecodeFrames(byte[] bytes)
        {
            if (bytes == null) return null;
            try
            {
                using (var reader = new BinaryReader(new MemoryStream(bytes)))
                {
                    if (reader.ReadByte() != Version) return null;
                    var count = reader.ReadInt32();
                    if (count < 0) return null;
                    var frames = new List<InputFrame>(Math.Min(count, 4096));
                    var last = new Dictionary<int, InputFrame>();
                    for (var i = 0; i < count; i++)
                    {
                        frames.Add(ReadFrame(reader, 0, last));
                    }

                    return reader.BaseStream.Position == bytes.Length ? frames : null;
                }
            }
            catch (Exception e) when (e is EndOfStreamException || e is ArgumentException || e is IOException)
            {
                return null;
            }
        }

        /// <summary><paramref name="frame"/> as a replica will read it (the wheel by its sign): what the authority routes.</summary>
        public static InputFrame Normalise(InputFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                frame.WriteTo(writer);
                writer.Flush();
                stream.Position = 0;
                var copy = new InputFrame();
                copy.ReadFrom(new BinaryReader(stream));
                return copy;
            }
        }

        static void WriteFrame(BinaryWriter writer, int seat, InputFrame frame, Dictionary<int, InputFrame> last)
        {
            if (last.TryGetValue(seat, out var previous) && frame.SameAs(previous))
            {
                writer.Write(Same);
                return;
            }

            writer.Write(Changed);
            frame.WriteTo(writer);
            last[seat] = frame;
        }

        static InputFrame ReadFrame(BinaryReader reader, int seat, Dictionary<int, InputFrame> last)
        {
            var kind = reader.ReadByte();
            var frame = new InputFrame();
            if (kind == Same)
            {
                if (!last.TryGetValue(seat, out var previous)) throw new IOException("A repeated frame with nothing before it.");
                frame.CopyFrom(previous);
                return frame;
            }

            if (kind != Changed) throw new IOException("Unknown frame kind " + kind + ".");
            frame.ReadFrom(reader);
            last[seat] = frame;
            return frame;
        }
    }
}
