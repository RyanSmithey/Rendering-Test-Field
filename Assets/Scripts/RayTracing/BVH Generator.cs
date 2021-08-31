using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;

public static class BVHGenerator
{

    //Get All verticies in single list
    //Get All triangles in single list
    //
    //
    //foreach triangle generate a "distance" to the next triangle
    //Take the smallest "distance"
    //Add it to the list of connections and remove the triangles from the potential list
    //asdf

    static List<Vector3> Vertices;
    static List<int> Triangles;

    static List<Node> AllNodes;
    static List<Delegate> FindSA;


    struct Box
    {
        public Vector3 MinV;
        public Vector3 MaxV;
    }
    struct Node
    {
        Box Bounds;
        int I1;     //Intersection 1 Index
        int I2;     //Intersection 2 Index
        int Type;   //NodeType
    }


    static void GenerateBVH()
    {
        List<Node> TopNodes = new List<Node>();
        List<int> TempTriangles = Triangles;

        float MinDistance = 0;

        while (TempTriangles.Count > 0 && TopNodes.Count <= 1)
        {
            int TCount = TempTriangles.Count;
            int TN = TopNodes.Count;

            //Find the smallest SA of bounding box

            for (int i = 0; i < TCount; i += 3)
            {
                for (int j = 0; j < TCount; j += 3)
                {

                }
                for (int j = 0; j < TN; j++)
                {

                }
            }
            for (int i = 0; i < TN; i++)
            {
                for (int j = 0; j < TCount; j += 3)
                {

                }
                for (int j = 0; j < TN; j++)
                {

                }
            }

            //Remove appropriate node/triangles
        }
    }


    static Box BoundingBoxBB(Box B1, Box B2)
    {
        Vector3 MinV = Vector3.zero;
        Vector3 MaxV = Vector3.zero;

        MinV.x = Mathf.Min(B1.MinV.x, B2.MinV.x);
        MinV.y = Mathf.Min(B1.MinV.y, B2.MinV.y);
        MinV.z = Mathf.Min(B1.MinV.z, B2.MinV.z);


        MaxV.x = Mathf.Max(B1.MaxV.x, B2.MaxV.x);
        MaxV.y = Mathf.Max(B1.MaxV.y, B2.MaxV.y);
        MaxV.z = Mathf.Max(B1.MaxV.z, B2.MaxV.z);

        Box Final = new Box();
        Final.MinV = MinV;
        Final.MaxV = MaxV;

        return Final;
    }

    static Box BoundingBoxBT(Box B, Vector3[] T)
    {
        return BoundingBoxBB(B, BoundingBoxT(T));
    }

    static Box BoundingBoxTT(Vector3[] T1, Vector3[] T2)
    {
        return BoundingBoxBB(BoundingBoxT(T1), BoundingBoxT(T2));
    }

    static Box BoundingBoxT(Vector3[] T)
    {
        Vector3 MinV = T[0];
        Vector3 MaxV = T[0];

        for (int i = 1; i < 3; i++)
        {
            MinV.x = Mathf.Min(MinV.x, T[i].x);
            MinV.y = Mathf.Min(MinV.y, T[i].y);
            MinV.z = Mathf.Min(MinV.z, T[i].z);

            MaxV.x = Mathf.Max(MaxV.x, T[i].x);
            MaxV.y = Mathf.Max(MaxV.y, T[i].y);
            MaxV.z = Mathf.Max(MaxV.z, T[i].z);
        }

        Box Final = new Box();
        Final.MinV = MinV;
        Final.MaxV = MaxV;

        return Final;
    }

    static float BoxSurfaceArea(Box Input)
    {
        float S1 = Input.MaxV.x - Input.MinV.x;
        float S2 = Input.MaxV.y - Input.MinV.y;
        float S3 = Input.MaxV.z - Input.MinV.z;

        return (S1 * S2 + S1 * S3 + S2 * S3) * 2;
    }
}
