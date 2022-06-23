using System.Collections.Generic;
using System.Linq;

namespace SharpYaml.Model
{
    public class PathTrie
    {
        class PathTrieNode
        {
            public bool Self { get; private set; }
            readonly Dictionary<ChildIndex, PathTrieNode> subPaths = new Dictionary<ChildIndex, PathTrieNode>();

            public void Add(IList<ChildIndex> indices, int start)
            {
                if (start == indices.Count)
                {
                    Self = true;
                    return;
                }

                if (!subPaths.TryGetValue(indices[start], out var subPath))
                {
                    subPath = new PathTrieNode();
                    subPaths[indices[start]] = subPath;
                }

                subPath.Add(indices, start + 1);
            }

            public bool Remove(IList<ChildIndex> indices, int start, bool removeChildren)
            {
                if (start == indices.Count)
                {
                    var result = Self || (removeChildren && subPaths.Count > 0);

                    Self = false;

                    if (removeChildren)
                        subPaths.Clear();

                    return result;
                }

                if (!subPaths.TryGetValue(indices[start], out var subPath))
                    return false;

                if (!subPath.Remove(indices, start + 1, removeChildren))
                    return false;

                if (subPath.IsEmpty)
                    subPaths.Remove(indices[start]);

                return true;
            }

            public bool IsEmpty
            {
                get { return !Self && subPaths.Count == 0; }
            }

            public PathTrieNode? Find(IList<ChildIndex> indices, int start)
            {
                if (start == indices.Count)
                    return this;

                if (!subPaths.TryGetValue(indices[start], out var subPath))
                    return null;

                return subPath.Find(indices, start + 1);
            }

            public IEnumerable<List<ChildIndex>> GetReversePaths()
            {
                if (Self)
                    yield return new List<ChildIndex>();

                foreach (var pair in subPaths)
                {
                    foreach (var path in pair.Value.GetReversePaths())
                    {
                        path.Add(pair.Key);
                        yield return path;
                    }
                }
            }
        }

        private readonly Dictionary<YamlNode, PathTrieNode> roots = new Dictionary<YamlNode, PathTrieNode>();

        public void Add(Path path)
        {
            if (!roots.TryGetValue(path.Root, out var root))
            {
                root = new PathTrieNode();
                roots[path.Root] = root;
            }

            root.Add(path.Indices, 0);
        }

        public bool Remove(Path path, bool removeChildren)
        {
            if (!roots.TryGetValue(path.Root, out var root))
                return false;

            if (!root.Remove(path.Indices, 0, removeChildren))
                return false;

            if (root.IsEmpty)
                roots.Remove(path.Root);

            return true;
        }

        public bool Contains(Path path, bool orChildren)
        {
            if (!roots.TryGetValue(path.Root, out var root))
                return false;

            var node = root.Find(path.Indices, 0);
            if (node == null)
                return false;

            return node.Self || orChildren;
        }

        public IEnumerable<Path> GetSubpaths(Path path)
        {
            if (!roots.TryGetValue(path.Root, out var root))
                yield break;

            var node = root.Find(path.Indices, 0);
            if (node == null)
                yield break;

            foreach (var reversePath in node.GetReversePaths())
            {
                reversePath.AddRange(path.Indices.Reverse());
                reversePath.Reverse();
                yield return new Path(path.Root, reversePath.ToArray());
            }
        }
    }
}
