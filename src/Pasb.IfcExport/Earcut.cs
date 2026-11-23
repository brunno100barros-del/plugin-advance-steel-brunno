using System;
using System.Collections.Generic;

namespace Pasb.IfcExport;

/// <summary>
/// Triangulação de polígono com furos por recorte de orelhas (ear clipping), no estilo do algoritmo "earcut" (Mapbox, ISC).
/// Usada para as faces com furos do corpo real: muitos visualizadores não preenchem IfcIndexedPolygonalFaceWithVoids
/// (as chapas furadas apareciam "transparentes"). A orientação de entrada dos contornos não importa.
/// </summary>
internal static class Earcut
{
    private sealed class Node
    {
        public int I; public double X, Y;
        public Node Prev = null!, Next = null!;
        public bool Steiner;
        public Node(int i, double x, double y) { I = i; X = x; Y = y; }
    }

    /// <summary>
    /// data: coordenadas 2D (x0,y0,x1,y1...) do contorno externo seguido dos furos; holeStarts: índice (em pontos) de início de cada furo.
    /// Devolve trios de índices de ponto. Lista vazia se não conseguir.
    /// </summary>
    public static List<int> Triangulate(double[] data, IReadOnlyList<int> holeStarts)
    {
        var tris = new List<int>();
        int outerLen = (holeStarts.Count > 0 ? holeStarts[0] : data.Length / 2) * 2;
        Node? outer = LinkedList(data, 0, outerLen, true);
        if (outer == null || outer.Next == outer.Prev) return tris;
        if (holeStarts.Count > 0) outer = EliminateHoles(data, holeStarts, outer);
        if (outer == null) return tris;
        EarcutLinked(outer, tris, 0);
        return tris;
    }

    private static Node? LinkedList(double[] data, int start, int end, bool clockwise)
    {
        Node? last = null;
        if (clockwise == (SignedArea(data, start, end) > 0))
            for (int i = start; i < end; i += 2) last = InsertNode(i / 2, data[i], data[i + 1], last);
        else
            for (int i = end - 2; i >= start; i -= 2) last = InsertNode(i / 2, data[i], data[i + 1], last);
        if (last != null && EqualsPt(last, last.Next)) { RemoveNode(last); last = last.Next; }
        return last;
    }

    private static Node? FilterPoints(Node? start, Node? end = null)
    {
        if (start == null) return start;
        end ??= start;
        Node p = start;
        bool again;
        do
        {
            again = false;
            if (!p.Steiner && (EqualsPt(p, p.Next) || Area(p.Prev, p, p.Next) == 0))
            {
                RemoveNode(p);
                p = end = p.Prev;
                if (p == p.Next) break;
                again = true;
            }
            else p = p.Next;
        } while (again || p != end);
        return end;
    }

    private static void EarcutLinked(Node? ear, List<int> tris, int pass)
    {
        if (ear == null) return;
        Node stop = ear;
        int guard = 0;
        while (ear.Prev != ear.Next)
        {
            if (++guard > 1_000_000) return;
            Node prev = ear.Prev, next = ear.Next;
            if (IsEar(ear))
            {
                tris.Add(prev.I); tris.Add(ear.I); tris.Add(next.I);
                RemoveNode(ear);
                ear = next.Next;
                stop = next.Next;
                continue;
            }
            ear = next;
            if (ear == stop)
            {
                if (pass == 0) EarcutLinked(FilterPoints(ear), tris, 1);
                else if (pass == 1)
                {
                    Node? cured = CureLocalIntersections(FilterPoints(ear)!, tris);
                    EarcutLinked(cured, tris, 2);
                }
                else if (pass == 2) SplitEarcut(ear, tris);
                break;
            }
        }
    }

    private static bool IsEar(Node ear)
    {
        Node a = ear.Prev, b = ear, c = ear.Next;
        if (Area(a, b, c) >= 0) return false;
        for (Node p = ear.Next.Next; p != ear.Prev; p = p.Next)
            if (PointInTriangle(a.X, a.Y, b.X, b.Y, c.X, c.Y, p.X, p.Y) && Area(p.Prev, p, p.Next) >= 0) return false;
        return true;
    }

