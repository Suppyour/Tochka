using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
// выжимка с данными ради ускорения
namespace First;

class Program
{
    private struct State
    {
        public readonly long Pods;
        public readonly int Energy;

        public State(long pods, int energy)
        {
            Pods = pods;
            Energy = energy;
        }
    }
    
    private static readonly (int x, int y)[] RoomPositions = new[]
    {
        (3, 2), (3, 3),
        (5, 2), (5, 3),
        (7, 2), (7, 3),
        (9, 2), (9, 3)
    };
    
    private static readonly (int x, int y)[] HallwayPositions = new[]
    {
        (1, 1), (2, 1), (4, 1), (6, 1), (8, 1), (10, 1), (11, 1)
    };

    private static readonly Dictionary<char, int> RoomOffsets = new()
    {
        ['A'] = 0, ['B'] = 2, ['C'] = 4, ['D'] = 6
    };
    
    private static readonly Dictionary<char, int> Costs = new()
    {
        ['A'] = 1, ['B'] = 10, ['C'] = 100, ['D'] = 1000
    };

    static int Solve(List<string> lines)
    {
        var initialPods = ParseInitialState(lines);
        var pq = new PriorityQueue<State, int>();
        var visited = new Dictionary<long, int>();

        pq.Enqueue(new State(initialPods, 0), 0);
        visited[initialPods] = 0;

        while (pq.Count > 0)
        {
            var state = pq.Dequeue();

            if (IsGoal(state.Pods))
                return state.Energy;

            if (visited[state.Pods] < state.Energy)
                continue;

            foreach (var (newPods, cost) in GetMoves(state.Pods))
            {
                var newEnergy = state.Energy + cost;
                if (!visited.TryGetValue(newPods, out var prevEnergy) || newEnergy < prevEnergy)
                {
                    visited[newPods] = newEnergy;
                    pq.Enqueue(new State(newPods, newEnergy), newEnergy);
                }
            }
        }

        return -1;
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
    
    static long ParseInitialState(List<string> lines)
    {
        var pods = new List<(char type, int x, int y)>();
        
        for (int y = 0; y < lines.Count; y++)
        {
            var line = lines[y];
            for (int x = 0; x < line.Length; x++)
            {
                var c = line[x];
                if (c is >= 'A' and <= 'D')
                {
                    pods.Add((c, x, y));
                }
            }
        }
        
        pods.Sort((a, b) => a.type != b.type ? a.type.CompareTo(b.type) : 
            a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));

        long state = 0;
        for (int i = 0; i < pods.Count; i++)
        {
            var (_, x, y) = pods[i];
            state |= (long)((x << 4) | y) << (i * 8);
        }
        return state;
    }

    static bool IsGoal(long pods)
    {
        for (int i = 0; i < 8; i++)
        {
            var (x, y) = GetPodPosition(pods, i);
            var expectedType = GetPodType(i);
            var roomOffset = RoomOffsets[expectedType];

            if (x != RoomPositions[roomOffset].x || 
                (y != RoomPositions[roomOffset].y && y != RoomPositions[roomOffset + 1].y))
                return false;
        }
        return true;
    }

    static IEnumerable<(long newPods, int cost)> GetMoves(long pods)
    {
        var occupied = new HashSet<(int x, int y)>();
        for (int i = 0; i < 8; i++)
        {
            occupied.Add(GetPodPosition(pods, i));
        }

        for (int podIndex = 0; podIndex < 8; podIndex++)
        {
            var (currentX, currentY) = GetPodPosition(pods, podIndex);
            var podType = GetPodType(podIndex);
            var costMultiplier = Costs[podType];
            
            if (currentY == 1)
            {
                foreach (var move in GetMovesToRoom(pods, podIndex, occupied, currentX, currentY, podType))
                {
                    yield return (move.pods, move.cost * costMultiplier);
                }
            }
            else
            {
                if (IsInTargetRoom(currentX, currentY, podType) && 
                    !ShouldMoveFromRoom(pods, podIndex, currentX, currentY, podType))
                {
                    continue;
                }
                
                foreach (var move in GetMovesToHallway(pods, podIndex, occupied, currentX, currentY))
                {
                    yield return (move.pods, move.cost * costMultiplier);
                }
                
                foreach (var move in GetMovesToRoom(pods, podIndex, occupied, currentX, currentY, podType))
                {
                    yield return (move.pods, move.cost * costMultiplier);
                }
            }
        }
    }

    static IEnumerable<(long pods, int cost)> GetMovesToRoom(long pods, int podIndex, 
        HashSet<(int x, int y)> occupied, int startX, int startY, char podType)
    {
        var targetRoomX = GetTargetRoomX(podType);
        var roomOffset = RoomOffsets[podType];
        
        if (!CanEnterRoom(pods, podType, roomOffset))
            yield break;

        var targetPos = GetAvailableRoomPosition(pods, roomOffset);
        if (targetPos == (-1, -1))
            yield break;
        
        var entrance = (targetRoomX, 1);
        var pathToEntrance = GetPath((startX, startY), entrance, occupied);
        if (pathToEntrance == null)
            yield break;
        
        var pathInRoom = GetPath(entrance, targetPos, occupied);
        if (pathInRoom == null)
            yield break;

        var totalCost = pathToEntrance.Value.distance + pathInRoom.Value.distance;
        var newPods = SetPodPosition(pods, podIndex, targetPos.x, targetPos.y);
        yield return (newPods, totalCost);
    }

