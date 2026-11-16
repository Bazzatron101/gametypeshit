using System;
using System.IO;
using System.Text;
using HunterSystem.Simulation;

namespace HunterSystem.Game
{
    /// <summary>The rules that turn heights into ground kinds (ticket 39).</summary>
    [Serializable]
    public sealed class GroundRules
    {
        /// <summary>Ground steeper than this cannot be walked (unless marked climbable).</summary>
        public float maxWalkableSlopeDegrees = 40f;

        /// <summary>Ground lower than this is water.</summary>
        public float seaLevel = 4f;
    }

    /// <summary>
    /// The bake from terrain heights to the simulation's ground (tickets 37 and 39): each cell's kind
    /// from its steepness, the sea level and the painted climbable faces, and the bytes the overworld
    /// asset keeps them in. Heights are corner samples, as <see cref="HeightmapGround"/> reads them.
    /// </summary>
    public static class GroundBake
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("HSGR");
        private const byte Version = 1;

        /// <summary>
        /// A kind for each of <paramref name="columns"/> × <paramref name="rows"/> cells. A cell is water when its
        /// middle is below the sea, or <paramref name="water"/> marks it (a river or lake above sea level); else
        /// blocked when any of its edges or diagonals is steeper than allowed, unless <paramref name="climbable"/>
        /// marks it (then climbable); else walkable.
        /// </summary>
        public static GroundKind[] Classify(float[] heights, int columns, int rows, float cellSize, GroundRules rules, Func<int, int, bool> climbable = null,
            Func<int, int, bool> water = null)
        {
            if (heights == null) throw new ArgumentNullException(nameof(heights));
            if (heights.Length != (columns + 1) * (rows + 1)) throw new ArgumentException("Heights must be (columns + 1) × (rows + 1) corner samples.", nameof(heights));
            var maxRise = (float)Math.Tan(rules.maxWalkableSlopeDegrees * Math.PI / 180.0);
            var diagonal = cellSize * (float)Math.Sqrt(2.0);
            var stride = columns + 1;
            var kinds = new GroundKind[columns * rows];
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                var h00 = heights[row * stride + column];
                var h10 = heights[row * stride + column + 1];
                var h01 = heights[(row + 1) * stride + column];
                var h11 = heights[(row + 1) * stride + column + 1];
                GroundKind kind;
                if ((h00 + h10 + h01 + h11) / 4f < rules.seaLevel || (water != null && water(column, row))) kind = GroundKind.Water;
                else
                {
                    var rise = Math.Max(Math.Max(Math.Abs(h10 - h00), Math.Abs(h11 - h01)), Math.Max(Math.Abs(h01 - h00), Math.Abs(h11 - h10))) / cellSize;
                    rise = Math.Max(rise, Math.Max(Math.Abs(h11 - h00), Math.Abs(h10 - h01)) / diagonal);
                    var steep = rise > maxRise;
                    if (climbable != null && climbable(column, row)) kind = GroundKind.Climbable;
                    else kind = steep ? GroundKind.Blocked : GroundKind.Walkable;
                }
                kinds[row * columns + column] = kind;
            }
            return kinds;
        }

        /// <summary>
        /// Regenerating keeps hand edits (ticket 39): whatever the developer changed since the last generation
        /// (<paramref name="current"/> minus <paramref name="lastGenerated"/>) is laid on the new generation.
        /// With no last generation, the new one as it is.
        /// </summary>
        public static float[] KeepHandEdits(float[] current, float[] lastGenerated, float[] newlyGenerated, float min = float.NegativeInfinity, float max = float.PositiveInfinity)
        {
            if (newlyGenerated == null) throw new ArgumentNullException(nameof(newlyGenerated));
            var result = new float[newlyGenerated.Length];
            var keep = current != null && lastGenerated != null && current.Length == newlyGenerated.Length && lastGenerated.Length == newlyGenerated.Length;
            for (var i = 0; i < result.Length; i++)
            {
                var h = newlyGenerated[i] + (keep ? current[i] - lastGenerated[i] : 0f);
                result[i] = Math.Min(Math.Max(h, min), max);
            }
            return result;
        }

        public static byte[] Encode(float originX, float originZ, float cellSize, int columns, int rows, float[] heights, GroundKind[] kinds)
        {
            using var stream = new MemoryStream();
            using var w = new BinaryWriter(stream);
            w.Write(Magic);
            w.Write(Version);
            w.Write(originX);
            w.Write(originZ);
            w.Write(cellSize);
            w.Write(columns);
            w.Write(rows);
            foreach (var h in heights) w.Write(h);
            foreach (var k in kinds) w.Write((byte)k);
            w.Flush();
            return stream.ToArray();
        }

        public static HeightmapGround Decode(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            try
            {
                using var r = new BinaryReader(new MemoryStream(bytes));
                var magic = r.ReadBytes(Magic.Length);
                for (var i = 0; i < Magic.Length; i++)
                    if (magic.Length != Magic.Length || magic[i] != Magic[i]) throw new FormatException("Not baked ground.");
                var version = r.ReadByte();
                if (version > Version) throw new FormatException($"Ground baked by a newer version (format {version}).");
                var originX = r.ReadSingle();
                var originZ = r.ReadSingle();
                var cellSize = r.ReadSingle();
                var columns = r.ReadInt32();
                var rows = r.ReadInt32();
                if (columns < 1 || rows < 1 || (long)(columns + 1) * (rows + 1) * 4 > bytes.Length) throw new FormatException("Impossible grid size.");
                var heights = new float[(columns + 1) * (rows + 1)];
                for (var i = 0; i < heights.Length; i++) heights[i] = r.ReadSingle();
                var raw = r.ReadBytes(columns * rows);
                if (raw.Length != columns * rows) throw new FormatException("The ground's kinds are cut short.");
                var kinds = new GroundKind[raw.Length];
                for (var i = 0; i < raw.Length; i++)
                {
                    if (raw[i] > (byte)GroundKind.Climbable) throw new FormatException("Unknown ground kind.");
                    kinds[i] = (GroundKind)raw[i];
                }
                return new HeightmapGround(originX, originZ, cellSize, columns, rows, heights, kinds);
            }
            catch (EndOfStreamException e)
            {
                throw new FormatException("The baked ground is cut short.", e);
            }
        }
    }
}
