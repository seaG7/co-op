using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MicroWorldNS
{
    public class EffectiveLinkedList<T> : IEnumerable<T> where T : class, ILinkedListNode
    {
        private ILinkedListNode _head = null;
        private ILinkedListNode _tail = null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddLast(T node)
        {
            node.Next = null;
            node.IsRemoved = false;

            if (_head == null)
            {
                _head = node;
                _tail = node;
            }
            else
            {
                _tail.Next = node;
                _tail = node;
            }
        }

        public void Remove(T node)
        {
            node.IsRemoved = true;
        }

        public void Clear()
        {
            _head = _tail = null;
        }

        public IEnumerator<T> GetEnumerator()
        {
            var node = _head;
            Clear();

            while (node != null)
            {
                if (node.IsRemoved)
                {
                    node = node.Next;
                    continue;
                }

                yield return (T)node;
                var next = node.Next;

                if (!node.IsRemoved)
                    AddLast((T)node);

                node = next;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public interface ILinkedListNode
    {
        bool IsRemoved { get; set; }
        ILinkedListNode Next { get; set; }
    }
}