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

            heightSeekBar.LayoutParameters.Width = (int)(width * 1.1);
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
            using (StreamReader sr = new StreamReader(assets.Open("models.txt")))
            {
                try
                {
                    lampModels = sr.ReadLine().Split(',');
                }
                catch (Exception) {}

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
                    ResourcePaths = paths
                });

            arrender.poleButton = FindViewById<ToggleButton>(Resource.Id.poleButton);
            arrender.lightButton = FindViewById<ToggleButton>(Resource.Id.lightButton);
            arrender.heightSeekBar = FindViewById<SeekBar>(Resource.Id.heightSeekBar);
            arrender.rotateSeekBar = FindViewById<SeekBar>(Resource.Id.rotateSeekBar);
            arrender.mainActivity = this;
            arrender.poleModels = poleModels;
            arrender.lampModels = lampModels;
            arrender.assetManager = Assets;

            arrender.PrepareInterface();

            SetTitle(Resource.String.app_name);
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
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

            if (state)
            {
                poleButton.Visibility = Android.Views.ViewStates.Visible;
                lightButton.Visibility = Android.Views.ViewStates.Visible;
                heightSeekBar.Visibility = Android.Views.ViewStates.Visible;
                rotateSeekBar.Visibility = Android.Views.ViewStates.Visible;
            }
            else
            {
                poleButton.Visibility = Android.Views.ViewStates.Invisible;
                lightButton.Visibility = Android.Views.ViewStates.Invisible;
                heightSeekBar.Visibility = Android.Views.ViewStates.Invisible;
                rotateSeekBar.Visibility = Android.Views.ViewStates.Invisible;
            }
        }
    }
}