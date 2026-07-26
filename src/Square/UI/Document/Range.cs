namespace Square.UI;

/// <summary>
/// Minimal DOM Range model used by text selection.
/// </summary>
public sealed class Range
{
    /// <summary>构造 Range 并以文档根元素作为初始边界。</summary>
    public Range(Document ownerDocument)
    {
        OwnerDocument = ownerDocument ?? throw new ArgumentNullException(nameof(ownerDocument));
        StartContainer = ownerDocument.DocumentElement;
        EndContainer = ownerDocument.DocumentElement;
    }

    /// <summary>所属文档。</summary>
    public Document OwnerDocument { get; }
    /// <summary>起始边界节点。</summary>
    public Node StartContainer { get; private set; }
    /// <summary>起始偏移。</summary>
    public int StartOffset { get; private set; }
    /// <summary>结束边界节点。</summary>
    public Node EndContainer { get; private set; }
    /// <summary>结束偏移。</summary>
    public int EndOffset { get; private set; }
    /// <summary>是否折叠为单点。</summary>
    public bool Collapsed => StartContainer == EndContainer && StartOffset == EndOffset;

    /// <summary>设置起始边界（对齐 <c>setStart</c>）。</summary>
    public void SetStart(Node node, int offset)
    {
        ValidateBoundary(node, offset);
        StartContainer = node;
        StartOffset = offset;
        if (CompareBoundaryPoints(StartContainer, StartOffset, EndContainer, EndOffset) > 0)
            Collapse(toStart: true);
    }

    /// <summary>设置结束边界（对齐 <c>setEnd</c>）。</summary>
    public void SetEnd(Node node, int offset)
    {
        ValidateBoundary(node, offset);
        EndContainer = node;
        EndOffset = offset;
        if (CompareBoundaryPoints(StartContainer, StartOffset, EndContainer, EndOffset) > 0)
            Collapse(toStart: false);
    }

    /// <summary>选中节点的全部内容（对齐 <c>selectNodeContents</c>）。</summary>
    public void SelectNodeContents(Node node)
    {
        ValidateBoundary(node, 0);
        StartContainer = node;
        StartOffset = 0;
        EndContainer = node;
        EndOffset = GetLength(node);
    }

    /// <summary>折叠到起点或终点（对齐 <c>collapse</c>）。</summary>
    public void Collapse(bool toStart)
    {
        if (toStart)
        {
            EndContainer = StartContainer;
            EndOffset = StartOffset;
        }
        else
        {
            StartContainer = EndContainer;
            StartOffset = EndOffset;
        }
    }

    /// <summary>返回选区内的文本。</summary>
    public override string ToString()
    {
        if (Collapsed) return string.Empty;

        var root = GetCommonRoot(StartContainer, EndContainer);
        var result = new System.Text.StringBuilder();
        foreach (var text in EnumerateTextNodes(root))
        {
            for (var i = 0; i < text.Length; i++)
            {
                if (CompareBoundaryPoints(text, i + 1, StartContainer, StartOffset) <= 0) continue;
                if (CompareBoundaryPoints(text, i, EndContainer, EndOffset) >= 0) continue;
                result.Append(text.Data[i]);
            }
        }
        return result.ToString();
    }

    private void ValidateBoundary(Node node, int offset)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!ReferenceEquals(node, OwnerDocument.DocumentElement) && node.OwnerDocument != OwnerDocument)
            throw new InvalidOperationException("Range boundary node belongs to a different document.");
        if (offset < 0 || offset > GetLength(node))
            throw new ArgumentOutOfRangeException(nameof(offset));
    }

    private static int GetLength(Node node) => node switch
    {
        CharacterData characterData => characterData.Length,
        Element element => element.ChildNodes.Count,
        Document => 1,
        _ => 0
    };

    private static IEnumerable<Text> EnumerateTextNodes(Node node)
    {
        if (node is Text text)
        {
            yield return text;
            yield break;
        }

        if (node is not Element element) yield break;
        foreach (var child in element.ChildNodes)
        foreach (var childText in EnumerateTextNodes(child))
            yield return childText;
    }

    private static Node GetCommonRoot(Node a, Node b)
    {
        var rootA = GetRoot(a);
        var rootB = GetRoot(b);
        if (!ReferenceEquals(rootA, rootB))
            throw new InvalidOperationException("Range boundaries are not in the same tree.");
        return rootA;
    }

    private static Node GetRoot(Node node)
    {
        while (node.ParentNode != null) node = node.ParentNode;
        return node;
    }

    private static int CompareBoundaryPoints(Node aContainer, int aOffset, Node bContainer, int bOffset)
    {
        if (ReferenceEquals(aContainer, bContainer)) return aOffset.CompareTo(bOffset);

        if (IsAncestor(aContainer, bContainer, out var childUnderA))
        {
            var childIndex = GetChildIndex(aContainer, childUnderA!);
            return aOffset <= childIndex ? -1 : 1;
        }

        if (IsAncestor(bContainer, aContainer, out var childUnderB))
        {
            var childIndex = GetChildIndex(bContainer, childUnderB!);
            return childIndex < bOffset ? -1 : 1;
        }

        var aPath = GetAncestorPath(aContainer);
        var bPath = GetAncestorPath(bContainer);
        var length = Math.Min(aPath.Count, bPath.Count);
        for (var i = 0; i < length; i++)
        {
            if (ReferenceEquals(aPath[i], bPath[i])) continue;
            var parent = aPath[i].ParentNode ?? throw new InvalidOperationException("Boundary node is detached.");
            return GetChildIndex(parent, aPath[i]).CompareTo(GetChildIndex(parent, bPath[i]));
        }

        return aPath.Count.CompareTo(bPath.Count);
    }

    private static bool IsAncestor(Node ancestor, Node node, out Node? childUnderAncestor)
    {
        childUnderAncestor = null;
        var current = node;
        while (current.ParentNode != null)
        {
            if (ReferenceEquals(current.ParentNode, ancestor))
            {
                childUnderAncestor = current;
                return true;
            }
            current = current.ParentNode;
        }
        return false;
    }

    private static List<Node> GetAncestorPath(Node node)
    {
        var path = new List<Node>();
        while (node != null)
        {
            path.Add(node);
            node = node.ParentNode!;
        }
        path.Reverse();
        return path;
    }

    private static int GetChildIndex(Node parent, Node child)
    {
        if (parent is not Element element)
            throw new InvalidOperationException("Only element child nodes are supported.");
        var index = element.ChildNodes.IndexOf(child);
        if (index < 0) throw new InvalidOperationException("Boundary node is detached.");
        return index;
    }
}
