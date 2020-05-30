using Android.App;
using Android.OS;
using Android.Support.V7.App;
using Android.Runtime;
using Android.Widget;
using Urho.Droid;
using System;
using Android.Support.V4.Content;
using Android.Support.V4.App;
using Android;
using Android.Content.PM;
using Android.Util;
using Android.Views;
using Android.Content.Res;
using System.IO;
using Java.Nio.FileNio;
using System.Collections.Generic;

namespace ARStreetLamp
{
    [Activity(Label = "ARStreetLamp", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        bool launched;
        ARRender arrender;
        RelativeLayout placeholder;
        UrhoSurfacePlaceholder surface;
        ToggleButton hudButton;

        private int hudScreenNum = 0;
        private int lastHudScreenNum = 0;
        private Toast toast;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);

            placeholder = FindViewById<RelativeLayout>(Resource.Id.UrhoSurfacePlaceHolder);

            hudButton = FindViewById<ToggleButton>(Resource.Id.hudButton);
            hudButton.Click += (o, e) => {
                if (hudButton.Checked)
                    ChangeVisibilityOfHUD(true);
                else
                    ChangeVisibilityOfHUD(false);
            };

            Button prevControlsScreenButton = FindViewById<Button>(Resource.Id.prevControlsScreenButton);
            Button nextControlsScreenButton = FindViewById<Button>(Resource.Id.nextControlsScreenButton);
            prevControlsScreenButton.Click += PrevControlsScreenButton_Click;
            nextControlsScreenButton.Click += NextControlsScreenButton_Click;

            LaunchUrho();

            SeekBar heightSeekBar = FindViewById<SeekBar>(Resource.Id.heightSeekBar);
            SeekBar rotateSeekBar = FindViewById<SeekBar>(Resource.Id.rotateSeekBar);
            FrameLayout rotateSeekBarFL = FindViewById<FrameLayout>(Resource.Id.rotateSeekBarFL);
            FrameLayout heightSeekBarFL = FindViewById<FrameLayout>(Resource.Id.heightSeekBarFL);

            var displayMetrics = Resources.DisplayMetrics;
            int height = displayMetrics.HeightPixels;
            int width = displayMetrics.WidthPixels;

            rotateSeekBar.LayoutParameters.Width = (int)(width * 0.9);
            rotateSeekBar.LayoutParameters.Height = (int)(height * 0.3);
            rotateSeekBarFL.SetPadding((int)(width * 0.05), (int)(height * 0.7), 0, 0);

            heightSeekBar.LayoutParameters.Width = (int)(width * 1.0);
            heightSeekBar.LayoutParameters.Height = (int)(height * 0.2);
        }

