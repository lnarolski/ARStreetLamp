using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Urho;
using Urho.Droid;
using Com.Google.AR.Core;
using Java.Util;
using Android.Content.Res;
using System.IO;
using Java.Lang;

namespace ARStreetLamp
{
    class ARRender : Urho.Application
    {
        private Zone zone;
        private Viewport viewport;
        private bool scaling;
        private Frame currentFrame;
        private MonoDebugHud fps;
        private bool gammaCorrected;

        public ToggleButton poleButton;
        public ToggleButton lightButton;
        public SeekBar heightSeekBar;
        public SeekBar rotateSeekBar;

        //private float objectsScale = 0.4674199f;
        //private float poleObjectsScale = 0.085f;
        private Scene scene;
        //private Node poleNode;
        //private Node poleStandNode;
        //private Node poleStandNutsNode;

        public Activity mainActivity;
        private Toast toast;

        public string[] lampModelsString;
        public string[] poleModelsString;
        public AssetManager assetManager;

        List<LampModel> lampModels;
        List<PoleModel> poleModels;
        List<LampModel> sceneLampModels;
        List<PoleModel> scenePoleModels;
        private int selectedLampModel = 0;
        private int selectedPoleModel = 0;

        public ARCoreComponent ArCore { get; private set; }

        public ARRender(ApplicationOptions options) : base(options) {
            lampModels = new List<LampModel>();
            poleModels = new List<PoleModel>();
            sceneLampModels = new List<LampModel>();
            scenePoleModels = new List<PoleModel>();
        }

        private void ShowToast(string mssg)
        {
            mainActivity.RunOnUiThread(() =>
            {
                if (toast != null)
                {
                    toast.Cancel();
                }
                toast = Toast.MakeText(Android.App.Application.Context, mssg, ToastLength.Long);
                toast.Show();
            });
        }

