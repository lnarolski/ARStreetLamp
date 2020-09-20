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
using Android.Graphics;
using MathNet.Numerics.LinearAlgebra;
using System.Numerics;
using Xamarin.Essentials;
using Urho.Shapes;
using MathNet.Numerics.Statistics;

namespace ARStreetLamp
{
    public class ARRender : Urho.Application
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

        public MainActivity mainActivity;
        private Toast toast;

        public string[] lampModelsString;
        public string[] poleModelsString;
        public AssetManager assetManager;

        List<LampModelDefinition> lampModels;
        List<PoleModelDefinition> poleModels;

        List<LampModel> sceneLampModels;
        private int selectedLampModel = 0;
        private int selectedPoleModel = 0;

        private int selectedLamp = -1;

        private int selectedValNumOfNewLampsDialog = 2;

        internal Button prevControlsScreenButton;
        internal Button nextControlsScreenButton;
        internal Button editLampsButton;
        internal Button createInstalationButton;
        internal Button deleteAllLampsButton;
        internal Button editLampButton;
        internal Button prevSelLampButton;
        internal Button nextSelLampButton;
        internal Button addLampButton;

        private List<Node> scenePlanes;
        private bool editingPlanesList;
        internal float sunAltitudeDeg;
        internal float sunAzimuthDeg;
        private bool gotCompassAngle;
        private bool calibrateScene = true;

        public Node sunNode;
        public StaticModel sunStaticModel;
        public Light sunLight;
        private Node CenterNode;
        private Node CenterNode2;

        private float compassInitialReadingDeg = 0.0f;
        private List<float> compassInitialReadingDegList = new List<float>();
        private int maxCompassReading = 10;

        public ARCoreComponent ArCore { get; private set; }
        public bool HUDVisible { get; private set; } = true;

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
            UnhandledException += ARRender_UnhandledException;

            lampModels = new List<LampModelDefinition>();
            poleModels = new List<PoleModelDefinition>();
            sceneLampModels = new List<LampModel>();
            scenePlanes = new List<Node>();

            // 3d scene with octree and ambient light
            scene = new Scene(Context);
            var octree = scene.CreateComponent<Octree>();
            zone = scene.CreateComponent<Zone>();
            zone.AmbientColor = new Urho.Color(1, 1, 1) * 0.2f;

            // Camera
            var cameraNode = scene.CreateChild(name: "Camera");
            var camera = cameraNode.CreateComponent<Urho.Camera>();

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

        private async void GetInitialCompassAngle()
        {
            Compass.ReadingChanged += Compass_InitialReadingChanged;
            Compass.Start(SensorSpeed.UI);

            while (!gotCompassAngle) { };

            Compass.ReadingChanged -= Compass_InitialReadingChanged;
            Compass.Stop();
        }



        // called by the update loop
        void OnARFrameUpdated(Com.Google.AR.Core.Frame arFrame)
        {
            currentFrame = arFrame;

            if (calibrateScene)
            {
                gotCompassAngle = false;
                GetInitialCompassAngle();

                //Light
                //// If sun is visible
                //sunAltitudeDeg = 45.0f;
                if (sunAltitudeDeg > 5.0f)
                {
                    SetSunLight();
                }
                else
                {
                    ShowToast("Sun too low, setting default light");

                    Urho.Application.InvokeOnMain(() =>
                    {
                        var lightNode = scene.CreateChild(name: "DirectionalLight");
                        lightNode.SetDirection(new Urho.Vector3(0.75f, -1.0f, 0f));
                        var light = lightNode.CreateComponent<Light>();
                        light.LightType = LightType.Directional;
                        light.CastShadows = true;
                        light.Brightness = 1.5f;
                        light.ShadowResolution = 4;
                        light.ShadowIntensity = 0.1f;
                        Renderer.ShadowMapSize *= 4;
                    });
                }

                calibrateScene = false;
            }

            if (HUDVisible)
            {
                while (editingPlanesList) { }
                editingPlanesList = true;

                // Showing detected planes for placing lamp models
                RemoveScenePlanes();
                foreach (var p in ArCore.Session.GetAllTrackables(Java.Lang.Class.FromType(typeof(Com.Google.AR.Core.Plane))))
                {
                    var planeArCore = (Com.Google.AR.Core.Plane)p;
                    var pose = planeArCore.CenterPose;

                    var planeNode = new Node();
                    var plane = planeNode.CreateComponent<StaticModel>();
                    planeNode.Position = new Urho.Vector3(pose.Tx(), pose.Ty() - 0.1f, -pose.Tz());
                    planeNode.Rotation = new Urho.Quaternion(0.0f, (pose.Qy() * 360.0f), 0.0f);
                    planeNode.Scale = new Urho.Vector3(planeArCore.ExtentX, 1, planeArCore.ExtentZ);
                    plane.Model = CoreAssets.Models.Plane;

                    var tileMaterial = new Material();
                    tileMaterial.SetTexture(TextureUnit.Diffuse, ResourceCache.GetTexture2D("PlaneTile.png"));
                    var tech = new Technique();
                    var pass = tech.CreatePass("alpha");
                    pass.DepthWrite = false;
                    pass.BlendMode = Urho.BlendMode.Alpha;
                    pass.PixelShader = "PlaneTile";
                    pass.VertexShader = "PlaneTile";
                    tileMaterial.SetTechnique(0, tech);
                    tileMaterial.SetShaderParameter("MeshColor", new Urho.Color(Randoms.Next(), 1, Randoms.Next()));
                    tileMaterial.SetShaderParameter("MeshAlpha", 0.75f); // set 0.0f if you want to hide them
                    tileMaterial.SetShaderParameter("MeshScale", 32.0f);

                    plane.Material = tileMaterial;

                    scene.AddChild(planeNode);
                    scenePlanes.Add(planeNode);
                }

                editingPlanesList = false;
            }

            // Adjust our ambient light based on the light estimates ARCore provides each frame
            var lightEstimate = arFrame.LightEstimate;
            //fps.AdditionalText = lightEstimate?.PixelIntensity.ToString("F1");
            zone.AmbientColor = new Urho.Color(1, 1, 1) * ((lightEstimate?.PixelIntensity ?? 0.2f) / 2f);
        }

