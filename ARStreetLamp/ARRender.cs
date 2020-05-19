﻿using System;
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

        private Node bodyNode;
        private Node glassNode;
        private Node lensesNode;
        private Node mirrorNode;
        private Node baseNode;

        private Node lampLightNode;
        private Light lampLightLight;

        private float yPosition = 0.0f;

        public ToggleButton poleButton;
        public ToggleButton lightButton;
        public SeekBar heightSeekBar;
        public SeekBar rotateSeekBar;

        private float objectsScale = 0.4674199f;
        private float poleObjectsScale = 0.085f;
        private Scene scene;
        private Node poleNode;
        private Node poleStandNode;
        private Node poleStandNutsNode;

        public Activity mainActivity;
        private Toast toast;

        public string[] lampModelsString;
        public string[] poleModelsString;
        public AssetManager assetManager;

        List<LampModel> lampModels;
        List<PoleModel> poleModels;

        public ARCoreComponent ArCore { get; private set; }

        public ARRender(ApplicationOptions options) : base(options) {
            lampModels = new List<LampModel>();
            poleModels = new List<PoleModel>();
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

            // Lamps

            while (assetManager == null) { } //Wait for finish of initialization

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
                        Node node = scene.CreateChild();
                        node.Position = new Vector3(0, yPosition, 0.5f); // 50cm Y, 50cm Z
                        node.SetScale(lampModel.lampScale);
                        StaticModel model = node.CreateComponent<StaticModel>();
                        model.CastShadows = true;
                        model.Model = ResourceCache.GetModel(lampModelsString[i] + @"/" + lampElements[i] + @".mdl");
                        model.Material = ResourceCache.GetMaterial(lampModelsString[i] + @"/" + lampMaterials[i] + @".xml");

                        lampModel.lampElements.Add(node);
                    }
                    Node lightLampNode = scene.CreateChild();
                    lightLampNode.Position = new Vector3(0, yPosition, 0.5f); // 50cm Y, 50cm Z
                    lightLampNode.SetScale(lampModel.lampScale);
                    StaticModel lightLampModel = lightLampNode.CreateComponent<StaticModel>();
                    lightLampModel.CastShadows = true;
                    lightLampModel.Model = ResourceCache.GetModel(lampModelsString[i] + @"/" + lightElement[0] + @".mdl");
                    lightLampModel.Material = ResourceCache.GetMaterial(lampModelsString[i] + @"/" + lightElement[1] + @".xml");
                    lampModel.lampElements.Add(lightLampNode);

                    //Lamp light
                    Node lampLightNode = scene.CreateChild();
                    lampLightNode.Rotation = new Quaternion(90.0f, 0.0f, 0.0f);

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

                    Node baseNode = scene.CreateChild();
                    baseNode.Position = new Vector3(0, yPosition, 0.5f); // 50cm Y, 50cm Z
                    baseNode.SetScale(lampModel.lampScale);
                    StaticModel baseModel = baseNode.CreateComponent<StaticModel>();
                    baseModel.CastShadows = true;
                    baseModel.Model = ResourceCache.GetModel(lampModelsString[i] + @"/" + baseElement[0] + @".mdl");
                    baseModel.Material = ResourceCache.GetMaterial(lampModelsString[i] + @"/" + baseElement[1] + @".xml");
                    lampModel.baseElement = baseNode;

                    lampModels.Add(lampModel);
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
                bodyNode.Position = new Vector3(hitPos.Tx(), hitPos.Ty(), -hitPos.Tz());
                glassNode.Position = new Vector3(hitPos.Tx(), hitPos.Ty(), -hitPos.Tz());
                lensesNode.Position = new Vector3(hitPos.Tx(), hitPos.Ty(), -hitPos.Tz());
                mirrorNode.Position = new Vector3(hitPos.Tx(), hitPos.Ty(), -hitPos.Tz());
                baseNode.Position = new Vector3(hitPos.Tx(), hitPos.Ty(), -hitPos.Tz());

                var components = glassNode.Components.GetEnumerator();
                if (components.MoveNext())
                {
                    lampLightNode.Position = ((Urho.StaticModel)components.Current).WorldBoundingBox.Center;
                }

                yPosition = hitPos.Ty();
                heightSeekBar.Progress = 0;

                if (poleNode != null)
                {
                    if (!poleNode.IsDeleted)
                    {
                        poleStandNode.Remove();
                        poleStandNutsNode.Remove();
                        poleNode.Remove();
                    }
                }
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
                bodyNode.SetScale(bodyNode.Scale.X + (distance1 - distance2) / 10000f);
                glassNode.SetScale(bodyNode.Scale.X + (distance1 - distance2) / 10000f);
                lensesNode.SetScale(bodyNode.Scale.X + (distance1 - distance2) / 10000f);
                mirrorNode.SetScale(bodyNode.Scale.X + (distance1 - distance2) / 10000f);
                baseNode.SetScale(bodyNode.Scale.X + (distance1 - distance2) / 10000f);

                ShowToast("Scale: " + bodyNode.Scale.X);

                var components = glassNode.Components.GetEnumerator();
                if (components.MoveNext())
                {
                    Vector3 centerOfGlass = ((Urho.StaticModel)components.Current).WorldBoundingBox.Center;
                    centerOfGlass.Y -= 0.1f * objectsScale;
                    lampLightNode.Position = centerOfGlass;
                }
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
            bodyNode.Position = new Vector3(bodyNode.Position.X, yPosition + (float)e.Progress / 100, bodyNode.Position.Z);
            glassNode.Position = new Vector3(glassNode.Position.X, yPosition + (float)e.Progress / 100, glassNode.Position.Z);
            lensesNode.Position = new Vector3(lensesNode.Position.X, yPosition + (float)e.Progress / 100, lensesNode.Position.Z);
            mirrorNode.Position = new Vector3(mirrorNode.Position.X, yPosition + (float)e.Progress / 100, mirrorNode.Position.Z);
            baseNode.Position = new Vector3(baseNode.Position.X, yPosition + (float)e.Progress / 100, baseNode.Position.Z);

            var components = glassNode.Components.GetEnumerator();
            if (components.MoveNext())
            {
                Vector3 centerOfGlass = ((Urho.StaticModel)components.Current).WorldBoundingBox.Center;
                centerOfGlass.Y -= 0.1f * objectsScale;
                lampLightNode.Position = centerOfGlass;
            }

            components = baseNode.Components.GetEnumerator();
            float baseYPosition = yPosition;
            if (components.MoveNext())
            {
                Vector3 centerOfBase = ((Urho.StaticModel)components.Current).WorldBoundingBox.Center;
                baseYPosition = centerOfBase.Y;
            }

            ShowToast("Height: " + (((System.Math.Abs(baseYPosition - yPosition) / 2) * 0.95f) / 0.45f) + " m");
        }

        private void RotateSeekBar_ProgressChanged(object sender, SeekBar.ProgressChangedEventArgs e)
        {
            bodyNode.Rotation = new Quaternion(0, e.Progress, 0);
            glassNode.Rotation = new Quaternion(0, e.Progress, 0);
            lensesNode.Rotation = new Quaternion(0, e.Progress, 0);
            mirrorNode.Rotation = new Quaternion(0, e.Progress, 0);
            baseNode.Rotation = new Quaternion(0, e.Progress, 0);
            lampLightNode.Rotation = new Quaternion(90.0f, e.Progress, 0);

            var components = glassNode.Components.GetEnumerator();
            if (components.MoveNext())
            {
                Vector3 centerOfGlass = ((Urho.StaticModel)components.Current).WorldBoundingBox.Center;
                centerOfGlass.Y -= 0.1f * objectsScale;
                lampLightNode.Position = centerOfGlass;
            }
        }

        private void LightButton_Click(object sender, EventArgs e)
        {
            var components = glassNode.Components.GetEnumerator();
            if (components.MoveNext())
            {
                Vector3 centerOfGlass = ((Urho.StaticModel)components.Current).WorldBoundingBox.Center;
                centerOfGlass.Y -= 0.1f * objectsScale;
                lampLightNode.Position = centerOfGlass;
            }

            if (lightButton.Checked)
            {
                lampLightLight.Brightness = 0.1f;
            }
            else
            {
                lampLightLight.Brightness = 0;
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
        public Node baseElement;
        public Node lightElement;
        public Light light;
        public float lampScale;
        public string name;


        public LampModel()
        {
            lampElements = new List<Node>();
        }
    }

    class PoleModel
    {
        public List<Node> poleElements;
        public Node scalablePoleElement;
        public float poleScale;
        public string name;

        public PoleModel()
        {
            poleElements = new List<Node>();
        }
    }
}