        protected override void Start()
        {
            // 3d scene with octree and ambient light
            scene = new Scene(Context);
            var octree = scene.CreateComponent<Octree>();
            zone = scene.CreateComponent<Zone>();
            zone.AmbientColor = new Color(1, 1, 1) * 0.2f;

            // Camera
            var cameraNode = scene.CreateChild(name: "Camera");
            var camera = cameraNode.CreateComponent<Urho.Camera>();

            // Light
            var lightNode = cameraNode.CreateChild();
            lightNode.SetDirection(new Vector3(1f, -1.0f, 1f));
            var light = lightNode.CreateComponent<Light>();
            light.Range = 10;
            light.LightType = LightType.Spot;
            light.CastShadows = true;
            Renderer.ShadowMapSize *= 4;

            // Viewport
            viewport = new Viewport(Context, scene, camera, null);
            Renderer.SetViewport(0, viewport);

            // ARCore component
            ArCore = scene.CreateComponent<ARCoreComponent>();
            ArCore.ARFrameUpdated += OnARFrameUpdated;
            ArCore.ConfigRequested += ArCore_ConfigRequested;
            ArCore.Run();

            while (assetManager == null) { } //Wait for finish of initialization

            // Lamps
            for (int i = 0; i < lampModelsString.Length; i++)
            {
                using (StreamReader sr = new StreamReader(assetManager.Open(lampModelsString[i] + @"/config.txt")))
                {
                    string[] lampElements = sr.ReadLine().Split(',');
                    string[] lampMaterials = sr.ReadLine().Split(',');
                    string[] lightElement = sr.ReadLine().Split(',');
                    string[] baseElement = sr.ReadLine().Split(',');

                    LampModel lampModel = new LampModel();
                    lampModel.lampScale = Float.ParseFloat(sr.ReadLine());
                    lampModel.name = lampModelsString[i];

                    for (int j = 0; j < lampElements.Length; j++)
                    {
                        Node node = new Node();
                        node.Position = new Vector3(0, lampModel.yPosition, 0.5f); // 50cm Z
                        node.SetScale(lampModel.lampScale);
                        StaticModel model = node.CreateComponent<StaticModel>();
                        model.CastShadows = true;
                        model.Model = ResourceCache.GetModel(lampModelsString[i] + @"/" + lampElements[i] + @".mdl");
                        model.Material = ResourceCache.GetMaterial(lampModelsString[i] + @"/" + lampMaterials[i] + @".xml");

                        lampModel.lampElements.Add(node);
                    }
                    Node lightLampNode = new Node();
                    lightLampNode.Position = new Vector3(0, lampModel.yPosition, 0.5f); // 50cm Z
                    lightLampNode.SetScale(lampModel.lampScale);
                    StaticModel lightLampModel = lightLampNode.CreateComponent<StaticModel>();
                    lightLampModel.CastShadows = true;
                    lightLampModel.Model = ResourceCache.GetModel(lampModelsString[i] + @"/" + lightElement[0] + @".mdl");
                    lightLampModel.Material = ResourceCache.GetMaterial(lampModelsString[i] + @"/" + lightElement[1] + @".xml");
                    lampModel.glassElement = lightLampNode;

                    //Lamp light
                    Node lampLightNode = new Node();
                    lampLightNode.Rotation = new Quaternion(90.0f, 0.0f, 0.0f);
                    var components = lampModel.glassElement.Components.GetEnumerator();
                    if (components.MoveNext())
                    {
                        lampLightNode.Position = ((Urho.StaticModel)components.Current).WorldBoundingBox.Center;
                    }

                    Light lampLightLight = lampLightNode.CreateComponent<Light>();
                    lampLightLight.LightType = LightType.Point;
                    lampLightLight.Length = 1;
                    lampLightLight.Range = 0.8f;
                    lampLightLight.Fov = 160.0f;
                    lampLightLight.AspectRatio = 1.05f;
                    lampLightLight.Color = new Color(255.0f, 209.0f, 163.0f, 1.0f);
                    lampLightLight.Brightness = 0.0f;
                    lampLightLight.CastShadows = true;
                    lampLightLight.ShadowBias = new BiasParameters(0.0f, 0.5f);

                    lampModel.lightElement = lampLightNode;
                    lampModel.light = lampLightLight;

                    Node baseNode = new Node();
                    baseNode.Position = new Vector3(0, lampModel.yPosition, 0.5f); // 50cm Z
                    baseNode.SetScale(lampModel.lampScale);
                    StaticModel baseModel = baseNode.CreateComponent<StaticModel>();
                    baseModel.CastShadows = true;
                    baseModel.Model = ResourceCache.GetModel(lampModelsString[i] + @"/" + baseElement[0] + @".mdl");
                    baseModel.Material = ResourceCache.GetMaterial(lampModelsString[i] + @"/" + baseElement[1] + @".xml");
                    lampModel.baseElement = baseNode;

                    lampModels.Add(lampModel);
                }
            }

            //Poles
            for (int i = 0; i < poleModelsString.Length; i++)
            {
                using (StreamReader sr = new StreamReader(assetManager.Open(poleModelsString[i] + @"/config.txt")))
                {
                    string[] poleElements = sr.ReadLine().Split(',');
                    string[] poleMaterials = sr.ReadLine().Split(',');
                    string[] scalablePoleElement = sr.ReadLine().Split(',');

                    PoleModel poleModel = new PoleModel();
                    poleModel.poleScale = Float.ParseFloat(sr.ReadLine());
                    poleModel.name = poleModelsString[i];

                    for (int j = 0; j < poleElements.Length; j++)
                    {
                        Node node = new Node();
                        node.Position = new Vector3(0, 0, 0.5f); // 50cm Z
                        node.SetScale(poleModel.poleScale);
                        StaticModel model = node.CreateComponent<StaticModel>();
                        model.CastShadows = true;
                        model.Model = ResourceCache.GetModel(lampModelsString[i] + @"/" + poleElements[i] + @".mdl");
                        model.Material = ResourceCache.GetMaterial(lampModelsString[i] + @"/" + poleMaterials[i] + @".xml");

                        poleModel.poleElements.Add(node);
                    }
                    Node scalableElementNode = new Node();
                    scalableElementNode.Position = new Vector3(0, 0, 0.5f); // 50cm Z
                    scalableElementNode.SetScale(poleModel.poleScale);

                    poleModels.Add(poleModel);
                }
            }

            //fps = new MonoDebugHud(this);
            //fps.Show(Color.Blue, 20);

            // Add some post-processing (also, see CorrectGamma())
            viewport.RenderPath.Append(CoreAssets.PostProcess.FXAA2);

            Input.TouchBegin += OnTouchBegin;
            Input.TouchEnd += OnTouchEnd;
        }

        void ArCore_ConfigRequested(Config config)
        {
            config.SetPlaneFindingMode(Config.PlaneFindingMode.Horizontal);
            config.SetLightEstimationMode(Config.LightEstimationMode.AmbientIntensity);
            config.SetUpdateMode(Config.UpdateMode.LatestCameraImage); //non blocking
        }

        void OnTouchBegin(TouchBeginEventArgs e)
        {
            scaling = false;
        }

        void OnTouchEnd(TouchEndEventArgs e)
        {
            if (scaling)
                return;

            var hitTest = currentFrame.HitTest(e.X, e.Y);
            if (hitTest != null && hitTest.Count > 0)
            {
                var hitPos = hitTest[0].HitPose;
                lampModels[selectedLampModel].pole = poleModels[selectedPoleModel];
                sceneLampModels.Add(lampModels[selectedLampModel]);
                sceneLampModels[sceneLampModels.Count - 1].MoveLamp(new Vector3(hitPos.Tx(), hitPos.Ty(), -hitPos.Tz()));

                heightSeekBar.Progress = 0;

                sceneLampModels[sceneLampModels.Count - 1].AddToScene(ref scene);
            }
        }