        private async void SetSunLight()
        {            
            while (!gotCompassAngle) { };
            gotCompassAngle = false;

            sunAzimuthDeg -= compassInitialReadingDeg;
            while (sunAzimuthDeg > 360.0f) sunAzimuthDeg -= 360.0f;
            while (sunAzimuthDeg < 0.0f) sunAzimuthDeg += 360.0f;

            ShowToast($"Sun angle: {sunAzimuthDeg} deg\n" +
                      $"compassInitialReadingDeg: {compassInitialReadingDeg} deg");

            float xLength, yLength, zLength, distanceToSun = 100.0f, a;

            a = (float)System.Math.Cos(sunAltitudeDeg * MathF.PI / 180.0f) * distanceToSun;
            yLength = (float)System.Math.Sin(sunAltitudeDeg * MathF.PI / 180.0f) * distanceToSun;

            if (sunAzimuthDeg >= 0.0f && sunAzimuthDeg < 90.0f)
            {
                xLength = System.MathF.Sin(sunAzimuthDeg * MathF.PI / 180.0f) * a;
                zLength = System.MathF.Cos(sunAzimuthDeg * MathF.PI / 180.0f) * a;
            }
            else if (sunAzimuthDeg >= 90.0f && sunAzimuthDeg < 180.0f)
            {
                sunAzimuthDeg -= 90.0f;

                xLength = System.MathF.Cos(sunAzimuthDeg * MathF.PI / 180.0f) * a;
                zLength = System.MathF.Sin(sunAzimuthDeg * MathF.PI / 180.0f) * a * -1;
            }
            else if (sunAzimuthDeg >= 180.0f && sunAzimuthDeg < 270.0f)
            {
                sunAzimuthDeg -= 180.0f;

                xLength = System.MathF.Sin(sunAzimuthDeg * MathF.PI / 180.0f) * a * -1;
                zLength = System.MathF.Cos(sunAzimuthDeg * MathF.PI / 180.0f) * a * -1;
            }
            else
            {
                sunAzimuthDeg -= 270.0f;

                xLength = System.MathF.Cos(sunAzimuthDeg * MathF.PI / 180.0f) * a * -1;
                zLength = System.MathF.Sin(sunAzimuthDeg * MathF.PI / 180.0f) * a;
            }

            InvokeOnMain(() =>
            {
                sunNode = scene.CreateChild(name: "sunNode");
                sunNode.Position = new Urho.Vector3(xLength, yLength, zLength);
                sunNode.SetScale(20.0f);
                sunStaticModel = sunNode.CreateComponent<Sphere>();
                sunStaticModel.CastShadows = false;
                var material = new Material();
                material.SetShaderParameter("MatDiffColor", new Urho.Vector4(255, 0, 0, 1.0f));
                sunStaticModel.Material = material;

                CenterNode = scene.CreateChild(name: "Center");
                CenterNode.Position = new Urho.Vector3(0, 0, 0);
                CenterNode.SetScale(0.5f);
                var CenterNodeModel = CenterNode.CreateComponent<Sphere>();
                CenterNodeModel.CastShadows = false;
                var CenterNodeModelMaterial = new Material();
                CenterNodeModelMaterial.SetShaderParameter("MatDiffColor", new Urho.Vector4(0, 255, 0, 1.0f));
                CenterNodeModel.Material = CenterNodeModelMaterial;

                CenterNode2 = scene.CreateChild(name: "Center2");
                CenterNode2.Position = new Urho.Vector3(0, 0, 1.0f);
                CenterNode2.SetScale(0.25f);
                var CenterNodeModel2 = CenterNode2.CreateComponent<Sphere>();
                CenterNodeModel2.CastShadows = false;
                var CenterNodeModelMaterial2 = new Material();
                CenterNodeModelMaterial2.SetShaderParameter("MatDiffColor", new Urho.Vector4(0, 255, 255, 1.0f));
                CenterNodeModel2.Material = CenterNodeModelMaterial2;

                var lightNode = scene.CreateChild(name: "DirectionalLight");
                lightNode.SetDirection(new Urho.Vector3(-xLength, -yLength, -zLength));
                var light = lightNode.CreateComponent<Light>();
                light.LightType = LightType.Directional;
                light.CastShadows = true;
                light.Brightness = 2.0f;
                light.ShadowResolution = 4;
                light.ShadowIntensity = 0.5f;
                Renderer.ShadowMapSize *= 4;
            });
        }

        private void Compass_InitialReadingChanged(object sender, CompassChangedEventArgs e)
        {
            compassInitialReadingDegList.Add((float)e.Reading.HeadingMagneticNorth);

            if (compassInitialReadingDegList.Count == maxCompassReading)
            {
                compassInitialReadingDeg = (float) compassInitialReadingDegList.Mean();

                gotCompassAngle = true;
            }
        }

        private void ARRender_UnhandledException(object sender, Urho.UnhandledExceptionEventArgs e)
        {
            ShowToast(e.ToString());
        }

        void ArCore_ConfigRequested(Config config)
        {
            config.SetPlaneFindingMode(Config.PlaneFindingMode.Horizontal);
            config.SetLightEstimationMode(Config.LightEstimationMode.AmbientIntensity);
            config.SetUpdateMode(Config.UpdateMode.LatestCameraImage); //non blocking
        }

