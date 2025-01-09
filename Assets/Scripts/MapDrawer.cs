using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Based on this concept: https://www.reddit.com/r/gamedev/comments/1dlwc4/procedural_dungeon_generation_algorithm_explained/
public class MapDrawer : MonoBehaviour
{
    public Tilemap tilemap;
    public Tilemap wallsTm;
    public Tile floorTile;
    public Tile wallTile;
    [Tooltip("The total amount of cells that will be generated at the start")]
    public int cellCount;
    [Tooltip("The number of rooms that will stay at the end. This needs to be in the range 2 < x < cellCount")]
    public int mainRoomCount;
    [Tooltip("The radius that the rooms will be spawned it before seperation")]
    public float spawnRadius;
    [Tooltip("Adds a margin to each cell to increase the distance that they need to be apart to not count as overlapping")]
    public int overlapMargin;
    [Tooltip("The percentage of edges that should be added on top of the minimum amount")]
    public float extraEdgePercentage;
    [Tooltip("How thick to draw each edge")]
    public int edgeThickness;
    public GameObject treasurePrefab;

    [HideInInspector]
    public List<Cell> startAndEnd;

    private List<Cell> cells;
    private List<Cell> mainRooms;
    private List<Vector2> points;
    private List<Vector2Int> tilePositions;
    private int[] dimensions = {9, 11, 13, 15, 17};

    private UIScript uiScript;

    void Start()
    {
        cells = new List<Cell>();
        mainRooms = new List<Cell>();
        points = new List<Vector2>();
        tilePositions = new List<Vector2Int>();
        startAndEnd = new List<Cell>();
        uiScript = GameObject.Find("UIManager").GetComponent<UIScript>();
    }

    private void Update() {
        if (Input.GetKeyDown(KeyCode.E)) {

            DrawTileMap();
        }
    }

