using System;
using Unity.Burst;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Rendering;
using Unity.Mathematics;
using UnityEngine.Networking;

public class MeshGenerator : MonoBehaviour
{
    [SerializeField] private string coordinates;
    
    [SerializeField] [Range(0, 13)] private int zoom;
    [SerializeField] [Range(16, 512, order = 32)] private int size = 512;
    [SerializeField] private int cxt;
    [SerializeField] private int cyt;
    private int worldx;
    private int worldy;
    
    private float metersPerPixel;
    private float chunkSize;

    
    async void GenerateChunk(int cx, int cy)
    {
        int tilex = worldx + cy;
        int tiley = worldy - cx;
        
        GameObject gameObject = new GameObject();
        Mesh mesh = new Mesh();

        MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();

        Material material = new Material(Shader.Find("Standard"));
        meshRenderer.material = material;

        UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(
            $"https://api.tomtom.com/map/1/tile/hill/main/{zoom}/{tilex}/{tiley}.png?key=a0hJ2q4TJoH2sqHAWihysI2HwgXjQual");

        await webRequest.SendWebRequest();

        Texture2D texture = DownloadHandlerTexture.GetContent(webRequest);

        SetMeshData(mesh, meshFilter, meshRenderer, texture, cx, cy);
    }

    [ContextMenu("Chunk")]
    void _Start()
    {
        float latitude = float.Parse(coordinates.Split(", ")[0]);
        float longitude = float.Parse(coordinates.Split(", ")[1]);

        worldx = long2tilex(longitude, zoom);
        worldy = lat2tiley(latitude, zoom);

        Debug.Log(
            $"https://api.tomtom.com/map/1/tile/hill/main/{zoom}/{worldx}/{worldy}.png?key=a0hJ2q4TJoH2sqHAWihysI2HwgXjQual)");

        metersPerPixel = 156543.0f / math.pow(2, zoom);
        chunkSize = metersPerPixel * 256.0f;
        
        GenerateChunk(cxt, cyt);
    }
    
    void SetMeshData(Mesh mesh, MeshFilter meshFilter, MeshRenderer meshRenderer, Texture2D texture, int cx, int cy)
    {
        float scale = 256.0f / (float)size;
        
        mesh.indexFormat = IndexFormat.UInt32;

        NativeArray<float3> vertices = new NativeArray<float3>((size + 1) * (size + 1), Allocator.TempJob);
        NativeArray<int> triangles = new NativeArray<int>(size * size * 6, Allocator.TempJob);
        NativeArray<Color32> colors = texture.GetRawTextureData<Color32>();
        
        GetMeshJob getMeshJob = new GetMeshJob()
        {
            vertices = vertices,
            triangles = triangles,
            size = size,
            scale = scale,
            metersPerPixel = metersPerPixel,
            colors = colors,
            cx = cx,
            cy = cy,
            chunkSize = chunkSize
        };

        JobHandle jobHandle = getMeshJob.Schedule(size * size, 1024);
        jobHandle.Complete();

        mesh.SetVertices(vertices);
        mesh.SetIndices(triangles, MeshTopology.Triangles, 0);
        
        vertices.Dispose();
        triangles.Dispose();

        meshFilter.mesh = mesh;
        mesh.RecalculateNormals();
    }
    
    int long2tilex(double lon, int z)
    {
        return (int)(Math.Floor((lon + 180.0) / 360.0 * (1 << z)));
    }

    int lat2tiley(double lat, int z)
    {
        var latRad = lat / 180 * Math.PI;
        return (int)Math.Floor((1 - Math.Log(Math.Tan(latRad) + 1 / Math.Cos(latRad)) / Math.PI) / 2 * (1 << z));
    }

    double tilex2long(int x, int z)
    {
        return x / (double)(1 << z) * 360.0 - 180;
    }

    double tiley2lat(int y, int z)
    {
        double n = Math.PI - 2.0 * Math.PI * y / (double)(1 << z);
        return 180.0 / Math.PI * Math.Atan(0.5 * (Math.Exp(n) - Math.Exp(-n)));
    }
}

[BurstCompile]
public struct GetMeshJob : IJobParallelFor
{   
    [NativeDisableParallelForRestriction] public NativeArray<float3> vertices;
    [NativeDisableParallelForRestriction] public NativeArray<int> triangles;
    [ReadOnly] [NativeDisableParallelForRestriction] public NativeArray<Color32> colors;

    public float scale;
    public float metersPerPixel;
    public float chunkSize;
    
    public int size;
    public int cx;
    public int cy;
    
    public void Execute(int threadIndex)
    {
        int x = threadIndex % size;
        int y = threadIndex / size;
        
        int vert = threadIndex + Mathf.FloorToInt(threadIndex / size);
        int tri = threadIndex;

        int scale_2 = 512 / size;

        Color32 color = colors[((int)(y * scale_2) + 1) + 514 * ((int)(x * scale_2) + 1)];
        float height = -10000 + (color.g * 256 * 256 + color.b * 256 + color.a) * 0.1f;

        vertices[vert] = new float3((x * scale * metersPerPixel) + (cx * chunkSize), 200 + Mathf.PerlinNoise(x * 0.1f, y * 0.1f), (y * scale * metersPerPixel)) + (cy * chunkSize);
     
        triangles[tri * 6 + 0] = vert;
        triangles[tri * 6 + 1] = vert + size;
        triangles[tri * 6 + 2] = vert + size + 1;
        triangles[tri * 6 + 3] = vert + size + 1;
        triangles[tri * 6 + 4] = vert + 1;
        triangles[tri * 6 + 5] = vert;
    }
}