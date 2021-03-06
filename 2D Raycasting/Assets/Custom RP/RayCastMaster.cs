﻿using UnityEngine;
using System.Collections.Generic;
using System.Linq;
public class RayCastMaster
{
    private ComputeShader m_RayTracingShader;
    private CamerInfoComponent m_camInfo;
    private Texture m_SkyBox;
    private RenderTexture m_target;
    private Camera m_cam;
    private Color m_BackgroundColor;

    private float m_seed = 0;
    private bool m_RebuildCompletely = false;
    private uint m_currentSample = 0;
    private LightContainerComponent m_dirLight;

    public RenderTexture ConvergedRT;

    private List<RayTracingObject> m_rtObjects = new List<RayTracingObject>();
    //Variables For Compute shader data
    private static List<RayTracingMesh> m_RayTracingObjects = new List<RayTracingMesh>();
    private static List<RayTracingSphere> m_RayTracingSpheres = new List<RayTracingSphere>();


    private List<SpriteRT> m_spriteRts = new List<SpriteRT>();
    private static List<Sphere> m_spheres = new List<Sphere>();
    private static List<MeshObject> m_MeshObjects = new List<MeshObject>();
    private static List<Vector3> m_vertices = new List<Vector3>();
    private static List<int> m_indices = new List<int>();
    private static List<RayTracingSprite> m_Sprites = new List<RayTracingSprite>();
    private ComputeBuffer m_meshBuffer;
    private ComputeBuffer m_vertexBuffer;
    private ComputeBuffer m_IndexBuffer;
    private ComputeBuffer m_SphereBuffer;
    private ComputeBuffer m_SpriteBuffer;
    private ComputeBuffer m_rtObjectBuffer;

    private static bool MeshWasRemoved = false;
    private static bool SphereWasRemoved = false;
    private static bool SpriteWasRemoved = false;
    private Texture2DArray m_tex2DArray512 = new Texture2DArray(512, 512, 4, TextureFormat.RGBA32, false);
    private Texture2DArray m_tex2DArray256 = new Texture2DArray(256, 256, 4, TextureFormat.RGBA32, false);
    private Texture2DArray m_tex2DArray128 = new Texture2DArray(128, 128, 4, TextureFormat.RGBA32, false);
    private Texture2DArray m_tex2DArray512_Normal = new Texture2DArray(512, 512, 4, TextureFormat.RGBA32, false);
    private Texture2DArray m_tex2DArray256_Normal = new Texture2DArray(256, 256, 4, TextureFormat.RGBA32, false);
    private Texture2DArray m_tex2DArray128_Normal = new Texture2DArray(128, 128, 4, TextureFormat.RGBA32, false);
    private List<Texture2D> m_tex512List = new List<Texture2D>();
    private List<Texture2D> m_tex256List = new List<Texture2D>();
    private List<Texture2D> m_tex128List = new List<Texture2D>();
    public void OnEnable()
    {
        MeshWasRemoved = true;
        SphereWasRemoved = true;
        Rebuild();
    }
    public void Init(ComputeShader newCS, Camera cam, Texture skybox, Color c, float seed, CamerInfoComponent newInfo)
    {
        Debug.Log("init Ray tracer!");
        m_camInfo = newInfo;
        m_seed = seed;
        GetLighting();

        // m_useAA = useAA;
        m_BackgroundColor = c;
        m_SkyBox = skybox;
        m_RayTracingShader = newCS;
        m_cam = cam;

        //make sure an initial rebuild happens
        m_tex2DArray512 = new Texture2DArray(512, 512, 4, TextureFormat.RGBA32, false);
        m_tex2DArray256 = new Texture2DArray(256, 256, 4, TextureFormat.RGBA32, false);
        m_tex2DArray128 = new Texture2DArray(128, 128, 4, TextureFormat.RGBA32, false);
        m_tex2DArray512_Normal = new Texture2DArray(512, 512, 4, TextureFormat.RGBA32, false);
        m_tex2DArray256_Normal = new Texture2DArray(256, 256, 4, TextureFormat.RGBA32, false);
        m_tex2DArray128_Normal = new Texture2DArray(128, 128, 4, TextureFormat.RGBA32, false);
        m_tex512List.Clear();
        m_tex256List.Clear();
        m_tex128List.Clear();
        //m_tex2DArray256.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
        MeshWasRemoved = true;
        SphereWasRemoved = true;
        Rebuild();
    }