    private void DrawTileMap() {
        Stopwatch totalWatch = new Stopwatch();
        totalWatch.Start();
        ClearTilemap();

        // create cells
        for (int i = 0; i < cellCount; i++) {
            Cell c;
            c.id = i + 1;

            float tmpX = 0;
            float tmpY = 0;

            do {
                tmpX = Random.Range(-spawnRadius, spawnRadius);
                tmpY = Random.Range(-spawnRadius, spawnRadius);
            } while (cells.Find(c => c.startPos.x == tmpX && c.startPos.y == tmpY).id != 0);

            c.startPos.x = tmpX;
            c.startPos.y = tmpY;

            // placeholder
            c.centerPos = new Vector2Int(0, 0);

            c.width = dimensions[Random.Range(0, dimensions.Length - 1)];
            c.height = dimensions[Random.Range(0, dimensions.Length - 1)];

            // skew rng a bit
            float roomRng = Random.Range(0f, 1f);

            if (roomRng >= 1 - (mainRoomCount / cellCount)) {
                c.width *= 2;
                c.height *= 2;
            }
            else if (roomRng >= 0.5) {
                c.width -= 2;
                c.height -= 2;
            }

            cells.Add(c);
        }

        Stopwatch watch = new Stopwatch();
        watch.Start();
        // seperate cells
        for (int iterations = 0; iterations < 3 * cellCount; iterations++) {
            for (int i = 0; i < cells.Count; i++) {
                Vector2 dirVec = ComputeSeparation(cells[i]);

                if (dirVec != Vector2.zero) {
                    Cell c = cells[i];
                    c.startPos.x += dirVec.x;
                    c.startPos.y += dirVec.y;
                    cells[i] = c;
                }
            }

            if (!IsAnyOverlapping()) {
                break;
            }
        }
        watch.Stop();
        print("separation time: " + watch.Elapsed.TotalSeconds + "s");

        for (int i = 0; i < cells.Count; i++) {
            Cell c = cells[i];
            c.centerPos.x = Mathf.FloorToInt(c.startPos.x);
            c.centerPos.y = Mathf.FloorToInt(c.startPos.y);
            cells[i] = c;
        }

        // determine main rooms
        watch.Restart();
        cells = cells.OrderByDescending(c => c.width * c.height).ToList();

        for (int i = 0; i < mainRoomCount; i++) {
            mainRooms.Add(cells[i]);
            points.Add(new Vector2(cells[i].centerPos.x, cells[i].centerPos.y));
        }
        watch.Stop();
        print("choosing main rooms time: " + watch.Elapsed.TotalMilliseconds + "ms");
        
        // create delanauy triangulation
        watch.Restart();
        List<Edge> edges = Triangulation.DelanauyTriangulation(points);

        watch.Stop();
        print("triangulation time" + watch.Elapsed.TotalMilliseconds + "ms");

        foreach (Edge e in edges) {
            UnityEngine.Debug.DrawLine(e.start, e.end, Color.yellow, 5); // development only
        }
        
        // create minimum spanning tree
        List<Edge> minimumEdges = ComputeMinimumSpanningTree(edges);

        foreach (Edge e in minimumEdges) {
            UnityEngine.Debug.DrawLine(e.start, e.end, Color.red, 5); // development only
        }
        
        // incorporate low percentage of edges into the final result
        List<Edge> finalEdges = new List<Edge>(minimumEdges);

        for (int i = 0; i < edges.Count; i++) {
            if (!minimumEdges.Contains(edges[i])) {
                float rng = Random.Range(0f, 1f);

                if (rng > 1 - extraEdgePercentage) {
                    finalEdges.Add(edges[i]);
                    UnityEngine.Debug.DrawLine(edges[i].start, edges[i].end, Color.blue, 5); // development only
                }
            }
        }
        
        // determine start and end
        startAndEnd = FindStartAndEnd(mainRooms);

        watch.Restart();
        floorTile.color = Color.green;
        foreach (Cell c in startAndEnd) {
            DrawCell(c, floorTile);
        }

        // add collider in start room
        GameObject startArea = new GameObject();
        startArea.transform.position = new Vector3(startAndEnd[0].centerPos.x, startAndEnd[0].centerPos.y);
        BoxCollider2D startCollider = startArea.AddComponent<BoxCollider2D>();
        startCollider.size = new Vector2(startAndEnd[0].width, startAndEnd[0].height);
        startCollider.isTrigger = true;
        startArea.name = "StartArea";
        startArea.tag = "StartArea";

        // draw cells
        floorTile.color = new Color(21f / 256f, 104f / 256f, 189f / 256f, 1f);
        foreach (Cell c in mainRooms) {
            DrawCell(c, floorTile);
        }

        // draw edges
        foreach (Edge e in finalEdges) {
            DrawEdge(e);
        }

        // draw walls
        wallTile.color = new Color(99f / 256f, 73f / 256f, 47f / 256f, 1f);
        foreach (Vector2Int v in tilePositions) {
            DrawWallTile(v.x, v.y);
        }

        watch.Stop();
        print("drawing time" + watch.Elapsed.Seconds + "s");

        // spawn player in start room
        GameObject.FindWithTag("Player").transform.position = new Vector3(startAndEnd[0].centerPos.x, startAndEnd[0].centerPos.y, 0);

        // spawn treasure in end room
        GameObject treasure = Instantiate(treasurePrefab, new Vector3(startAndEnd[1].centerPos.x, startAndEnd[1].centerPos.y, 0), Quaternion.identity);

        totalWatch.Stop();
        print("total generation time: " + totalWatch.Elapsed.TotalSeconds + "s");

        // start game timer
        uiScript.StartTimer();
    }

    private IEnumerator DebugDrawRoutine() {
        int iterations;
        for (iterations = 0; iterations < 3 * cellCount; iterations++) {
            for (int i = 0; i < cells.Count; i++) {
                Vector2 dirVec = ComputeSeparation(cells[i]);

                if (dirVec != Vector2.zero) {
                    Cell c = cells[i];
                    c.startPos.x += dirVec.x;
                    c.startPos.y += dirVec.y;
                    cells[i] = c;
                }

                DrawDebugRect(cells[i]);
            }

            if (!IsAnyOverlapping()) {
                break;
            }

            yield return new WaitForSeconds(0.1f);
        }

        print("iterations: " + iterations);
    }

    private void DrawDebugRect(Cell c) {
        Vector3 topLeft = new Vector3(c.startPos.x - (c.width / 2), c.startPos.y - (c.height / 2), 0);
        Vector3 topRight = new Vector3(c.startPos.x + (c.width / 2), c.startPos.y - (c.height / 2), 0);
        Vector3 bottomRight = new Vector3(c.startPos.x + (c.width / 2), c.startPos.y + (c.height / 2), 0);
        Vector3 bottomLeft = new Vector3(c.startPos.x - (c.width / 2), c.startPos.y + (c.height / 2), 0);

        float duration = 0.1f;
        UnityEngine.Debug.DrawLine(topLeft, topRight, Color.red, duration);
        UnityEngine.Debug.DrawLine(topRight, bottomRight, Color.red, duration);
        UnityEngine.Debug.DrawLine(bottomRight, bottomLeft, Color.red, duration);
        UnityEngine.Debug.DrawLine(bottomLeft, topLeft, Color.red, duration);
    }