    private static Node CureLocalIntersections(Node start, List<int> tris)
    {
        Node p = start;
        do
        {
            Node a = p.Prev, b = p.Next.Next;
            if (!EqualsPt(a, b) && Intersects(a, p, p.Next, b) && LocallyInside(a, b) && LocallyInside(b, a))
            {
                tris.Add(a.I); tris.Add(p.I); tris.Add(b.I);
                RemoveNode(p); RemoveNode(p.Next);
                p = start = b;
            }
            p = p.Next;
        } while (p != start);
        return FilterPoints(p)!;
    }

    private static void SplitEarcut(Node start, List<int> tris)
    {
        Node a = start;
        do
        {
            Node b = a.Next.Next;
            while (b != a.Prev)
            {
                if (a.I != b.I && IsValidDiagonal(a, b))
                {
                    Node c = SplitPolygon(a, b);
                    a = FilterPoints(a, a.Next)!;
                    c = FilterPoints(c, c.Next)!;
                    EarcutLinked(a, tris, 0);
                    EarcutLinked(c, tris, 0);
                    return;
                }
                b = b.Next;
            }
            a = a.Next;
        } while (a != start);
    }

    private static Node? EliminateHoles(double[] data, IReadOnlyList<int> holeStarts, Node outer)
    {
        var queue = new List<Node>();
        for (int i = 0; i < holeStarts.Count; i++)
        {
            int start = holeStarts[i] * 2;
            int end = i < holeStarts.Count - 1 ? holeStarts[i + 1] * 2 : data.Length;
            Node? list = LinkedList(data, start, end, false);
            if (list == null) continue;
            if (list == list.Next) list.Steiner = true;
            queue.Add(GetLeftmost(list));
        }
        queue.Sort((a, b) => a.X.CompareTo(b.X));
        foreach (Node hole in queue)
        {
            outer = EliminateHole(hole, outer)!;
            if (outer == null) return null;
        }
        return outer;
    }

    private static Node? EliminateHole(Node hole, Node outer)
    {
        Node? bridge = FindHoleBridge(hole, outer);
        if (bridge == null) return outer;
        Node bridgeReverse = SplitPolygon(bridge, hole);
        FilterPoints(bridgeReverse, bridgeReverse.Next);
        return FilterPoints(bridge, bridge.Next);
    }

    private static Node? FindHoleBridge(Node hole, Node outer)
    {
        Node p = outer;
        double hx = hole.X, hy = hole.Y, qx = double.NegativeInfinity;
        Node? m = null;
        do
        {
            if (hy <= p.Y && hy >= p.Next.Y && p.Next.Y != p.Y)
            {
                double x = p.X + (hy - p.Y) * (p.Next.X - p.X) / (p.Next.Y - p.Y);
                if (x <= hx && x > qx)
                {
                    qx = x;
                    m = p.X < p.Next.X ? p : p.Next;
                    if (x == hx) return m;
                }
            }
            p = p.Next;
        } while (p != outer);
        if (m == null) return null;

        Node stop = m;
        double mx = m.X, my = m.Y, tanMin = double.PositiveInfinity;
        p = m;
        do
        {
            if (hx >= p.X && p.X >= mx && hx != p.X &&
                PointInTriangle(hy < my ? hx : qx, hy, mx, my, hy < my ? qx : hx, hy, p.X, p.Y))
            {
                double tan = Math.Abs(hy - p.Y) / (hx - p.X);
                if (LocallyInside(p, hole) && (tan < tanMin || (tan == tanMin && (p.X > m.X || (p.X == m.X && SectorContainsSector(m, p))))))
                {
                    m = p; tanMin = tan;
                }
            }
            p = p.Next;
        } while (p != stop);
        return m;
    }

    private static bool SectorContainsSector(Node m, Node p) => Area(m.Prev, m, p.Prev) < 0 && Area(p.Next, m, m.Next) < 0;

    private static Node GetLeftmost(Node start)
    {
        Node p = start, leftmost = start;
        do
        {
            if (p.X < leftmost.X || (p.X == leftmost.X && p.Y < leftmost.Y)) leftmost = p;
            p = p.Next;
        } while (p != start);
        return leftmost;
    }

    private static bool PointInTriangle(double ax, double ay, double bx, double by, double cx, double cy, double px, double py) =>
        (cx - px) * (ay - py) >= (ax - px) * (cy - py) &&
        (ax - px) * (by - py) >= (bx - px) * (ay - py) &&
        (bx - px) * (cy - py) >= (cx - px) * (by - py);

