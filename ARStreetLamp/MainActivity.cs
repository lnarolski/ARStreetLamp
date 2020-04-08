﻿using Android.App;
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

            launched = true;
            surface = UrhoSurface.CreateSurface(this);
            placeholder.AddView(surface);
            arrender = await surface.Show<ARRender>(
                new Urho.ApplicationOptions
                {
                    ResourcePaths = new[] { "LampsData" }
                });

            arrender.poleButton = FindViewById<ToggleButton>(Resource.Id.poleButton);
            arrender.lightButton = FindViewById<ToggleButton>(Resource.Id.lightButton);
            arrender.heightSeekBar = FindViewById<SeekBar>(Resource.Id.heightSeekBar);
            arrender.rotateSeekBar = FindViewById<SeekBar>(Resource.Id.rotateSeekBar);

            arrender.PrepareInterface();

            SetTitle(Resource.String.app_name);

            //hudButton.PerformClick();
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