    public static void SubscribeTexture(RayTracingSprite sprite)
    {
        m_Sprites.Add(sprite);
    }
    public static void UnSubscribeTexture(RayTracingSprite sprite)
    {
        m_Sprites.Remove(sprite);
        SpriteWasRemoved = true;
    }
    public static void SubscribeSphere(RayTracingSphere sphere)
    {
        m_RayTracingSpheres.Add(sphere);
    }
    public static void UnSubscribeSphere(RayTracingSphere sphere)
    {
        m_RayTracingSpheres.Remove(sphere);
        SphereWasRemoved = true;
    }
    public static void SubscribeMesh(RayTracingMesh mesh)
    {
        m_RayTracingObjects.Add(mesh);
    }
    public static void UnsubscribeMesh(RayTracingMesh mesh)
    {
        m_RayTracingObjects.Remove(mesh);
        MeshWasRemoved = true;
    }
    private void Rebuild()
    {
        //cancel if not running
        if (!Application.isPlaying) return;
        //Debug.Log("Rebuilding");
        if (m_camInfo == null) Camera.main.GetComponent<CamerInfoComponent>();
        //rebuild if a mesh was removed
        if (MeshWasRemoved) RebuildMeshes();
        //else check if a mesh has moved
        else
        {
            bool rebuild = false;
            foreach (RayTracingMesh rtMesh in m_RayTracingObjects)
            {
                if (rtMesh.NeedsRebuilding == true)
                {
                    rebuild = true;
                    rtMesh.NeedsRebuilding = false;
                }
            }
            //rebuild meshes
            if (rebuild) RebuildMeshes();
        }
        //rebuild spheres if a sphere has been removed
        if (SphereWasRemoved) RebuildSpheres();
        //else check if a sphere has been moved
        else
        {
            bool rebuild = false;
            foreach (RayTracingSphere rtSphere in m_RayTracingSpheres)
            {
                if (rtSphere.NeedsRebuilding == true)
                {
                    rebuild = true;
                    rtSphere.NeedsRebuilding = false;
                }
            }
            if (rebuild) RebuildSpheres();
        }
        if (SpriteWasRemoved) RebuildSprites();
        else
        {
            bool rebuild = false;
            foreach (RayTracingSprite rtSprite in m_Sprites)
            {
                if (rtSprite.Rebuild == true)
                {
                    rebuild = true;
                    rtSprite.Rebuild = false;
                }
            }
            if (rebuild) RebuildSprites();
        }
    }

   
    private void SetShaderParams()
    {
        m_RayTracingShader.SetMatrix("_CameraToWorld", m_cam.cameraToWorldMatrix);
        m_RayTracingShader.SetMatrix("_CameraInverseProjection", m_cam.projectionMatrix.inverse);
        m_RayTracingShader.SetTexture(0, "_SkyBoxTexture", m_SkyBox);
        m_RayTracingShader.SetVector("_PixelOffset", new Vector2(Random.value, Random.value));

        //        m_RayTracingShader.SetVector("_DirLight", m_dirLight.Light);
        m_RayTracingShader.SetVector("_SkyColor", new Vector3(m_BackgroundColor.r, m_BackgroundColor.g, m_BackgroundColor.b));
        m_RayTracingShader.SetFloat("_Seed", Random.value);

        SetComputeBuffer("_Spheres", m_SphereBuffer);
        //     SetComputeBuffer("_MeshObjects", m_meshBuffer);
        //     SetComputeBuffer("_Vertices", m_vertexBuffer);
        //   SetComputeBuffer("_Indices", m_IndexBuffer);
        SetComputeBuffer("_Sprites", m_SpriteBuffer);
    }

