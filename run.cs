using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

class Program
{
    record State(Dictionary<char, List<(int x, int y)>> Pods, int Energy);

    static int Solve(List<string> lines)
    {
        var map = ParseMap(lines);
        var start = CreateInitialState(map);
        var pq = new PriorityQueue<State, int>();
        var seen = new Dictionary<string, int>();

        string startKey = StateKey(start);
        pq.Enqueue(start, 0);
        seen[startKey] = 0;

        while (pq.Count > 0)
        {
            pq.TryDequeue(out var state, out var cost);

            var key = StateKey(state);
            if (cost > seen[key])
                continue;

            if (IsGoal(state))
            {
                return state.Energy;
            }

            foreach (var next in GetNextStates(state, map))
            {
                var nextKey = StateKey(next);
                if (!seen.TryGetValue(nextKey, out var prevCost) || next.Energy < prevCost)
                {
                    seen[nextKey] = next.Energy;
                    pq.Enqueue(next, next.Energy);
                }
            }
        }

        return 0;
    }

    static void Main()
    {
        var lines = new List<string>();
        string line;

        while ((line = Console.ReadLine()) != null)
        {
            lines.Add(line);
        }

        int result = Solve(lines);
        Console.WriteLine(result);
    }

    static Dictionary<(int x, int y), char> ParseMap(List<string> lines)
    {
        var map = new Dictionary<(int x, int y), char>();
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            for (int j = 0; j < line.Length; j++)
            {
                char c = line[j];
                if (c == ' ') continue;
                map[(j, i)] = c;
            }
        }

