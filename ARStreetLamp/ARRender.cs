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

        public ARRender(ApplicationOptions options) : base(options) {}

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
            lampModels = new List<LampModel>();
            poleModels = new List<PoleModel>();
            sceneLampModels = new List<LampModel>();
            scenePoleModels = new List<PoleModel>();

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

            Urho.IO.File modelsCache = ResourceCache.GetFile("models.txt");
            modelsCache.

            // Lamps
            for (int i = 0; i < lampModelsString.Length; i++)
            {
                LampModel lampModel = new LampModel();

                using (StreamReader sr = new StreamReader(assetManager.Open(@"ArData/" + lampModelsString[i] + @"/config.txt")))
                {
                    string[] lampElements = sr.ReadLine().Split(',');
                    string[] lampMaterials = sr.ReadLine().Split(',');
                    string[] lightElement = sr.ReadLine().Split(',');
                    string[] baseElement = sr.ReadLine().Split(',');

                    lampModel.lampScale = Float.ParseFloat(sr.ReadLine());
                    lampModel.name = lampModelsString[i];

                    for (int j = 0; j < lampElements.Length; j++)
                    {
                        Node node = new Node();
                        //Node node = scene.CreateChild();
                        node.Position = new Vector3(0, lampModel.yPosition, 0.5f); // 50cm Z
                        node.SetScale(lampModel.lampScale);
                        StaticModel model = node.CreateComponent<StaticModel>();
                        model.CastShadows = true;
                        model.Model = ResourceCache.GetModel(lampModelsString[i] + "/" + lampElements[j] + ".mdl");
                        model.Material = ResourceCache.GetMaterial(lampModelsString[i] + "/" + lampMaterials[j] + ".xml");

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
                    baseModel.Model = ResourceCache.GetModel(lampModelsString[i] + "/" + baseElement[0] + ".mdl");
                    baseModel.Material = ResourceCache.GetMaterial(lampModelsString[i] + "/" + baseElement[1] + ".xml");
                    lampModel.baseElement = baseNode;
                }

                lampModels.Add(lampModel);
            }

            //Poles
            for (int i = 0; i < poleModelsString.Length; i++)
            {
                PoleModel poleModel = new PoleModel();

                using (StreamReader sr = new StreamReader(assetManager.Open(poleModelsString[i] + @"/config.txt")))
                {
                    string[] poleElements = sr.ReadLine().Split(',');
                    string[] poleMaterials = sr.ReadLine().Split(',');
                    string[] scalablePoleElement = sr.ReadLine().Split(',');

                    poleModel.poleScale = Float.ParseFloat(sr.ReadLine());
                    poleModel.name = poleModelsString[i];

                    for (int j = 0; j < poleElements.Length; j++)
                    {
                        Node node = new Node();
                        node.Position = new Vector3(0, 0, 0.5f); // 50cm Z
                        node.SetScale(poleModel.poleScale * 12.0f);
                        StaticModel poleElementModel = node.CreateComponent<StaticModel>();
                        poleElementModel.CastShadows = true;
                        poleElementModel.Model = ResourceCache.GetModel(lampModelsString[i] + @"/" + poleElements[j] + @".mdl");
                        poleElementModel.Material = ResourceCache.GetMaterial(lampModelsString[i] + @"/" + poleMaterials[j] + @".xml");

                        poleModel.poleElements.Add(node);
                    }
                    Node scalableElementNode = new Node();
                    scalableElementNode.Position = new Vector3(0, 0, 0.5f); // 50cm Z
                    scalableElementNode.SetScale(poleModel.poleScale);
                    StaticModel modelScalableElement = scalableElementNode.CreateComponent<StaticModel>();
                    modelScalableElement.CastShadows = true;
                    modelScalableElement.Model = ResourceCache.GetModel(lampModelsString[i] + @"/" + scalablePoleElement[0] + @".mdl");
                    modelScalableElement.Material = ResourceCache.GetMaterial(lampModelsString[i] + @"/" + scalablePoleElement[1] + @".xml");

                }

                poleModels.Add(poleModel);
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

        public void PrepareAR()
        {
            poleButton.Click += PoleButton_Click;
            lightButton.Click += LightButton_Click;

            rotateSeekBar.ProgressChanged += RotateSeekBar_ProgressChanged;
            heightSeekBar.ProgressChanged += HeightSeekBar_ProgressChanged;
        }

        private void HeightSeekBar_ProgressChanged(object sender, SeekBar.ProgressChangedEventArgs e)
        {
            sceneLampModels[selectedLampModel].ChangeLampHeight(e.Progress / 100.0f);

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
                    if (sceneLampModels[selectedLampModel].pole == null)
                        sceneLampModels[selectedLampModel].pole = poleModels[selectedPoleModel];

                    sceneLampModels[selectedLampModel].ShowPole();
                }
                else
                {
                    if (sceneLampModels[selectedLampModel].pole != null)
                        sceneLampModels[selectedLampModel].HidePole();


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

        public void ShowPole()
        {
            if (pole == null)
                return;

            pole.MovePole(new Vector3(baseElement.Position.X, yPosition, baseElement.Position.Z));
            pole.ChangeHeight(baseElement, yPosition);
            pole.Show();
        }

        public void HidePole()
        {
            if (pole == null)
                return;

            pole.Hide();
        }

        public void MovePole()
        {
            if (pole == null)
                return;

            pole.MovePole(new Vector3(baseElement.Position.X, yPosition, baseElement.Position.Z));
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

        public void ChangeLampHeight(float height)
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
            poleScale += scale;
            foreach (var item in poleElements)
            {
                item.SetScale(poleScale);
            }
            scalablePoleElement.SetScale(scale);
        }

        public void MovePole(Vector3 vector3)
        {
            foreach (var item in poleElements)
            {
                item.Position = vector3;
            }
            scalablePoleElement.Position = vector3;
        }

        public void ChangeHeight(Node baseNode, float yPosition)
        {
            scalablePoleElement.Scale = new Vector3(poleScale, System.Math.Abs(baseNode.Position.Y - yPosition) / 2, poleScale);
        }
    }
}