        // game update
        protected override void OnUpdate(float timeStep)
        {
            // multitouch scaling:
            if (Input.NumTouches == 2)
            {
                scaling = true;
                var state1 = Input.GetTouch(0);
                var state2 = Input.GetTouch(1);
                var distance1 = IntVector2.Distance(state1.Position, state2.Position);
                var distance2 = IntVector2.Distance(state1.LastPosition, state2.LastPosition);
                sceneLampModels[selectedLampModel].ScaleLamp((distance1 - distance2) / 10000f);

                ShowToast("Scale: " + sceneLampModels[selectedLampModel].lampScale);
            }
        }

        // called by the update loop
        void OnARFrameUpdated(Frame arFrame)
        {
            currentFrame = arFrame;
            var anchors = arFrame.UpdatedAnchors;
            //TODO: visulize anchors (don't forget ARCore uses RHD coordinate system)

            // Adjust our ambient light based on the light estimates ARCore provides each frame
            var lightEstimate = arFrame.LightEstimate;
            //fps.AdditionalText = lightEstimate?.PixelIntensity.ToString("F1");
            zone.AmbientColor = new Color(1, 1, 1) * ((lightEstimate?.PixelIntensity ?? 0.2f) / 2f);
        }

        public void PrepareInterface()
        {
            poleButton.Click += PoleButton_Click;
            lightButton.Click += LightButton_Click;

            rotateSeekBar.ProgressChanged += RotateSeekBar_ProgressChanged;
            heightSeekBar.ProgressChanged += HeightSeekBar_ProgressChanged;
        }

        private void HeightSeekBar_ProgressChanged(object sender, SeekBar.ProgressChangedEventArgs e)
        {
            sceneLampModels[selectedLampModel].ChangeHeight(e.Progress / 100.0f);

            var components = sceneLampModels[selectedLampModel].baseElement.Components.GetEnumerator();
            float baseYPosition = sceneLampModels[selectedLampModel].yPosition;
            if (components.MoveNext())
            {
                Vector3 centerOfBase = ((Urho.StaticModel)components.Current).WorldBoundingBox.Center;
                baseYPosition = centerOfBase.Y;
            }

            ShowToast("Height: " + (((System.Math.Abs(baseYPosition - sceneLampModels[selectedLampModel].yPosition) / 2) * 0.95f) / 0.45f) + " m");
        }

        private void RotateSeekBar_ProgressChanged(object sender, SeekBar.ProgressChangedEventArgs e)
        {
            sceneLampModels[selectedLampModel].RotateLamp(new Quaternion(0, e.Progress, 0));
        }

        private void LightButton_Click(object sender, EventArgs e)
        {
            if (lightButton.Checked)
            {
                sceneLampModels[selectedLampModel].TurnOn();
            }
            else
            {
                sceneLampModels[selectedLampModel].TurnOff();
            }
        }

        private void PoleButton_Click(object sender, EventArgs e)
        {
            Urho.Application.InvokeOnMain(() =>
            {
                if (poleButton.Checked)
                {
                    poleStandNode = scene.CreateChild();
                    poleStandNode.Position = new Vector3(bodyNode.Position.X, yPosition, bodyNode.Position.Z);
                    poleStandNode.SetScale(poleObjectsScale * objectsScale);
                    var model = poleStandNode.CreateComponent<StaticModel>();
                    model.Model = ResourceCache.GetModel("standardPole/poleStandSmooth.mdl");
                    model.Material = ResourceCache.GetMaterial("standardPole/poleStand.xml");
                    model.CastShadows = true;


                    poleStandNutsNode = scene.CreateChild();
                    poleStandNutsNode.Position = new Vector3(bodyNode.Position.X, yPosition, bodyNode.Position.Z);
                    poleStandNutsNode.SetScale(poleObjectsScale * objectsScale * 12.0f);
                    model = poleStandNutsNode.CreateComponent<StaticModel>();
                    model.Model = ResourceCache.GetModel("standardPole/poleStandNuts.mdl");
                    model.Material = ResourceCache.GetMaterial("standardPole/poleStandNuts.xml");
                    model.CastShadows = true;

                    var components = baseNode.Components.GetEnumerator();
                    float baseYPosition = yPosition;
                    if (components.MoveNext())
                    {
                        Vector3 centerOfBase = ((Urho.StaticModel)components.Current).WorldBoundingBox.Center;
                        baseYPosition = centerOfBase.Y;
                    }

                    poleNode = scene.CreateChild();
                    poleNode.Scale = new Vector3(poleObjectsScale * objectsScale, System.Math.Abs(baseYPosition - yPosition) / 2, poleObjectsScale * objectsScale);
                    poleNode.Position = new Vector3(bodyNode.Position.X, yPosition, bodyNode.Position.Z);
                    model = poleNode.CreateComponent<StaticModel>();
                    //model.Model = ResourceCache.GetModel("standardPole/poleSmooth.mdl");
                    model.Model = ResourceCache.GetModel("standardPole/Pole.mdl");
                    model.Material = ResourceCache.GetMaterial("standardPole/Pole.xml");
                    model.CastShadows = true;
                }
                else
                {
                    if (!poleNode.IsDeleted)
                    {
                        poleStandNode.Remove();
                        poleStandNutsNode.Remove();
                        poleNode.Remove();
                    }
                }
            });
        }
    }