        void HidePlanes()
        {
            while (editingPlanesList) { }

            editingPlanesList = true;

            bool wait = true;
            InvokeOnMain(() =>
            {
                foreach (var plane in scenePlanes)
                {
                    plane.Enabled = false;
                }
                wait = false;
            });
            while (wait) { }

            editingPlanesList = false;
        }

        void ShowPlanes()
        {
            while (editingPlanesList) { }

            editingPlanesList = true;

            //bool wait = true;
            InvokeOnMain(() =>
            {
                foreach (var plane in scenePlanes)
                {
                    plane.Enabled = true;
                }
                //wait = false;
            });
            //while (wait) { }

            editingPlanesList = false;
        }

        public void ShowingHUD()
        {
            HUDVisible = true;

            ShowPlanes();
            ShowOrientationModels();
        }

        private void ShowOrientationModels()
        {
            Urho.Application.InvokeOnMain(() =>
            {
                if (CenterNode != null)
                    CenterNode.Enabled = true;
                if (CenterNode2 != null)
                    CenterNode2.Enabled = true;
                if (sunStaticModel != null)
                    sunStaticModel.Enabled = true;
            });
        }

        public void HidingHUD()
        {
            HUDVisible = false;

            HidePlanes();
            HideOrientationModels();
        }

        private void HideOrientationModels()
        {
            Urho.Application.InvokeOnMain(() =>
            {
                if (CenterNode != null)
                    CenterNode.Enabled = false;
                if (CenterNode2 != null)
                    CenterNode2.Enabled = false;
                if (sunStaticModel != null)
                    sunStaticModel.Enabled = false;
            });
        }

        void OnTouchBegin(TouchBeginEventArgs e)
        {
            scaling = false;
        }

        void OnTouchEnd(TouchEndEventArgs e)
        {
            if (!HUDVisible)
                return;

            if (sceneLampModels.Count == 0)
            {
                ShowToast("No lamps in scene!");
                return;
            }

            if (scaling || currentFrame == null || selectedLamp < 0)
                return;

            var hitTest = currentFrame.HitTest(e.X, e.Y);
            if (hitTest != null && hitTest.Count > 0)
            {
                var hitPos = hitTest[0].HitPose;
                sceneLampModels[selectedLamp].MoveLamp(new Urho.Vector3(hitPos.Tx(), hitPos.Ty(), -hitPos.Tz()));
            }
        }

        // game update
        protected override void OnUpdate(float timeStep)
        {
            if (!HUDVisible)
                return;

            //// multitouch scaling:
            //if (Input.NumTouches == 2)
            //{
            //    scaling = true;
            //    var state1 = Input.GetTouch(0);
            //    var state2 = Input.GetTouch(1);
            //    var distance1 = IntVector2.Distance(state1.Position, state2.Position);
            //    var distance2 = IntVector2.Distance(state1.LastPosition, state2.LastPosition);
            //    sceneLampModels[selectedLampModel].ScaleLamp((distance1 - distance2) / 10000f);

            //    ShowToast("Scale: " + sceneLampModels[selectedLampModel].lampScale);
            //}
        }

        private void RemoveScenePlanes()
        {
            foreach (var item in scenePlanes)
            {
                item.Remove();
                item.Dispose();
            }

            scenePlanes.Clear();
        }

