﻿namespace UnityEngine.Rendering
{
    [CreateAssetMenu(menuName = "Rendering/Custom Pipeline")]
    public class CustomPipelineAsset : RenderPipelineAsset
    {
        // [SerializeField]
        private bool m_useSRPBatcher = false;
        // [SerializeField]
        private bool m_useDynamicBatching = false;
        //  [SerializeField]
        private bool m_GPUInstancing = false;
        [SerializeField]
        private bool m_useComputeShader = false;
        [SerializeField]
        private ComputeShader m_ComputeShader = null;
        [SerializeField]
        private Texture m_SkyBoxTexture = null;
        [SerializeField]
        private bool m_UseSkyBox = false;
        [SerializeField]
        private Color m_SkyBoxColor = Color.blue;
        protected override RenderPipeline CreatePipeline()
        {


            return new CostumRenderPipeline(m_useDynamicBatching, m_GPUInstancing, m_useSRPBatcher,
                m_ComputeShader, m_useComputeShader, m_SkyBoxTexture, m_UseSkyBox, m_SkyBoxColor);
        }
    }
    public class CostumRenderPipeline : RenderPipeline
    {
        private bool m_useDynamicBatching = false;
        private bool m_GPUInstancing = false;
        private bool m_useCS = false;
        private ComputeShader m_computShader;
        private RayCastMaster m_RayCastMaster;
        public CostumRenderPipeline(bool DynamicBatching, bool Instancing, bool batcher, ComputeShader cs, bool useCS, Texture skyboxTexture, bool useSkyBox, Color color)
        {
            m_useCS = useCS;
            m_computShader = cs;
            m_useDynamicBatching = DynamicBatching;
            m_GPUInstancing = Instancing;
            GraphicsSettings.useScriptableRenderPipelineBatching = batcher;

            m_RayCastMaster = new RayCastMaster();
            m_RayCastMaster.Init(m_computShader, Camera.main, skyboxTexture, color);
        }
        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            CameraRenderer renderer = new CameraRenderer();
            //iterate cameras && call actual render pass for the camera
            foreach (Camera cam in cameras)
            {
                renderer.Render(context, cam, m_useDynamicBatching, m_GPUInstancing, m_RayCastMaster, m_useCS);
            }
            OnRenderFinished();

        }
        private void OnRenderFinished()
        {

        }
    }
}