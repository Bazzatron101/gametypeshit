using System;
using System.Collections.Generic;

namespace HunterSystem.Simulation
{
    /// <summary>
    /// Finding a way across ground with edges (ticket 43): around steep ground and water, through passes.
    /// A search over a grid laid on the ground (2 m cells by default), asking the ground which cells can be
    /// stood on. Plain and deterministic, so host and clients agree; no Unity NavMesh. When the straight
    /// line is clear, the way is the straight line and no search is made.
    /// </summary>
    public static class GroundPath
    {
        public const float DefaultCellSize = 2f;

        /// <summary>
        /// The next point to head for, going from <paramref name="from"/> to <paramref name="to"/>: <paramref name="to"/>
        /// itself when the line between them is clear, else the first corner of a found way round, else (no way found
        /// within the search's bounds) <paramref name="to"/>, to slide along whatever is in the way.
        /// </summary>
        public static Vec3 NextWaypoint(IGround ground, Vec3 from, Vec3 to, float cellSize = DefaultCellSize, float margin = 60f, int maxNodes = 8000)
        {
            if (IsClear(ground, from, to)) return to;
            var path = Find(ground, from, to, cellSize, margin, maxNodes);
            if (path == null || path.Count == 0) return to;
            // The furthest corner in plain sight: smooths the grid's zigzag.
            for (var i = path.Count - 1; i > 0; i--)
                if (IsClear(ground, from, path[i])) return path[i];
            return path[0];
        }

        /// <summary>Whether a straight walk from <paramref name="from"/> to <paramref name="to"/> meets nothing it cannot stand on.</summary>
        public static bool IsClear(IGround ground, Vec3 from, Vec3 to)
        {
            var offset = new Vec3(to.X - from.X, 0f, to.Z - from.Z);
            var distance = offset.Length;
            if (distance < 1e-3f) return true;
            var direction = offset * (1f / distance);
            return ground.ReachAlong(from, direction, distance) >= distance - 0.15f;
        }

        /// <summary>
        /// A way from <paramref name="from"/> to <paramref name="to"/> as cell centres (ending at <paramref name="to"/>), searched
        /// within the box round both points widened by <paramref name="margin"/>; null when there is none in reach.
        /// </summary>
        public static List<Vec3> Find(IGround ground, Vec3 from, Vec3 to, float cellSize = DefaultCellSize, float margin = 60f, int maxNodes = 8000)
        {
            var minX = Math.Min(from.X, to.X) - margin;
            var minZ = Math.Min(from.Z, to.Z) - margin;
            var columns = (int)Math.Ceiling((Math.Max(from.X, to.X) + margin - minX) / cellSize) + 1;
            var rows = (int)Math.Ceiling((Math.Max(from.Z, to.Z) + margin - minZ) / cellSize) + 1;

            int Cell(Vec3 p) => (int)Math.Floor((p.Z - minZ) / cellSize) * columns + (int)Math.Floor((p.X - minX) / cellSize);
            Vec3 Centre(int cell) => new Vec3(minX + (cell % columns + 0.5f) * cellSize, 0f, minZ + (cell / columns + 0.5f) * cellSize);

            var start = Cell(from);
            var goal = Cell(to);
            var walkable = new Dictionary<int, bool>();
            bool CanStand(int cell)
            {
                if (cell == start || cell == goal) return true;
                if (!walkable.TryGetValue(cell, out var ok)) walkable[cell] = ok = ground.IsWalkable(Centre(cell));
                return ok;
            }

            var cameFrom = new Dictionary<int, int>();
            var cost = new Dictionary<int, float> { [start] = 0f };
            var open = new MinHeap();
            var order = 0;
            open.Push(start, Heuristic(start, goal, columns), order++);
            var expanded = 0;
            while (open.Count > 0 && expanded < maxNodes)
            {
                var current = open.Pop();
                if (current == goal) return Rebuild(cameFrom, current, Centre, to);
                expanded++;
                var cx = current % columns;
                var cz = current / columns;
                for (var dz = -1; dz <= 1; dz++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    var nx = cx + dx;
                    var nz = cz + dz;
                    if (nx < 0 || nz < 0 || nx >= columns || nz >= rows) continue;
                    var next = nz * columns + nx;
                    if (!CanStand(next)) continue;
                    if (dx != 0 && dz != 0 && (!CanStand(cz * columns + nx) || !CanStand(nz * columns + cx))) continue; // no cutting corners
                    var step = dx != 0 && dz != 0 ? 1.41421356f : 1f;
                    var tentative = cost[current] + step;
                    if (cost.TryGetValue(next, out var known) && known <= tentative) continue;
                    cost[next] = tentative;
                    cameFrom[next] = current;
                    open.Push(next, tentative + Heuristic(next, goal, columns), order++);
                }
            }
            return null;
        }

        private static float Heuristic(int cell, int goal, int columns)
        {
            var dx = Math.Abs(cell % columns - goal % columns);
            var dz = Math.Abs(cell / columns - goal / columns);
            return Math.Max(dx, dz) + 0.41421356f * Math.Min(dx, dz); // octile
        }

        private static List<Vec3> Rebuild(Dictionary<int, int> cameFrom, int current, Func<int, Vec3> centre, Vec3 to)
        {
            var path = new List<Vec3> { to };
            while (cameFrom.TryGetValue(current, out var previous))
            {
                current = previous;
                if (cameFrom.ContainsKey(current)) path.Add(centre(current)); // the start cell is where we are
            }
            path.Reverse();
            return path;
        }

        /// <summary>A binary heap of cells by score, ties broken by the order they were pushed, so the search is the same everywhere.</summary>
        private sealed class MinHeap
        {
            private readonly List<(float score, int order, int cell)> _items = new List<(float, int, int)>();

            public int Count => _items.Count;

            public void Push(int cell, float score, int order)
            {
                _items.Add((score, order, cell));
                var i = _items.Count - 1;
                while (i > 0)
                {
                    var parent = (i - 1) / 2;
                    if (!Less(_items[i], _items[parent])) break;
                    (_items[i], _items[parent]) = (_items[parent], _items[i]);
                    i = parent;
                }
            }

            public int Pop()
            {
                var top = _items[0].cell;
                var last = _items.Count - 1;
                _items[0] = _items[last];
                _items.RemoveAt(last);
                var i = 0;
                while (true)
                {
                    var l = 2 * i + 1;
                    var r = l + 1;
                    var smallest = i;
                    if (l < _items.Count && Less(_items[l], _items[smallest])) smallest = l;
                    if (r < _items.Count && Less(_items[r], _items[smallest])) smallest = r;
                    if (smallest == i) break;
                    (_items[i], _items[smallest]) = (_items[smallest], _items[i]);
                    i = smallest;
                }
                return top;
            }

            private static bool Less((float score, int order, int cell) a, (float score, int order, int cell) b) =>
                a.score < b.score || (a.score == b.score && a.order < b.order);
        }
    }
}