    private void ClearTilemap() {
        tilemap.ClearAllTiles();
        wallsTm.ClearAllTiles();

        cells.Clear();
        mainRooms.Clear();
        points.Clear();
        tilePositions.Clear();

        GameObject t = GameObject.FindWithTag("Treasure");
        if (t != null) {
            Destroy(t);
        }

        GameObject sa = GameObject.FindWithTag("StartArea");
        if (sa != null) {
            Destroy(sa);
        }

        uiScript.ToggleTreasureImage(false);
        uiScript.ToggleDropTreasureText(false);
        GameObject.FindWithTag("Player").GetComponent<PlayerScript>().ResetPlayer();
    }

    private void DrawCell(Cell c, Tile tile) {
        for (int x = c.centerPos.x - Mathf.FloorToInt(c.width / 2); x <= c.centerPos.x + Mathf.FloorToInt(c.width / 2); x++) {
            for (int y = c.centerPos.y - Mathf.FloorToInt(c.height / 2); y <= c.centerPos.y + Mathf.FloorToInt(c.height / 2); y++) {
                if (tilemap.GetTile(new Vector3Int(x, y, 0)) == null) {
                    tilemap.SetTile(new Vector3Int(x, y, 0), floorTile);
                }

                // add to list of all tiles to draw
                if (!tilePositions.Contains(new Vector2Int(x, y))) {
                    tilePositions.Add(new Vector2Int(x, y));
                }
            }
        }
    }

    // Bresenham's Algorithm
    private void DrawEdge(Edge e) {
        if (Mathf.Abs(e.end.x - e.start.x) > Mathf.Abs(e.end.y - e.start.y)) {
            DrawEdgeH(e);
        } else {
            DrawEdgeV(e);
        }
    }

    private void DrawEdgeH(Edge e) {
        int x0 = (int)e.start.x;
        int x1 = (int)e.end.x;
        int y0 = (int)e.start.y;
        int y1 = (int)e.end.y;

        if (x0 > x1) {
            int tmp = x1;
            x1 = x0;
            x0 = tmp;

            tmp = y1;
            y1 = y0;
            y0 = tmp;
        }

        int dx = x1 - x0;
        int dy = y1 - y0;

        int dir = (dy < 0) ? -1 : 1;
        dy *= dir;

        if (dx != 0) {
            int y = y0;
            int D = 2 * dy - dx;

            for (int i = 0; i <= dx; i++) {
                // draw tile with thickness
                for (int u = x0 + i - edgeThickness; u <= x0 + i + edgeThickness; u++) {
                    for (int v = y - edgeThickness; v <= y + edgeThickness; v++) {
                        if (tilemap.GetTile(new Vector3Int(u, v, 0)) == null) {
                            tilemap.SetTile(new Vector3Int(u, v, 0), floorTile);
                        }

                        // add to list of all tiles to draw
                        if (!tilePositions.Contains(new Vector2Int(u, v))) {
                            tilePositions.Add(new Vector2Int(u, v));
                        }
                    }
                }

                if (D >= 0) {
                    y += dir;
                    D = D - 2 * dx;
                }

                D = D + 2 * dy;
            }
        }
    }

    private void DrawEdgeV(Edge e) {
        int x0 = (int)e.start.x;
        int x1 = (int)e.end.x;
        int y0 = (int)e.start.y;
        int y1 = (int)e.end.y;

        if (y0 > y1) {
            int tmp = x1;
            x1 = x0;
            x0 = tmp;

            tmp = y1;
            y1 = y0;
            y0 = tmp;
        }

        int dx = x1 - x0;
        int dy = y1 - y0;

        int dir = (dx < 0) ? -1 : 1;
        dx *= dir;

        if (dy != 0) {
            int x = x0;
            int D = 2 * dx - dy;

            for (int i = 0; i <= dy; i++) {
                // draw tile with thickness
                for (int u = x - edgeThickness; u <= x + edgeThickness; u++) {
                    for (int v = y0 + i - edgeThickness; v <= y0 + i + edgeThickness; v++) {
                        if (tilemap.GetTile(new Vector3Int(u, v, 0)) == null) {
                            tilemap.SetTile(new Vector3Int(u, v, 0), floorTile);
                        }

                        // add to list of all tiles to draw
                        if (!tilePositions.Contains(new Vector2Int(u, v))) {
                            tilePositions.Add(new Vector2Int(u, v));
                        }
                    }
                }

                if (D >= 0) {
                    x += dir;
                    D = D - 2 * dy;
                }

                D = D + 2 * dx;
            }
        }
    }

