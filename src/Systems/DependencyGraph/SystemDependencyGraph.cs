using System.Collections.Generic;

namespace Wargon.Nukecs
{
    public struct SystemNode
    {
        public int Index;
        public string Name;
        public ISystemRunner Runner;
        public SystemDependencyInfo Info;
        public Threads ThreadMode;
    }

    public struct ExecutionGroup
    {
        public int[] MainIndices;
        public int[] ParallelIndices;
        public bool HasECB;
    }

    public class SystemDependencyGraph
    {
        private SystemNode[] _nodes;
        private int[][] _executionGroups;
        private int[] _inDegree;
        private List<int>[] _adjacency;
        private ExecutionGroup[] _precomputedGroups;
        private int[][] _predecessors;

        public int NodeCount => _nodes?.Length ?? 0;
        public int GroupCount => _executionGroups?.Length ?? 0;
        public SystemNode[] Nodes => _nodes;
        public int[][] ExecutionGroups => _executionGroups;

        private static bool IsMainBlocking(Threads mode) =>
            mode == Threads.Main || mode == Threads.MainRun;

        public void Build(SystemNode[] nodes)
        {
            _nodes = nodes;
            var n = nodes.Length;

            if (n == 0)
            {
                _executionGroups = System.Array.Empty<int[]>();
                _precomputedGroups = System.Array.Empty<ExecutionGroup>();
                return;
            }

            _adjacency = new List<int>[n];
            _inDegree = new int[n];

            for (int i = 0; i < n; i++)
                _adjacency[i] = new List<int>();

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    if (IsMainBlocking(nodes[i].ThreadMode) || IsMainBlocking(nodes[j].ThreadMode))
                        continue;

                    if (nodes[i].Info.HasConflict(in nodes[j].Info))
                    {
                        if (nodes[i].Index < nodes[j].Index)
                        {
                            _adjacency[i].Add(j);
                            _inDegree[j]++;
                        }
                        else
                        {
                            _adjacency[j].Add(i);
                            _inDegree[i]++;
                        }
                    }
                }
            }

            var predsList = new List<int>[n];
            for (int i = 0; i < n; i++) predsList[i] = new List<int>();
            for (int i = 0; i < n; i++)
            {
                foreach (var j in _adjacency[i])
                    predsList[j].Add(i);
            }
            _predecessors = new int[n][];
            for (int i = 0; i < n; i++)
                _predecessors[i] = predsList[i].ToArray();

            _executionGroups = TopologicalSortParallelGroups(n);
            PrecomputeExecutionGroups2();
        }
        private void PrecomputeExecutionGroups2()
        {
            _precomputedGroups = new ExecutionGroup[_executionGroups.Length];
            for (int g = 0; g < _executionGroups.Length; g++)
            {
                var group = _executionGroups[g];
                var mains = new List<int>();
                var parallels = new List<int>();
                var hasECB = false;

                foreach (var idx in group)
                {
                    // MainRun тоже требует Main thread
                    if (_nodes[idx].ThreadMode == Threads.Main || 
                        _nodes[idx].ThreadMode == Threads.MainRun)
                        mains.Add(idx);
                    else
                        parallels.Add(idx);

                    if (_nodes[idx].Info.UsesECB)
                        hasECB = true;
                }
                _precomputedGroups[g] = new ExecutionGroup
                {
                    MainIndices = mains.ToArray(),
                    ParallelIndices = parallels.ToArray(),
                    HasECB = hasECB
                };
            }
        }
        private void PrecomputeExecutionGroups()
        {
            _precomputedGroups = new ExecutionGroup[_executionGroups.Length];
            for (int g = 0; g < _executionGroups.Length; g++)
            {
                var group = _executionGroups[g];
                var mains = new List<int>();
                var parallels = new List<int>();
                var hasECB = false;
                foreach (var idx in group)
                {
                    if (_nodes[idx].ThreadMode == Threads.Main)
                        mains.Add(idx);
                    else
                        parallels.Add(idx);
                    if (_nodes[idx].Info.UsesECB)
                        hasECB = true;
                }
                _precomputedGroups[g] = new ExecutionGroup
                {
                    MainIndices = mains.ToArray(),
                    ParallelIndices = parallels.ToArray(),
                    HasECB = hasECB
                };
            }
        }

        public ExecutionGroup[] GetPrecomputedGroups() => _precomputedGroups;
        public int[][] GetPredecessors() => _predecessors;
        public int[][] GetSuccessors()
        {
            if (_nodes == null) return null;
            var n = _nodes.Length;
            var result = new int[n][];
            for (int i = 0; i < n; i++)
                result[i] = _adjacency[i].ToArray();
            return result;
        }

        private int[][] TopologicalSortParallelGroups(int n)
        {
            var groups = new List<int[]>();
            var completed = new bool[n];
            var remaining = n;

            while (remaining > 0)
            {
                var currentGroup = new List<int>();

                for (int i = 0; i < n; i++)
                {
                    if (!completed[i] && _inDegree[i] == 0)
                        currentGroup.Add(i);
                }

                if (currentGroup.Count == 0)
                {
                    var fallback = new int[remaining];
                    int idx = 0;
                    for (int i = 0; i < n; i++)
                    {
                        if (!completed[i])
                            fallback[idx++] = i;
                    }
                    groups.Add(fallback);
                    break;
                }

                foreach (var nodeIdx in currentGroup)
                {
                    completed[nodeIdx] = true;
                    remaining--;

                    foreach (var neighbor in _adjacency[nodeIdx])
                    {
                        _inDegree[neighbor]--;
                    }
                }

                groups.Add(currentGroup.ToArray());
            }

            return groups.ToArray();
        }

        public int[][] GetExecutionGroups() => _executionGroups;

        public bool HasCyclicDependency()
        {
            if (_nodes == null || _nodes.Length == 0) return false;

            var n = _nodes.Length;
            var tempInDegree = new int[n];
            System.Array.Copy(_inDegree, tempInDegree, n);

            int count = 0;
            var queue = new Queue<int>();

            for (int i = 0; i < n; i++)
            {
                if (tempInDegree[i] == 0)
                    queue.Enqueue(i);
            }

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                count++;

                foreach (var neighbor in _adjacency[node])
                {
                    tempInDegree[neighbor]--;
                    if (tempInDegree[neighbor] == 0)
                        queue.Enqueue(neighbor);
                }
            }

            return count != n;
        }
    }
}