    private static bool IsValidDiagonal(Node a, Node b) =>
        a.Next.I != b.I && a.Prev.I != b.I && !IntersectsPolygon(a, b) &&
        (LocallyInside(a, b) && LocallyInside(b, a) && MiddleInside(a, b) && (Area(a.Prev, a, b.Prev) != 0 || Area(a, b.Prev, b) != 0) ||
         EqualsPt(a, b) && Area(a.Prev, a, a.Next) > 0 && Area(b.Prev, b, b.Next) > 0);

    private static double Area(Node p, Node q, Node r) => (q.Y - p.Y) * (r.X - q.X) - (q.X - p.X) * (r.Y - q.Y);

    private static bool EqualsPt(Node a, Node b) => a.X == b.X && a.Y == b.Y;

    private static bool Intersects(Node p1, Node q1, Node p2, Node q2)
    {
        int o1 = Sign(Area(p1, q1, p2)), o2 = Sign(Area(p1, q1, q2)), o3 = Sign(Area(p2, q2, p1)), o4 = Sign(Area(p2, q2, q1));
        if (o1 != o2 && o3 != o4) return true;
        if (o1 == 0 && OnSegment(p1, p2, q1)) return true;
        if (o2 == 0 && OnSegment(p1, q2, q1)) return true;
        if (o3 == 0 && OnSegment(p2, p1, q2)) return true;
        if (o4 == 0 && OnSegment(p2, q1, q2)) return true;
        return false;
    }

    private static bool OnSegment(Node p, Node q, Node r) =>
        q.X <= Math.Max(p.X, r.X) && q.X >= Math.Min(p.X, r.X) && q.Y <= Math.Max(p.Y, r.Y) && q.Y >= Math.Min(p.Y, r.Y);

    private static int Sign(double v) => v > 0 ? 1 : v < 0 ? -1 : 0;

    private static bool IntersectsPolygon(Node a, Node b)
    {
        Node p = a;
        do
        {
            if (p.I != a.I && p.Next.I != a.I && p.I != b.I && p.Next.I != b.I && Intersects(p, p.Next, a, b)) return true;
            p = p.Next;
        } while (p != a);
        return false;
    }

    private static bool LocallyInside(Node a, Node b) =>
        Area(a.Prev, a, a.Next) < 0
            ? Area(a, b, a.Next) >= 0 && Area(a, a.Prev, b) >= 0
            : Area(a, b, a.Prev) < 0 || Area(a, a.Next, b) < 0;

    private static bool MiddleInside(Node a, Node b)
    {
        Node p = a;
        bool inside = false;
        double px = (a.X + b.X) / 2, py = (a.Y + b.Y) / 2;
        do
        {
            if (((p.Y > py) != (p.Next.Y > py)) && p.Next.Y != p.Y && (px < (p.Next.X - p.X) * (py - p.Y) / (p.Next.Y - p.Y) + p.X))
                inside = !inside;
            p = p.Next;
        } while (p != a);
        return inside;
    }

    private static Node SplitPolygon(Node a, Node b)
    {
        var a2 = new Node(a.I, a.X, a.Y);
        var b2 = new Node(b.I, b.X, b.Y);
        Node an = a.Next, bp = b.Prev;
        a.Next = b; b.Prev = a;
        a2.Next = an; an.Prev = a2;
        b2.Next = a2; a2.Prev = b2;
        bp.Next = b2; b2.Prev = bp;
        return b2;
    }

    private static Node InsertNode(int i, double x, double y, Node? last)
    {
        var p = new Node(i, x, y);
        if (last == null) { p.Prev = p; p.Next = p; }
        else { p.Next = last.Next; p.Prev = last; last.Next.Prev = p; last.Next = p; }
        return p;
    }

    private static void RemoveNode(Node p) { p.Next.Prev = p.Prev; p.Prev.Next = p.Next; }

    private static double SignedArea(double[] data, int start, int end)
    {
        double sum = 0;
        for (int i = start, j = end - 2; i < end; i += 2) { sum += (data[j] - data[i]) * (data[i + 1] + data[j + 1]); j = i; }
        return sum;
    }
}
