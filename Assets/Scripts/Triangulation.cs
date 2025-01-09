using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Triangulation : MonoBehaviour
{
    // Based on this implementation: https://youtu.be/pPqZPX9DvTg?si=_GnNUlDX_YunFbEF
    public static List<Edge> DelanauyTriangulation(List<Vector2> points) {
        // find min and max boundaries of point cloud
        float minX = 0;
        float maxX = 0;
        float minY = 0;
        float maxY = 0;

        for (int i = 0; i < points.Count; i++) {
            if (points[i].x > maxX) {
                maxX = points[i].x;
            } else if (points[i].x < minX) {
                minX = points[i].x;
            }

            if (points[i].y > maxY) {
                maxY = points[i].y;
            } else if (points[i].y < minY) {
                minY = points[i].y;
            }
        }

        // remap to (0,0)-(1,1), preserving aspect ratio
        float height = maxY - minY;
        float width = maxX - minX;
        float d = height; // d = largest dimension

        if (width > d) {
            d = width;
        }

        for (int i = 0; i < points.Count; i++) {
            Vector2 point = points[i];
            point.x = (points[i].x - minX) / d;
            point.y = (points[i].y - minY) / d;
            points[i] = point;
        }

        // sort points by proximity
        int nBinRows = Mathf.CeilToInt(Mathf.Pow(points.Count, 0.25f));
        int[] bins = new int[points.Count];

        for (int i = 0; i < points.Count; i++) {
            int p = (int)(points[i].y * nBinRows * 0.999); // bin row
            int q = (int)(points[i].x * nBinRows * 0.999); // bin column

            if (p % 2 == 1) {
                bins[i] = (p + 1) * nBinRows - q;
            } else {
                bins[i] = p * nBinRows + q + 1;
            }
        }

        int key = 0;

        for (int i = 1; i < points.Count; i++) { // insertion sort
            key = bins[i];
            Vector2 tempF = points[i];
            int j = i - 1;

            while (j >= 0 && (bins[j] > key)) {
                bins[j+1] = bins[j];
                Vector2 p = points[j + 1];
                p.x = points[j].x;
                p.y = points[j].y;
                points[j + 1] = p;
                j--;
            }
            bins[j + 1] = key;

            Vector2 p1 = points[j + 1];
            p1.x = tempF.x;
            p1.y = tempF.y;
            points[j + 1] = p1;
        }

        // add big triangle
        points.Add(new Vector2(-100, -100));
        points.Add(new Vector2(100, -100));
        points.Add(new Vector2(0, 100));

        // data structures required
        List<int[]> verts = new List<int[]>();
        verts.Add(new int[] { points.Count - 3, points.Count - 2, points.Count - 1 });

        List<int[]> tris = new List<int[]>();
        tris.Add(new int[] {-1, -1, -1});

        Stack<int> triangleStack = new Stack<int>();
        int tos = -1;

        // insert all points and triangulate one by one
        for (int ii = 0; ii < (points.Count - 3); ii++) {
            // find triangle which contains point
            int j = verts.Count - 1;

            while (true) {
                if (PlanarPointWithinTriangle(points[ii], points[verts[j][0]], points[verts[j][1]], points[verts[j][2]])) {
                    // vertices of new triangle
                    verts.Add(new int[] { ii, verts[j][1], verts[j][2] });
                    verts.Add(new int[] { ii, verts[j][2], verts[j][0] });

                    // update adjacencies of triangles
                    // fix adjacency of A
                    int adj1 = tris[j][0];
                    int adj2 = tris[j][1];
                    int adj3 = tris[j][2];

                    if (adj1 >= 0) {
                        for (int m = 0; m < 3; m++) {
                            if (tris[adj1][m] == j) {
                                tris[adj1][m] = j;
                                break;
                            }
                        }
                    }

                    if (adj2 >= 0) {
                        for (int m = 0; m < 3; m++) {
                            if (tris[adj2][m] == j) {
                                tris[adj2][m] = verts.Count - 2;
                                break;
                            }
                        }
                    }

                    if (adj3 >= 0) {
                        for (int m = 0; m < 3; m++) {
                            if (tris[adj3][m] == j) {
                                tris[adj3][m] = verts.Count - 1;
                                break;
                            }
                        }
                    }

                    // adjacencies of new triangles
                    tris.Add(new int[] { j, tris[j][1], verts.Count - 1 });
                    tris.Add(new int[] { verts.Count - 2, tris[j][2], j });

                    // replace v3 of containing triangle with P and rotate to v1
                    verts[j][2] = verts[j][1];
                    verts[j][1] = verts[j][0];
                    verts[j][0] = ii;

                    // replace 1st and 3rd adjacencies of containing triangle with new triangles
                    tris[j][1] = tris[j][0];
                    tris[j][2] = verts.Count - 2;
                    tris[j][0] = verts.Count - 1;

                    // place each triangle containing P onto a stack, if the edge opposite P has an adjacent triangle
                    if (tris[j][1] >= 0) {
                        triangleStack.Push(j);
                        tos++;
                    }

                    if (tris[verts.Count - 2][1] >= 0) {
                        triangleStack.Push(verts.Count - 2);
                        tos++;
                    }

                    if (tris[verts.Count - 1][1] >= 0) {
                        triangleStack.Push(verts.Count - 1);
                        tos++;
                    }

                    while (tos >= 0) { // looping through the stack
                        int L = triangleStack.Pop();
                        tos--;

                        Vector2 v1 = points[verts[L][2]];
                        Vector2 v2 = points[verts[L][1]];
                        int oppVert = -1;
                        int oppVertID = -1;

                        for (int k = 0; k < 3; k++) {
                            if ((verts[tris[L][1]][k] != verts[L][1]) &&
                                (verts[tris[L][1]][k] != verts[L][2])) {
                                oppVert = verts[tris[L][1]][k];
                                oppVertID = k;
                                break;
                            }
                        }

                        Vector2 v3 = points[oppVert];
                        Vector2 P = points[ii];

                        // check if P in circumference of triangle on top of stack
                        float cosA = ((v1.x - v3.x) * (v2.x - v3.x) + (v1.y - v3.y) * (v2.y - v3.y));
                        float cosB = ((v2.x - P.x) * (v1.x - P.x) + (v2.y - P.y) * (v1.y - P.y));
                        float sinA = ((v1.x - v3.x) * (v2.y - v3.y) - (v1.y - v3.y) * (v2.x - v3.x));
                        float sinB = ((v2.x - P.x) * (v1.y - P.y) - (v2.y - P.y) * (v1.x - P.x));

                        // NOTE: this should work, but I didnt see any result once so need to test further
                        //if (((cosA < 0) && (cosB < 0)) ||
                        //    ((-cosA * sinB) >
                        //    (cosB * sinA))) { 

                        if (((cosA < 0) && (cosB < 0)) ||
                            ((-cosA * ((v2.x - P.x) * (v1.y - P.y) - (v2.y - P.y) * (v1.x - P.x))) >
                            (cosB * ((v1.x - v3.x) * (v2.y - v3.y) - (v1.y - v3.y) * (v2.x - v3.x))))) {
                            // swap diagonal, and redo triangles L, R, A & C
                            // initial state:
                            int R = tris[L][1];
                            int C = tris[L][2];
                            int A = tris[R][(oppVertID + 2) % 3];

                            // fix adjacency of A
                            if (A >= 0) {
                                for (int m = 0; m < 3; m++) {
                                    if (tris[A][m] == R) {
                                        tris[A][m] = L;
                                        break;
                                    }
                                }
                            }

                            // fix adjacency of C
                            if (C >= 0) {
                                for (int m = 0; m < 3; m++) {
                                    if (tris[C][m] == L) {
                                        tris[C][m] = R;
                                        break;
                                    }
                                }
                            }

                            // fix vertices and adjacency of R
                            for (int m = 0; m < 3; m++) {
                                if (verts[R][m] == oppVert) {
                                    verts[R][(m + 2) % 3] = ii;
                                    break;
                                }
                            }

                            for (int m = 0; m < 3; m++) {
                                if (tris[R][m] == L) {
                                    tris[R][m] = C;
                                    break;
                                }
                            }

                            for (int m = 0; m < 3; m++) {
                                if (tris[R][m] == A) {
                                    tris[R][m] = L;
                                    break;
                                }
                            }

                            for (int m = 0; m < 3; m++) {
                                if (verts[R][0] != ii) {
                                    int temp1 = verts[R][0];
                                    int temp2 = tris[R][0];

                                    verts[R][0] = verts[R][1];
                                    verts[R][1] = verts[R][2];
                                    verts[R][2] = temp1;

                                    tris[R][0] = tris[R][1];
                                    tris[R][1] = tris[R][2];
                                    tris[R][2] = temp2;
                                }
                            }

                            // fix vertices and adjacency of L
                            verts[L][2] = oppVert;

                            for (int m = 0; m < 3; m++) {
                                if (tris[L][m] == C) {
                                    tris[L][m] = R;
                                    break;
                                }
                            }

                            for (int m = 0; m < 3; m++) {
                                if (tris[L][m] == R) {
                                    tris[L][m] = A;
                                    break;
                                }
                            }

                            // add L and R to stack if they have triangles opposite P
                            if (tris[L][1] >= 0) {
                                triangleStack.Push(L);
                                tos++;
                            }

                            if (tris[R][1] >= 0) {
                                triangleStack.Push(R);
                                tos++;
                            }
                        }
                    }
                    break;
                }

                // adjust j in the direction of target point ii
                Vector2 AB = new Vector2(points[verts[j][1]].x - points[verts[j][0]].x, points[verts[j][1]].y - points[verts[j][0]].y);
                Vector2 BC = new Vector2(points[verts[j][2]].x - points[verts[j][1]].x, points[verts[j][2]].y - points[verts[j][1]].y);
                Vector2 CA = new Vector2(points[verts[j][0]].x - points[verts[j][2]].x, points[verts[j][0]].y - points[verts[j][2]].y);

                Vector2 AP = new Vector2(points[ii].x - points[verts[j][0]].x, points[ii].y - points[verts[j][0]].y);
                Vector2 BP = new Vector2(points[ii].x - points[verts[j][1]].x, points[ii].y - points[verts[j][1]].y);
                Vector2 CP = new Vector2(points[ii].x - points[verts[j][2]].x, points[ii].y - points[verts[j][2]].y);

                Vector2 N1 = new Vector2(AB.y, -AB.x);
                Vector2 N2 = new Vector2(BC.y, -BC.x);
                Vector2 N3 = new Vector2(CA.y, -CA.x);

                float S1 = AP.x * N1.x + AP.y * N1.y;
                float S2 = BP.x * N2.x + BP.y * N2.y;
                float S3 = CP.x * N3.x + CP.y * N3.y;

                if ((S1 > 0) && (S1 >= S2) && (S1 >= S3)) {
                    j = tris[j][0];
                } else if ((S2 > 0) && (S2 >= S1) && (S2 >= S3)) {
                    j = tris[j][1];
                } else if ((S3 > 0) && (S3 >= S1) && (S3 >= S2)) {
                    j = tris[j][2];
                }
            }
        }

        // count how many triangles we have that dont involve supertriangle vertices
        int finalNt = verts.Count;
        int[] renumberAdj = new int[verts.Count];
        bool[] deadTris = new bool[verts.Count];

        for (int i = 0; i < verts.Count; i++) {
            if ((verts[i][0] >= (points.Count - 3)) ||
                (verts[i][1] >= (points.Count - 3)) ||
                (verts[i][2] >= (points.Count - 3))) {
                deadTris[i] = true;
                renumberAdj[i] = verts.Count - (finalNt--);
            } else {
                renumberAdj[i] = verts.Count - finalNt;
            }
        }

        // delete any triangles that contain the supertriangle vertices
        List<int[]> finalVerts = new List<int[]>();
        List<int[]> finalTris = new List<int[]>();

        for (int i = 0; i < verts.Count; i++) {
            if ((verts[i][0] < (points.Count - 3)) &&
                (verts[i][1] < (points.Count - 3)) &&
                (verts[i][2] < (points.Count - 3))) {
                finalVerts.Add(verts[i]);
                finalTris.Add(new int[] { (1 - Convert.ToInt32(deadTris[tris[i][0]])) * tris[i][0] - Convert.ToInt32(deadTris[tris[i][0]]),
                                          (1 - Convert.ToInt32(deadTris[tris[i][1]])) * tris[i][1] - Convert.ToInt32(deadTris[tris[i][1]]),
                                          (1 - Convert.ToInt32(deadTris[tris[i][2]])) * tris[i][2] - Convert.ToInt32(deadTris[tris[i][2]])});
            }
        }

        for (int i = 0; i < finalNt; i++) {
            if (finalTris[i][0] >= 0) {
                finalTris[i][0] -= renumberAdj[finalTris[i][0]];    
            }

            if (finalTris[i][1] >= 0) {
                finalTris[i][1] -= renumberAdj[finalTris[i][1]];    
            }

            if (finalTris[i][2] >= 0) {
                finalTris[i][2] -= renumberAdj[finalTris[i][2]];    
            }
        }

        points.RemoveRange(points.Count - 3, 3);
        // undo the mapping
        if (width > d) {
            d = width;
        }

        for (int i = 0; i < points.Count; i++) {
            Vector2 p = points[i];
            p.x = points[i].x * d + minX;
            p.y = points[i].y * d + minY;
            points[i] = p;
        }

        List<Edge> edges = new List<Edge>();

        for (int i = 0; i < finalVerts.Count; i++) {
            Edge e1 = new Edge { start = points[finalVerts[i][0]], end = points[finalVerts[i][1]] };
            Edge e2 = new Edge { start = points[finalVerts[i][1]], end = points[finalVerts[i][2]] };
            Edge e3 = new Edge { start = points[finalVerts[i][2]], end = points[finalVerts[i][0]] };

            e1.weight = Vector2.Distance(e1.end, e1.start);
            e2.weight = Vector2.Distance(e2.end, e2.start);
            e3.weight = Vector2.Distance(e3.end, e3.start);

            edges.Add(e1);
            edges.Add(e2);
            edges.Add(e3);
        }

        return edges;
    }

    private static bool PlanarPointWithinTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c) {
        Vector2 ab = b - a;
        Vector2 bc = c - b;
        Vector2 ca = a - c;

        Vector2 ap = p - a;
        Vector2 bp = p - b;
        Vector2 cp = p - c;

        Vector2 n1 = new Vector2(ab.y, -ab.x);
        Vector2 n2 = new Vector2(bc.y, -bc.x);
        Vector2 n3 = new Vector2(ca.y, -ca.x);

        float s1 = ap.x * n1.x + ap.y * n1.y;
        float s2 = bp.x * n2.x + bp.y * n2.y;
        float s3 = cp.x * n3.x + cp.y * n3.y;

        float tolerance = 0.0001f;

        return (s1 < 0 && s2 < 0 && s3 < 0) ||
               (s1 < tolerance && s2 < 0 && s3 < 0) ||
               (s2 < tolerance && s1 < 0 && s3 < 0) ||
               (s3 < tolerance && s1 < 0 && s2 < 0);
    }
}
