using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CRTM : MonoBehaviour
{
    //World Construction
    public Texture SkyboxTexture;
    public int Seed = 1223832719;
    public float PlacementRadius = 100.0f;

    public uint CubesMax = 10;
    public Vector3 MinCubeWidth = new Vector3(1, 1, 1);
    public Vector3 MaxCubeWidth = new Vector3(10, 10, 10);

    public uint SpheresMax = 10;
    public Vector2 SphereSize;

    //GPU Interaction
    public ComputeShader RayTracingShader;
    private ComputeBuffer sphereBuffer;
    private ComputeBuffer cubeBuffer;

    //Display Results
    private RenderTexture target;

    private Camera Cam;
    private void Awake()
    {
        Cam = GetComponent<Camera>();
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 30;
    }

    // Update is called once per frame
    void Update()
    {

    }

    //Reset Scene on enable/disable
    private void OnEnable()
    {
        SetUpScene();
    }
    private void OnDisable()
    {
        if (sphereBuffer != null)
            sphereBuffer.Release();
        if (cubeBuffer != null)
            cubeBuffer.Release();
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        SetShaderParameters();
        Render(destination);
    }

    private void Render(RenderTexture destination)
    {
        // Make sure we have a current render target
        InitRenderTexture();
        // Set the target and dispatch the compute shader
        RayTracingShader.SetTexture(0, "Result", target);
        int threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);
        RayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);


        // Blit the result texture to the screen
        Graphics.Blit(target, destination);
    }

    //Useful Struct
    struct Sphere
    {
        public Vector3 position;
        public float radius;
        public Vector3 albedo;
        public float Smoothness;
    };
    struct Cube
    {
        public Vector3 min;
        public Vector3 max;
        public Vector3 albedo;
        public float Smoothness;
    }

    private void SetUpScene()
    {
        //Creates cubes and spheres moves them to the gpu
        Random.InitState(Seed);

        List<Sphere> spheres = new List<Sphere>();
        List<Cube> cubes = new List<Cube>();

        Sphere sphere = new Sphere();
        Cube cube = new Cube();

        cube.min = new Vector3(5.5f, 0.0f, 0.0f);
        cube.max = new Vector3(10.0f, 5.5f, 5.0f);
        cube.albedo = new Vector3(0.8f, 0.4f, 1.0f);
        cube.Smoothness = 0.0f;

        sphere.position = new Vector3(0, 5.0f, 0);
        sphere.radius = 5.0f;
        sphere.albedo = new Vector3(0.4f, 0.4f, 0.8f);
        sphere.Smoothness = 0.5f;

        spheres.Add(sphere);
        cubes.Add(cube);

        bool failed;

        //Check Intersections and create Spheres and cubes
        for (int i = 0; i < SpheresMax; i++)
        {
            sphere = GenRandomSphere();
            failed = false;
            foreach (Sphere S in spheres)
            {
                if (doesSphereIntersectSphere(S, sphere)) { failed = true; }
            }
            foreach (Cube C in cubes)
            {
                if (doesCubeIntersectSphere(C, sphere)) { failed = true; }
            }

            if (!false) { spheres.Add(sphere); }
        }
        for (int i = 0; i < CubesMax; i++)
        {
            cube = GenRandomCube();
            failed = false;
            foreach (Sphere S in spheres)
            {
                if (doesCubeIntersectSphere(cube, S)) { failed = true; }
            }
            foreach (Cube C in cubes)
            {
                if (doesCubeIntersectCube(C, cube)) { failed = true; }
            }

            if (!failed) { cubes.Add(cube); }
        }


        // Assign to compute buffer
        cubeBuffer = new ComputeBuffer(cubes.Count, 40);
        cubeBuffer.SetData(cubes);

        sphereBuffer = new ComputeBuffer(spheres.Count, 32);
        sphereBuffer.SetData(spheres);

    }

    private void SetShaderParameters()
    {
        RayTracingShader.SetMatrix("CameraToWorld", Cam.cameraToWorldMatrix);
        RayTracingShader.SetMatrix("CameraInverseProjection", Cam.projectionMatrix.inverse);
        RayTracingShader.SetTexture(0, "SkyboxTexture", SkyboxTexture);
        RayTracingShader.SetVector("PixelOffset", new Vector2(0.5f, 0.5f));

        RayTracingShader.SetBuffer(0, "Cubes", cubeBuffer);
        RayTracingShader.SetBuffer(0, "Spheres", sphereBuffer);
        RayTracingShader.SetFloat("Seed", Random.value);
    }

    private void InitRenderTexture()
    {
        if (target == null || target.width != Screen.width || target.height != Screen.height) // Always resets
        {
            // Release render texture if we already have one
            if (target != null) { target.Release(); }

            // Get a render target for Ray Tracing
            target = new RenderTexture(Screen.width, Screen.height, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            target.enableRandomWrite = true;
            target.Create();
        }
    }

    private Cube GenRandomCube()
    {
        Cube Final = new Cube();

        //Placement Location
        Vector2 randomPos = Random.insideUnitCircle * PlacementRadius;
        Final.min = new Vector3(randomPos.x, 0, randomPos.y);
        Final.max = Final.min + new Vector3(Random.Range(MinCubeWidth.x, MaxCubeWidth.x), Random.Range(MinCubeWidth.y, MaxCubeWidth.y), Random.Range(MinCubeWidth.z, MaxCubeWidth.z));

        //Visual Properties
        Color color = Random.ColorHSV();
        Final.albedo = new Vector3(color.r, color.g, color.b);
        Final.Smoothness = Random.value;

        return Final;
    }
    private Sphere GenRandomSphere()
    {
        Sphere Final = new Sphere();

        // Position and radius
        Final.radius = SphereSize.x + Random.value * (SphereSize.y - SphereSize.x);
        Vector2 randomPos = Random.insideUnitCircle * PlacementRadius;
        
        Final.position = new Vector3(randomPos.x, Final.radius, randomPos.y);

        //Visual Properties
        Color color = Random.ColorHSV();
        Final.albedo = new Vector3(color.r, color.g, color.b);
        Final.Smoothness = Random.value;

        return Final;
    }

    private float squared(float v) { return v * v; }
    bool doesCubeIntersectSphere(Cube cube, Sphere sphere)
    {
        float dist_squared = sphere.radius * sphere.radius;
        /* assume C1 and C2 are element-wise sorted, if not, do that now */
        if (sphere.position.x < cube.min.x) dist_squared -= squared(sphere.position.x - cube.min.x);
        else if (sphere.position.x > cube.max.x) dist_squared -= squared(sphere.position.x - cube.max.x);
        if (sphere.position.y < cube.min.y) dist_squared -= squared(sphere.position.y - cube.min.y);
        else if (sphere.position.y > cube.max.y) dist_squared -= squared(sphere.position.y - cube.max.y);
        if (sphere.position.z < cube.min.z) dist_squared -= squared(sphere.position.z - cube.min.z);
        else if (sphere.position.z > cube.max.z) dist_squared -= squared(sphere.position.z - cube.max.z);
        return dist_squared > 0;
    }
    bool doesSphereIntersectSphere(Sphere S1, Sphere S2)
    {
        float minDist = S1.radius + S2.radius;
        return Vector3.SqrMagnitude(S1.position - S2.position) < minDist * minDist;
    }
    bool doesCubeIntersectCube(Cube C1, Cube C2)
    {
        bool[] Checks = new bool[6];

        for (int i = 0; i < 3; i++)
        {
            Checks[i * 2] = C1.max[i] > C2.min[i];
            Checks[(i * 2) + 1] = C1.min[i] < C2.max[i];
        }
        return (Checks[0] && Checks[1] && Checks[2] && Checks[3] && Checks[4] && Checks[5]);
    }
}
