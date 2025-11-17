using System;
using System.Collections.Generic;
using Actuarius.Collections;

namespace Shared
{
    public interface ITopologicalGraphNode<in TNode>
        where TNode : class, ITopologicalGraphNode<TNode>
    {
        bool DependsOn(TNode other);
    }

    /// <summary>
    /// Топологически отсортированная приоритетная очередь
    /// </summary>
    /// <typeparam name="TNode"></typeparam>
    /// <typeparam name="TNodeAttr"></typeparam>
    public class TopologicalQueue<TNode, TNodeAttr>  : IQueue_old<TopologicalQueue<TNode, TNodeAttr>.NodeRecord>
        where TNode : class, ITopologicalGraphNode<TNode>
        where TNodeAttr : IComparable<TNodeAttr>
    {
        public struct NodeRecord
        {
            public readonly TNode Node;
            public readonly TNodeAttr Attr;

            public NodeRecord(TNode node, TNodeAttr attr)
            {
                Node = node;
                Attr = attr;
            }
        }

        private struct Key : IComparable<Key>
        {
            public readonly int DependencyPower;
            public readonly TNodeAttr Attr;

            public Key(int power, TNodeAttr attr)
            {
                DependencyPower = power;
                Attr = attr;
            }

            public int CompareTo(Key other)
            {
                int cmp = DependencyPower.CompareTo(other.DependencyPower);
                if (cmp == 0)
                {
                    cmp = Attr.CompareTo(other.Attr);
                }
                return cmp;
            }
        }

        private readonly PriorityQueue<Key, NodeRecord> mQueue = new PriorityQueue<Key, NodeRecord>();

        public bool Put(NodeRecord value)
        {
            int newNodePower = 0;
            foreach (var data in mQueue.Values)
            {
                if (value.Node.DependsOn(data.Node))
                {
                    newNodePower += 1;
                }
            }

            foreach (var element in mQueue.Enumerate(QueueEnumerationOrder.TailToHead))
            {
                var node = element.ShowData().Node;
                if (node.DependsOn(value.Node))
                {
                    var key = element.ShowKey();
                    element.Update(new Key(key.DependencyPower + 1, key.Attr));
                }
            }

            Key newKey = new Key(newNodePower, value.Attr);
            mQueue.Enqueue(newKey, value);
            return true;
        }

        public bool TryPeekTop(out NodeRecord value)
        {
            if (mQueue.Count > 0)
            {
                value = mQueue.Peek();
                return true;
            }

            value = default(NodeRecord);
            return false;
        }

        public bool TryPop(out NodeRecord value)
        {
            KeyValuePair<Key, NodeRecord> kv;
            if (mQueue.TryPop(out kv))
            {
                value = kv.Value;
                if (kv.Key.DependencyPower != 0)
                {
                    throw new Exception("Impossible to sort dependency graph. Probably wrong TNode.DependsOn() implementation!");
                }

                var removedNode = value.Node;
                foreach (var element in mQueue.Enumerate(QueueEnumerationOrder.HeadToTail))
                {
                    var node = element.ShowData().Node;
                    if (node.DependsOn(removedNode))
                    {
                        var key = element.ShowKey();
                        element.Update(new Key(key.DependencyPower - 1, key.Attr));
                    }
                }

                return true;
            }
            value = default(NodeRecord);
            return false;
        }
    }
}