    private void DrawWallTile(int x, int y) {
        for (int dx = x - 1; dx < x + 2; dx++) {
            for (int dy = y - 1; dy < y + 2; dy++) {
                TileBase tb = tilemap.GetTile(new Vector3Int(dx, dy, 0));

                if (tb == null) {
                    wallsTm.SetTile(new Vector3Int(dx, dy, 0), wallTile);
                }
            }
        }
    }

    private bool IsOverlapping(Cell c1, Cell c2) {
        float distanceX = Mathf.Abs(c2.startPos.x - c1.startPos.x);
        float distanceY = Mathf.Abs(c2.startPos.y - c1.startPos.y);

        return distanceX <= (c1.width / 2) + (c2.width / 2) + overlapMargin && distanceY <= (c1.height / 2) + (c2.height / 2) + overlapMargin;
    }

    private bool IsAnyOverlapping() {
        for (int i = 0; i < cells.Count; i++) {
            for (int j = i + 1; j < cells.Count; j++) {
                if (IsOverlapping(cells[i], cells[j])) {
                    return true;
                }
            }
        }

        return false;
    }

    private Vector2 ComputeSeparation(Cell cell) {
        Vector2 dirVec = new Vector2();
        int neighborCount = 0;

        foreach (Cell c in cells) {
            if (c.id != cell.id) {
                if (IsOverlapping(cell, c)) {
                    dirVec.x += c.startPos.x - cell.startPos.x;
                    dirVec.y += c.startPos.y - cell.startPos.y;
                    neighborCount++;
                }
            }
        }

        if (neighborCount == 0) {
            return Vector2.zero;
        }

        dirVec.x /= neighborCount;
        dirVec.y /= neighborCount;

        dirVec.x *= -1;
        dirVec.y *= -1;

        //dirVec.Normalize();

        return dirVec;
    }

    // Kruskal's Algorithm
    private List<Edge> ComputeMinimumSpanningTree(List<Edge> edges) {
        List<Edge> result = new List<Edge>();
        UnionFind unionFind = new UnionFind();

        // create seperate set for each vertex
        List<Vector2> vertices = new List<Vector2>();

        for (int i = 0; i < edges.Count; i++) {
            if (!vertices.Contains(edges[i].start)) {
                vertices.Add(edges[i].start);
            }

            if (!vertices.Contains(edges[i].end)) {
                vertices.Add(edges[i].end);
            }
        }

        for (int i = 0; i < vertices.Count; i++) {
            unionFind.MakeSet(vertices[i]);
        }

        // sort edges by weight
        edges = edges.OrderBy(e => e.weight).ToList();

        // iterate over edges
        for (int i = 0; i < edges.Count; i++) {
            Vector2 r1 = unionFind.Find(edges[i].start);
            Vector2 r2 = unionFind.Find(edges[i].end);

            // if the roots are not in the same set, add edge and union the two sets
            if (r1 != r2) {
                result.Add(edges[i]);
                unionFind.Union(unionFind.sets.Find(s => s.root == r1), unionFind.sets.Find(s => s.root == r2));
            }
        }

        return result;
    }

    private List<Cell> FindStartAndEnd(List<Cell> cells) {
        Cell start = cells[0];
        Cell end = cells[1];

        // based on distance

        //for (int i = 0; i < cells.Count; i++) {
        //    for (int j = i + 1; j < cells.Count; j++) {
        //        if (Vector2.Distance(cells[i].centerPos, cells[j].centerPos) > Vector2.Distance(start.centerPos, end.centerPos)) {
        //            start = cells[i];
        //            end = cells[j];
        //        }

        //    }
        //}

        // highest and lowest cell

        for (int i = 0; i < cells.Count; i++) {
            if (cells[i].centerPos.y > start.centerPos.y) {
                start = cells[i];
            }

            if (cells[i].centerPos.y < end.centerPos.y) {
                end = cells[i];
            }
        }

        return new List<Cell> { start, end };
    }
}