    class LampModel
    {
        public List<Node> lampElements;
        public Node glassElement;
        public Node baseElement;
        public Node lightElement;
        public Light light;
        public float lampScale;
        public float lightHeight = -0.1f;
        public string name;

        public float yPosition = 0.0f;

        public PoleModel pole = null;

        public LampModel()
        {
            lampElements = new List<Node>();
        }

        public void MoveLamp(Vector3 vector3)
        {
            foreach (var item in lampElements)
            {
                item.Position = vector3;
            }
            baseElement.Position = vector3;
            glassElement.Position = vector3;

            var components = glassElement.Components.GetEnumerator();
            if (components.MoveNext())
            {
                Vector3 centerOfGlass = ((Urho.StaticModel)components.Current).WorldBoundingBox.Center;
                centerOfGlass.Y -= 0.1f * lampScale;
                lightElement.Position = centerOfGlass;
            }

            yPosition = vector3.Y;
        }

        public void ChangeHeight(float height)
        {
            foreach (var item in lampElements)
            {
                item.Position = new Vector3(item.Position.X, yPosition + height, item.Position.Z);
            }
            baseElement.Position = new Vector3(baseElement.Position.X, yPosition + height, baseElement.Position.Z);
            glassElement.Position = new Vector3(glassElement.Position.X, yPosition + height, glassElement.Position.Z);

            var components = glassElement.Components.GetEnumerator();
            if (components.MoveNext())
            {
                Vector3 centerOfGlass = ((Urho.StaticModel)components.Current).WorldBoundingBox.Center;
                centerOfGlass.Y -= 0.1f * lampScale;
                lightElement.Position = centerOfGlass;
            }
        }

        public void ScaleLamp(float scale)
        {
            lampScale += scale;
            foreach (var item in lampElements)
            {
                item.SetScale(lampScale);
            }
            baseElement.SetScale(lampScale);
            lightElement.SetScale(lampScale);

            var components = glassElement.Components.GetEnumerator();
            if (components.MoveNext())
            {
                Vector3 centerOfGlass = ((Urho.StaticModel)components.Current).WorldBoundingBox.Center;
                centerOfGlass.Y -= 0.1f * lampScale;
                lightElement.Position = centerOfGlass;
            }

        }

        public void RotateLamp(Quaternion quaternion)
        {
            foreach (var item in lampElements)
            {
                item.Rotation = quaternion;
            }
            baseElement.Rotation = quaternion;
            lightElement.Rotation = new Quaternion(90.0f, quaternion.Y, quaternion.Z);

            var components = glassElement.Components.GetEnumerator();
            if (components.MoveNext())
            {
                Vector3 centerOfGlass = ((Urho.StaticModel)components.Current).WorldBoundingBox.Center;
                centerOfGlass.Y -= 0.1f * lampScale;
                lightElement.Position = centerOfGlass;
            }
        }

        public void TurnOn()
        {
            light.Brightness = 0.1f;
        }

        public void TurnOff()
        {
            light.Brightness = 0.0f;
        }

        public void AddToScene(ref Scene scene)
        {
            foreach (var item in lampElements)
            {
                scene.AddChild(item);
            }
            scene.AddChild(baseElement);
            scene.AddChild(lightElement);
        }
    }

    class PoleModel
    {
        public List<Node> poleElements;
        public Node scalablePoleElement;
        public float poleScale;
        public string name;
        public bool removed = false;

        public PoleModel()
        {
            poleElements = new List<Node>();
        }

        public void Show()
        {
            foreach (var item in poleElements)
            {
                item.Enabled = true;
            }
            scalablePoleElement.Enabled = true;
        }

        public void Hide()
        {
            foreach (var item in poleElements)
            {
                item.Enabled = false;
            }
            scalablePoleElement.Enabled = false;
        }

        public void Remove()
        {
            foreach (var item in poleElements)
            {
                item.Remove();
            }
            scalablePoleElement.Remove();
        }

        public void ScalePole(float scale)
        {

        }
    }
}