#nullable disable
using System;
using System.Threading;

namespace Base.BaseSystem.EventSystem.New
{
    internal readonly struct HandlerNode<T>
    {
        public readonly Action<T> Handler;
        public readonly CancellationToken Token;
        public readonly int SubscriptionId;
        public readonly byte Priority;
        public readonly HandlerFlags Flags;

        public HandlerNode(
            Action<T> handler,
            CancellationToken token,
            int subscriptionId,
            byte priority,
            HandlerFlags flags)
        {
            Handler = handler;
            Token = token;
            SubscriptionId = subscriptionId;
            Priority = priority;
            Flags = flags;
        }
    }

    internal sealed class HandlerArray<T>
    {
        private HandlerNode<T>[] _nodes;
        private int _count;
        private int _version;

        private const int InitialCapacity = 8;

        public HandlerArray()
        {
            _nodes = new HandlerNode<T>[InitialCapacity];
            _count = 0;
            _version = 0;
        }

        public int Version => _version;

        public int Count => _count;

        public int Add(HandlerNode<T> node)
        {
            if (_count >= _nodes.Length)
            {
                var newCapacity = _nodes.Length * 2;
                var newNodes = new HandlerNode<T>[newCapacity];
                System.Array.Copy(_nodes, newNodes, _count);
                _nodes = newNodes;
            }

            int insertIndex = _count;
            for (int i = 0; i < _count; i++)
            {
                if (_nodes[i].Priority < node.Priority)
                {
                    insertIndex = i;
                    break;
                }
            }

            if (insertIndex < _count)
            {
                System.Array.Copy(_nodes, insertIndex, _nodes, insertIndex + 1, _count - insertIndex);
            }

            _nodes[insertIndex] = node;
            _count++;
            _version++;
            return node.SubscriptionId;
        }

        public bool MarkForRemoval(int subscriptionId)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_nodes[i].SubscriptionId == subscriptionId)
                {
                    var node = _nodes[i];
                    _nodes[i] = new HandlerNode<T>(
                        node.Handler,
                        node.Token,
                        node.SubscriptionId,
                        node.Priority,
                        node.Flags | HandlerFlags.Removed);
                    _version++;
                    return true;
                }
            }
            return false;
        }

        public void Compact()
        {
            int writeIndex = 0;
            for (int i = 0; i < _count; i++)
            {
                if (!_nodes[i].Flags.HasFlag(HandlerFlags.Removed))
                {
                    if (i != writeIndex)
                    {
                        _nodes[writeIndex] = _nodes[i];
                    }
                    writeIndex++;
                }
            }

            for (int i = writeIndex; i < _count; i++)
            {
                _nodes[i] = default;
            }

            _count = writeIndex;
            _version++;
        }

        public System.ReadOnlySpan<HandlerNode<T>> AsReadOnlySpan()
        {
            return new System.ReadOnlySpan<HandlerNode<T>>(_nodes, 0, _count);
        }
    }
}