    static IEnumerable<(long pods, int cost)> GetMovesToHallway(long pods, int podIndex,
        HashSet<(int x, int y)> occupied, int startX, int startY)
    {
        foreach (var target in HallwayPositions)
        {
            if (occupied.Contains(target))
                continue;

            var path = GetPath((startX, startY), target, occupied);
            if (path != null)
            {
                var newPods = SetPodPosition(pods, podIndex, target.x, target.y);
                yield return (newPods, path.Value.distance);
            }
        }
    }

    static (int distance, List<(int x, int y)> path)? GetPath((int x, int y) start, (int x, int y) end, 
        HashSet<(int x, int y)> occupied)
    {
        var queue = new Queue<(int x, int y)>();
        var distances = new Dictionary<(int x, int y), int>();
        var prev = new Dictionary<(int x, int y), (int x, int y)>();

        queue.Enqueue(start);
        distances[start] = 0;
        prev[start] = (-1, -1);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (current == end)
            {
                return (distances[current], ReconstructPath(prev, start, end));
            }

            foreach (var neighbor in GetNeighbors(current))
            {
                if (IsWall(neighbor) || occupied.Contains(neighbor) || distances.ContainsKey(neighbor))
                    continue;

                distances[neighbor] = distances[current] + 1;
                prev[neighbor] = current;
                queue.Enqueue(neighbor);
            }
        }

        return null;
    }

    static List<(int x, int y)> ReconstructPath(Dictionary<(int x, int y), (int x, int y)> prev, 
        (int x, int y) start, (int x, int y) end)
    {
        var path = new List<(int x, int y)>();
        var current = end;

        while (current != start)
        {
            path.Add(current);
            current = prev[current];
        }

        path.Reverse();
        return path;
    }

    static IEnumerable<(int x, int y)> GetNeighbors((int x, int y) pos)
    {
        yield return (pos.x - 1, pos.y);
        yield return (pos.x + 1, pos.y);
        yield return (pos.x, pos.y - 1);
        yield return (pos.x, pos.y + 1);
    }

    static bool IsWall((int x, int y) pos)
    {
        if (pos.y == 0) return true;
        if (pos.y == 1 && (pos.x == 0 || pos.x == 12)) return true;
        if (pos.y >= 2 && pos.y <= 3 && (pos.x < 3 || pos.x > 9 || (pos.x - 3) % 2 != 0)) return true;
        if (pos.y == 4 && (pos.x < 3 || pos.x > 9 || pos.x == 3 || pos.x == 5 || pos.x == 7 || pos.x == 9)) return true;
        
        return false;
    }

    static bool CanEnterRoom(long pods, char podType, int roomOffset)
    {
        var roomX = RoomPositions[roomOffset].x;
        
        for (int i = 0; i < 8; i++)
        {
            var (x, y) = GetPodPosition(pods, i);
            if (x == roomX && y >= 2)
            {
                var otherType = GetPodType(i);
                if (otherType != podType)
                    return false;
            }
        }
        return true;
    }

    static (int x, int y) GetAvailableRoomPosition(long pods, int roomOffset)
    {
        var bottomPos = RoomPositions[roomOffset + 1];
        var topPos = RoomPositions[roomOffset];
        
        if (!IsPositionOccupied(pods, bottomPos))
            return bottomPos;
        
        if (!IsPositionOccupied(pods, topPos))
        {
            var bottomOccupantType = GetPodTypeAtPosition(pods, bottomPos);
            var expectedType = GetExpectedTypeForRoom(roomOffset);
            if (bottomOccupantType == expectedType)
                return topPos;
        }

        return (-1, -1);
    }

    static bool ShouldMoveFromRoom(long pods, int podIndex, int x, int y, char podType)
    {
        if (IsInTargetRoom(x, y, podType))
        {
            if (y == 2)
            {
                var bottomPos = (x, 3);
                var bottomType = GetPodTypeAtPosition(pods, bottomPos);
                if (bottomType == podType)
                    return false;
            }
            else if (y == 3)
            {
                return false;
            }
        }
        return true;
    }

    static bool IsInTargetRoom(int x, int y, char podType)
    {
        var targetX = GetTargetRoomX(podType);
        return x == targetX && y >= 2 && y <= 3;
    }

    static int GetTargetRoomX(char podType) => podType switch
    {
        'A' => 3, 'B' => 5, 'C' => 7, 'D' => 9
    };

    static char GetExpectedTypeForRoom(int roomOffset) => roomOffset switch
    {
        0 => 'A', 2 => 'B', 4 => 'C', 6 => 'D', _ => '?'
    };
    
    static (int x, int y) GetPodPosition(long pods, int index)
    {
        var shift = index * 8;
        var value = (int)((pods >> shift) & 0xFF);
        return ((value >> 4) & 0xF, value & 0xF);
    }

    static long SetPodPosition(long pods, int index, int x, int y)
    {
        var shift = index * 8;
        var mask = ~(0xFFL << shift);
        var newValue = (long)((x << 4) | y) << shift;
        return (pods & mask) | newValue;
    }

    static char GetPodType(int index) => index switch
    {
        0 or 1 => 'A', 2 or 3 => 'B', 4 or 5 => 'C', 6 or 7 => 'D', _ => '?'
    };

    static bool IsPositionOccupied(long pods, (int x, int y) pos)
    {
        for (int i = 0; i < 8; i++)
        {
            if (GetPodPosition(pods, i) == pos)
                return true;
        }
        return false;
    }

    static char GetPodTypeAtPosition(long pods, (int x, int y) pos)
    {
        for (int i = 0; i < 8; i++)
        {
            if (GetPodPosition(pods, i) == pos)
                return GetPodType(i);
        }
        return '?';
    }
}