        public void PrepareAR()
        {
            poleButton.Click += PoleButton_Click;
            lightButton.Click += LightButton_Click;
            deleteAllLampsButton.Click += DeleteAllLampsButton_Click;
            addLampButton.Click += AddLampButton_Click;
            createInstalationButton.Click += CreateInstalationButton_Click;
            nextSelLampButton.Click += NextSelLampButton_Click;
            prevSelLampButton.Click += PrevSelLampButton_Click;
            editLampsButton.Click += EditLampsButton_Click;

            rotateSeekBar.ProgressChanged += RotateSeekBar_ProgressChanged;
            heightSeekBar.ProgressChanged += HeightSeekBar_ProgressChanged;

            Urho.Application.InvokeOnMain(() =>
            {
                // Lamps
                for (int i = 0; i < lampModelsString.Length; i++)
                {
                    LampModelDefinition lampModel = new LampModelDefinition();

                    string[] mainBodyElement;
                    string[] lampElements;
                    string[] lampMaterials;
                    string[] lightElement;
                    string[] baseElement;

                    using (StreamReader sr = new StreamReader(assetManager.Open(@"ArData/" + lampModelsString[i] + @"/config.txt")))
                    {
                        mainBodyElement = sr.ReadLine().Split(',');
                        lampElements = sr.ReadLine().Split(',');
                        lampMaterials = sr.ReadLine().Split(',');
                        lightElement = sr.ReadLine().Split(',');
                        baseElement = sr.ReadLine().Split(',');

                        lampModel.lampScale = Float.ParseFloat(sr.ReadLine());
                        lampModel.name = lampModelsString[i];
                    }

                    lampModel.mainBodyElement = mainBodyElement[0];
                    lampModel.mainBodyElementMaterial = mainBodyElement[1];

                    for (int j = 0; j < lampElements.Length; j++)
                    {
                        lampModel.lampElements.Add(lampElements[j]);
                        lampModel.lampElementsMaterials.Add(lampMaterials[j]);
                    }

                    lampModel.glassElement = lightElement[0];
                    lampModel.glassElementMaterial = lightElement[1];

                    lampModel.baseElement = baseElement[0];
                    lampModel.baseElementMaterial = baseElement[1];

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
                        poleModel.poleElements.Add(poleElements[j]);
                        poleModel.poleElementsMaterials.Add(poleMaterials[j]);
                    }
                    poleModel.scalablePoleElement = scalablePoleElement[0];
                    poleModel.scalablePoleElementMaterial = scalablePoleElement[1];

                    poleModels.Add(poleModel);
                }
            });
        }

        private int GetNextLampDefinition(int lampDefinitionNum)
        {
            int nextLampDefinition = ++lampDefinitionNum;

            if (nextLampDefinition >= lampModels.Count)
                nextLampDefinition = 0;

            return nextLampDefinition;
        }

        private void EditLampsButton_Click(object sender, EventArgs e)
        {
            if (selectedLamp < 0)
            {
                ShowToast("No lamps in scene!");
                return;
            }

            int nextLampDefinition = GetNextLampDefinition(sceneLampModels[selectedLamp].lampDefinitionNum);
            selectedLampModel = nextLampDefinition;

            foreach (var item in sceneLampModels)
            {
                item.lampDefinitionNum = nextLampDefinition;

                item.ChangeLamp(lampModels[item.lampDefinitionNum], ResourceCache);
                item.AddToScene(scene);
            }

            //int nextLampDefinition = GetNextLampDefinition(sceneLampModels[selectedLamp].lampDefinitionNum);
            //sceneLampModels[selectedLamp].lampDefinitionNum = nextLampDefinition;

            //sceneLampModels[selectedLamp].ChangeLamp(lampModels[sceneLampModels[selectedLamp].lampDefinitionNum], ResourceCache);
            //sceneLampModels[selectedLamp].AddToScene(scene);

            //selectedLampModel = nextLampDefinition;
        }

        private void PrevSelLampButton_Click(object sender, EventArgs e)
        {
            if (selectedLamp < 0)
            {
                ShowToast("No lamps in scene!");
                return;
            }

            if (selectedLamp == 0)
                selectedLamp = sceneLampModels.Count - 1;
            else
                --selectedLamp;

            ShowToast("Selected lamp: " + sceneLampModels[selectedLamp].name);
        }

        private void NextSelLampButton_Click(object sender, EventArgs e)
        {
            if (selectedLamp < 0)
            {
                ShowToast("No lamps in scene!");
                return;
            }

            if (selectedLamp == sceneLampModels.Count - 1)
                selectedLamp = 0;
            else
                ++selectedLamp;

            ShowToast("Selected lamp: " + sceneLampModels[selectedLamp].name);
        }

        private void CreateInstalationButton_Click(object sender, EventArgs e)
        {
            if (sceneLampModels.Count < 2)
            {
                ShowToast("Not enough lamps to create instalation!");
                return;
            }

            var dialog = new MainActivity.NumOfNewLampsDialog(mainActivity, mainActivity, selectedValNumOfNewLampsDialog);
            dialog.Show(mainActivity.FragmentManager, "numOfNewLamps");
        }

        public void StartCreatingNewInstallation(int numOfNewLamps)
        {
            if (numOfNewLamps == -1)
                return;

            selectedValNumOfNewLampsDialog = numOfNewLamps;

            //int numOfEstParam = sceneLampModels.Count > 7 ? 7 : sceneLampModels.Count;
            int numOfEstParam = 2;

            Matrix<double> H = Matrix<double>.Build.Dense(sceneLampModels.Count, numOfEstParam);
            Matrix<double> y = Matrix<double>.Build.Dense(sceneLampModels.Count, 1);
            Matrix<double> bEst = Matrix<double>.Build.Dense(numOfEstParam, 1);

            int i = 0;
            foreach (var item in sceneLampModels)
            {
                for (int j = numOfEstParam - 1; j >= 0; --j)
                {
                    H[i, j] = System.Math.Pow(item.baseElement.Position.X, j);
                }
                y[i, 0] = item.baseElement.Position.Z;
                ++i;
            }

            bEst = (H.Transpose() * H).Inverse() * (H.Transpose() * y);

            // Looking for the farest lamps
            int[] foundLampsIndexes = new int[2] { 0, 1 };
            double maxDistance = GetDistanceBetweenLamps(sceneLampModels[foundLampsIndexes[0]], sceneLampModels[foundLampsIndexes[1]]);
            double minDistance = maxDistance;

            for (int j = 0; j < sceneLampModels.Count - 1; j++)
            {
                for (int k = j + 1; k < sceneLampModels.Count; k++)
                {
                    double distance = GetDistanceBetweenLamps(sceneLampModels[j], sceneLampModels[k]);
                    if (distance > maxDistance)
                    {
                        maxDistance = distance;
                        foundLampsIndexes[0] = j;
                        foundLampsIndexes[1] = k;
                    }
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                    }
                }
            }
            //////////////////////////////

            // Calculating mean heigth
            float height = sceneLampModels[0].yPosition;
            for (int j = 1; j < sceneLampModels.Count; j++)
            {
                height = (height + sceneLampModels[j].yPosition) / 2;
            }
            //////////////////////////

            // Calculating estimated function values
            double[] estFuncVal = new double[2] { 0.0, 0.0 };

            for (int j = numOfEstParam - 1; j >= 0; --j)
            {
                estFuncVal[0] += bEst[j, 0] * System.Math.Pow(sceneLampModels[foundLampsIndexes[0]].baseElement.Position.X, j);
                estFuncVal[1] += bEst[j, 0] * System.Math.Pow(sceneLampModels[foundLampsIndexes[1]].baseElement.Position.X, j);
            }
            ////////////////////////////////////////

            // Calculating coordinates for new lamps
            bool lampSideSwitch = false;

            double k1, k2, k3; // coefficients

            for (int j = sceneLampModels.Count; j < numOfNewLamps; j++)
            {
                if (lampSideSwitch)
                {
                    k1 = 1 + System.Math.Pow(bEst[1, 0], 2);
                    k2 = -2 * sceneLampModels[foundLampsIndexes[0]].baseElement.Position.X + 2 * bEst[1, 0] * bEst[0, 0] - 2 * bEst[1, 0] * estFuncVal[0];
                    k3 = System.Math.Pow(sceneLampModels[foundLampsIndexes[0]].baseElement.Position.X, 2) + System.Math.Pow(bEst[0, 0], 2) - 2 * bEst[0, 0] * estFuncVal[0] + System.Math.Pow(estFuncVal[0], 2) - System.Math.Pow(minDistance, 2);

                    double x1 = (-1 * k2 - System.Math.Sqrt(System.Math.Pow(k2, 2) - 4 * k1 * k3)) / (2 * k1);
                    double x2 = (-1 * k2 + System.Math.Sqrt(System.Math.Pow(k2, 2) - 4 * k1 * k3)) / (2 * k1);

                    double z1 = bEst[1, 0] * x1 + bEst[0, 0];
                    double z2 = bEst[1, 0] * x2 + bEst[0, 0];

                    double distance1 = GetDistance(x1, z1, sceneLampModels[foundLampsIndexes[1]].baseElement.Position.X, sceneLampModels[foundLampsIndexes[1]].baseElement.Position.Z);
                    double distance2 = GetDistance(x2, z2, sceneLampModels[foundLampsIndexes[1]].baseElement.Position.X, sceneLampModels[foundLampsIndexes[1]].baseElement.Position.Z);

                    double x, z;

                    if (distance1 > distance2)
                    {
                        x = x1;
                        z = z1;
                    }
                    else
                    {
                        x = x2;
                        z = z2;
                    }

                    estFuncVal[0] = z;

                    sceneLampModels.Add(new LampModel(lampModels[selectedLampModel], ResourceCache, sceneLampModels.Count.ToString(), selectedLampModel));
                    selectedLamp = sceneLampModels.Count - 1;

                    foundLampsIndexes[0] = sceneLampModels.Count - 1;

                    sceneLampModels[sceneLampModels.Count - 1].MoveLamp(new Urho.Vector3((float)x, height, (float)z));

                    heightSeekBar.Progress = 0;

                    sceneLampModels[selectedLamp].AddToScene(scene);

                    lampSideSwitch = !lampSideSwitch;
                }
                else
                {
                    k1 = 1 + System.Math.Pow(bEst[1, 0], 2);
                    k2 = -2 * sceneLampModels[foundLampsIndexes[1]].baseElement.Position.X + 2 * bEst[1, 0] * bEst[0, 0] - 2 * bEst[1, 0] * estFuncVal[1];
                    k3 = System.Math.Pow(sceneLampModels[foundLampsIndexes[1]].baseElement.Position.X, 2) + System.Math.Pow(bEst[0, 0], 2) - 2 * bEst[0, 0] * estFuncVal[1] + System.Math.Pow(estFuncVal[1], 2) - System.Math.Pow(minDistance, 2);

                    double x1 = (-1 * k2 - System.Math.Sqrt(System.Math.Pow(k2, 2) - 4 * k1 * k3)) / (2 * k1);
                    double x2 = (-1 * k2 + System.Math.Sqrt(System.Math.Pow(k2, 2) - 4 * k1 * k3)) / (2 * k1);

                    double z1 = bEst[1, 0] * x1 + bEst[0, 0];
                    double z2 = bEst[1, 0] * x2 + bEst[0, 0];

                    double distance1 = GetDistance(x1, z1, sceneLampModels[foundLampsIndexes[0]].baseElement.Position.X, sceneLampModels[foundLampsIndexes[0]].baseElement.Position.Z);
                    double distance2 = GetDistance(x2, z2, sceneLampModels[foundLampsIndexes[0]].baseElement.Position.X, sceneLampModels[foundLampsIndexes[0]].baseElement.Position.Z);

                    double x, z;

                    if (distance1 > distance2)
                    {
                        x = x1;
                        z = z1;
                    }
                    else
                    {
                        x = x2;
                        z = z2;
                    }

                    estFuncVal[1] = z;

                    sceneLampModels.Add(new LampModel(lampModels[selectedLampModel], ResourceCache, sceneLampModels.Count.ToString(), selectedLampModel));
                    selectedLamp = sceneLampModels.Count - 1;

                    foundLampsIndexes[1] = sceneLampModels.Count - 1;

                    sceneLampModels[sceneLampModels.Count - 1].MoveLamp(new Urho.Vector3((float)x, height, (float)z));

                    heightSeekBar.Progress = 0;

                    sceneLampModels[selectedLamp].AddToScene(scene);

                    lampSideSwitch = !lampSideSwitch;
                }
            }
            ////////////////////////////////////////

            selectedLamp = sceneLampModels.Count - 1;

            ShowToast("Installation created");
        }

        private double GetDistanceBetweenLamps(LampModel lampModel1, LampModel lampModel2)
        {
            return System.Math.Sqrt(System.Math.Pow(lampModel1.baseElement.Position.X - lampModel2.baseElement.Position.X, 2) + System.Math.Pow(lampModel1.baseElement.Position.Z - lampModel2.baseElement.Position.Z, 2));
        }

        private double GetDistance(double x1, double z1, double x2, double z2)
        {
            return System.Math.Sqrt(System.Math.Pow(x1 - x2, 2) + System.Math.Pow(z1 - z2, 2));
        }

        private void AddLampButton_Click(object sender, EventArgs e)
        {
            sceneLampModels.Add(new LampModel(lampModels[selectedLampModel], ResourceCache, sceneLampModels.Count.ToString(), selectedLampModel));
            selectedLamp = sceneLampModels.Count - 1;

            heightSeekBar.Progress = 0;

            sceneLampModels[selectedLamp].AddToScene(scene);

            ShowToast("Lamps in scene: " + sceneLampModels.Count.ToString());
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
                Urho.Vector3 centerOfBase = ((Urho.StaticModel)components.Current).WorldBoundingBox.Center;
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
                {
                    item.RotateLamp(new Urho.Quaternion(0, e.Progress, 0));
                }
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
                            item.pole = new PoleModel(poleModels[selectedPoleModel], ResourceCache, item.name, item.baseElement.Rotation);
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
        public string mainBodyElement;
        public string mainBodyElementMaterial;
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
        public int lampDefinitionNum;
        public Node glassElement;
        public Material glassElementMaterial;
        public Node baseElement;
        public Node lightElement;
        public Node mainBodyElement;
        public Light light;
        public float lampScale;
        public float lightHeight = -0.1f;
        public string name;

        public float yPosition = 0.0f;
        private Urho.Vector3 position;
        private bool isLightTurnedOn = false;

        public PoleModel pole = null;

        public LampModel()
        {
            lampElements = new List<Node>();
        }

        public void ChangeLamp(LampModelDefinition lampModelDefinition, Urho.Resources.ResourceCache cache)
        {
            Remove(false);

            lampElements = new List<Node>();

            this.lampScale = lampModelDefinition.lampScale;

            // mainBodyElement
            mainBodyElement = new Node();
            mainBodyElement.Position = position;
            mainBodyElement.SetScale(this.lampScale);
            StaticModel mainBodyElementStaticModel = mainBodyElement.CreateComponent<StaticModel>();
            mainBodyElementStaticModel.CastShadows = true;

            Urho.Application.InvokeOnMain(() =>
            {
                mainBodyElementStaticModel.Model = cache.GetModel(lampModelDefinition.mainBodyElement);
                mainBodyElementStaticModel.Material = cache.GetMaterial(lampModelDefinition.mainBodyElementMaterial);
            });
            ////////////////////

            for (int j = 0; j < lampModelDefinition.lampElements.Count; j++)
            {
                Node node = new Node();
                node.Position = position;
                node.SetScale(this.lampScale);
                StaticModel model = node.CreateComponent<StaticModel>();
                model.CastShadows = false;

                bool wait = true;
                Urho.Application.InvokeOnMain(() =>
                {
                    model.Model = cache.GetModel(lampModelDefinition.lampElements[j]);
                    model.Material = cache.GetMaterial(lampModelDefinition.lampElementsMaterials[j]);

                    wait = false;
                });

                while (wait) { };
                this.lampElements.Add(node);
            }

            Node lightLampNode = new Node();
            lightLampNode.Position = position;
            lightLampNode.SetScale(this.lampScale);

            StaticModel lightLampModel = lightLampNode.CreateComponent<StaticModel>();
            lightLampModel.CastShadows = true;
            Urho.Application.InvokeOnMain(() =>
            {
                lightLampModel.Model = cache.GetModel(lampModelDefinition.glassElement);
                glassElementMaterial = cache.GetMaterial(lampModelDefinition.glassElementMaterial);
                lightLampModel.Material = glassElementMaterial;
            });
            this.glassElement = lightLampNode;

            //Lamp light
            Node lampLightNode = new Node();
            lampLightNode.Rotation = new Urho.Quaternion(90.0f, 0.0f, 0.0f);
            var components = this.glassElement.Components.GetEnumerator();
            if (components.MoveNext())
            {
                lampLightNode.Position = ((Urho.StaticModel)components.Current).WorldBoundingBox.Center;
            }

            Light lampLightLight = lampLightNode.CreateComponent<Light>();
            lampLightLight.Brightness = 0.0f;
            lampLightLight.LightType = LightType.Spot;
            lampLightLight.Fov = 90.0f;
            lampLightLight.Range = 2.0f;
            lampLightLight.Color = new Urho.Color(1.0f, 0.819607f, 0.639216f, 1.0f);
            lampLightLight.CastShadows = true;

            this.lightElement = lampLightNode;
            this.light = lampLightLight;

            Node baseNode = new Node();
            baseNode.Position = position;
            baseNode.SetScale(this.lampScale);
            StaticModel baseModel = baseNode.CreateComponent<StaticModel>();
            baseModel.CastShadows = true;
            Urho.Application.InvokeOnMain(() =>
            {
                baseModel.Model = cache.GetModel(lampModelDefinition.baseElement);
                baseModel.Material = cache.GetMaterial(lampModelDefinition.baseElementMaterial);
            });
            this.baseElement = baseNode;

            if (isLightTurnedOn)
                TurnOn();
        }

        public LampModel(LampModelDefinition lampModelDefinition, Urho.Resources.ResourceCache cache, string lampNum, int lampDefinitionNum)
        {
            lampElements = new List<Node>();

            this.lampDefinitionNum = lampDefinitionNum;
            this.lampScale = lampModelDefinition.lampScale;
            this.name = lampModelDefinition.name + " " + lampNum;
            this.position = new Urho.Vector3(0, this.yPosition, 0);

            // mainBodyElement
            mainBodyElement = new Node();
            mainBodyElement.Position = position;
            mainBodyElement.SetScale(this.lampScale);
            StaticModel mainBodyElementStaticModel = mainBodyElement.CreateComponent<StaticModel>();
            mainBodyElementStaticModel.CastShadows = true;

            Urho.Application.InvokeOnMain(() =>
            {
                mainBodyElementStaticModel.Model = cache.GetModel(lampModelDefinition.mainBodyElement);
                mainBodyElementStaticModel.Material = cache.GetMaterial(lampModelDefinition.mainBodyElementMaterial);
            });
            ////////////////////

            for (int j = 0; j < lampModelDefinition.lampElements.Count; j++)
            {
                Node node = new Node();
                node.Position = position;
                node.SetScale(this.lampScale);
                StaticModel model = node.CreateComponent<StaticModel>();
                model.CastShadows = true;

                bool wait = true;
                Urho.Application.InvokeOnMain(() =>
                {
                    model.Model = cache.GetModel(lampModelDefinition.lampElements[j]);
                    model.Material = cache.GetMaterial(lampModelDefinition.lampElementsMaterials[j]);

                    wait = false;
                });

                while (wait) { };
                this.lampElements.Add(node);
            }

            Node lightLampNode = new Node();
            lightLampNode.Position = position;
            lightLampNode.SetScale(this.lampScale);

            StaticModel lightLampModel = lightLampNode.CreateComponent<StaticModel>();
            lightLampModel.CastShadows = true;
            Urho.Application.InvokeOnMain(() =>
            {
                lightLampModel.Model = cache.GetModel(lampModelDefinition.glassElement);
                glassElementMaterial = cache.GetMaterial(lampModelDefinition.glassElementMaterial);
                lightLampModel.Material = glassElementMaterial;
            });
            this.glassElement = lightLampNode;

            //Lamp light
            Node lampLightNode = new Node();
            lampLightNode.Rotation = new Urho.Quaternion(90.0f, 0.0f, 0.0f);
            var components = this.glassElement.Components.GetEnumerator();
            if (components.MoveNext())
            {
                lampLightNode.Position = ((Urho.StaticModel)components.Current).WorldBoundingBox.Center;
            }

            Light lampLightLight = lampLightNode.CreateComponent<Light>();
            lampLightLight.Brightness = 0.0f;
            lampLightLight.LightType = LightType.Spot;
            lampLightLight.Fov = 90.0f;
            lampLightLight.Range = 2.0f;
            lampLightLight.Color = new Urho.Color(1.0f, 0.819607f, 0.639216f, 1.0f);
            lampLightLight.CastShadows = true;

            this.lightElement = lampLightNode;
            this.light = lampLightLight;

            Node baseNode = new Node();
            baseNode.Position = position;
            baseNode.SetScale(this.lampScale);
            StaticModel baseModel = baseNode.CreateComponent<StaticModel>();
            baseModel.CastShadows = true;
            Urho.Application.InvokeOnMain(() =>
            {
                baseModel.Model = cache.GetModel(lampModelDefinition.baseElement);
                baseModel.Material = cache.GetMaterial(lampModelDefinition.baseElementMaterial);
            });
            this.baseElement = baseNode;
        }

        public void Remove(bool removePole = true)
        {
            bool wait = true;
            Urho.Application.InvokeOnMain(() =>
            {
                mainBodyElement.Remove();
                mainBodyElement.Dispose();
                foreach (var item in lampElements)
                {
                    item.Remove();
                    item.Dispose();
                }
                baseElement.Remove();
                glassElement.Remove();
                light.Remove();
                lightElement.Remove();

                baseElement.Dispose();
                glassElement.Dispose();
                light.Dispose();
                lightElement.Dispose();

                if (pole != null && removePole)
                {
                    pole.Remove();
                }

                wait = false;
            });
            while (wait) { }
        }

        public void ShowPole()
        {
            if (pole == null)
                return;

            pole.MovePole(new Urho.Vector3(baseElement.Position.X, yPosition, baseElement.Position.Z));
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

            pole.MovePole(new Urho.Vector3(baseElement.Position.X, yPosition, baseElement.Position.Z));
        }

        public void MoveLamp(Urho.Vector3 vector3)
        {
            position = vector3;

            mainBodyElement.Position = vector3;
            foreach (var item in lampElements)
            {
                item.Position = vector3;
            }
            baseElement.Position = vector3;
            glassElement.Position = vector3;

            var components = glassElement.Components.GetEnumerator();
            if (components.MoveNext())
            {
                Urho.Vector3 centerOfGlass = ((Urho.StaticModel)components.Current).WorldBoundingBox.Center;
                centerOfGlass.Y -= 0.1f * lampScale;
                lightElement.Position = centerOfGlass;
            }

            yPosition = vector3.Y;
        }

        public void ChangeLampHeight(float height)
        {
            position = new Urho.Vector3(position.X, yPosition + height, position.Z);

            mainBodyElement.Position = new Urho.Vector3(baseElement.Position.X, yPosition + height, baseElement.Position.Z);
            foreach (var item in lampElements)
            {
                item.Position = new Urho.Vector3(item.Position.X, yPosition + height, item.Position.Z);
            }
            baseElement.Position = new Urho.Vector3(baseElement.Position.X, yPosition + height, baseElement.Position.Z);
            glassElement.Position = new Urho.Vector3(glassElement.Position.X, yPosition + height, glassElement.Position.Z);

            var components = glassElement.Components.GetEnumerator();
            if (components.MoveNext())
            {
                Urho.Vector3 centerOfGlass = ((Urho.StaticModel)components.Current).WorldBoundingBox.Center;
                centerOfGlass.Y -= 0.1f * lampScale;
                lightElement.Position = centerOfGlass;
            }
        }

        public void ScaleLamp(float scale)
        {
            lampScale += scale;

            mainBodyElement.SetScale(lampScale);
            foreach (var item in lampElements)
            {
                item.SetScale(lampScale);
            }
            baseElement.SetScale(lampScale);
            lightElement.SetScale(lampScale);

            var components = glassElement.Components.GetEnumerator();
            if (components.MoveNext())
            {
                Urho.Vector3 centerOfGlass = ((Urho.StaticModel)components.Current).WorldBoundingBox.Center;
                centerOfGlass.Y -= 0.1f * lampScale;
                lightElement.Position = centerOfGlass;
            }

        }

        public void RotateLamp(Urho.Quaternion quaternion)
        {
            bool wait = true;
            Urho.Application.InvokeOnMain(() =>
            {
                mainBodyElement.Rotation = quaternion;
                foreach (var item in lampElements)
                {
                    item.Rotation = quaternion;
                }
                baseElement.Rotation = quaternion;
                glassElement.Rotation = quaternion;
                lightElement.Rotation = new Urho.Quaternion(90.0f, quaternion.Y, quaternion.Z);

                var components = glassElement.Components.GetEnumerator();
                if (components.MoveNext())
                {
                    Urho.Vector3 centerOfGlass = ((Urho.StaticModel)components.Current).WorldBoundingBox.Center;
                    centerOfGlass.Y -= 0.1f * lampScale;
                    lightElement.Position = centerOfGlass;
                }
                wait = false;
            });
            while (wait) { }

            if (pole != null)
            {
                pole.RotatePole(quaternion);
            }
        }

        public void TurnOn()
        {
            light.Brightness = 10.0f;
            glassElementMaterial.SetShaderParameter("MatDiffColor", new Urho.Vector4(light.Color.R * 255, light.Color.G * 255, light.Color.B * 255, 0.6f));

            isLightTurnedOn = true;
        }

        public void TurnOff()
        {
            light.Brightness = 0.0f;
            glassElementMaterial.SetShaderParameter("MatDiffColor", new Urho.Vector4(1.0f, 1.0f, 1.0f, 0.5f));

            isLightTurnedOn = false;
        }

        public void AddToScene(Scene scene)
        {
            Urho.Application.InvokeOnMain(() =>
            {
                scene.AddChild(mainBodyElement);
                foreach (var item in lampElements)
                {
                    scene.AddChild(item);
                }
                scene.AddChild(baseElement);
                scene.AddChild(glassElement);
                scene.AddChild(lightElement);
            });
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
        private Node shadowNode;

        public PoleModel()
        {
            poleElements = new List<Node>();
        }

        public PoleModel(PoleModelDefinition poleModelDefinition, Urho.Resources.ResourceCache cache, string poleNum, Urho.Quaternion quaternion)
        {
            poleElements = new List<Node>();

            this.poleScale = poleModelDefinition.poleScale;
            this.name = poleModelDefinition.name + " " + poleNum;

            for (int j = 0; j < poleModelDefinition.poleElements.Count; j++)
            {
                Node node = new Node();
                node.Position = new Urho.Vector3(0, 0, 0);
                node.SetScale(this.poleScale * 12.0f);
                StaticModel poleElementModel = node.CreateComponent<StaticModel>();
                poleElementModel.CastShadows = true;
                poleElementModel.Model = cache.GetModel(poleModelDefinition.poleElements[j]);
                poleElementModel.Material = cache.GetMaterial(poleModelDefinition.poleElementsMaterials[j]);

                this.poleElements.Add(node);
            }
            Node scalableElementNode = new Node();
            scalableElementNode.Position = new Urho.Vector3(0, 0, 0);
            scalableElementNode.SetScale(this.poleScale);

            var material = cache.GetMaterial("ARMaterials/ShadowBackground.xml");
            material.SetTechnique(0, cache.GetTechnique("ARTechniques/ShadowTechnique.xml"));
            shadowNode = scalableElementNode.CreateChild();
            shadowNode.Scale = new Urho.Vector3(100, 1, 100);
            var shadowNodePlane = shadowNode.CreateComponent<StaticModel>();
            shadowNodePlane.Model = cache.GetModel("ARModels/ShadowFloorObject.mdl");
            shadowNodePlane.Material = material;


            StaticModel modelScalableElement = scalableElementNode.CreateComponent<StaticModel>();
            modelScalableElement.CastShadows = true;
            modelScalableElement.Model = cache.GetModel(poleModelDefinition.scalablePoleElement);
            modelScalableElement.Material = cache.GetMaterial(poleModelDefinition.scalablePoleElementMaterial);
            this.scalablePoleElement = scalableElementNode;

            RotatePole(quaternion);
        }

        public void RotatePole(Urho.Quaternion quaternion)
        {
            Urho.Application.InvokeOnMain(() =>
            {
                foreach (var item in poleElements)
                {
                    item.Rotation = quaternion;
                }
                scalablePoleElement.Rotation = quaternion;
            });
        }

        public void Show()
        {
            foreach (var item in poleElements)
            {
                item.Enabled = true;
            }
            scalablePoleElement.Enabled = true;
            //shadowNode.Enabled = true;
        }

        public void Hide()
        {
            foreach (var item in poleElements)
            {
                item.Enabled = false;
            }
            scalablePoleElement.Enabled = false;
            //shadowNode.Enabled = false;
        }

        public void Remove()
        {
            foreach (var item in poleElements)
            {
                item.Remove();
                item.Dispose();
            }
            //shadowNode.Remove();
            //shadowNode.Dispose();
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
            //shadowNode.SetScale(scale);
        }

        public void MovePole(Urho.Vector3 vector3)
        {
            foreach (var item in poleElements)
            {
                item.Position = vector3;
            }
            scalablePoleElement.Position = vector3;
            //shadowNode.Position = vector3;
        }

        public void ChangeHeight(Node baseNode, float yPosition)
        {
            scalablePoleElement.Scale = new Urho.Vector3(poleScale, System.Math.Abs(baseNode.Position.Y - yPosition) / 2, poleScale);
            //shadowNode.Scale = new Urho.Vector3(1000 * System.Math.Abs(baseNode.Position.Y - yPosition) / 2, 1000 * System.Math.Abs(baseNode.Position.Y - yPosition) / 2, 1000 * System.Math.Abs(baseNode.Position.Y - yPosition) / 2);
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