        private async System.Threading.Tasks.Task LaunchUrho()
        {
            if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.Camera) != Permission.Granted)
            {
                ActivityCompat.RequestPermissions(this, new[] { Manifest.Permission.Camera }, 42);
                return;
            }

            if (launched)
                return;

            string[] lampModels = new string[0];
            string[] poleModels = new string[0];

            AssetManager assets = this.Assets;
            using (StreamReader sr = new StreamReader(assets.Open("ArData/models.txt")))
            {
                try
                {
                    lampModels = sr.ReadLine().Split(',');
                }
                catch (Exception) { }

                try
                {
                    poleModels = sr.ReadLine().Split(',');
                }
                catch (Exception) { }
            }

            var paths = new string[lampModels.Length + poleModels.Length];
            lampModels.CopyTo(paths, 0);
            poleModels.CopyTo(paths, lampModels.Length);

            launched = true;
            surface = UrhoSurface.CreateSurface(this);
            placeholder.AddView(surface);
            arrender = await surface.Show<ARRender>(
                new Urho.ApplicationOptions
                {
                    ResourcePaths = new string[] { "ArData" }
                });

            arrender.poleButton = FindViewById<ToggleButton>(Resource.Id.poleButton);
            arrender.lightButton = FindViewById<ToggleButton>(Resource.Id.lightButton);
            arrender.heightSeekBar = FindViewById<SeekBar>(Resource.Id.heightSeekBar);
            arrender.rotateSeekBar = FindViewById<SeekBar>(Resource.Id.rotateSeekBar);
            arrender.mainActivity = this;
            arrender.poleModelsString = poleModels;
            arrender.lampModelsString = lampModels;
            arrender.assetManager = Assets;

            arrender.PrepareAR();

            SetTitle(Resource.String.app_name);
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        public void NextControlsScreenButton_Click(object sender, EventArgs e)
        {
            if (hudScreenNum == lastHudScreenNum)
                hudScreenNum = 0;
            else
                ++hudScreenNum;

            SetHud(hudScreenNum);
        }

        public void PrevControlsScreenButton_Click(object sender, EventArgs e)
        {
            if (hudScreenNum == 0)
                hudScreenNum = lastHudScreenNum;
            else
                --hudScreenNum;

            SetHud(hudScreenNum);
        }

        private void SetHud(int hudScreenNum)
        {
            ToggleButton poleButton = FindViewById<ToggleButton>(Resource.Id.poleButton);
            ToggleButton lightButton = FindViewById<ToggleButton>(Resource.Id.lightButton);
            SeekBar heightSeekBar = FindViewById<SeekBar>(Resource.Id.heightSeekBar);
            SeekBar rotateSeekBar = FindViewById<SeekBar>(Resource.Id.rotateSeekBar);
            Button prevControlsScreenButton = FindViewById<Button>(Resource.Id.prevControlsScreenButton);
            Button nextControlsScreenButton = FindViewById<Button>(Resource.Id.nextControlsScreenButton);

            poleButton.Visibility = ViewStates.Invisible;
            lightButton.Visibility = ViewStates.Invisible;
            heightSeekBar.Visibility = ViewStates.Invisible;
            rotateSeekBar.Visibility = ViewStates.Invisible;
            prevControlsScreenButton.Visibility = ViewStates.Invisible;
            nextControlsScreenButton.Visibility = ViewStates.Invisible;

            switch (hudScreenNum)
            {
                case 0:
                    poleButton.Visibility = ViewStates.Visible;
                    lightButton.Visibility = ViewStates.Visible;
                    heightSeekBar.Visibility = ViewStates.Visible;
                    rotateSeekBar.Visibility = ViewStates.Visible;
                    prevControlsScreenButton.Visibility = ViewStates.Visible;
                    nextControlsScreenButton.Visibility = ViewStates.Visible;
                    ShowToast("Main HUD");
                    break;
                case 1:

                    break;
                default:
                    break;
            }
        }

        private void ShowToast(string mssg)
        {
            if (toast != null)
            {
                toast.Cancel();
            }
            toast = Toast.MakeText(Android.App.Application.Context, mssg, ToastLength.Short);
            toast.Show();
        }

        protected override void OnResume()
        {
            base.OnResume();
            UrhoSurface.OnResume();

            // if was resumed by the Camera Permission dialog
            LaunchUrho();
        }
        protected override void OnPause()
        {
            UrhoSurface.OnPause();
            base.OnPause();
        }

        protected override void OnDestroy()
        {
            UrhoSurface.OnDestroy();
            base.OnDestroy();
        }

        public override void OnBackPressed()
        {
            UrhoSurface.OnDestroy();
            Finish();
        }

        public override void OnLowMemory()
        {
            UrhoSurface.OnLowMemory();
            base.OnLowMemory();
        }

        private void ChangeVisibilityOfHUD(bool state)
        {
            ToggleButton poleButton = FindViewById<ToggleButton>(Resource.Id.poleButton);
            ToggleButton lightButton = FindViewById<ToggleButton>(Resource.Id.lightButton);
            SeekBar heightSeekBar = FindViewById<SeekBar>(Resource.Id.heightSeekBar);
            SeekBar rotateSeekBar = FindViewById<SeekBar>(Resource.Id.rotateSeekBar);
            Button prevControlsScreenButton = FindViewById<Button>(Resource.Id.prevControlsScreenButton);
            Button nextControlsScreenButton = FindViewById<Button>(Resource.Id.nextControlsScreenButton);

            if (state)
            {
                SetHud(hudScreenNum);
            }
            else
            {
                poleButton.Visibility = Android.Views.ViewStates.Invisible;
                lightButton.Visibility = Android.Views.ViewStates.Invisible;
                heightSeekBar.Visibility = Android.Views.ViewStates.Invisible;
                rotateSeekBar.Visibility = Android.Views.ViewStates.Invisible;
                prevControlsScreenButton.Visibility = Android.Views.ViewStates.Invisible;
                nextControlsScreenButton.Visibility = Android.Views.ViewStates.Invisible;
            }
        }
    }
}