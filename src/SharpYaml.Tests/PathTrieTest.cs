using System.Linq;
using NUnit.Framework;
using SharpYaml.Model;
using Path = SharpYaml.Model.Path;
using YamlStream = SharpYaml.Model.YamlStream;

namespace SharpYaml.Tests {
    public class PathTrieTest {
        [Test]
        public void BasicTest() {
            var trie = new PathTrie();
            var root = new YamlStream();
            var path = new Path(root, new[] {
                new ChildIndex(0, false),
                new ChildIndex(-1, false),
                new ChildIndex(2, false)
            });
            
            trie.Add(path);
            
            Assert.IsTrue(trie.Contains(path, false));
            Assert.IsTrue(trie.Contains(path, true));
            
            Assert.IsFalse(trie.Contains(path.GetParentPath().Value, false));
            Assert.IsTrue(trie.Contains(path.GetParentPath().Value, true));

            var childPath = path.Clone();
            childPath.Append(new ChildIndex(1, false));

            Assert.IsFalse(trie.Contains(childPath, false));
            Assert.IsFalse(trie.Contains(childPath, true));

            var subPaths = trie.GetSubpaths(path).ToList();
            
            Assert.AreEqual(1, subPaths.Count);
            Assert.AreEqual(path, subPaths[0]);
            
            trie.Add(childPath);

            Assert.IsTrue(trie.Contains(childPath, false));
            Assert.IsTrue(trie.Contains(childPath, true));

            subPaths = trie.GetSubpaths(path).ToList();

            Assert.AreEqual(2, subPaths.Count);
            Assert.AreEqual(path, subPaths[0]);
            Assert.AreEqual(childPath, subPaths[1]);

            var result = trie.Remove(path.GetParentPath().Value, false);
            Assert.IsFalse(result);
            Assert.IsTrue(trie.Contains(path, false));
            Assert.IsTrue(trie.Contains(childPath, false));

            result = trie.Remove(path, false);
            Assert.IsTrue(result);
            Assert.IsFalse(trie.Contains(path, false));
            Assert.IsTrue(trie.Contains(path, true));
            Assert.IsTrue(trie.Contains(childPath, false));
            
            trie.Add(path);

            result = trie.Remove(path, true);
            Assert.IsTrue(result);
            Assert.IsFalse(trie.Contains(path, false));
            Assert.IsFalse(trie.Contains(path, true));
            Assert.IsFalse(trie.Contains(childPath, false));
        }
    }
}