using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnionFind
{
    public List<PointSet> sets = new List<PointSet>();

    public void MakeSet(Vector2 r) {
        sets.Add(new PointSet { root = r, children = new List<Vector2>() });
    }

    public void Union(PointSet p1, PointSet p2) {
        p1.children.Add(p2.root);
        p1.children.AddRange(p2.children);
        sets.Remove(p2);
    }

    public Vector2 Find(Vector2 p) {
        Vector2 result = new Vector2();

        for (int i = 0; i < sets.Count; i++) {
            if (sets[i].root == p || sets[i].children.Contains(p)) {
                result = sets[i].root;
                break;
            }
        }

        return result;
    }
}
