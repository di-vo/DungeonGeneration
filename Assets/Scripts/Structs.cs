using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct Cell {
    public Vector2Int centerPos;
    public int id, width, height;

    // starting values, used for the seperation algorithm
    public Vector2 startPos;
}

public struct Edge {
    public Vector2 start, end;
    public float weight;
}

public struct PointSet {
    public Vector2 root;
    public List<Vector2> children;
}