        return map;
    }

    static State CreateInitialState(Dictionary<(int, int), char> map)
    {
        var pods = new Dictionary<char, List<(int, int)>>();
        foreach (var kv in map)
        {
            char c = kv.Value;
            var position = kv.Key;

            if (c is >= 'A' and <= 'D')
            {
                if (!pods.ContainsKey(c))
                    pods[c] = new List<(int, int)>();
                pods[c].Add(position);
            }
        }

        return new State(pods, 0);
    }

    static bool IsGoal(State state)
    {
        var home = new Dictionary<char, int>
        {
            { 'A', 3 },
            { 'B', 5 },
            { 'C', 7 },
            { 'D', 9 }
        };

        foreach (var kv in state.Pods)
        {
            var type = kv.Key;
            var correct = home[type];
            foreach (var (x, _) in kv.Value)
            {
                if (x != correct)
                    return false;
            }
        }

        return true;
    }

    static List<State> GetNextStates(State state, Dictionary<(int, int), char> map)
    {
        var nextStates = new List<State>();
        var occupied = new HashSet<(int, int)>(state.Pods.Values.SelectMany(p => p));

        var costPerStep = new Dictionary<char, int>
        {
            ['A'] = 1,
            ['B'] = 10,
            ['C'] = 100,
            ['D'] = 1000
        };

        foreach (var kv in state.Pods)
        {
            char type = kv.Key;

            foreach (var pos in kv.Value)
            {
                var moves = FindAvailableMoves(map, occupied, pos, type, state);
                foreach (var (dest, dist) in moves)
                {
                    int cost = dist * costPerStep[type];
                    var newPods = ClonePods(state.Pods);
                    newPods[type].Remove(pos);
                    newPods[type].Add(dest);
                    nextStates.Add(new State(newPods, state.Energy + cost));
                }
            }
        }

        return nextStates;
    }

    static List<((int x, int y) dest, int dist)> FindAvailableMoves(
        Dictionary<(int, int), char> map,
        HashSet<(int, int)> occupied,
        (int x, int y) start,
        char type,
        State state)
    {
        int hallwayY = map.Where(kv => kv.Value == '.').Min(kv => kv.Key.Item2);
        int targetX = GetTargetColumn(type);

        var roomCells = map.Keys
            .Where(p => p.Item1 == targetX && p.Item2 > hallwayY && map[p] != '#')
            .OrderBy(p => p.Item2)
            .ToList();

        var roomEntranceXs = map.Keys
            .Where(p => p.Item2 == hallwayY && map[p] != '#')
            .Select(p => p.Item1)
            .Where(x => map.ContainsKey((x, hallwayY + 1)) && map[(x, hallwayY + 1)] != '#')
            .ToHashSet();

        if (start.x == targetX && start.y > hallwayY)
        {
            bool belowAllSame = true;
            foreach (var cell in roomCells)
            {
                if (cell.Item2 <= start.y) continue;
                var occupant = state.Pods
                    .SelectMany(kv => kv.Value.Select(p => (type: kv.Key, pos: p)))
                    .FirstOrDefault(t => t.pos == cell);

                if (occupant == default) continue;
                if (occupant.type != type)
                {
                    belowAllSame = false;
                    break;
                }
            }

            if (belowAllSame)
                return new List<((int x, int y), int)>();
        }

        var q = new Queue<(int x, int y)>();
        var dist = new Dictionary<(int, int), int>();
        var seen = new HashSet<(int, int)>();
        var occ = new HashSet<(int, int)>(occupied);
        occ.Remove(start);
        q.Enqueue(start);
        dist[start] = 0;
        seen.Add(start);

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            var (cx, cy) = cur;
            var neighbors = new (int dx, int dy)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
            foreach (var (dx, dy) in neighbors)
            {
                var np = (cx + dx, cy + dy);
                if (!map.ContainsKey(np)) continue;
                if (map[np] == '#') continue;
                if (occ.Contains(np)) continue;
                if (seen.Contains(np)) continue;
                seen.Add(np);
                dist[np] = dist[cur] + 1;
                q.Enqueue(np);
            }
        }

        var result = new List<((int x, int y) dest, int dist)>();

        bool RoomAcceptsType()
        {
            foreach (var cell in roomCells)
            {
                var occupant = state.Pods
                    .SelectMany(kv => kv.Value.Select(p => (type: kv.Key, pos: p)))
                    .FirstOrDefault(t => t.pos == cell);
                if (occupant == default) continue;
                if (occupant.type != type) return false;
            }

            return true;
        }

        bool canEnterRoom = RoomAcceptsType();
        (int x, int y)? deepestFreeInRoom = null;
        if (canEnterRoom && roomCells.Count > 0)
        {
            for (int i = roomCells.Count - 1; i >= 0; i--)
            {
                var cell = roomCells[i];
                bool occupiedByAny = state.Pods.Values.SelectMany(v => v).Any(p => p == cell);
                if (!occupiedByAny)
                {
                    deepestFreeInRoom = cell;
                    break;
                }
            }
        }

        foreach (var kv in dist)
        {
            var pos = kv.Key;
            int d = kv.Value;
            if (pos == start) continue;

            if (pos.Item2 == hallwayY)
            {
                if (roomEntranceXs.Contains(pos.Item1)) continue;
                result.Add((pos, d));
                continue;
            }

            if (pos.Item1 != targetX) continue;
            if (!canEnterRoom) continue;
            if (deepestFreeInRoom == null) continue;
            if (pos != deepestFreeInRoom.Value) continue;
            result.Add((pos, d));
        }

        return result;
    }

    static int GetTargetColumn(char type) => type switch
    {
        'A' => 3,
        'B' => 5,
        'C' => 7,
        'D' => 9
    };

    static Dictionary<char, List<(int, int)>> ClonePods(Dictionary<char, List<(int, int)>> pods)
    {
        var copy = new Dictionary<char, List<(int, int)>>();
        foreach (var kv in pods)
            copy[kv.Key] = new List<(int, int)>(kv.Value);
        return copy;
    }

    static string StateKey(State s)
    {
        var sb = new StringBuilder();
        foreach (var kv in s.Pods.OrderBy(k => k.Key))
        {
            sb.Append(kv.Key);
            foreach (var p in kv.Value.OrderBy(p => p.y).ThenBy(p => p.x))
                sb.Append($"{p.x},{p.y};");
        }

        return sb.ToString();
    }
}