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

        public string[] lampModels;
        public string[] poleModels;
        public AssetManager assetManager;

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

            StaticModel model;

            bodyNode = scene.CreateChild();
            bodyNode.Position = new Vector3(0, yPosition, 0.5f); // 50cm Y, 50cm Z
            bodyNode.SetScale(objectsScale);
            model = bodyNode.CreateComponent<StaticModel>();
            model.CastShadows = true;
            model.Model = ResourceCache.GetModel("Cel/Body.mdl");
            model.Material = ResourceCache.GetMaterial("Cel/bodyLamp.xml");

            baseNode = scene.CreateChild();
            baseNode.Position = new Vector3(0, yPosition, 0.5f); // 50cm Y, 50cm Z
            baseNode.SetScale(objectsScale);
            model = baseNode.CreateComponent<StaticModel>();
            model.CastShadows = true;
            model.Model = ResourceCache.GetModel("Cel/Base.mdl");
            model.Material = ResourceCache.GetMaterial("Cel/bodyLamp.xml");

            glassNode = scene.CreateChild();
            glassNode.Position = new Vector3(0, yPosition, 0.5f); // 50cm Y, 50cm Z
            glassNode.SetScale(objectsScale);
            model = glassNode.CreateComponent<StaticModel>();
            model.CastShadows = true;
            model.Model = ResourceCache.GetModel("Cel/Glass.mdl");
            model.Material = ResourceCache.GetMaterial("Cel/glassLamp.xml");

            lensesNode = scene.CreateChild();
            lensesNode.Position = new Vector3(0, yPosition, 0.5f); // 50cm Y, 50cm Z
            lensesNode.SetScale(objectsScale);
            model = lensesNode.CreateComponent<StaticModel>();
            model.CastShadows = true;
            model.Model = ResourceCache.GetModel("Cel/Lenses.mdl");
            model.Material = ResourceCache.GetMaterial("Cel/lensesLamp.xml");

            mirrorNode = scene.CreateChild();
            mirrorNode.Position = new Vector3(0, yPosition, 0.5f); // 50cm Y, 50cm Z
            mirrorNode.SetScale(objectsScale);
            model = mirrorNode.CreateComponent<StaticModel>();
            model.CastShadows = true;
            model.Model = ResourceCache.GetModel("Cel/Mirror.mdl");
            model.Material = ResourceCache.GetMaterial("Cel/waterMirrorLamp.xml");

            //Lamp light
            lampLightNode = scene.CreateChild();
            lampLightNode.Rotation = new Quaternion(90.0f, 0.0f, 0.0f);

            lampLightLight = lampLightNode.CreateComponent<Light>();
            lampLightLight.LightType = LightType.Point;
            lampLightLight.Length = 1;
            lampLightLight.Range = 0.8f;
            lampLightLight.Fov = 160.0f;
            lampLightLight.AspectRatio = 1.05f;
            lampLightLight.Color = new Color(255.0f, 209.0f, 163.0f, 1.0f);
            lampLightLight.Brightness = 0.0f;
            lampLightLight.CastShadows = true;
            lampLightLight.ShadowBias = new BiasParameters(0.0f, 0.5f);

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

            ShowToast("Height: " + (((Math.Abs(baseYPosition - yPosition) / 2) * 0.95f) / 0.45f) + " m");
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
                    poleNode.Scale = new Vector3(poleObjectsScale * objectsScale, Math.Abs(baseYPosition - yPosition) / 2, poleObjectsScale * objectsScale);
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
}