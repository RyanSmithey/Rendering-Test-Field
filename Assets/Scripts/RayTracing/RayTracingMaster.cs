using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class RayTracingMaster : MonoBehaviour
{
    //The Relevant compute shader
    public ComputeShader RayTracingShader;
    public Texture SkyboxTexture;
    public Light DirectionalLight;

    public uint CubesMax = 10;
    public Vector3 CubeWidth = new Vector3(10, 10, 10);


    public Vector2 SphereRadius = new Vector2(5, 30);
    public uint SpheresMax = 10000;
    public float SpherePlacementRadius = 100.0f;

    public int SphereSeed = 1223832719;

    private ComputeBuffer _sphereBuffer;
    private ComputeBuffer _cubeBuffer;

    //RenderTexture? Something Overlayed On top of the screen?
    private RenderTexture _target;
    //private RenderTexture _
    private RenderTexture _converged;

    //Camera of this object
    private Camera _camera;

    private uint _currentSample = 0;
    public Material _addMaterial;

    //Importing Mesh Objects
    private static bool _meshObjectsNeedRebuilding = false;
    private static List<RayTracingObject> _rayTracingObjects = new List<RayTracingObject>();

    private static List<MeshObject> _meshObjects = new List<MeshObject>();
    private static List<Vector3> _vertices = new List<Vector3>();
    private static List<int> _indices = new List<int>();
    private ComputeBuffer _meshObjectBuffer;
    private ComputeBuffer _vertexBuffer;
    private ComputeBuffer _indexBuffer;

    //Useful Struct
    struct Sphere
    {
        public Vector3 position;
        public float radius;
        public Vector3 albedo;
        public Vector3 specular;
        public float smoothness;
        public Vector3 emission;
    };

    struct Cube
    {
        public Vector3 min;
        public Vector3 max;
        public Vector3 albedo;
        public Vector3 specular;
        public float smoothness;
        public Vector3 emission;
    }

    struct MeshObject
    {
        public Matrix4x4 localToWorldMatrix;
        public int indices_offset;
        public int indices_count;
    }

    //Set Camera
    private void Awake()
    {
        _camera = GetComponent<Camera>();
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 30;
    }

    private void Update()
    {
        //Check to see if transform has changed if true do relevant stuff
        if (transform.hasChanged)
        {
            _currentSample = 0;
            transform.hasChanged = false;
        }
        //Check to see if Light direction has changed
        if (DirectionalLight.gameObject.transform.hasChanged)
        {
            SetShaderParameters();
            _currentSample = 0;
            DirectionalLight.gameObject.transform.hasChanged = false;
        }
    }

    //Reset Scene on enable/disable
    private void OnEnable()
    {
        _currentSample = 0;
        SetUpScene();
    }
    private void OnDisable()
    {
        if (_sphereBuffer != null)      _sphereBuffer.Release();
        if (_cubeBuffer != null)        _cubeBuffer.Release();
        if (_meshObjectBuffer != null)  _meshObjectBuffer.Release();
        if (_vertexBuffer != null)      _vertexBuffer.Release();
        if (_indexBuffer != null)       _indexBuffer.Release();
    }

    //Unity Call after rendering an image
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        RebuildMeshObjectBuffers();
        SetShaderParameters();
        Render(destination);
    }

    //Conversion matrix
    private void SetShaderParameters()
    {
        RayTracingShader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        RayTracingShader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);
        RayTracingShader.SetTexture(0, "_SkyboxTexture", SkyboxTexture);
        RayTracingShader.SetVector("_PixelOffset", new Vector2(Random.value, Random.value));

        Vector3 l = DirectionalLight.transform.forward;
        RayTracingShader.SetVector("_DirectionalLight", new Vector4(l.x, l.y, l.z, DirectionalLight.intensity));

        RayTracingShader.SetBuffer(0, "_Cubes", _cubeBuffer);
        RayTracingShader.SetBuffer(0, "_Spheres", _sphereBuffer);
        RayTracingShader.SetFloat("_Seed", Random.value);

        SetComputeBuffer("_MeshObjects", _meshObjectBuffer);
        SetComputeBuffer("_Vertices", _vertexBuffer);
        SetComputeBuffer("_Indices", _indexBuffer);
    }

    //Dispatch Compute Shader
    private void Render(RenderTexture destination)
    {
        // Make sure we have a current render target
        InitRenderTexture();
        // Set the target and dispatch the compute shader
        RayTracingShader.SetTexture(0, "Result", _target);
        int threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);
        RayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);
        
        
        // Blit the result texture to the screen
        if (_addMaterial == null)
        {
            _addMaterial = new Material(Shader.Find("Hidden/AddShader"));
        }

        _addMaterial.SetFloat("_Sample", _currentSample);

        //Accumulate samples and display results
        //Graphics.Blit(_target, destination);
        Graphics.Blit(_target, _converged, _addMaterial);
        Graphics.Blit(_converged, destination);
        
        //Increase number of samples over time to have TAA
        _currentSample++;
    }

    //Standard Unity Stuff
    private void InitRenderTexture()
    {
        if (_target == null || _target.width != Screen.width || _target.height != Screen.height) // Always resets
        {
            //Set Alaising samples to 0
            _currentSample = 0;

            // Release render texture if we already have one
            if (_target != null) { _target.Release(); }
            if (_converged != null) { _converged.Release(); }
            
            // Get a render target for Ray Tracing
            _target = new RenderTexture(Screen.width, Screen.height, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _target.enableRandomWrite = true;
            _target.Create();

            _converged = new RenderTexture(Screen.width, Screen.height, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _converged.enableRandomWrite = true;
            _converged.Create();
        }
    }
    
    private void SetUpScene()
    {
        Random.InitState(SphereSeed);

        List<Sphere> spheres = new List<Sphere>();
        List<Cube> cubes = new List<Cube>();

        Sphere sphere = new Sphere();
        Cube cube = new Cube();


        // Add a number of random spheres
        for (int i = 0; i < SpheresMax; i++)
        {
            sphere = new Sphere();
            
            // Radius and radius
            sphere.radius = SphereRadius.x + Random.value * (SphereRadius.y - SphereRadius.x);
            Vector2 randomPos = Random.insideUnitCircle * SpherePlacementRadius;
            sphere.position = new Vector3(randomPos.x, sphere.radius, randomPos.y);
            
            // Reject spheres that are intersecting others
            foreach (Sphere other in spheres)
            {
                float minDist = sphere.radius + other.radius;
                if (Vector3.SqrMagnitude(sphere.position - other.position) < minDist * minDist)
                    goto SkipSphere;
            }
            
            // Albedo and specular color
            Color color = Random.ColorHSV();
            bool metal = Random.value < 0.5f;
            sphere.albedo = metal ? Vector3.zero : new Vector3(color.r, color.g, color.b);
            sphere.specular = metal ? new Vector3(color.r, color.g, color.b) : Vector3.one * 0.04f;
            
            //Emission and Smoothness
            sphere.smoothness = metal ? 0 : (Random.Range(0.5f, 1.0f));
            sphere.emission = (Random.Range(0.0f, 1.0f) < 1.4f) ?  sphere.albedo : Vector3.zero;//new Vector3(0.7f, 0.7f, 0.7f)

            // Add the sphere to the list
            spheres.Add(sphere);
        SkipSphere:
            continue;
        }


        cube = new Cube();

        cube.min = new Vector3(-10, 0, -10);
        cube.max = Vector3.one * 10;

        cube.albedo = Vector3.one * 0.04f;
        cube.specular = Vector3.one * 1.0f;

        cube.smoothness = 2.0f;

        cube.emission = new Vector3(0.0f, 0.0f, 0.0f);

        cubes.Add(cube);

        //Create 10 Cubes
        for (int i = 0; i < CubesMax; i++)
        {
            cube = new Cube();

            // Radius and radius
            Vector2 randomPos = Random.insideUnitCircle * SpherePlacementRadius;
            Vector3 pos = new Vector3(randomPos.x, 0, randomPos.y);

            cube.min = new Vector3(pos.x, 0, pos.z);
            cube.max = new Vector3(pos.x + CubeWidth.x, CubeWidth.y, pos.z + CubeWidth.z);
            
            // Albedo and specular color
            Color color = Random.ColorHSV();
            bool metal = Random.value < 1.5f;
            cube.albedo = metal ? Vector3.zero : new Vector3(color.r, color.g, color.b);
            cube.specular = metal ? new Vector3(color.r, color.g, color.b) : Vector3.one * 0.04f;

            //Emission and Smoothness
            cube.smoothness = metal ? 0 : (Random.Range(0.7f, 1.0f));
            cube.emission = (Random.Range(0.0f, 1.0f) < 1.4f) ? cube.albedo : Vector3.zero;//new Vector3(0.7f, 0.7f, 0.7f)

            // Add the sphere to the list
            cubes.Add(cube);
        }


        // Assign to compute buffer
        _cubeBuffer = new ComputeBuffer(cubes.Count, 64);
        _cubeBuffer.SetData(cubes);

        //Debug.Log(spheres.Count);

        _sphereBuffer = new ComputeBuffer(spheres.Count, 56);
        _sphereBuffer.SetData(spheres);
    }

    //This function set imports objects that need to be ray traced

    private void RebuildMeshObjectBuffers()
    {
        if (!_meshObjectsNeedRebuilding)
        {
            return;
        }
        _meshObjectsNeedRebuilding = false;
        _currentSample = 0;
        // Clear all lists
        _meshObjects.Clear();
        _vertices.Clear();
        _indices.Clear();

        // Loop over all objects and gather their data
        foreach (RayTracingObject obj in _rayTracingObjects)
        {
            Mesh mesh = obj.GetComponent<MeshFilter>().sharedMesh;
            // Add vertex data
            int firstVertex = _vertices.Count;
            _vertices.AddRange(mesh.vertices);
            // Add index data - if the vertex buffer wasn't empty before, the
            // indices need to be offset
            int firstIndex = _indices.Count;
            var indices = mesh.GetIndices(0);
            _indices.AddRange(indices.Select(index => index + firstVertex));
            // Add the object itself
            _meshObjects.Add(new MeshObject()
            {
                localToWorldMatrix = obj.transform.localToWorldMatrix,
                indices_offset = firstIndex,
                indices_count = indices.Length
            });
        }
        CreateComputeBuffer(ref _meshObjectBuffer, _meshObjects, 72);
        CreateComputeBuffer(ref _vertexBuffer, _vertices, 12);
        CreateComputeBuffer(ref _indexBuffer, _indices, 4);
    }

    private static void CreateComputeBuffer<T>(ref ComputeBuffer buffer, List<T> data, int stride)
    where T : struct
    {
        // Do we already have a compute buffer?
        if (buffer != null)
        {
            // If no data or buffer doesn't match the given criteria, release it
            if (data.Count == 0 || buffer.count != data.Count || buffer.stride != stride)
            {
                buffer.Release();
                buffer = null;
            }
        }
        if (data.Count != 0)
        {
            // If the buffer has been released or wasn't there to
            // begin with, create it
            if (buffer == null)
            {
                buffer = new ComputeBuffer(data.Count, stride);
            }
            // Set data on the buffer
            buffer.SetData(data);
        }
    }
    private void SetComputeBuffer(string name, ComputeBuffer buffer)
    {
        if (buffer != null)
        {
            RayTracingShader.SetBuffer(0, name, buffer);
        }
    }

    public static void RegisterObject(RayTracingObject obj)
    {
        _rayTracingObjects.Add(obj);
        _meshObjectsNeedRebuilding = true;
    }
    public static void UnregisterObject(RayTracingObject obj)
    {
        _rayTracingObjects.Remove(obj);
        _meshObjectsNeedRebuilding = true;
    }
}