    private void GetLighting()
    {
        //find object tagged as light
        GameObject obj = GameObject.FindGameObjectWithTag("DirectionalLight");
        //return if none was found
        if (obj == null) return;
        //grab info container
        m_dirLight = obj.GetComponent<LightContainerComponent>();
        //add contaienr if container was null
        if (m_dirLight == null)
        {
            m_dirLight = obj.AddComponent<LightContainerComponent>();
        }
        //init info container
        m_dirLight.Init();
    }
    public RenderTexture Render()
    {
        Rebuild();
        InitTexture();
        SetShaderParams();

        m_RayTracingShader.SetTexture(0, "Result", m_target);
        m_RayTracingShader.SetTexture(0, "_SpriteTextures_512_512", m_tex2DArray512, 0);
        m_RayTracingShader.SetTexture(0, "_SpriteTextures_256_256", m_tex2DArray256, 0);
        m_RayTracingShader.SetTexture(0, "_SpriteTextures_128_128", m_tex2DArray128, 0);

        m_RayTracingShader.SetTexture(0, "_SNormalTextures_512_512", m_tex2DArray512_Normal, 0);
        m_RayTracingShader.SetTexture(0, "_SNormalTextures_256_256", m_tex2DArray256_Normal, 0);
        m_RayTracingShader.SetTexture(0, "_SNormalTextures_128_128", m_tex2DArray128_Normal, 0);


        int threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);
        m_RayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);
        return m_target;
    }
    private void InitTexture()
    {
        //create Render texture if its null or does not match screen dimensions
        if (m_target == null || m_target.width != Screen.width || m_target.height != Screen.height)
        {
            if (m_target != null)
                m_target.Release();

            m_target = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);

            m_target.enableRandomWrite = true;
            m_target.Create();
        }


        if (ConvergedRT == null || ConvergedRT.width != Screen.width || ConvergedRT.height != Screen.height)
        {
            if (ConvergedRT != null)
                ConvergedRT.Release();

            ConvergedRT = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);

            ConvergedRT.enableRandomWrite = true;
            ConvergedRT.Create();
        }
    }
    private void RebuildSprites()
    {

        SpriteWasRemoved = false;
        m_camInfo.transform.hasChanged = true;

        m_spriteRts = new List<SpriteRT>();
        foreach (RayTracingSprite sprite in m_Sprites)
        {
            if (sprite == null) continue;
            RayTracingSprite.TextureMode texMode = sprite.TexSize;
            Texture2D tex = sprite.GetTexture();
            Texture2D normal = sprite.GetNormal();
            int index = 0;
            switch (texMode)
            {
                case RayTracingSprite.TextureMode.TEX64: break;
                case RayTracingSprite.TextureMode.TEX128:
                    if (m_tex128List.Contains(tex))
                    {
                        //  Debug.Log("countains!");
                        index = m_tex128List.IndexOf(tex);
                    }
                    else
                    {
                        index = m_tex128List.Count;
                        m_tex128List.Add(tex);
                        m_tex2DArray128.SetPixels32(tex.GetPixels32(), index, 0);
                        m_tex2DArray128_Normal.SetPixels32(normal.GetPixels32(), index, 0);

                    }
                    break;
                case RayTracingSprite.TextureMode.TEX256:
                    if (m_tex256List.Contains(tex))
                    {
                        //   Debug.Log("countains!");
                        index = m_tex256List.IndexOf(tex);
                    }
                    else
                    {
                        index = m_tex256List.Count;
                        m_tex256List.Add(tex);
                        m_tex2DArray256.SetPixels32(tex.GetPixels32(), index, 0);
                        m_tex2DArray256_Normal.SetPixels32(tex.GetPixels32(), index, 0);

                    }
                    break;
                case RayTracingSprite.TextureMode.TEX512:
                    if (m_tex512List.Contains(tex))
                    {
                        //   Debug.Log("countains!");
                        index = m_tex512List.IndexOf(tex);
                    }
                    else
                    {
                        index = m_tex512List.Count;
                        m_tex512List.Add(tex);
                        m_tex2DArray512.SetPixels32(tex.GetPixels32(), index, 0);
                        m_tex2DArray512_Normal.SetPixels32(tex.GetPixels32(), index, 0);
                    }
                    break;
            }
            SpriteRT newSprite = sprite.GenSprite();
            newSprite.TextureIndex = index;
            m_spriteRts.Add(newSprite);
        }
         CreateComputeBuffer(ref m_SpriteBuffer, m_spriteRts, 36);
        m_tex2DArray512.Apply();
        m_tex2DArray256.Apply();
        m_tex2DArray128.Apply();
        m_RebuildCompletely = true;
    }
    private void RebuildSpheres()
    {
        Debug.Log("Rebuilding spheres");

        SphereWasRemoved = false;
        m_camInfo.transform.hasChanged = true;
        m_spheres.Clear();
        foreach (RayTracingSphere rtSphere in m_RayTracingSpheres)
        {
            if (rtSphere != null)
                m_spheres.Add(GenerateSphere.Generate(rtSphere.gameObject));
        }
          CreateComputeBuffer(ref m_SphereBuffer, m_spheres, 56);
        m_RebuildCompletely = true;
    }
    private void RebuildMeshes()
    {
        Debug.Log("Rebuilding meshes");

        m_camInfo.transform.hasChanged = true;
        MeshWasRemoved = false;

        m_MeshObjects.Clear();
        m_vertices.Clear();
        m_indices.Clear();
        foreach (RayTracingMesh rtObj in m_RayTracingObjects)
        {
            Mesh currentMesh = rtObj.GetComponent<MeshFilter>().sharedMesh;

            int firstVertex = m_vertices.Count;
            m_vertices.AddRange(currentMesh.vertices);

            int firstIndex = m_indices.Count;
            var indices = currentMesh.GetIndices(0);
            m_indices.AddRange(indices.Select(index => index + firstVertex));

            m_MeshObjects.Add(new MeshObject()
            {
                localToWorldMat = rtObj.transform.localToWorldMatrix,
                indices_offset = firstIndex,
                indices_count = indices.Length
            });
        }

        CreateComputeBuffer(ref m_meshBuffer, m_MeshObjects, 72);
        CreateComputeBuffer(ref m_IndexBuffer, m_indices, 4);
        CreateComputeBuffer(ref m_vertexBuffer, m_vertices, 12);

    }
    private static void CreateComputeBuffer<T>(ref ComputeBuffer buffer, List<T> data, int stride) where T : struct
    {
        if (buffer != null)
        {
            if (data.Count == 0 || buffer.count != data.Count || buffer.stride != stride)
            {
                buffer.Release();
                buffer = null;
            }
        }
        if (data.Count != 0)
        {
            if (buffer == null)
            {
                Debug.Log("buffer created!");
                buffer = new ComputeBuffer(data.Count, stride);
            }
            buffer.SetData(data);
        }
        else Debug.Log("no data");
    }

    private void SetComputeBuffer(string name, ComputeBuffer buffer)
    {
        if (buffer != null)
        {
            m_RayTracingShader.SetBuffer(0, name, buffer);
        }
    }
}
