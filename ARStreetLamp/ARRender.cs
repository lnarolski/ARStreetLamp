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
using System.Dynamic;
using Urho.Resources;

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

        private Scene scene;

        public Activity mainActivity;
        private Toast toast;

        public string[] lampModelsString;
        public string[] poleModelsString;
        public AssetManager assetManager;

        //List<LampModel> lampModels;
        //List<PoleModel> poleModels;

        List<LampModelDefinition> lampModels;
        List<PoleModelDefinition> poleModels;

        List<LampModel> sceneLampModels;
        private int selectedLampModel = 0;
        private int selectedPoleModel = 0;

        private int selectedLamp = -1;

        internal Button prevControlsScreenButton;
        internal Button nextControlsScreenButton;
        internal Button editLampsButton;
        internal Button createInstalationButton;
        internal Button deleteAllLampsButton;
        internal Button editLampButton;
        internal Button prevSelLampButton;
        internal Button nextSelLampButton;

        public ARCoreComponent ArCore { get; private set; }

        public ARRender(ApplicationOptions options) : base(options) { }

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
            lampModels = new List<LampModelDefinition>();
            poleModels = new List<PoleModelDefinition>();
            sceneLampModels = new List<LampModel>();

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

            if (currentFrame == null)
                return;

            var hitTest = currentFrame.HitTest(e.X, e.Y);
            if (hitTest != null && hitTest.Count > 0)
            {
                var hitPos = hitTest[0].HitPose;
                sceneLampModels.Add(new LampModel(lampModels[selectedLampModel], sceneLampModels.Count.ToString()));
                selectedLamp = sceneLampModels.Count - 1;

                sceneLampModels[selectedLamp].MoveLamp(new Vector3(hitPos.Tx(), hitPos.Ty(), -hitPos.Tz()));

                heightSeekBar.Progress = 0;

                sceneLampModels[selectedLamp].AddToScene(ref scene);
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
            deleteAllLampsButton.Click += DeleteAllLampsButton_Click;

            rotateSeekBar.ProgressChanged += RotateSeekBar_ProgressChanged;
            heightSeekBar.ProgressChanged += HeightSeekBar_ProgressChanged;

            //Urho.Application.InvokeOnMain(() =>
            //{
            //    // Lamps
            //    for (int i = 0; i < lampModelsString.Length; i++)
            //    {
            //        LampModel lampModel = new LampModel();

            //        string[] lampElements;
            //        string[] lampMaterials;
            //        string[] lightElement;
            //        string[] baseElement;

            //        using (StreamReader sr = new StreamReader(assetManager.Open(@"ArData/" + lampModelsString[i] + @"/config.txt")))
            //        {
            //            lampElements = sr.ReadLine().Split(',');
            //            lampMaterials = sr.ReadLine().Split(',');
            //            lightElement = sr.ReadLine().Split(',');
            //            baseElement = sr.ReadLine().Split(',');

            //            lampModel.lampScale = Float.ParseFloat(sr.ReadLine());
            //            lampModel.name = lampModelsString[i];
            //        }

            //        for (int j = 0; j < lampElements.Length; j++)
            //        {
            //            Node node = new Node();
            //            node.Position = new Vector3(0, lampModel.yPosition, 0.5f); // 50cm Z
            //            node.SetScale(lampModel.lampScale);
            //            StaticModel model = node.CreateComponent<StaticModel>();
            //            model.CastShadows = true;
            //            model.Model = ResourceCache.GetModel(lampModelsString[i] + "/" + lampElements[j] + ".mdl");
            //            model.Material = ResourceCache.GetMaterial(lampModelsString[i] + "/" + lampMaterials[j] + ".xml");

            //            lampModel.lampElements.Add(node);
            //        }
            //        Node lightLampNode = new Node();
            //        lightLampNode.Position = new Vector3(0, lampModel.yPosition, 0.5f); // 50cm Z
            //        lightLampNode.SetScale(lampModel.lampScale);
            //        StaticModel lightLampModel = lightLampNode.CreateComponent<StaticModel>();
            //        lightLampModel.CastShadows = true;
            //        lightLampModel.Model = ResourceCache.GetModel(lampModelsString[i] + @"/" + lightElement[0] + @".mdl");
            //        lightLampModel.Material = ResourceCache.GetMaterial(lampModelsString[i] + @"/" + lightElement[1] + @".xml");
            //        lampModel.glassElement = lightLampNode;

            //        //Lamp light
            //        Node lampLightNode = new Node();
            //        lampLightNode.Rotation = new Quaternion(90.0f, 0.0f, 0.0f);
            //        var components = lampModel.glassElement.Components.GetEnumerator();
            //        if (components.MoveNext())
            //        {
            //            lampLightNode.Position = ((Urho.StaticModel)components.Current).WorldBoundingBox.Center;
            //        }

            //        Light lampLightLight = lampLightNode.CreateComponent<Light>();
            //        lampLightLight.LightType = LightType.Point;
            //        lampLightLight.Length = 1;
            //        lampLightLight.Range = 0.8f;
            //        lampLightLight.Fov = 160.0f;
            //        lampLightLight.AspectRatio = 1.05f;
            //        lampLightLight.Color = new Color(255.0f, 209.0f, 163.0f, 1.0f);
            //        lampLightLight.Brightness = 0.0f;
            //        lampLightLight.CastShadows = true;
            //        lampLightLight.ShadowBias = new BiasParameters(0.0f, 0.5f);

            //        lampModel.lightElement = lampLightNode;
            //        lampModel.light = lampLightLight;

            //        Node baseNode = new Node();
            //        baseNode.Position = new Vector3(0, lampModel.yPosition, 0.5f); // 50cm Z
            //        baseNode.SetScale(lampModel.lampScale);
            //        StaticModel baseModel = baseNode.CreateComponent<StaticModel>();
            //        baseModel.CastShadows = true;
            //        baseModel.Model = ResourceCache.GetModel(lampModelsString[i] + "/" + baseElement[0] + ".mdl");
            //        baseModel.Material = ResourceCache.GetMaterial(lampModelsString[i] + "/" + baseElement[1] + ".xml");
            //        lampModel.baseElement = baseNode;

            //        lampModels.Add(lampModel);
            //    }

            //    //Poles
            //    for (int i = 0; i < poleModelsString.Length; i++)
            //    {
            //        PoleModel poleModel = new PoleModel();

            //        string[] poleElements;
            //        string[] poleMaterials;
            //        string[] scalablePoleElement;

            //        using (StreamReader sr = new StreamReader(assetManager.Open(@"ArData/" + poleModelsString[i] + @"/config.txt")))
            //        {
            //            poleElements = sr.ReadLine().Split(',');
            //            poleMaterials = sr.ReadLine().Split(',');
            //            scalablePoleElement = sr.ReadLine().Split(',');

            //            poleModel.poleScale = Float.ParseFloat(sr.ReadLine());
            //            poleModel.name = poleModelsString[i];
            //        }

            //        for (int j = 0; j < poleElements.Length; j++)
            //        {
            //            Node node = new Node();
            //            node.Position = new Vector3(0, 0, 0.5f); // 50cm Z
            //            node.SetScale(poleModel.poleScale * 12.0f);
            //            StaticModel poleElementModel = node.CreateComponent<StaticModel>();
            //            poleElementModel.CastShadows = true;
            //            poleElementModel.Model = ResourceCache.GetModel(poleModelsString[i] + @"/" + poleElements[j] + @".mdl");
            //            poleElementModel.Material = ResourceCache.GetMaterial(poleModelsString[i] + @"/" + poleMaterials[j] + @".xml");

            //            poleModel.poleElements.Add(node);
            //        }
            //        Node scalableElementNode = new Node();
            //        scalableElementNode.Position = new Vector3(0, 0, 0.5f); // 50cm Z
            //        scalableElementNode.SetScale(poleModel.poleScale);
            //        StaticModel modelScalableElement = scalableElementNode.CreateComponent<StaticModel>();
            //        modelScalableElement.CastShadows = true;
            //        modelScalableElement.Model = ResourceCache.GetModel(poleModelsString[i] + @"/" + scalablePoleElement[0] + @".mdl");
            //        modelScalableElement.Material = ResourceCache.GetMaterial(poleModelsString[i] + @"/" + scalablePoleElement[1] + @".xml");
            //        poleModel.scalablePoleElement = scalableElementNode;

            //        poleModels.Add(poleModel);
            //    }
            //});

            Urho.Application.InvokeOnMain(() =>
            {
                // Lamps
                for (int i = 0; i < lampModelsString.Length; i++)
                {
                    LampModelDefinition lampModel = new LampModelDefinition();

                    string[] lampElements;
                    string[] lampMaterials;
                    string[] lightElement;
                    string[] baseElement;

                    using (StreamReader sr = new StreamReader(assetManager.Open(@"ArData/" + lampModelsString[i] + @"/config.txt")))
                    {
                        lampElements = sr.ReadLine().Split(',');
                        lampMaterials = sr.ReadLine().Split(',');
                        lightElement = sr.ReadLine().Split(',');
                        baseElement = sr.ReadLine().Split(',');

                        lampModel.lampScale = Float.ParseFloat(sr.ReadLine());
                        lampModel.name = lampModelsString[i];
                    }

                    for (int j = 0; j < lampElements.Length; j++)
                    {
                        lampModel.lampElements.Add(lampModelsString[i] + "/" + lampElements[j]);
                        lampModel.lampElements.Add(lampModelsString[i] + "/" + lampMaterials[j]);
                    }

                    lampModel.glassElement = lampModelsString[i] + @"/" + lightElement[0];
                    lampModel.glassElementMaterial = lampModelsString[i] + @"/" + lightElement[1];

                    lampModel.baseElement = lampModelsString[i] + "/" + baseElement[0];
                    lampModel.baseElementMaterial = lampModelsString[i] + "/" + baseElement[1];

                    lampModels.Add(lampModel);
                }

                //Poles
                for (int i = 0; i < poleModelsString.Length; i++)
                {
                    PoleModelDefinition poleModel = new PoleModelDefinition();

                    string[] poleElements;
                    string[] poleMaterials;
                    string[] scalablePoleElement;

                    using (StreamReader sr = new StreamReader(assetManager.Open(@"ArData/" + poleModelsString[i] + @"/config.txt")))
                    {
                        poleElements = sr.ReadLine().Split(',');
                        poleMaterials = sr.ReadLine().Split(',');
                        scalablePoleElement = sr.ReadLine().Split(',');

                        poleModel.poleScale = Float.ParseFloat(sr.ReadLine());
                        poleModel.name = poleModelsString[i];
                    }

                    for (int j = 0; j < poleElements.Length; j++)
                    {
                        poleModel.poleElements.Add(poleModelsString[i] + @"/" + poleElements[j]);
                        poleModel.poleElementsMaterials.Add(poleModelsString[i] + @"/" + poleMaterials[j]);
                    }
                    poleModel.scalablePoleElement = poleModelsString[i] + @"/" + scalablePoleElement[0];
                    poleModel.scalablePoleElementMaterial = poleModelsString[i] + @"/" + scalablePoleElement[1];

                    poleModels.Add(poleModel);
                }
            });
        }

        private void HeightSeekBar_ProgressChanged(object sender, SeekBar.ProgressChangedEventArgs e)
        {
            if (sceneLampModels.Count == 0)
                return;

            foreach (var item in sceneLampModels)
            {
                item.ChangeLampHeight(e.Progress / 100.0f);
            }

            var components = sceneLampModels[0].baseElement.Components.GetEnumerator();
            float baseYPosition = sceneLampModels[0].yPosition;
            if (components.MoveNext())
            {
                Vector3 centerOfBase = ((Urho.StaticModel)components.Current).WorldBoundingBox.Center;
                baseYPosition = centerOfBase.Y;
            }

            ShowToast("Height: " + (((System.Math.Abs(baseYPosition - sceneLampModels[0].yPosition) / 2) * 0.95f) / 0.45f) + " m");
        }

        private void RotateSeekBar_ProgressChanged(object sender, SeekBar.ProgressChangedEventArgs e)
        {
            if (sceneLampModels.Count == 0)
                return;

            foreach (var item in sceneLampModels)
            {
                item.RotateLamp(new Quaternion(0, e.Progress, 0));
            }
        }

        private void LightButton_Click(object sender, EventArgs e)
        {
            if (sceneLampModels.Count == 0)
            {
                ((ToggleButton)sender).Checked = false;
                return;
            }

            foreach (var item in sceneLampModels)
            {
                if (lightButton.Checked)
                {
                    item.TurnOn();
                }
                else
                {
                    item.TurnOff();
                }
            }
        }

        private void PoleButton_Click(object sender, EventArgs e)
        {
            if (sceneLampModels.Count == 0)
            {
                ((ToggleButton)sender).Checked = false;
                return;
            }

            foreach (var item in sceneLampModels)
            {
                Urho.Application.InvokeOnMain(() =>
                {
                    if (poleButton.Checked)
                    {
                        if (item.pole == null)
                        {
                            item.pole = new PoleModel(poleModels[selectedPoleModel], item.name);
                            item.pole.AddToScene(ref scene);
                        }

                        item.ShowPole();
                    }
                    else
                    {
                        if (item.pole != null)
                            item.HidePole();
                    }
                });
            }
        }

        public void DeleteAllLampsButton_Click(object sender, EventArgs e)
        {
            foreach (var item in sceneLampModels)
            {
                item.Remove();
            }

            sceneLampModels.Clear();

            selectedLamp = -1;
        }
    }

    class LampModelDefinition
    {
        public List<string> lampElements;
        public List<string> lampElementsMaterials;
        public string glassElement;
        public string glassElementMaterial;
        public string baseElement;
        public string baseElementMaterial;
        public float lampScale;
        public string name;

        public LampModelDefinition()
        {
            lampElements = new List<string>();
            lampElementsMaterials = new List<string>();
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

        public LampModel(LampModelDefinition lampModelDefinition, string lampNum)
        {
            Urho.Application.InvokeOnMain(() =>
            {
                lampElements = new List<Node>();
                
                this.lampScale = lampModelDefinition.lampScale;
                this.name = lampModelDefinition.name + " " + lampNum;

                for (int j = 0; j < lampModelDefinition.lampElements.Count; j++)
                {
                    Node node = new Node();
                    node.Position = new Vector3(0, this.yPosition, 0.5f); // 50cm Z
                    node.SetScale(this.lampScale);
                    StaticModel model = node.CreateComponent<StaticModel>();
                    model.CastShadows = true;
                    model.Model = ResourceCache.GetModel(lampModelDefinition.lampElements[j] + @".mdl");
                    model.Material = ResourceCache.GetMaterial(lampModelDefinition.lampElementsMaterials[j] + @".xml");

                    this.lampElements.Add(node);
                }

                Node lightLampNode = new Node();
                lightLampNode.Position = new Vector3(0, this.yPosition, 0.5f); // 50cm Z
                lightLampNode.SetScale(this.lampScale);
                StaticModel lightLampModel = lightLampNode.CreateComponent<StaticModel>();
                lightLampModel.CastShadows = true;
                lightLampModel.Model = ResourceCache.GetModel(lampModelDefinition.glassElement + @".mdl");
                lightLampModel.Material = ResourceCache.GetMaterial(lampModelDefinition.glassElementMaterial + @".xml");
                this.glassElement = lightLampNode;

                //Lamp light
                Node lampLightNode = new Node();
                lampLightNode.Rotation = new Quaternion(90.0f, 0.0f, 0.0f);
                var components = this.glassElement.Components.GetEnumerator();
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

                this.lightElement = lampLightNode;
                this.light = lampLightLight;

                Node baseNode = new Node();
                baseNode.Position = new Vector3(0, this.yPosition, 0.5f); // 50cm Z
                baseNode.SetScale(this.lampScale);
                StaticModel baseModel = baseNode.CreateComponent<StaticModel>();
                baseModel.CastShadows = true;
                baseModel.Model = ResourceCache.GetModel(lampModelDefinition.baseElement + @".mdl");
                baseModel.Material = ResourceCache.GetMaterial(lampModelDefinition.baseElementMaterial + @".xml");
                this.baseElement = baseNode;
            });
        }

        public void Remove()
        {
            foreach (var item in lampElements)
            {
                item.Remove();
                item.Dispose();
            }
            baseElement.Remove();
            glassElement.Remove();

            baseElement.Dispose();
            glassElement.Dispose();

            if (pole != null)
            {
                pole.Remove();
            }
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
            glassElement.Rotation = quaternion;
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
            scene.AddChild(glassElement);
            scene.AddChild(lightElement);
        }
    }

    class PoleModelDefinition
    {
        public List<string> poleElements;
        public List<string> poleElementsMaterials;
        public string scalablePoleElement;
        public string scalablePoleElementMaterial;
        public float poleScale;
        public string name;

        public PoleModelDefinition()
        {
            poleElements = new List<string>();
            poleElementsMaterials = new List<string>();
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

        public PoleModel(PoleModelDefinition poleModelDefinition, string poleNum)
        {
            Urho.Application.InvokeOnMain(() =>
            {
                poleElements = new List<Node>();

                this.poleScale = poleModelDefinition.poleScale;
                this.name = poleModelDefinition.name + " " + poleNum;

                ResourceCache resourceCache = new ResourceCache();

                for (int j = 0; j < poleModelDefinition.poleElements.Count; j++)
                {
                    Node node = new Node();
                    node.Position = new Vector3(0, 0, 0.5f); // 50cm Z
                    node.SetScale(this.poleScale * 12.0f);
                    StaticModel poleElementModel = node.CreateComponent<StaticModel>();
                    poleElementModel.CastShadows = true;
                    poleElementModel.Model = ResourceCache.GetModel(poleModelDefinition.poleElements + @".mdl");
                    poleElementModel.Material = ResourceCache.GetMaterial(poleModelDefinition.poleElementsMaterials + @".xml");

                    this.poleElements.Add(node);
                }
                Node scalableElementNode = new Node();
                scalableElementNode.Position = new Vector3(0, 0, 0.5f); // 50cm Z
                scalableElementNode.SetScale(this.poleScale);
                StaticModel modelScalableElement = scalableElementNode.CreateComponent<StaticModel>();
                modelScalableElement.CastShadows = true;
                modelScalableElement.Model = ResourceCache.GetModel(poleModelDefinition.scalablePoleElement + @".mdl");
                modelScalableElement.Material = ResourceCache.GetMaterial(poleModelDefinition.scalablePoleElementMaterial + @".xml");
                this.scalablePoleElement = scalableElementNode;
            });
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
                item.Dispose();
            }
            scalablePoleElement.Remove();
            scalablePoleElement.Dispose();
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

        public void AddToScene(ref Scene scene)
        {
            foreach (var item in poleElements)
            {
                scene.AddChild(item);
            }
            scene.AddChild(scalablePoleElement);
        }
    }
}